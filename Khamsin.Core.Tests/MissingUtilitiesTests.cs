namespace Khamsin.Core.Tests;

/// <summary>Coverage for the small registry additions: per-side widths, inset full, numeric variants, decoration, float/clear, gradients and named groups.</summary>
public class MissingUtilitiesTests
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
    public void Per_side_border_widths_bind_per_side()
    {
        Assert.Contains(".border-b-2{border-bottom-width:2px;border-style:solid}", Css("border-b-2"));
        Assert.Contains(".border-l-8{border-left-width:8px;border-style:solid}", Css("border-l-8"));
        // The bare form still emits the 1px default and resolves against the width steps in the merge.
        Assert.Contains(".border-t{border-top-width:1px;border-style:solid}", Css("border-t"));
        Assert.Equal("border-t-4", KhamsinMerger.Merge("border-t", "border-t-4"));
    }

    [Fact]
    public void Inset_accepts_the_full_keyword()
    {
        Assert.Contains(".top-full{top:100%}", Css("top-full"));
        Assert.Contains(".inset-x-full{inset-inline:100%}", Css("inset-x-full"));
        Assert.Contains(".bottom-auto{bottom:auto}", Css("bottom-auto"));
    }

    [Fact]
    public void Numeric_variants_compose()
    {
        var css = Css("tabular-nums", "ordinal");
        Assert.Contains(".tabular-nums{--khamsin-numeric:tabular-nums;font-variant-numeric:var(--khamsin-numeric,) var(--khamsin-ordinal,) var(--khamsin-slashed-zero,) var(--khamsin-fraction,)}", css);
        Assert.Contains(".ordinal{--khamsin-ordinal:ordinal;font-variant-numeric:", css);
        // Different slots survive the merge; the same slot resolves.
        var merged = KhamsinMerger.Merge("tabular-nums", "ordinal slashed-zero");
        Assert.Contains("tabular-nums", merged);
        Assert.Contains("ordinal", merged);
        Assert.Contains("slashed-zero", merged);
        Assert.Equal("ordinal proportional-nums", KhamsinMerger.Merge("tabular-nums ordinal", "proportional-nums"));
        Assert.Contains("font-variant-numeric:var(--khamsin-numeric,) var(--khamsin-ordinal,) var(--khamsin-slashed-zero,) var(--khamsin-fraction,)", Css("normal-nums"));
    }

    [Fact]
    public void Decoration_family_binds()
    {
        var css = Css("underline", "decoration-ember-500", "decoration-2", "decoration-wavy", "underline-offset-4");
        Assert.Contains(".decoration-ember-500{text-decoration-color:var(--color-ember-500)}", css);
        Assert.Contains(".decoration-2{text-decoration-thickness:2px}", css);
        Assert.Contains(".decoration-wavy{text-decoration-style:wavy}", css);
        Assert.Contains(".underline-offset-4{text-underline-offset:4px}", css);
    }

    [Fact]
    public void Float_and_clear_bind()
    {
        Assert.Contains(".float-left{float:left}", Css("float-left"));
        Assert.Contains(".float-start{float:inline-start}", Css("float-start"));
        Assert.Contains(".clear-both{clear:both}", Css("clear-both"));
    }

    [Fact]
    public void Gradients_compose_stops()
    {
        var css = Css("bg-gradient-to-r", "from-ocean-600", "via-teal-400", "to-forest-500");
        Assert.Contains(".bg-gradient-to-r{background-image:linear-gradient(to right,var(--khamsin-gradient-stops,transparent))}", css);
        Assert.Contains("--khamsin-gradient-from:var(--color-ocean-600);--khamsin-gradient-from-position:0%", css);
        Assert.Contains("--khamsin-gradient-stops:var(--khamsin-gradient-from) var(--khamsin-gradient-from-position),var(--khamsin-gradient-to) var(--khamsin-gradient-to-position)", css);
        Assert.Contains("--khamsin-gradient-via:var(--color-teal-400);--khamsin-gradient-via-position:50%", css);
        Assert.Contains("--khamsin-gradient-stops:var(--khamsin-gradient-from) var(--khamsin-gradient-from-position),var(--khamsin-gradient-via) var(--khamsin-gradient-via-position),var(--khamsin-gradient-to) var(--khamsin-gradient-to-position)", css);
        Assert.Contains("--khamsin-gradient-to:var(--color-forest-500)", css);

        // Direction, stops and opacity modifiers all work together; the merger keeps every group.
        var withModifier = Css("from-ocean-600/50");
        Assert.Contains("color-mix(in oklab,var(--color-ocean-600) 50%,transparent)", withModifier);
        var merged = KhamsinMerger.Merge("bg-gradient-to-r from-ocean-600", "to-forest-500 via-amber-400");
        Assert.Contains("bg-gradient-to-r", merged);
        Assert.Contains("from-ocean-600", merged);
        Assert.Contains("to-forest-500", merged);
        Assert.Contains("via-amber-400", merged);
    }

    [Fact]
    public void Named_group_variants_target_the_labeled_sentinel()
    {
        var css = Css("group-hover/card:text-ocean-700");
        Assert.Contains(".group\\/card:hover .group-hover\\/card\\:text-ocean-700{color:var(--color-ocean-700)}", css);
    }

    [Fact]
    public void Named_peer_variants_target_labeled_siblings()
    {
        var css = Css("peer-checked/dialog:block");
        Assert.Contains(".peer\\/dialog:checked ~ .peer-checked\\/dialog\\:block{display:block}", css);
    }

    [Fact]
    public void Named_variants_compose_with_media_variants_and_states()
    {
        var css = Css("md:group-hover/card:hidden", "group-active/sidebar:first:font-bold");
        Assert.Contains("@media (min-width: 48rem){.group\\/card:hover .md\\:group-hover\\/card\\:hidden{display:none}}", css);
        Assert.Contains(".group\\/sidebar:active .group-active\\/sidebar\\:first\\:font-bold:first-child{font-weight:700}", css);
    }

    [Fact]
    public void Unknown_named_states_stay_rejected()
    {
        Assert.False(KhamsinEngine.Default.TryParse("group-hover/card/bogus:text-ocean-700", out _, out _));
        Assert.False(KhamsinEngine.Default.TryParse("group-explode/card:text-ocean-700", out _, out _));
    }

    [Fact]
    public void Background_url_builds_background_image()
    {
        var css = Css("bg-[url(/img/hero.png)]");
        Assert.Contains(".bg-\\[url\\(\\/img\\/hero\\.png\\)\\]{background-image:url(/img/hero.png)}", css);
        // A url replaces a plain color in the merge like any other bg utility.
        Assert.Equal("bg-[url(/img/hero.png)]", KhamsinMerger.Merge("bg-white", "bg-[url(/img/hero.png)]"));
    }

    [Fact]
    public void Background_underscores_become_spaces_unless_escaped()
    {
        Assert.Contains("background-image:url(/img/my hero.png)", Css("bg-[url(/img/my_hero.png)]"));
        Assert.Contains("background-image:url(/img/hero_2.png)", Css("bg-[url(/img/hero\\_2.png)]"));
    }

    [Fact]
    public void Background_type_hints_select_the_property()
    {
        Assert.Contains("background-image:var(--hero)", Css("bg-[image:var(--hero)]"));
        Assert.Contains("background-size:200px 100%", Css("bg-[length:200px_100%]"));
        Assert.Contains("background-position:left top", Css("bg-[position:left_top]"));
        Assert.Contains("background-color:var(--brand)", Css("bg-[color:var(--brand)]"));
        Assert.Contains("color-mix(in oklab,var(--brand) 50%,transparent)", Css("bg-[color:var(--brand)]/50"));
    }

    [Fact]
    public void Background_gradients_bind_as_images()
    {
        Assert.Contains(
            "background-image:linear-gradient(to right,#ff0000,#0000ff)",
            Css("bg-[linear-gradient(to_right,#ff0000,#0000ff)]"));
    }
}
