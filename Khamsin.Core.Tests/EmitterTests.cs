namespace Khamsin.Core.Tests;

public class EmitterTests
{
    private static string Css(params string[] classes)
    {
        var parsed = new List<ParsedClass>();
        foreach (var token in classes)
        {
            Assert.True(KhamsinEngine.Default.TryParse(token, out var parsedClass, out var error), error?.Message);
            parsed.Add(parsedClass);
        }

        return CssEmitter.Emit(parsed, Theme.Default, includeTheme: false);
    }

    [Fact]
    public void Emits_spacing_utility()
    {
        Assert.Equal("@layer utilities{.p-4{padding:calc(var(--spacing) * 4)}}", Css("p-4"));
    }

    [Fact]
    public void Emits_static_utility()
    {
        Assert.Equal("@layer utilities{.flex{display:flex}}", Css("flex"));
    }

    [Fact]
    public void Escapes_selector_and_applies_pseudo_variant()
    {
        Assert.Equal(
            "@layer utilities{.hover\\:bg-ocean-600\\/50:hover{background-color:color-mix(in oklab,var(--color-ocean-600) 50%,transparent)}}",
            Css("hover:bg-ocean-600/50"));
    }

    [Fact]
    public void Wraps_breakpoint_variants_in_media_queries()
    {
        Assert.Equal(
            "@layer utilities{@media (min-width: 48rem){.md\\:hover\\:flex:hover{display:flex}}}",
            Css("md:hover:flex"));
    }

    [Fact]
    public void Escapes_numeric_variant_prefix_with_a_hex_escape()
    {
        Assert.Equal(
            "@layer utilities{@media (min-width: 96rem){.\\32 xl\\:flex{display:flex}}}",
            Css("2xl:flex"));
    }

    [Fact]
    public void Emits_dark_variant_with_ancestor_selector()
    {
        Assert.Equal(
            "@layer utilities{.dark .dark\\:bg-ink-900\\/50{background-color:color-mix(in oklab,var(--color-ink-900) 50%,transparent)}}",
            Css("dark:bg-ink-900/50"));
    }

    [Fact]
    public void Emits_group_and_peer_variants()
    {
        Assert.Equal(
            "@layer utilities{.group:hover .group-hover\\:opacity-50{opacity:0.5}}",
            Css("group-hover:opacity-50"));

        Assert.Equal(
            "@layer utilities{.peer:checked ~ .peer-checked\\:text-forest-600{color:var(--color-forest-600)}}",
            Css("peer-checked:text-forest-600"));
    }

    [Fact]
    public void Emits_fraction_and_negative_values()
    {
        Assert.Equal("@layer utilities{.w-1\\/3{width:33.333333%}}", Css("w-1/3"));
        Assert.Equal("@layer utilities{.-mt-4{margin-top:calc(var(--spacing) * -4)}}", Css("-mt-4"));
    }

    [Fact]
    public void Emits_negative_fraction_translates()
    {
        Assert.Contains(".-translate-x-1\\/2{--khamsin-translate-x:-50%", Css("-translate-x-1/2"));
        Assert.Contains(".-translate-y-1\\/3{--khamsin-translate-y:-33.333333%", Css("-translate-y-1/3"));
        Assert.Contains(".-top-1\\/2{top:-50%}", Css("-top-1/2"));
    }

    [Fact]
    public void Emits_arbitrary_and_css_variable_values()
    {
        Assert.Equal("@layer utilities{.w-\\[320px\\]{width:320px}}", Css("w-[320px]"));
        Assert.Equal("@layer utilities{.w-\\(--sidebar\\){width:var(--sidebar)}}", Css("w-(--sidebar)"));
    }

    [Fact]
    public void Emits_child_selector_utilities()
    {
        Assert.Equal("@layer utilities{.space-y-4>*+*{margin-block-start:calc(var(--spacing) * 4)}}", Css("space-y-4"));
    }

    [Fact]
    public void Emits_important_declarations()
    {
        Assert.Equal("@layer utilities{.\\!p-4{padding:calc(var(--spacing) * 4)!important}}", Css("!p-4"));
    }

    [Fact]
    public void Emits_radius_and_shadow_tokens()
    {
        Assert.Equal("@layer utilities{.rounded-lg{border-radius:var(--radius-lg)}}", Css("rounded-lg"));
        Assert.Equal("@layer utilities{.shadow-md{--khamsin-shadow-colored:0 4px 8px -2px var(--khamsin-shadow-color),0 2px 4px -2px var(--khamsin-shadow-color);box-shadow:var(--shadow-md)}}", Css("shadow-md"));
    }

    [Fact]
    public void Orders_by_property_not_by_input()
    {
        var first = Css("px-2", "p-4", "mx-1");
        var second = Css("mx-1", "p-4", "px-2");

        Assert.Equal(first, second);
        Assert.True(first.IndexOf(".p-4", StringComparison.Ordinal) < first.IndexOf(".px-2", StringComparison.Ordinal));
    }

    [Fact]
    public void Output_is_deterministic_and_deduplicated()
    {
        var once = Css("p-4", "bg-ocean-600", "hover:text-ink-50");
        var twice = Css("hover:text-ink-50", "bg-ocean-600", "p-4", "p-4");

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Escapes_arbitrary_color_tokens()
    {
        Assert.Equal(
            "@layer utilities{.bg-\\[\\#fff\\]{background-color:#fff}}",
            Css("bg-[#fff]"));
    }

    [Fact]
    public void Emits_unknown_classes_nowhere()
    {
        var result = KhamsinEngine.Default.Generate(new[] { "p-4", "definitely-not-a-class" });

        Assert.DoesNotContain("definitely-not-a-class", result.Css);
        Assert.Equal(new[] { "definitely-not-a-class" }, result.UnknownClasses);
    }

    [Fact]
    public void Theme_contains_our_own_tokens()
    {
        var css = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default);

        Assert.StartsWith("@layer theme,base,utilities;@layer theme{:root{", css);
        Assert.Contains("--spacing:0.25rem", css);
        Assert.Contains("--color-ocean-600:#2f6bc6", css);
        Assert.Contains("--radius-lg:0.5rem", css);
        Assert.Contains("--breakpoint-md:48rem", css);
        Assert.Contains("@layer base{*,::before,::after,::backdrop{box-sizing:border-box", css);
        Assert.EndsWith("@layer utilities{}", css);
    }

    [Fact]
    public void Preflight_is_emitted_with_the_theme_only()
    {
        var withTheme = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default);
        var utilitiesOnly = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default, includeTheme: false);

        Assert.Contains("@layer base{*,::before,::after,::backdrop{box-sizing:border-box", withTheme);
        Assert.DoesNotContain("@layer base", utilitiesOnly);
        Assert.Equal("@layer utilities{}", utilitiesOnly);
    }

    [Fact]
    public void Internal_composition_variables_are_registered_as_non_inheriting()
    {
        var css = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default);

        Assert.Contains("@property --khamsin-translate-x{syntax:\"*\";inherits:false}", css);
        Assert.Contains("@property --khamsin-translate-y{syntax:\"*\";inherits:false}", css);
        Assert.Contains("@property --khamsin-ring-color{syntax:\"*\";inherits:false}", css);
        Assert.Contains("@property --khamsin-mask-top{syntax:\"*\";inherits:false}", css);
        Assert.Contains("@property --khamsin-enter-opacity{syntax:\"*\";inherits:false}", css);
        Assert.Contains("@property --khamsin-blur{syntax:\"*\";inherits:false}", css);
    }

    [Fact]
    public void Property_registrations_are_sorted_and_unique()
    {
        var css = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default);
        var names = new List<string>();
        var index = 0;
        while ((index = css.IndexOf("@property ", index, StringComparison.Ordinal)) >= 0)
        {
            var start = index + "@property ".Length;
            var end = css.IndexOf('{', start);
            names.Add(css.Substring(start, end - start));
            index = end;
        }

        Assert.NotEmpty(names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal), names);
    }

    [Fact]
    public void Utilities_only_output_omits_property_registrations()
    {
        var css = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default, includeTheme: false);

        Assert.DoesNotContain("@property", css);
    }

    [Fact]
    public void Readable_property_registrations_are_indented()
    {
        var css = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default, minify: false);

        Assert.Contains("@property --khamsin-translate-x {\n  syntax: \"*\";\n  inherits: false;\n}\n\n", css);
    }

    [Fact]
    public void Readable_output_indents_rules_and_declarations()
    {
        var css = Readable("p-4", "border", "md:flex");

        Assert.StartsWith("@layer utilities {\n", css);
        Assert.Contains("  .p-4 {\n    padding: calc(var(--spacing) * 4);\n  }\n", css);
        Assert.Contains("  .border {\n    border-width: 1px;\n    border-style: solid;\n  }\n", css);
        Assert.Contains("  @media (min-width: 48rem) {\n    .md\\:flex {\n      display: flex;\n    }\n  }\n", css);
        Assert.EndsWith("}", css);
    }

    [Fact]
    public void Readable_theme_layer_uses_one_token_per_line()
    {
        var css = CssEmitter.Emit(Array.Empty<ParsedClass>(), Theme.Default, minify: false);

        Assert.StartsWith("@layer theme, base, utilities;\n\n@layer theme {\n  :root {\n    --breakpoint-2xl: 96rem;\n", css);
        Assert.Contains("\n  }\n}\n\n@layer base {\n  *,::before,::after,::backdrop {\n    box-sizing: border-box;\n", css);
        Assert.Contains("\n}\n\n@layer utilities {\n", css);
    }

    [Fact]
    public void Readable_output_keeps_the_important_suffix()
    {
        Assert.Contains("padding: calc(var(--spacing) * 4) !important;", Readable("!p-4"));
    }

    [Fact]
    public void Engine_switches_between_minified_and_readable()
    {
        var minified = KhamsinEngine.Default.GenerateCss(new[] { "p-4" });
        var readable = KhamsinEngine.Default.GenerateCss(new[] { "p-4" }, minify: false);

        Assert.DoesNotContain("\n  .p-4", minified);
        Assert.Contains("\n  .p-4 {", readable);
    }

    private static string Readable(params string[] classes)
    {
        var parsed = new List<ParsedClass>();
        foreach (var token in classes)
        {
            Assert.True(KhamsinEngine.Default.TryParse(token, out var parsedClass, out var error), error?.Message);
            parsed.Add(parsedClass);
        }

        return CssEmitter.Emit(parsed, Theme.Default, includeTheme: false, minify: false);
    }
}
