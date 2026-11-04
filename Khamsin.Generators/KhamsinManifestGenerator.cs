using System.Collections.Immutable;
using System.Text;
using Khamsin.Extraction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Khamsin.Generators;

/// <summary>
/// Emits a <see cref="KhamsinManifestAttribute"/> part for every Razor component, merging markup
/// literals, code-behind <c>Kx</c> literals and the components it renders. Helper classes with
/// <c>Kx</c> calls become an assembly-level manifest.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class KhamsinManifestGenerator : IIncrementalGenerator
{
    /// <summary>Groups at or above this value come from <c>Kx</c> calls and are diagnosed by the analyzer.</summary>
    private const int KxGroupBase = 1_000_000;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var razorTexts = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) && !IsGeneratedPath(file.Path))
            .Select(static (file, cancellationToken) => new RazorInput(file.Path, file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Collect()
            .Select(static (files, _) => Deduplicate(files));

        var kxManifests = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => KxSyntax.IsCandidate(node) || CnSyntax.IsCandidate(node),
                static (syntaxContext, cancellationToken) => CnSyntax.IsCandidate(syntaxContext.Node)
                    ? CnSyntax.Extract(syntaxContext, cancellationToken)
                    : KxSyntax.Extract(syntaxContext, cancellationToken))
            .Where(static manifest => manifest != null)
            .Select(static (manifest, _) => manifest!)
            .Collect();

        var classLiteralManifests = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => KxSyntax.IsClassLiteralCandidate(node),
                static (syntaxContext, cancellationToken) => KxSyntax.ExtractClassLiteral(syntaxContext, cancellationToken))
            .Where(static manifest => manifest != null)
            .Select(static (manifest, _) => manifest!)
            .Collect()
            .Select(static (manifests, _) => manifests);

        var settings = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            var rootNamespace = Option(provider.GlobalOptions, "build_property.RootNamespace");
            if (rootNamespace.Length == 0) rootNamespace = Option(provider.GlobalOptions, "build_property.AssemblyName");

            var projectDirectory = Option(provider.GlobalOptions, "build_property.ProjectDir");
            if (projectDirectory.Length == 0) projectDirectory = Option(provider.GlobalOptions, "build_property.MSBuildProjectDirectory");

            var configName = Option(provider.GlobalOptions, "build_property.KhamsinConfig");
            return new ProjectSettings(rootNamespace, projectDirectory, configName);
        });

        var configInput = settings
            .Combine(context.AdditionalTextsProvider.Collect())
            .Select(static (input, _) => LoadConfig(input.Left, input.Right));

        var combined = razorTexts
            .Combine(kxManifests)
            .Combine(classLiteralManifests)
            .Combine(settings)
            .Combine(context.CompilationProvider)
            .Combine(configInput)
            .Select(static (input, _) =>
            {
                // input.Right = config; input.Left.Right = compilation; input.Left.Left.Right = settings.
                var razor = input.Left.Left.Left.Left.Left;
                var kx = input.Left.Left.Left.Left.Right;
                var literals = input.Left.Left.Left.Right;
                return new GeneratorInputs(razor, [
                    .. kx,
                    .. literals
                ], input.Left.Left.Right, input.Left.Right, input.Right);
            });

        context.RegisterSourceOutput(combined, Execute);
    }

    /// <summary>Finds the configured configuration file among the additional files and parses it.</summary>
    private static ConfigInput LoadConfig(ProjectSettings settings, ImmutableArray<AdditionalText> additionalFiles)
    {
        var name = settings.ConfigFileName;
        if (name.Length == 0)
            return ConfigInput.None;

        AdditionalText? match = null;
        foreach (var file in additionalFiles)
        {
            if (!IsConfigFilePath(file.Path, name))
                continue;

            // Deterministic pick when several files share the configured name.
            if (match is null || string.CompareOrdinal(file.Path, match.Path) < 0)
                match = file;
        }

        if (match is null)
            return ConfigInput.None;

        var text = match.GetText()?.ToString();
        if (text is null)
            return ConfigInput.From(null, ["The configuration file could not be read."], match.Path);

        if (!KhamsinConfig.TryParse(text, out var config, out var errors))
            return ConfigInput.From(null, [.. errors], match.Path, text);

        return ConfigInput.From(config, [], match.Path, text);
    }

    /// <summary>Matches by file name (or path suffix) so relative and absolute project paths agree.</summary>
    private static bool IsConfigFilePath(string path, string configuredName)
    {
        if (configuredName.Length == 0)
            return false;

        if (string.Equals(path, configuredName, StringComparison.OrdinalIgnoreCase))
            return true;

        var fileName = Path.GetFileName(path.TrimEnd('/', '\\'));
        if (fileName.Length == 0)
            return false;

        if (configuredName.IndexOf('/') < 0 && configuredName.IndexOf('\\') < 0)
            return string.Equals(fileName, configuredName, StringComparison.OrdinalIgnoreCase);

        return path.Replace('\\', '/').EndsWith(configuredName.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    private static void Execute(SourceProductionContext context, GeneratorInputs inputs)
    {
        var config = inputs.Config;
        if (config is { Errors.Length: > 0 })
        {
            var location = config.FilePath.Length > 0
                ? Location.Create(config.FilePath, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)))
                : Location.None;
            foreach (var error in config.Errors)
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidConfiguration, location, error));
        }

        var referenced = ConfigDiscovery.Read(inputs.Compilation);
        foreach (var assembly in referenced)
        {
            if (assembly.Error is null)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.InvalidReferencedConfiguration,
                Location.None,
                assembly.AssemblyName,
                assembly.Error));
        }

        // Referenced configurations first, then the current project's configuration so an app can
        // override a library token without restating the whole document.
        var configurations = new List<KhamsinConfig>(referenced.Length + 1);
        foreach (var assembly in referenced)
            if (assembly.Config is not null)
                configurations.Add(assembly.Config);

        if (config.Config is not null)
            configurations.Add(config.Config);

        var merged = configurations.Count == 0 ? null : KhamsinConfig.Merge(configurations);
        var engine = merged is null ? KhamsinEngine.Default : new KhamsinEngine(merged.ToTheme());

        if (config.Config is not null && config.Json.Length > 0)
            EmitConfigAttribute(context, config.Json);

        if (merged is not null &&
            inputs.Compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is not null)
            EmitThemeRegistration(context, merged);

        var kxByType = MergeKxManifests(inputs.KxManifests, out var kxOrder);

        var scans = new List<(RazorInput Input, RazorScanResult Scan)>(inputs.RazorFiles.Length);
        var allImports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in inputs.RazorFiles)
        {
            var scan = RazorSourceScanner.Scan(file.Text);
            scans.Add((file, scan));
            foreach (var import in scan.Imports) allImports.Add(import);
        }

        allImports.Add("Microsoft.AspNetCore.Components");
        var razorTypes = new HashSet<string>(StringComparer.Ordinal);
        var projectComponents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (file, scan) in scans)
        {
            var name = Path.GetFileNameWithoutExtension(file.Path);
            if (!SyntaxFacts.IsValidIdentifier(name))
                continue;

            var ns = ComputeNamespace(file.Path, scan.NamespaceOverride, inputs.Settings);
            if (!projectComponents.ContainsKey(name)) projectComponents.Add(name, Combine(ns, name));
        }

        var assemblyClasses = new List<string>();

        foreach (var (file, scan) in scans)
        {
            var className = Path.GetFileNameWithoutExtension(file.Path);
            if (!SyntaxFacts.IsValidIdentifier(className))
                continue;

            var namespaceName = ComputeNamespace(file.Path, scan.NamespaceOverride, inputs.Settings);
            var fullName = Combine(namespaceName, className);
            if (!razorTypes.Add(fullName))
                continue;

            var classes = new List<string>();
            foreach (var literal in scan.Literals) AddDistinct(classes, literal.Value);

            foreach (var literal in scan.AuthorLiterals)
                if (engine.TryParse(literal.Value, out _, out _)) AddDistinct(classes, literal.Value);

            if (kxByType.TryGetValue(fullName, out var kx))
                foreach (var value in kx.Classes) AddDistinct(classes, value);

            ReportRazorDiagnostics(context, engine, file, scan);

            var uses = ResolveComponents(context, inputs.Compilation, scan.UsedComponents, namespaceName, allImports, projectComponents, file);

            var accessibility = kx?.Accessibility ?? AccessibilityFromCompilation(inputs.Compilation, fullName) ?? "public";

            if (!IsExtendable(accessibility))
                continue;

            EmitComponent(context, fullName, namespaceName, className, accessibility, classes, uses);
        }

        foreach (var fullName in kxOrder)
        {
            if (razorTypes.Contains(fullName))
                continue;

            var kx = kxByType[fullName];
            if (!kx.IsComponent || !kx.CanBeExtended || !IsExtendable(kx.Accessibility) || fullName.IndexOf('+') >= 0)
            {
                foreach (var value in kx.Classes) AddDistinct(assemblyClasses, value);

                continue;
            }

            var separator = fullName.LastIndexOf('.');
            var namespaceName = separator < 0 ? string.Empty : fullName.Substring(0, separator);
            var className = separator < 0 ? fullName : fullName.Substring(separator + 1);
            EmitComponent(context, fullName, namespaceName, className, kx.Accessibility, kx.Classes, []);
        }

        if (assemblyClasses.Count <= 0) return;
        
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.Append("[assembly: global::Khamsin.KhamsinManifestAttribute(Classes=\"");
        source.Append(Escape(string.Join(" ", assemblyClasses)));
        source.AppendLine("\")]");
        context.AddSource("Khamsin.AssemblyManifest.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void ReportRazorDiagnostics(SourceProductionContext context, KhamsinEngine engine, RazorInput file, RazorScanResult scan)
    {
        foreach (var usage in scan.NonLiteralUsages)
        {
            var text = file.Text.Substring(usage.Offset, Math.Min(usage.Length, Math.Max(0, file.Text.Length - usage.Offset))).Trim();
            if (text.Length > 60) text = text.Substring(0, 60) + "...";

            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NonLiteralInput,
                Diagnostics.CreateLocation(file.Path, file.Text, usage.Offset, usage.Length),
                text.Length == 0 ? "expression" : text));
        }

        foreach (var literal in scan.Literals)
        {
            // Kx groups are diagnosed by the C# analyzer; author class attributes are open-world.
            if (literal.Group is >= KxGroupBase and < 3_000_000)
                continue;

            if (!engine.TryParse(literal.Value, out _, out _))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnknownClass,
                    Diagnostics.CreateLocation(file.Path, file.Text, literal.Offset, literal.Value.Length),
                    literal.Value));
            }
        }

        var closedGroups = scan.Literals.Where(static l => l.Group < KxGroupBase || l.Group >= 3_000_000);
        foreach (var group in closedGroups.GroupBy(static l => l.Group))
        {
            var tokens = group.ToList();
            for (var i = 1; i < tokens.Count; i++)
            {
                if (!engine.TryParse(tokens[i].Value, out var later, out _))
                    continue;

                for (var j = 0; j < i; j++)
                {
                    if (!engine.TryParse(tokens[j].Value, out var earlier, out _))
                        continue;

                    if (!string.Equals(VariantKey(earlier), VariantKey(later), StringComparison.Ordinal))
                        continue;

                    if (!Shadows(later.Entry.Group, earlier.Entry.Group))
                        continue;

                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.ConflictingClasses,
                        Diagnostics.CreateLocation(file.Path, file.Text, tokens[i].Offset, tokens[i].Value.Length),
                        earlier.Raw,
                        later.Raw));
                }
            }
        }
    }

    private static IReadOnlyList<string> ResolveComponents(
        SourceProductionContext context,
        Compilation compilation,
        IReadOnlyList<RazorComponentTag> tags,
        string namespaceName,
        IReadOnlyCollection<string> imports,
        IReadOnlyDictionary<string, string> projectComponents,
        RazorInput file)
    {
        if (tags.Count == 0)
            return [];

        var uses = new List<string>();
        foreach (var tag in tags)
        {
            var type = ResolveTag(compilation, tag.Name, namespaceName, imports, projectComponents, out var projectTypeName);
            if (projectTypeName is not null)
            {
                AddDistinct(uses, "global::" + projectTypeName);
                continue;
            }

            if (type is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ComponentNotResolvable,
                    Diagnostics.CreateLocation(file.Path, file.Text, tag.Offset, tag.Name.Length),
                    tag.Name));
                continue;
            }

            if (type.IsGenericType || type.Arity > 0)
            {
                // Open generic components cannot be represented as typeof(T) in an attribute.
                continue;
            }

            AddDistinct(uses, type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return uses;
    }

    private static INamedTypeSymbol? ResolveTag(
        Compilation compilation,
        string tag,
        string namespaceName,
        IReadOnlyCollection<string> imports,
        IReadOnlyDictionary<string, string> projectComponents,
        out string? projectTypeName)
    {
        projectTypeName = null;
        if (tag.IndexOf('.') >= 0)
        {
            if (projectComponents.TryGetValue(tag, out var exact))
            {
                projectTypeName = exact;
                return null;
            }

            var qualified = compilation.GetTypeByMetadataName(tag);
            return IsComponent(qualified) ? qualified : null;
        }

        if (projectComponents.TryGetValue(tag, out var projectType))
        {
            projectTypeName = projectType;
            return null;
        }

        return CandidateNamespaces(namespaceName, imports)
            .Select(candidateNamespace => candidateNamespace.Length == 0 ? tag : candidateNamespace + "." + tag)
            .Select(compilation.GetTypeByMetadataName).FirstOrDefault(IsComponent);
    }

    private static IEnumerable<string> CandidateNamespaces(string namespaceName, IReadOnlyCollection<string> imports)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = namespaceName;
        while (!string.IsNullOrEmpty(current))
        {
            if (seen.Add(current))
                yield return current;

            var separator = current.LastIndexOf('.');
            current = separator < 0 ? string.Empty : current.Substring(0, separator);
        }

        if (seen.Add(string.Empty))
            yield return string.Empty;

        foreach (var import in imports)
            if (seen.Add(import))
                yield return import;
    }

    private static bool IsComponent(INamedTypeSymbol? type)
    {
        if (type is null)
            return false;

        return Enumerable.Any(type.AllInterfaces,
            @interface => string.Equals(@interface.ToDisplayString(), "Microsoft.AspNetCore.Components.IComponent",
                StringComparison.Ordinal));
    }

    private static string VariantKey(ParsedClass parsed)
    {
        if (parsed.Variants.Count == 0)
            return parsed.Important ? "!" : string.Empty;

        var variants = parsed.Variants.OrderBy(static v => v, StringComparer.Ordinal);
        return string.Join(":", variants) + (parsed.Important ? "!" : string.Empty);
    }

    private static bool Shadows(string incoming, string existing)
    {
        if (string.Equals(incoming, existing, StringComparison.Ordinal))
            return true;

        return existing.Length > incoming.Length &&
               existing[incoming.Length] == '/' &&
               existing.StartsWith(incoming, StringComparison.Ordinal);
    }

    /// <summary>
    /// Embeds this project's own configuration in metadata. Referenced builds read the attribute
    /// and merge it under their own configuration; the attributed document stays unchanged.
    /// </summary>
    private static void EmitConfigAttribute(SourceProductionContext context, string json)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.Append("[assembly: global::Khamsin.KhamsinConfigAttribute(Json=\"");
        source.Append(Escape(json));
        source.AppendLine("\")]");
        context.AddSource("Khamsin.Config.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Emits the runtime registration that teaches <see cref="Khamsin.KhamsinMerger"/> about the
    /// configuration's custom tokens. Only the entry assembly may register, so generated code in
    /// referenced libraries never competes with the application's configuration. Callers must
    /// ensure the compilation targets a runtime that has ModuleInitializerAttribute.
    /// </summary>
    private static void EmitThemeRegistration(SourceProductionContext context, Khamsin.KhamsinConfig config)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine("namespace Khamsin.Generated");
        source.AppendLine("{");
        source.AppendLine("    internal static class ThemeRegistration");
        source.AppendLine("    {");
        source.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        source.AppendLine("        internal static void Register()");
        source.AppendLine("        {");
        source.AppendLine("            if (!global::System.Object.ReferenceEquals(global::System.Reflection.Assembly.GetEntryAssembly(), typeof(ThemeRegistration).Assembly))");
        source.AppendLine("                return;");
        source.AppendLine();
        source.AppendLine("            Khamsin.KhamsinMerger.Configure(new global::Khamsin.KhamsinConfig");
        source.AppendLine("            {");
        if (config.Dark is { Length: > 0 }) AppendSetting(source, "Dark", config.Dark);
        if (config.Spacing is { Length: > 0 }) AppendSetting(source, "Spacing", config.Spacing);
        AppendDictionary(source, "Colors", config.Colors);
        AppendDictionary(source, "Fonts", config.Fonts);
        AppendDictionary(source, "Radius", config.Radius);
        AppendDictionary(source, "Shadows", config.Shadows);
        AppendDictionary(source, "Eases", config.Eases);
        AppendDictionary(source, "Breakpoints", config.Breakpoints);
        AppendDictionary(source, "Containers", config.Containers);
        if (config.TextSizes is { Count: > 0 })
        {
            source.AppendLine("                TextSizes = new global::System.Collections.Generic.Dictionary<string, global::Khamsin.TextSize>");
            source.AppendLine("                {");
            foreach (var pair in config.TextSizes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                source.AppendLine($"                    {{ {Quote(pair.Key)}, new global::Khamsin.TextSize({Quote(pair.Value.Size)}, {Quote(pair.Value.LineHeight)}) }},");
            source.AppendLine("                },");
        }

        if (config.Animations is { Count: > 0 })
        {
            source.AppendLine("                Animations = new global::System.Collections.Generic.Dictionary<string, global::Khamsin.AnimationDefinition>");
            source.AppendLine("                {");
            foreach (var pair in config.Animations.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                var keyframes = new StringBuilder();
                foreach (var frame in pair.Value.Keyframes)
                    keyframes.Append(", new global::System.Collections.Generic.KeyValuePair<string, string>(").Append(Quote(frame.Key)).Append(", ").Append(Quote(frame.Value)).Append(')');

                source.AppendLine($"                    {{ {Quote(pair.Key)}, new global::Khamsin.AnimationDefinition({Quote(pair.Value.Value)}{keyframes}) }},");
            }

            source.AppendLine("                },");
        }

        source.AppendLine("            });");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");
        context.AddSource("Khamsin.ThemeRegistration.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void AppendSetting(StringBuilder source, string name, string value)
        => source.AppendLine($"                {name} = {Quote(value)},");

    private static void AppendDictionary(StringBuilder source, string name, Dictionary<string, string>? entries)
    {
        if (entries is not { Count: > 0 })
            return;

        source.AppendLine($"                {name} = new global::System.Collections.Generic.Dictionary<string, string>");
        source.AppendLine("                {");
        foreach (var pair in entries.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            source.AppendLine($"                    {{ {Quote(pair.Key)}, {Quote(pair.Value)} }},");
        source.AppendLine("                },");
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var c in value)
        {
            if (c is '"' or '\\')
                builder.Append('\\').Append(c);
            else if (c == '\r' || c == '\n' || c == '\t')
                builder.Append('\\').Append(c switch { '\r' => 'r', '\n' => 'n', _ => 't' });
            else
                builder.Append(c);
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static void EmitComponent(
        SourceProductionContext context,
        string fullName,
        string namespaceName,
        string className,
        string accessibility,
        IEnumerable<string> classes,
        IEnumerable<string> uses)
    {
        var classList = string.Join(" ", classes);
        var useList = uses.ToList();

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        if (namespaceName.Length > 0) source.Append("namespace ").Append(namespaceName).AppendLine("{");

        source.Append("    [global::Khamsin.KhamsinManifestAttribute(Classes=\"").Append(Escape(classList)).Append('"');
        if (useList.Count > 0)
        {
            source.Append(", Uses=new global::System.Type[] { ");
            for (var i = 0; i < useList.Count; i++)
            {
                if (i > 0) source.Append(", ");

                source.Append("typeof(").Append(useList[i]).Append(')');
            }

            source.Append(" }");
        }

        source.AppendLine(")]");
        source.Append("    ").Append(accessibility).Append(" partial class ").Append(className).AppendLine(" { }");
        if (namespaceName.Length > 0) source.AppendLine("}");

        context.AddSource(HintName(fullName), SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static Dictionary<string, KxManifest> MergeKxManifests(ImmutableArray<KxManifest> manifests, out List<string> order)
    {
        var merged = new Dictionary<string, KxManifest>(StringComparer.Ordinal);
        order = [];

        foreach (var manifest in manifests)
        {
            if (!merged.TryGetValue(manifest.TypeFullName, out var existing))
            {
                merged.Add(manifest.TypeFullName, manifest);
                order.Add(manifest.TypeFullName);
                continue;
            }

            var classes = existing.Classes.ToList();
            foreach (var value in manifest.Classes) AddDistinct(classes, value);

            merged[manifest.TypeFullName] = new KxManifest(
                manifest.TypeFullName,
                existing.Accessibility,
                existing.IsComponent || manifest.IsComponent,
                existing.CanBeExtended && manifest.CanBeExtended,
                classes.ToImmutableArray());
        }

        return merged;
    }

    private static string? AccessibilityFromCompilation(Compilation compilation, string fullName)
    {
        try
        {
            var type = compilation.GetTypeByMetadataName(fullName);
            return type is null ? null : KxSyntax.AccessibilityOf(type);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsExtendable(string accessibility) => accessibility is "public" or "internal";

    private static string ComputeNamespace(string filePath, string? directive, ProjectSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(directive))
            return directive!.Trim();

        var root = settings.RootNamespace.Trim();
        var relative = RelativePath(settings.ProjectDirectory, filePath);
        var directory = Path.GetDirectoryName(relative);
        if (string.IsNullOrEmpty(directory))
            return root;

        var builder = new StringBuilder(root);
        foreach (var segment in directory.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!SyntaxFacts.IsValidIdentifier(segment))
                continue;

            if (string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase))
                continue;

            if (builder.Length > 0) builder.Append('.');

            builder.Append(segment);
        }

        return builder.ToString();
    }

    private static string RelativePath(string projectDirectory, string filePath)
    {
        if (string.IsNullOrEmpty(projectDirectory))
            return Path.GetFileName(filePath);

        var directory = projectDirectory.TrimEnd('/', '\\');
        if (filePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
            filePath.Length > directory.Length &&
            (filePath[directory.Length] == '/' || filePath[directory.Length] == '\\'))
            return filePath.Substring(directory.Length + 1);

        return Path.GetFileName(filePath);
    }

    private static string Combine(string namespaceName, string className)
        => namespaceName.Length == 0 ? className : namespaceName + "." + className;

    private static string HintName(string fullName)
    {
        var builder = new StringBuilder(fullName.Length + 16);
        foreach (var c in fullName) builder.Append(char.IsLetterOrDigit(c) || c == '.' ? c : '_');

        return builder.Append(".Khamsin.g.cs").ToString();
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                case '\n':
                    builder.Append(' ');
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static void AddDistinct(List<string> values, string value)
    {
        if (!values.Contains(value)) values.Add(value);
    }

    private static bool IsGeneratedPath(string path)
        => path.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0 ||
           path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0 ||
           path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);

    private static ImmutableArray<RazorInput> Deduplicate(ImmutableArray<RazorInput> files)
    {
        var byPath = new Dictionary<string, RazorInput>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(file => !byPath.ContainsKey(file.Path))) byPath.Add(file.Path, file);

        var result = byPath.Values.ToList();
        result.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        return result.ToImmutableArray();
    }

    private static string Option(AnalyzerConfigOptions options, string key)
        => options.TryGetValue(key, out var value) ? value : string.Empty;
}
