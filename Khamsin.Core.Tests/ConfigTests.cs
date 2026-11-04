namespace Khamsin.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void Json_escapes_and_arrays_survive_a_round_trip()
    {
        var config = Parse("""
            {
              "fonts": { "display": "Caf\u00e9 \"Pro\", Georgia" },
              "textSizes": { "pair": ["1.5rem", "2rem"], "plain": "0.75rem" }
            }
            """);

        Assert.Equal("Café \"Pro\", Georgia", config.Fonts!["display"]);
        Assert.Equal("1.5rem", config.TextSizes!["pair"].Size);
        Assert.Equal("2rem", config.TextSizes!["pair"].LineHeight);
        Assert.Equal("0.75rem", config.TextSizes!["plain"].Size);
        Assert.Equal("0.75rem", config.TextSizes!["plain"].LineHeight);
    }

    [Theory]
    [InlineData("""{ "dark": }""")]
    [InlineData("""{ "dark": ".a" } trailing""")]
    [InlineData("""{ "dark": ".a", "dark": ".b" }""")]
    [InlineData("""{ "fonts": { "display": "unterminated }""")]
    [InlineData("""{ "colors": ["not", "an", "object"] }""")]
    [InlineData("""{ fonts: "x" }""")]
    [InlineData("""{ "textSizes": { "weird": ["1rem", "2rem", "3rem"] } }""")]
    public void Malformed_documents_and_entries_are_rejected(string json)
    {
        Assert.False(KhamsinConfig.TryParse(json, out _, out var errors));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Duplicate_json_keys_are_reported_with_a_position()
    {
        Assert.False(KhamsinConfig.TryParse("""{ "dark": ".a", "dark": ".b" }""", out _, out var errors));
        var error = Assert.Single(errors);
        Assert.Contains("duplicate", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Line 1", error);
    }

    [Fact]
    public void Full_schema_parses_every_category()
    {
        var config = Parse("""
            {
              "dark": ".theme-dark",
              "spacing": "0.3rem",
              "colors": {
                "primary": "var(--primary)",
                "state": { "DEFAULT": "var(--state)", "active": "var(--state-active)" }
              },
              "fonts": { "display": "Georgia, serif" },
              "radius": { "card": "1.25rem" },
              "shadows": { "glow": "0 0 12px var(--primary)" },
              "textSizes": { "display": ["2rem", "2.4rem"], "tiny": "0.6rem" },
              "eases": { "snap": "cubic-bezier(0.8,0,0.2,1)" },
              "breakpoints": { "3xl": "112rem" },
              "containers": { "content": "48rem" }
            }
            """);

        Assert.Equal(".theme-dark", config.Dark);
        Assert.Equal("0.3rem", config.Spacing);
        Assert.Equal("var(--primary)", config.Colors!["primary"]);
        Assert.Equal("var(--state)", config.Colors!["state"]);
        Assert.Equal("var(--state-active)", config.Colors!["state-active"]);
        Assert.Equal("Georgia, serif", config.Fonts!["display"]);
        Assert.Equal("1.25rem", config.Radius!["card"]);
        Assert.Equal("0 0 12px var(--primary)", config.Shadows!["glow"]);
        Assert.Equal("2rem", config.TextSizes!["display"].Size);
        Assert.Equal("2.4rem", config.TextSizes!["display"].LineHeight);
        Assert.Equal("0.6rem", config.TextSizes!["tiny"].Size);
        Assert.Equal("cubic-bezier(0.8,0,0.2,1)", config.Eases!["snap"]);
        Assert.Equal("112rem", config.Breakpoints!["3xl"]);
        Assert.Equal("48rem", config.Containers!["content"]);
    }

    [Fact]
    public void Invalid_entries_are_reported_not_thrown()
    {
        Assert.False(KhamsinConfig.TryParse(
            """{ "colors": { "primary": "red; }" }, "bogus": true, "fonts": { "Bad Name": "x" } }""",
            out var config,
            out var errors));

        Assert.Null(config);
        Assert.Contains(errors, e => e.Contains("'colors.primary'"));
        Assert.Contains(errors, e => e.Contains("Unknown configuration key 'bogus'"));
        Assert.Contains(errors, e => e.Contains("'fonts.Bad Name'"));
    }

    [Fact]
    public void ToTheme_merges_over_the_defaults()
    {
        var theme = Parse("""
            {
              "dark": ".theme-dark",
              "colors": { "primary": "#0a7d33" },
              "fonts": { "display": "Georgia, serif" },
              "breakpoints": { "3xl": "112rem" }
            }
            """).ToTheme();

        Assert.True(theme.IsKnownColor("primary"));
        Assert.True(theme.IsKnownColor("ocean-600"), "built-in palette entries survive the merge");
        Assert.True(theme.Fonts.ContainsKey("display"));
        Assert.True(theme.Fonts.ContainsKey("sans"), "built-in font tokens survive the merge");
        Assert.True(theme.Breakpoints.ContainsKey("3xl"));
        Assert.Equal(".theme-dark", theme.DarkSelector);
    }

    [Fact]
    public void Config_tokens_parse_and_emit()
    {
        var theme = Parse("""
            {
              "colors": { "primary": "#0a7d33" },
              "fonts": { "display": "Georgia, serif" },
              "radius": { "card": "1.25rem" }
            }
            """).ToTheme();
        var engine = new KhamsinEngine(theme);

        Assert.True(engine.TryParse("bg-primary", out var bg, out var bgError), bgError?.Message);
        Assert.Equal("bg-color", bg.Entry.Group);
        Assert.True(engine.TryParse("font-display", out var font, out var fontError), fontError?.Message);
        Assert.Equal("font-family", font.Entry.Group);
        Assert.True(engine.TryParse("rounded-card", out var radius, out var radiusError), radiusError?.Message);

        var result = engine.Generate(["bg-primary", "text-primary/50", "font-display", "rounded-card"]);
        Assert.Empty(result.UnknownClasses);
        var css = result.Css;
        Assert.Contains("--color-primary:#0a7d33", css);
        Assert.Contains(".bg-primary{background-color:var(--color-primary)}", css);
        Assert.Contains("--font-display:Georgia, serif", css);
        Assert.Contains(".font-display{font-family:var(--font-display)}", css);
        Assert.Contains("--radius-card:1.25rem", css);
    }

    [Fact]
    public void Dark_selector_override_changes_emission()
    {
        var theme = Parse("""{ "dark": ".theme-dark" }""").ToTheme();
        var engine = new KhamsinEngine(theme);

        var css = engine.GenerateCss(["dark:bg-forest-600"]);
        Assert.Contains(".theme-dark .dark\\:bg-forest-600{", css);
        Assert.DoesNotContain(".dark .dark\\:bg-forest-600{", css);
    }

    [Fact]
    public void Custom_breakpoints_become_variants_in_width_order()
    {
        var theme = Parse("""{ "breakpoints": { "3xl": "112rem", "xs": "20rem" } }""").ToTheme();
        var engine = new KhamsinEngine(theme);

        Assert.True(engine.TryParse("3xl:hidden", out _, out var error), error?.Message);
        Assert.True(engine.TryParse("max-xs:block", out _, out error), error?.Message);

        var css = engine.GenerateCss(["3xl:hidden", "xs:hidden"]);
        Assert.Contains("(min-width: 112rem)", css);
        Assert.Contains("(min-width: 20rem)", css);
        // Cascade weights follow viewport width: xs rules come before 3xl rules.
        Assert.True(css.IndexOf("(min-width: 20rem)", StringComparison.Ordinal) < css.IndexOf("(min-width: 112rem)", StringComparison.Ordinal));
    }

    [Fact]
    public void Merger_resolves_conflicts_for_configured_colors()
    {
        KhamsinMerger.Configure(Parse("""{ "colors": { "primary": "#0a7d33" } }"""));

        Assert.Equal("bg-primary", KhamsinMerger.Merge("bg-white", "bg-primary"));
        Assert.Equal("bg-white", KhamsinMerger.Merge("bg-primary", "bg-white"));
        Assert.Equal("p-4 bg-primary", KhamsinMerger.Merge("p-4", "bg-primary"));
    }

    [Fact]
    public void Merge_applies_configurations_in_order()
    {
        var library = Parse("""
            {
              "dark": ".library-dark",
              "colors": { "primary": "#111111", "brand": "#222222" },
              "fonts": { "display": "Georgia, serif" },
              "textSizes": { "display": ["2rem", "2.4rem"] }
            }
            """);
        var app = Parse("""{ "colors": { "primary": "#333333" } }""");

        var merged = KhamsinConfig.Merge([library, app]);

        Assert.Equal(".library-dark", merged.Dark);
        Assert.Equal("#333333", merged.Colors!["primary"]);
        Assert.Equal("#222222", merged.Colors!["brand"]);
        Assert.Equal("Georgia, serif", merged.Fonts!["display"]);
        Assert.Equal("2rem", merged.TextSizes!["display"].Size);
    }

    [Fact]
    public void Merge_leaves_unspecified_entries_for_the_defaults()
    {
        var merged = KhamsinConfig.Merge([Parse("""{ "colors": { "primary": "#111111" } }""")]);

        Assert.Null(merged.Spacing);
        Assert.Null(merged.Fonts);
        Assert.True(merged.ToTheme().Fonts.ContainsKey("sans"), "built-in tokens still apply");
        Assert.True(merged.ToTheme().IsKnownColor("ocean-600"), "built-in palette still applies");
    }

    private static KhamsinConfig Parse(string json)
    {
        Assert.True(KhamsinConfig.TryParse(json, out var config, out var errors), string.Join("; ", errors));
        return config!;
    }
}
