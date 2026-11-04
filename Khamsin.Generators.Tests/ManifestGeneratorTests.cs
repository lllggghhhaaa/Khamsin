using Xunit;

namespace Khamsin.Generators.Tests;

public sealed class ManifestGeneratorTests
{
    private const string Path = @"C:\proj\Components\Widget.razor";

    [Fact]
    public void Emits_literals_defaults_and_used_components()
    {
        const string razor = """
            @using Sample.Components
            <section class="p-4 bg-ink-50">
                <Child Khamsin="@("mt-2")" />
            </section>

            @code
            {
                private static readonly KhamsinClass Defaults = "rounded-lg";
            }
            """;

        var (result, _, diagnostics) = GeneratorHarness.Run(new[] { (Path, razor) });

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        var generated = result.GeneratedTrees.Select(tree => tree.ToString()).ToList();
        Assert.Contains(generated, source => source.Contains("[global::Khamsin.KhamsinManifestAttribute(", StringComparison.Ordinal));

        var manifest = generated.Single(source => source.Contains("partial class Widget", StringComparison.Ordinal));
        Assert.Contains("Classes=\"mt-2 rounded-lg p-4 bg-ink-50\"", manifest, StringComparison.Ordinal);
        Assert.Contains("Uses=new global::System.Type[] { typeof(global::Sample.Components.Child) }", manifest, StringComparison.Ordinal);
        Assert.Contains("public partial class Widget", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_non_literal_input()
    {
        const string razor = """
            @using Sample.Components
            <Child Khamsin="@State.CurrentClasses" />

            @code
            {
                private readonly State State = new();

                private sealed class State
                {
                    public string CurrentClasses { get; set; } = "";
                }
            }
            """;

        var (_, _, diagnostics) = GeneratorHarness.Run(new[] { (Path, razor) });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "KHM001");
    }

    [Fact]
    public void Reports_unknown_class()
    {
        const string razor = """
            @using Sample.Components
            <Child Khamsin="px-2 not-a-real-utility" />
            """;

        var (_, _, diagnostics) = GeneratorHarness.Run(new[] { (Path, razor) });

        var unknown = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "KHM002");
        Assert.Contains("not-a-real-utility", unknown.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_conflicting_classes()
    {
        const string razor = """
            @using Sample.Components
            <Child Khamsin="px-2 px-4" />
            """;

        var (_, _, diagnostics) = GeneratorHarness.Run(new[] { (Path, razor) });

        var conflict = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "KHM003");
        Assert.Contains("px-2", conflict.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("px-4", conflict.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Preserves_internal_accessibility()
    {
        const string razor = """
            <div class="p-4"></div>
            """;

        var extra = GeneratorHarness.Components + """

            namespace Sample.Components
            {
                internal partial class Widget : global::Microsoft.AspNetCore.Components.ComponentBase { }
            }
            """;

        var (result, _, _) = GeneratorHarness.Run(new[] { (Path, razor) }, extra);

        var manifest = result.GeneratedTrees.Select(tree => tree.ToString())
            .Single(source => source.Contains("partial class Widget", StringComparison.Ordinal));
        Assert.Contains("internal partial class Widget", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Assembly_manifest_carries_kx_literals_from_helper_classes()
    {
        var extra = "using Khamsin;\n" + GeneratorHarness.Components + """

            namespace Sample.Components
            {
                internal static class Styles
                {
                    internal static readonly KhamsinClass Card = Kx.When(true, "p-2", "p-4");
                }
            }
            """;

        var (result, _, _) = GeneratorHarness.Run(Array.Empty<(string, string)>(), extra);

        var assembly = Assert.Single(
            result.GeneratedTrees.Select(tree => tree.ToString()),
            source => source.Contains("assembly: global::Khamsin.KhamsinManifestAttribute", StringComparison.Ordinal));
        Assert.Contains("p-2", assembly, StringComparison.Ordinal);
        Assert.Contains("p-4", assembly, StringComparison.Ordinal);
    }
}
