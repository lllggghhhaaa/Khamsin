using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Khamsin.Generators.Tests;

internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText _text;

    public InMemoryAdditionalText(string path, string text)
    {
        Path = path;
        _text = SourceText.From(text, Encoding.UTF8);
    }

    public override string Path { get; }

    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}

internal sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly TestOptions _options;

    public TestOptionsProvider(Dictionary<string, string> values)
    {
        _options = new TestOptions(values);
    }

    public override AnalyzerConfigOptions GlobalOptions => _options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

    private sealed class TestOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public TestOptions(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value)
            => _values.TryGetValue(key, out value!);
    }
}

internal static class GeneratorHarness
{
    internal const string Stubs = @"
namespace Khamsin
{
    public readonly struct KhamsinClass
    {
        public KhamsinClass(string? value) { }
        public static implicit operator KhamsinClass(string? value) => default;
    }

    public static class Kx
    {
        public static KhamsinClass When(bool condition, string whenTrue, string whenFalse = """") => default;
        public static KhamsinClass Variant<T>(T value, params (T Value, string Classes)[] options) => default;
    }

    public static class Cn
    {
        public static KhamsinClass Of(params object?[]? inputs) => default;
        public static KhamsinClass Merge(KhamsinClass defaults, params object?[]? inputs) => default;
    }

    public sealed class KhamsinManifestAttribute : System.Attribute
    {
        public string Classes { get; set; } = """";
        public System.Type[] Uses { get; set; } = System.Array.Empty<System.Type>();
    }
}

namespace Microsoft.AspNetCore.Components
{
    public interface IComponent { }
    public abstract class ComponentBase : IComponent { }
    public sealed class ParameterAttribute : System.Attribute { }
}
";

    internal static readonly string Components =
        @"
namespace Sample.Components
{
    public partial class Child : global::Microsoft.AspNetCore.Components.ComponentBase
    {
        [global::Microsoft.AspNetCore.Components.Parameter]
        public global::Khamsin.KhamsinClass Khamsin { get; set; }
    }
}
";

    internal static (GeneratorDriverRunResult Result, Compilation Output, ImmutableArray<Diagnostic> Diagnostics) Run(
        IEnumerable<(string Path, string Text)> razorFiles,
        string? extraCSharp = null,
        string rootNamespace = "Sample",
        string projectDirectory = @"C:\proj\",
        string? configJson = null,
        string configName = "khamsin.json",
        IEnumerable<MetadataReference>? extraReferences = null)
    {
        var parseOptions = ParseOptions();
        var compilation = CreateCompilation(extraCSharp, parseOptions, extraReferences);

        var additionalTexts = razorFiles
            .Select(file => (AdditionalText)new InMemoryAdditionalText(file.Path, file.Text))
            .ToList();
        if (configJson is not null)
            additionalTexts.Add(new InMemoryAdditionalText(configName, configJson));

        var options = new TestOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = rootNamespace,
            ["build_property.ProjectDir"] = projectDirectory,
            ["build_property.MSBuildProjectDirectory"] = projectDirectory,
            ["build_property.KhamsinConfig"] = configName,
        });

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new KhamsinManifestGenerator().AsSourceGenerator() },
            additionalTexts,
            parseOptions,
            options);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return (driver.GetRunResult(), output, diagnostics);
    }

    internal static CSharpParseOptions ParseOptions() => new(LanguageVersion.Latest);

    /// <summary>Keeps test-supplied <c>using</c> directives ahead of the stub declarations.</summary>
    private static string ComposeSource(string? extraCSharp)
    {
        var extra = extraCSharp ?? Components;
        var usings = new StringBuilder();
        var index = 0;
        while (index < extra.Length)
        {
            var newline = extra.IndexOf('\n', index);
            var line = newline < 0 ? extra.Substring(index) : extra.Substring(index, newline - index);
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("using ", StringComparison.Ordinal) || !trimmed.EndsWith(";", StringComparison.Ordinal))
            {
                break;
            }

            usings.AppendLine(trimmed);
            index = newline < 0 ? extra.Length : newline + 1;
        }

        return usings.Append(Stubs).Append('\n').Append(extra.Substring(index)).ToString();
    }

    internal static CSharpCompilation CreateCompilation(
        string? extraCSharp = null,
        CSharpParseOptions? parseOptions = null,
        IEnumerable<MetadataReference>? extraReferences = null)
    {
        var source = ComposeSource(extraCSharp);
        var references = References().ToList();
        if (extraReferences is not null)
            references.AddRange(extraReferences);

        return CSharpCompilation.Create(
            "Sample",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions ?? ParseOptions()) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Compiles a tiny assembly that carries a <c>KhamsinConfigAttribute</c>.</summary>
    internal static MetadataReference CreateAssemblyWithConfig(string assemblyName, string json)
    {
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(json, quote: true);
        var source = $$"""
            [assembly: global::Khamsin.KhamsinConfigAttribute(Json = {{literal}})]
            public static class ConfigMarker { }
            """;

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, ParseOptions()) },
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    internal static ImmutableArray<MetadataReference> References()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();
        foreach (var assembly in new[] { typeof(object).Assembly, typeof(Enumerable).Assembly, typeof(KhamsinEngine).Assembly })
        {
            if (paths.Add(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDirectory != null)
        {
            foreach (var name in new[] { "System.Runtime.dll", "System.Collections.dll", "netstandard.dll" })
            {
                var path = Path.Combine(runtimeDirectory, name);
                if (File.Exists(path) && paths.Add(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        return references.ToImmutableArray();
    }
}
