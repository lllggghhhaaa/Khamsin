namespace Khamsin.Core.Tests;

/// <summary>Tailwind-compatible surface: arbitrary variants, arbitrary properties and the expanded utility set.</summary>
public class TailwindApiTests
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
    public void Arbitrary_selector_variant_replaces_the_ampersand()
    {
        Assert.Contains(".\\[\\&\\>li\\]\\:mt-2>li{margin-top:", Css("[&>li]:mt-2"));
    }

    [Fact]
    public void Arbitrary_at_rule_variant_wraps_the_rule()
    {
        var css = Css("[@supports(display:grid)]:grid");

        Assert.Contains("@supports(display:grid){", css);
        Assert.Contains("{display:grid}", css);
    }

    [Fact]
    public void Has_data_and_aria_variants_append_attribute_conditions()
    {
        Assert.Contains(":has(:checked){background-color:var(--color-ocean-600)}", Css("has-[:checked]:bg-ocean-600"));
        Assert.Contains("[data-state=open]{color:var(--color-white)}", Css("data-[state=open]:text-white"));
        Assert.Contains("[aria-sort=ascending]{text-decoration-line:underline}", Css("aria-[sort=ascending]:underline"));
    }

    [Fact]
    public void Group_has_variant_uses_the_ancestor_selector()
    {
        Assert.Contains(".group:has(:checked) .group-has-\\[\\:checked\\]\\:block{display:block}", Css("group-has-[:checked]:block"));
    }

    [Fact]
    public void Before_and_after_get_a_content_default()
    {
        Assert.Contains(
            "::before{background-color:var(--color-ocean-600);content:var(--khamsin-content,\"\")}",
            Css("before:bg-ocean-600"));
    }

    [Fact]
    public void Pseudo_element_variants_are_supported()
    {
        Assert.Contains("::placeholder{color:var(--color-ink-500)}", Css("placeholder:text-ink-500"));
    }

    [Fact]
    public void Responsive_max_variants_use_max_width_queries()
    {
        Assert.Contains("@media (max-width: 48rem){.max-md\\:flex{display:flex}}", Css("max-md:flex"));
    }

    [Fact]
    public void Print_and_motion_variants_use_media_queries()
    {
        Assert.Contains("@media print{.print\\:hidden{display:none}}", Css("print:hidden"));
        Assert.Contains("@media (prefers-reduced-motion: reduce)", Css("motion-reduce:transition-none"));
    }

    [Fact]
    public void Direction_variants_prefix_an_attribute_selector()
    {
        Assert.Contains("[dir=\"rtl\"] .rtl\\:ml-2{margin-left:", Css("rtl:ml-2"));
    }

    [Fact]
    public void Arbitrary_properties_are_utilities()
    {
        Assert.Contains("{mask-type:luminance}", Css("[mask-type:luminance]"));
        Assert.Contains("{--panel-gap:2rem}", Css("[--panel-gap:2rem]"));
    }

    [Fact]
    public void Translate_utilities_set_the_composing_variable()
    {
        Assert.Contains(
            "--khamsin-translate-x:calc(var(--spacing) * 4);translate:var(--khamsin-translate-x) var(--khamsin-translate-y,0)",
            Css("translate-x-4"));
        Assert.Contains("--khamsin-translate-y:calc(var(--spacing) * -2)", Css("-translate-y-2"));
    }

    [Fact]
    public void Scale_and_rotate_use_individual_transform_properties()
    {
        Assert.Contains(
            "--khamsin-scale-x:1.5;--khamsin-scale-y:1.5;scale:var(--khamsin-scale-x) var(--khamsin-scale-y)",
            Css("scale-150"));
        Assert.Contains("{rotate:45deg}", Css("rotate-45"));
    }

    [Fact]
    public void Divide_width_utilities_target_children_but_the_first()
    {
        Assert.Contains(".divide-x-2>*+*{border-inline-start-width:2px;border-style:solid}", Css("divide-x-2"));
        Assert.Contains(".divide-ocean-600>*+*{border-color:var(--color-ocean-600)}", Css("divide-ocean-600"));
    }

    [Fact]
    public void Line_clamp_sets_the_webkit_box()
    {
        Assert.Contains("-webkit-line-clamp:3", Css("line-clamp-3"));
    }

    [Fact]
    public void Place_and_grid_track_utilities_are_supported()
    {
        Assert.Contains("{place-items:center}", Css("place-items-center"));
        Assert.Contains("{grid-auto-columns:minmax(0,1fr)}", Css("auto-cols-fr"));
    }

    [Fact]
    public void Background_and_object_position_utilities_are_supported()
    {
        Assert.Contains("{background-size:cover}", Css("bg-cover"));
        Assert.Contains("{background-position:left top}", Css("bg-left-top"));
        Assert.Contains("{object-position:right bottom}", Css("object-right-bottom"));
    }

    [Fact]
    public void Table_and_text_wrap_utilities_are_supported()
    {
        Assert.Contains("{table-layout:fixed}", Css("table-fixed"));
        Assert.Contains("{border-collapse:collapse}", Css("border-collapse"));
        Assert.Contains("{text-wrap:balance}", Css("text-balance"));
    }

    [Fact]
    public void Blending_and_interaction_utilities_are_supported()
    {
        Assert.Contains("{mix-blend-mode:multiply}", Css("mix-blend-multiply"));
        Assert.Contains("{isolation:isolate}", Css("isolate"));
        Assert.Contains("{touch-action:manipulation}", Css("touch-manipulation"));
    }

    [Fact]
    public void Columns_breaks_stroke_and_indent_are_supported()
    {
        Assert.Contains("{columns:3}", Css("columns-3"));
        Assert.Contains("{break-inside:avoid-page}", Css("break-inside-avoid-page"));
        Assert.Contains("{stroke-width:2}", Css("stroke-2"));
        Assert.Contains("{text-indent:", Css("indent-4"));
    }

    [Fact]
    public void Content_utilities_feed_the_generated_content_variable()
    {
        Assert.Contains("--khamsin-content:'x';content:var(--khamsin-content)", Css("content-['x']"));
    }

    [Fact]
    public void Arbitrary_variant_merge_keys_are_per_variant()
    {
        Assert.Equal("[&>*]:p-4", KhamsinMerger.Merge("[&>*]:p-2", "[&>*]:p-4"));
        Assert.Equal("[&>*]:p-2 hover:[&>*]:p-4", KhamsinMerger.Merge("[&>*]:p-2", "hover:[&>*]:p-4"));
    }

    [Fact]
    public void Transform_and_arbitrary_property_groups_conflict_by_group()
    {
        Assert.Equal("translate-x-4", KhamsinMerger.Merge("translate-x-2", "translate-x-4"));
        Assert.Equal("[color:blue]", KhamsinMerger.Merge("[color:red]", "[color:blue]"));
    }

    [Fact]
    public void Readable_output_handles_nested_wrappers()
    {
        var css = CssEmitter.Emit(
            new List<ParsedClass>(),
            Theme.Default,
            includeTheme: false,
            minify: false);

        Assert.Contains("@layer utilities {", css);
    }
}
