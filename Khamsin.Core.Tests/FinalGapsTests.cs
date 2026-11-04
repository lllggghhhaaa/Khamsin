namespace Khamsin.Core.Tests;

/// <summary>Coverage for the final registry batch: logical utilities, scroll/snap, ring offsets, one-liners, gradient shapes and the v3 trailing important form.</summary>
public class FinalGapsTests
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
    public void Logical_margin_and_padding_utilities_bind()
    {
        var css = Css("ps-4", "pe-2", "ms-6", "me-1");
        Assert.Contains(".ps-4{padding-inline-start:calc(var(--spacing) * 4)}", css);
        Assert.Contains(".pe-2{padding-inline-end:calc(var(--spacing) * 2)}", css);
        Assert.Contains(".ms-6{margin-inline-start:calc(var(--spacing) * 6)}", css);
        Assert.Contains(".me-1{margin-inline-end:calc(var(--spacing) * 1)}", css);
        // Logical siblings refine the physical shorthand instead of being removed by it.
        Assert.Contains("ps-4", KhamsinMerger.Merge("px-8", "ps-4"));
        Assert.Equal("px-8", KhamsinMerger.Merge("ps-4", "px-8"));
    }

    [Fact]
    public void Logical_inset_and_border_sides_bind()
    {
        var css = Css("start-4", "end-auto", "-start-2", "border-s", "border-e-2", "border-s-ember-500");
        Assert.Contains(".start-4{inset-inline-start:calc(var(--spacing) * 4)}", css);
        Assert.Contains(".end-auto{inset-inline-end:auto}", css);
        Assert.Contains(".-start-2{inset-inline-start:calc(var(--spacing) * -2)}", css);
        Assert.Contains(".border-s{border-inline-start-width:1px;border-style:solid}", css);
        Assert.Contains(".border-e-2{border-inline-end-width:2px;border-style:solid}", css);
        Assert.Contains(".border-s-ember-500{border-inline-start-color:var(--color-ember-500)}", css);
        Assert.Contains("border-start-start-radius", Css("rounded-s-lg"));
        Assert.Contains("border-end-end-radius", Css("rounded-e-lg"));
    }

    [Fact]
    public void Scroll_and_snap_families_bind()
    {
        var css = Css("scroll-m-4", "-scroll-mt-2", "scroll-p-6", "snap-x", "snap-mandatory", "snap-center", "snap-none");
        Assert.Contains(".scroll-m-4{scroll-margin:calc(var(--spacing) * 4)}", css);
        Assert.Contains(".-scroll-mt-2{scroll-margin-top:calc(var(--spacing) * -2)}", css);
        Assert.Contains(".scroll-p-6{scroll-padding:calc(var(--spacing) * 6)}", css);
        Assert.Contains(".snap-x{scroll-snap-type:x var(--khamsin-snap-strictness,proximity)}", css);
        Assert.Contains(".snap-mandatory{--khamsin-snap-strictness:mandatory}", css);
        Assert.Contains(".snap-center{scroll-snap-align:center}", css);
        Assert.Contains(".snap-none{scroll-snap-type:none}", css);
    }

    [Fact]
    public void Ring_offset_composes_with_the_ring()
    {
        var css = Css("ring-2", "ring-offset-4", "ring-offset-ember-500");
        Assert.Contains("box-shadow:var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-offset-width,0px) var(--khamsin-ring-offset-color,#fff), var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-width) var(--khamsin-ring-color,currentColor)", css);
        Assert.Contains(".ring-offset-4{--khamsin-ring-offset-width:4px;", css);
        Assert.Contains(".ring-offset-ember-500{--khamsin-ring-offset-color:var(--color-ember-500);", css);
    }

    [Fact]
    public void One_liners_bind()
    {
        var css = Css("bg-none", "hyphens-auto", "font-stretch-125", "antialiased", "list-image-none", "forced-color-adjust-none", "scheme-dark");
        Assert.Contains(".bg-none{background-image:none}", css);
        Assert.Contains(".hyphens-auto{hyphens:auto}", css);
        Assert.Contains(".font-stretch-125{font-stretch:125%}", css);
        Assert.Contains(".antialiased{-webkit-font-smoothing:antialiased;-moz-osx-font-smoothing:grayscale}", css);
        Assert.Contains(".list-image-none{list-style-image:none}", css);
        Assert.Contains(".forced-color-adjust-none{forced-color-adjust:none}", css);
        Assert.Contains(".scheme-dark{color-scheme:dark}", css);
    }

    [Fact]
    public void Gradient_shapes_and_angles_bind()
    {
        var css = Css("bg-radial", "bg-conic", "bg-linear-45", "bg-gradient-to-r", "bg-none");
        Assert.Contains("background-image:radial-gradient(circle,var(--khamsin-gradient-stops,transparent))", css);
        Assert.Contains("background-image:conic-gradient(var(--khamsin-gradient-stops,transparent))", css);
        Assert.Contains("background-image:linear-gradient(45deg,var(--khamsin-gradient-stops,transparent))", css);
        Assert.Contains("background-image:linear-gradient(to right,var(--khamsin-gradient-stops,transparent))", css);
        Assert.Contains(".bg-none{background-image:none}", css);
    }

    [Fact]
    public void Trailing_important_matches_the_leading_form()
    {
        Assert.Contains(".p-2\\!{padding:calc(var(--spacing) * 2)!important}", Css("p-2!"));
        Assert.Contains(".md\\:flex\\!{display:flex!important}", Css("md:flex!"));
    }

    [Fact]
    public void Details_content_and_container_shorthand_variants_bind()
    {
        var css = Css("details-content:block", "@min-[475px]:flex", "@max-[500px]:hidden");
        Assert.Contains(".details-content\\:block::details-content{", css);
        Assert.Contains("@container (min-width:475px){.\\@min-\\[475px\\]\\:flex{display:flex}}", css);
        Assert.Contains("@container (max-width:500px){.\\@max-\\[500px\\]\\:hidden{display:none}}", css);
    }

    [Fact]
    public void Named_group_negation_targets_the_labeled_sentinel()
    {
        var css = Css("group-not-hover/card:text-ocean-700");
        Assert.Contains(".group\\/card:not(:hover) .group-not-hover\\/card\\:text-ocean-700{", css);
    }
}
