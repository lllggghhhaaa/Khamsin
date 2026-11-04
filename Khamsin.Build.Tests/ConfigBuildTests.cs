using Xunit;

namespace Khamsin.Build.Tests;

/// <summary>
/// End-to-end coverage for khamsin.json: the props/targets wiring, the task's ConfigPath
/// parameter, the incremental input (config edits regenerate exactly once) and the analyzer's
/// config-aware validation during a real SDK build.
/// </summary>
public class ConfigBuildTests
{
    [Fact]
    public void Config_tokens_reach_the_generated_stylesheet()
    {
        using var fixture = new BuildFixture(homeMarkup: """
            @page "/"

            <h1 class="text-3xl tracking-wide bg-primary dark:bg-primary">Home</h1>
            """);
        fixture.SetConfig("""
            {
              "dark": ".theme-dark",
              "colors": { "primary": "#0a7d33" },
              "fonts": { "display": "Georgia, serif" }
            }
            """);

        var result = fixture.Build();
        Assert.True(result.ExitCode == 0, result.AllOutput);
        var css = fixture.Css;

        Assert.Contains("--color-primary:#0a7d33", css);
        Assert.Contains(".bg-primary{background-color:var(--color-primary)}", css);
        Assert.Contains("--font-display:Georgia, serif", css);
        Assert.Contains(".theme-dark .dark\\:bg-primary{", css);
        Assert.DoesNotContain(".dark .dark\\:bg-primary{", css);
    }

    [Fact]
    public void Config_colors_pass_analyzer_validation()
    {
        using var fixture = new BuildFixture(homeMarkup: """
            @page "/"

            <h1 class="text-3xl tracking-wide bg-primary">Home</h1>
            """);
        fixture.SetConfig("""{ "colors": { "primary": "#0a7d33" } }""");

        var result = fixture.Build();
        Assert.True(result.ExitCode == 0, result.AllOutput);
        Assert.DoesNotContain("KHM002", result.AllOutput);
        Assert.Contains(".bg-primary{", fixture.Css);
    }

    [Fact]
    public void Config_edit_regenerates_the_stylesheet()
    {
        using var fixture = new BuildFixture();
        fixture.SetConfig("""{ "colors": { "primary": "#0a7d33" } }""");
        Assert.Equal(0, fixture.Build().ExitCode);
        Assert.Contains("#0a7d33", fixture.Css);

        fixture.SetConfig("""{ "colors": { "primary": "#1240ab" } }""");
        var rebuild = fixture.Build();
        Assert.Equal(0, rebuild.ExitCode);
        Assert.Contains("#1240ab", fixture.Css);
        Assert.DoesNotContain("#0a7d33", fixture.Css);
    }

    [Fact]
    public void Invalid_config_fails_the_build_with_errors()
    {
        using var fixture = new BuildFixture();
        fixture.SetConfig("""{ "colors": { "primary": "red; bad" } }""");

        var result = fixture.Build();
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("KHM", result.AllOutput);
    }

    [Fact]
    public void Library_config_reaches_the_app_stylesheet()
    {
        using var fixture = new BuildFixture(homeMarkup: """
            @page "/"

            <Card Khamsin="@("bg-brand")">Body</Card>
            """);
        fixture.SetLibraryConfig("""{ "colors": { "brand": "#123456" } }""");

        var result = fixture.Build();
        Assert.True(result.ExitCode == 0, result.AllOutput);
        Assert.DoesNotContain("KHM002", result.AllOutput);
        Assert.Contains("--color-brand:#123456", fixture.Css);
        Assert.Contains(".bg-brand{background-color:var(--color-brand)}", fixture.Css);
    }

    [Fact]
    public void App_config_overrides_library_config()
    {
        using var fixture = new BuildFixture(homeMarkup: """
            @page "/"

            <Card Khamsin="@("bg-brand")">Body</Card>
            """);
        fixture.SetLibraryConfig("""{ "colors": { "brand": "#123456" } }""");
        fixture.SetConfig("""{ "colors": { "brand": "#654321" } }""");

        var result = fixture.Build();
        Assert.True(result.ExitCode == 0, result.AllOutput);
        Assert.Contains("--color-brand:#654321", fixture.Css);
        Assert.DoesNotContain("--color-brand:#123456", fixture.Css);
    }
}
