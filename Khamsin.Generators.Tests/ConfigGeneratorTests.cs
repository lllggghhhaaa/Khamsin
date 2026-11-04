using Microsoft.CodeAnalysis;
using Xunit;

namespace Khamsin.Generators.Tests;

public class ConfigGeneratorTests
{
    private const string Config = """
        {
          "dark": ".theme-dark",
          "colors": { "primary": "#0a7d33" },
          "fonts": { "display": "Georgia, serif" }
        }
        """;

    [Fact]
    public void Registration_is_emitted_and_compiles()
    {
        var (result, output, diagnostics) = GeneratorHarness.Run(
            [(RazorPath, """<p class="text-lg">Home</p>""")],
            configJson: Config);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var code = Generated(result, "Khamsin.ThemeRegistration.g.cs");
        Assert.Contains("ModuleInitializer", code);
        Assert.Contains("KhamsinMerger.Configure", code);
        Assert.Contains("\"primary\"", code);
        Assert.Contains("\".theme-dark\"", code);
        // The generated registration references real runtime types; the updated compilation must compile.
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }

    [Fact]
    public void No_registration_without_a_config_file()
    {
        var (result, _, _) = GeneratorHarness.Run([(RazorPath, """<p class="text-lg">Home</p>""")]);

        var generated = result.GeneratedTrees.Select(tree => tree.FilePath).ToList();
        Assert.DoesNotContain(generated, path => path.EndsWith("Khamsin.ThemeRegistration.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Malformed_config_reports_KHM005()
    {
        var (_, _, diagnostics) = GeneratorHarness.Run(
            [(RazorPath, """<p class="text-lg">Home</p>""")],
            configJson: """{ "colors": { "primary": } }""");

        Assert.Contains(diagnostics, d => d.Id == "KHM005");
    }

    [Fact]
    public void Config_classes_join_the_closed_set()
    {
        const string razor = """<p class="bg-primary text-ocean-600">Home</p>""";

        var without = GeneratorHarness.Run([(RazorPath, razor)]);
        var manifestWithout = Generated(without.Result, "Home.Khamsin.g.cs");
        Assert.DoesNotContain("bg-primary", manifestWithout);
        Assert.Contains("text-ocean-600", manifestWithout);

        var with = GeneratorHarness.Run([(RazorPath, razor)], configJson: Config);
        var manifestWith = Generated(with.Result, "Home.Khamsin.g.cs");
        Assert.Contains("bg-primary", manifestWith);
    }

    [Fact]
    public void Razor_Khamsin_attribute_accepts_config_colors()
    {
        const string razor = """<p Khamsin="bg-primary hover:bg-primary/50">Home</p>""";

        var with = GeneratorHarness.Run([(RazorPath, razor)], configJson: Config);
        Assert.DoesNotContain(with.Diagnostics, d => d.Id == "KHM002");

        var without = GeneratorHarness.Run([(RazorPath, razor)]);
        Assert.Contains(without.Diagnostics, d => d.Id == "KHM002");
    }

    [Fact]
    public void Configured_dark_selector_and_colors_validate_together()
    {
        // dark:bg-primary parses only when the config engine is in effect; the literal must not
        // produce KHM002, and the registration must carry the configured selector.
        var (result, _, diagnostics) = GeneratorHarness.Run(
            [(RazorPath, """<p Khamsin="dark:bg-primary">Home</p>""")],
            configJson: Config);

        Assert.DoesNotContain(diagnostics, d => d.Id == "KHM002");
        Assert.Contains(".theme-dark", Generated(result, "Khamsin.ThemeRegistration.g.cs"));
    }

    [Fact]
    public void Own_config_is_embedded_for_consumers()
    {
        var (result, output, diagnostics) = GeneratorHarness.Run(
            [(RazorPath, """<p class="text-lg">Home</p>""")],
            configJson: Config);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var code = Generated(result, "Khamsin.Config.g.cs");
        Assert.Contains("KhamsinConfigAttribute", code);
        Assert.Contains("theme-dark", code);
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }

    [Fact]
    public void Referenced_library_config_joins_the_closed_set()
    {
        var library = GeneratorHarness.CreateAssemblyWithConfig(
            "SampleLib",
            """{ "colors": { "brand": "#123456" }, "fonts": { "display": "Georgia, serif" } }""");

        var (result, output, diagnostics) = GeneratorHarness.Run(
            [(RazorPath, """<p Khamsin="bg-brand font-display">Home</p>""")],
            extraReferences: [library]);

        Assert.DoesNotContain(diagnostics, d => d.Id is "KHM002" or "KHM006");
        Assert.Contains("bg-brand", Generated(result, "Home.Khamsin.g.cs"));
        var registration = Generated(result, "Khamsin.ThemeRegistration.g.cs");
        Assert.Contains("\"brand\"", registration);
        Assert.Contains("\"#123456\"", registration);
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }

    [Fact]
    public void App_config_overrides_referenced_library_config()
    {
        var library = GeneratorHarness.CreateAssemblyWithConfig(
            "SampleLib",
            """{ "colors": { "primary": "#111111" } }""");

        var (result, _, diagnostics) = GeneratorHarness.Run(
            [(RazorPath, """<p class="text-lg">Home</p>""")],
            configJson: """{ "colors": { "primary": "#222222" } }""",
            extraReferences: [library]);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var registration = Generated(result, "Khamsin.ThemeRegistration.g.cs");
        Assert.Contains("\"#222222\"", registration);
        Assert.DoesNotContain("\"#111111\"", registration);
    }

    [Fact]
    public void Invalid_referenced_config_reports_KHM006()
    {
        var library = GeneratorHarness.CreateAssemblyWithConfig(
            "BrokenLib",
            """{ "colors": { "brand": } }""");

        var (_, _, diagnostics) = GeneratorHarness.Run(
            [(RazorPath, """<p class="text-lg">Home</p>""")],
            extraReferences: [library]);

        Assert.Contains(diagnostics, d => d.Id == "KHM006" && d.Severity == DiagnosticSeverity.Warning);
    }

    private const string RazorPath = @"C:\proj\Components\Home.razor";

    private static string Generated(GeneratorDriverRunResult result, string hintName)
    {
        var tree = result.GeneratedTrees.Single(tree => tree.FilePath.Replace('\\', '/').EndsWith(hintName, StringComparison.Ordinal));
        return tree.ToString();
    }
}
