using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Khamsin.Generators;

/// <summary>
/// Enforces the closed class set in C#: every class passed to <c>Kx.When</c> or <c>Kx.Variant</c>
/// must be a literal, must exist in the registry, and must not conflict inside one literal. Razor
/// markup is checked by the generator because Razor files are not C# syntax trees.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KhamsinAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [Diagnostics.NonLiteralInput, Diagnostics.UnknownClass, Diagnostics.ConflictingClasses];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            var engine = CreateEngine(startContext.Options, startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, engine), SyntaxKind.InvocationExpression);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeClassLiteral(nodeContext, engine), SyntaxKind.VariableDeclarator, SyntaxKind.PropertyDeclaration);
        });
    }

    /// <summary>
    /// Builds the validation engine from referenced assemblies' configurations and the current
    /// project's configuration file. Parse errors are not reported here; the generator owns the
    /// KHM005/KHM006 diagnostics, and validation falls back to the remaining configurations so the
    /// same mistake is not reported twice.
    /// </summary>
    private static KhamsinEngine CreateEngine(AnalyzerOptions options, Compilation compilation)
    {
        var configurations = new List<KhamsinConfig>();

        foreach (var referenced in ConfigDiscovery.Read(compilation))
            if (referenced.Config is not null)
                configurations.Add(referenced.Config);

        var own = ReadOwnConfig(options);
        if (own is not null)
            configurations.Add(own);

        return configurations.Count == 0
            ? KhamsinEngine.Default
            : new KhamsinEngine(KhamsinConfig.Merge(configurations).ToTheme());
    }

    private static KhamsinConfig? ReadOwnConfig(AnalyzerOptions options)
    {
        var provider = options.AnalyzerConfigOptionsProvider;
        if (provider?.GlobalOptions.TryGetValue("build_property.KhamsinConfig", out var configuredName) != true)
            return null;

        var name = configuredName ?? string.Empty;
        if (name.Length == 0)
            return null;

        AdditionalText? match = null;
        foreach (var file in options.AdditionalFiles)
        {
            if (!IsConfigFilePath(file.Path, name))
                continue;

            if (match is null || string.CompareOrdinal(file.Path, match.Path) < 0)
                match = file;
        }

        if (match is null)
            return null;

        var text = match.GetText()?.ToString();
        if (text is null)
            return null;

        return KhamsinConfig.TryParse(text, out var config, out _) ? config : null;
    }

    private static bool IsConfigFilePath(string path, string configuredName)
    {
        if (string.Equals(path, configuredName, StringComparison.OrdinalIgnoreCase))
            return true;

        var fileName = Path.GetFileName(path.TrimEnd('/', '\\'));
        if (fileName.Length == 0)
            return false;

        if (configuredName.IndexOf('/') < 0 && configuredName.IndexOf('\\') < 0)
            return string.Equals(fileName, configuredName, StringComparison.OrdinalIgnoreCase);

        return path.Replace('\\', '/').EndsWith(configuredName.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzeClassLiteral(SyntaxNodeAnalysisContext context, KhamsinEngine engine)
    {
        if (!KxSyntax.IsClassLiteralCandidate(context.Node))
            return;

        var initializer = KxSyntax.Initializer(context.Node);
        if (initializer is null)
            return;

        var declared = context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken);
        var type = declared switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            ILocalSymbol local => local.Type,
            _ => null,
        };

        if (type is null || !string.Equals(type.ToDisplayString(), "Khamsin.KhamsinClass", StringComparison.Ordinal))
            return;

        var literal = KxSyntax.UnwrapInitializerLiteral(initializer);
        if (literal is not null) ValidateLiteral(context, engine, literal);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, KhamsinEngine engine)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (CnSyntax.IsCandidate(invocation))
        {
            if (!CnSyntax.TryGetMethod(invocation, context.SemanticModel, context.CancellationToken, out _))
                return;

            CnSyntax.CollectArguments(invocation, context.SemanticModel, context.CancellationToken, out var cnLiterals, out var cnNonLiterals);

            foreach (var (text, location) in cnNonLiterals)
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NonLiteralInput, location, text));

            foreach (var (value, location) in cnLiterals)
                ValidateTokens(context, engine, value, location);

            return;
        }

        if (!KxSyntax.IsCandidate(invocation))
            return;

        if (!KxSyntax.TryGetMethod(invocation, context.SemanticModel, context.CancellationToken, out var method))
            return;

        KxSyntax.CollectClassArguments(invocation, method, out var literals, out var nonLiterals);

        foreach (var expression in nonLiterals)
        {
            var text = expression.ToString();
            if (text.Length > 60) text = text.Substring(0, 60) + "...";

            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NonLiteralInput, expression.GetLocation(), text));
        }

        foreach (var literal in literals) ValidateLiteral(context, engine, literal);
    }

    private static void ValidateLiteral(SyntaxNodeAnalysisContext context, KhamsinEngine engine, LiteralExpressionSyntax literal)
        => ValidateTokens(context, engine, literal.Token.ValueText, literal.GetLocation());

    private static void ValidateTokens(SyntaxNodeAnalysisContext context, KhamsinEngine engine, string text, Location location)
    {
        var tokens = new List<(string Token, ParsedClass? Parsed)>();
        foreach (var token in KhamsinEngine.Tokenize(text))
        {
            engine.TryParse(token, out var parsed, out _);
            tokens.Add((token, parsed));
        }

        foreach (var token in tokens)
        {
            if (token.Parsed is not null) continue;
            
            var properties = ImmutableDictionary<string, string?>.Empty.Add("token", token.Token);
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.UnknownClass,
                location,
                properties,
                token.Token));
        }

        for (var i = 1; i < tokens.Count; i++)
        {
            var later = tokens[i].Parsed;
            if (later is null)
                continue;

            for (var j = 0; j < i; j++)
            {
                var earlier = tokens[j].Parsed;
                if (earlier is null)
                    continue;

                if (!string.Equals(VariantKey(earlier), VariantKey(later), StringComparison.Ordinal))
                    continue;

                if (!Shadows(later.Entry.Group, earlier.Entry.Group))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ConflictingClasses,
                    location,
                    earlier.Raw,
                    later.Raw));
            }
        }
    }

    private static string VariantKey(ParsedClass parsed)
        => ClassParser.BuildVariantKey(parsed);

    private static bool Shadows(string incoming, string existing)
    {
        if (string.Equals(incoming, existing, StringComparison.Ordinal))
            return true;

        return existing.Length > incoming.Length &&
               existing[incoming.Length] == '/' &&
               existing.StartsWith(incoming, StringComparison.Ordinal);
    }
}
