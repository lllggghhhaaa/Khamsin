using Xunit;

namespace Khamsin.Build.Tests;

/// <summary>
/// Phase-4 acceptance tests. Every test writes a throw-away app plus library and builds it with the
/// real SDK, so the props/targets wiring, the metadata reader and the source scanners all run.
/// </summary>
public class BuildTaskTests
{
    [Fact]
    public void Build_emits_app_and_used_component_classes()
    {
        using var fixture = new BuildFixture();

        var result = fixture.Build();

        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;
        Assert.Contains(".tracking-wide{", css, StringComparison.Ordinal);
        Assert.Contains(".text-3xl{", css, StringComparison.Ordinal);
        Assert.Contains(".rounded-lg{", css, StringComparison.Ordinal);
        Assert.Contains(".bg-white{", css, StringComparison.Ordinal);
        Assert.Contains(".p-8{", css, StringComparison.Ordinal);
        Assert.Contains(".text-lg{", css, StringComparison.Ordinal);
        Assert.Contains(":root{", css, StringComparison.Ordinal);
        Assert.Contains("@layer utilities", css, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unused_library_component_contributes_nothing()
    {
        using var fixture = new BuildFixture();

        var result = fixture.Build();

        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;
        Assert.DoesNotContain("rounded-xl", css, StringComparison.Ordinal);
        Assert.DoesNotContain("bg-rose-700", css, StringComparison.Ordinal);
        Assert.DoesNotContain("tracking-widest", css, StringComparison.Ordinal);
    }

    [Fact]
    public void A_consumer_override_keeps_both_statically_reachable_classes()
    {
        using var fixture = new BuildFixture();

        var result = fixture.Build();

        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;
        // The library default and the consumer override both exist as CSS; the runtime merge picks
        // p-8, so the stylesheet must not be forced to choose at build time.
        Assert.Contains(".p-4{", css, StringComparison.Ordinal);
        Assert.Contains(".p-8{", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Variant_literals_in_razor_code_blocks_reach_the_stylesheet()
    {
        using var fixture = new BuildFixture(
            """
            @page "/"

            <span class="@Status">Status</span>
            <Card Khamsin="@("p-8")">Body</Card>

            @code {
                private static readonly KhamsinClass Status = Kx.Variant("warn", ("ok", "text-forest-600"), ("warn", "text-amber-600"));
            }
            """);

        var result = fixture.Build();

        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;
        Assert.Contains(".text-forest-600{", css, StringComparison.Ordinal);
        Assert.Contains(".text-amber-600{", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpolated_input_fails_the_build_with_KHM001()
    {
        using var fixture = new BuildFixture(
            """
            @page "/"

            <div Khamsin="@classes">Dynamic</div>

            @code {
                private string classes = "p-4";
            }
            """);

        var result = fixture.Build();

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("KHM001", result.AllOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void A_no_op_rebuild_skips_the_css_target()
    {
        using var fixture = new BuildFixture();

        var first = fixture.Build("normal");
        Assert.True(first.ExitCode == 0, first.AllOutput);

        var second = fixture.Build("normal");

        Assert.True(second.ExitCode == 0, second.AllOutput);
        Assert.Contains(
            "Skipping target \"KhamsinGenerateCss\"",
            second.AllOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Output_is_byte_identical_when_regenerated()
    {
        using var fixture = new BuildFixture();
        var first = fixture.Build();
        Assert.True(first.ExitCode == 0, first.AllOutput);
        var firstCss = fixture.Css;

        File.Delete(fixture.CssPath);
        var second = fixture.Build();
        Assert.True(second.ExitCode == 0, second.AllOutput);

        Assert.Equal(firstCss, fixture.Css, StringComparer.Ordinal);
    }

    [Fact]
    public void Generic_library_components_contribute_their_classes()
    {
        using var fixture = new BuildFixture(
            """
            @page "/"

            <Picker Value="@("x")" />
            """);

        var result = fixture.Build();

        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;
        Assert.Contains(".bg-violet-700{", css, StringComparison.Ordinal);
        Assert.Contains(".text-forest-600{", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_generic_components_fall_back_to_the_stub_manifest_and_IL_usage()
    {
        using var fixture = new BuildFixture(
            """
            @page "/"

            <SampleLib.Legacy.LegacyCard Value="@("x")" />
            """);

        var result = fixture.Build();

        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;
        // The stub carries the generic component's manifest; the child is only reachable through
        // the real generic type's IL, not through attribute Uses.
        Assert.Contains(".bg-amber-600{", css, StringComparison.Ordinal);
        Assert.Contains(".text-forest-600{", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Minification_can_be_disabled_for_debugging()
    {
        using var fixture = new BuildFixture(minify: false);

        var result = fixture.Build();

        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;
        Assert.Contains("\n  .p-4 {", css, StringComparison.Ordinal);
        Assert.Contains("\n  .rounded-lg {", css, StringComparison.Ordinal);
        Assert.Contains("@layer utilities {", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Toggling_minification_regenerates_the_stylesheet()
    {
        using var fixture = new BuildFixture();
        var first = fixture.Build();
        Assert.True(first.ExitCode == 0, first.AllOutput);
        var minified = fixture.Css;
        Assert.DoesNotContain("\n  .p-4 {", minified, StringComparison.Ordinal);

        fixture.SetMinify(false);
        var second = fixture.Build();

        Assert.True(second.ExitCode == 0, second.AllOutput);
        Assert.Contains("\n  .p-4 {", fixture.Css, StringComparison.Ordinal);
        Assert.NotEqual(minified, fixture.Css);
    }
}
