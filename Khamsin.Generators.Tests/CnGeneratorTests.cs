using Microsoft.CodeAnalysis;

using Xunit;

namespace Khamsin.Generators.Tests;

/// <summary>Build-time extraction of Cn.Of/Cn.Merge call sites.</summary>
public class CnGeneratorTests
{
    private const string RazorPath = @"C:\proj\Components\Home.razor";

    [Fact]
    public void Cn_call_in_razor_class_attribute_extracts_all_branches()
    {
        const string razor = """<div class="@(Cn.Of("p-4", isActive ? "bg-forest-600" : "bg-ember-600"))">Home</div>""";

        var (result, _, _) = GeneratorHarness.Run([(RazorPath, razor)]);
        var manifest = Generated(result, "Home.Khamsin.g.cs");
        Assert.Contains("p-4", manifest);
        Assert.Contains("bg-forest-600", manifest);
        Assert.Contains("bg-ember-600", manifest);
    }

    [Fact]
    public void Cn_field_initializer_in_csharp_extracts()
    {
        var extra = "using Khamsin;\n" + GeneratorHarness.Components + """

            namespace Sample.Components
            {
                public partial class Panel : global::Microsoft.AspNetCore.Components.ComponentBase
                {
                    private static readonly KhamsinClass Card = Cn.Of("p-4", compact ? "bg-forest-600" : "bg-ocean-600");
                }
            }
            """;

        var (result, _, _) = GeneratorHarness.Run(Array.Empty<(string, string)>(), extra);
        var manifest = Generated(result, "Panel.Khamsin.g.cs");
        Assert.Contains("p-4", manifest);
        Assert.Contains("bg-forest-600", manifest);
        Assert.Contains("bg-ocean-600", manifest);
    }

    [Fact]
    public void Cn_merge_collects_the_defaults_and_overrides()
    {
        var extra = "using Khamsin;\n" + GeneratorHarness.Components + """

            namespace Sample.Components
            {
                public partial class Row : global::Microsoft.AspNetCore.Components.ComponentBase
                {
                    private static readonly bool Compact = true;
                    private static readonly KhamsinClass Class = Cn.Merge("p-4 rounded-lg", Compact, "p-2");
                }
            }
            """;

        var (result, _, _) = GeneratorHarness.Run(Array.Empty<(string, string)>(), extra);
        var manifest = Generated(result, "Row.Khamsin.g.cs");
        Assert.Contains("p-4", manifest);
        Assert.Contains("rounded-lg", manifest);
        Assert.Contains("p-2", manifest);
    }

    [Fact]
    public void Cn_literals_feed_the_manifest_not_the_diagnostic_paths()
    {
        const string razor = """<div Khamsin="@(Cn.Of("p-4 shadow-huge"))">Home</div>""";

        var (_, _, diagnostics) = GeneratorHarness.Run([(RazorPath, razor)]);
        Assert.DoesNotContain(diagnostics, d => d.Id == "KHM002");
    }

    [Fact]
    public void Khamsin_attribute_with_cn_call_is_not_reported_non_literal()
    {
        const string razor = """<div Khamsin="@(Cn.Of(SomeClasses()))">Home</div>""";

        var (_, _, diagnostics) = GeneratorHarness.Run([(RazorPath, razor)]);
        Assert.DoesNotContain(diagnostics, d => d.Id == "KHM001");
    }

    private static string Generated(GeneratorDriverRunResult result, string hintName)
    {
        var tree = result.GeneratedTrees.Single(tree => tree.FilePath.Replace('\\', '/').EndsWith(hintName, StringComparison.Ordinal));
        return tree.ToString();
    }
}
