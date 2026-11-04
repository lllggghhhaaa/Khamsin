namespace Khamsin.Core.Tests;

/// <summary>Coverage for the Tailwind v4 surface: masks, 3D transforms, container queries, negation, attribute shorthands and the v4 utilities.</summary>
public class V4Tests
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

    private static string CssWith(Theme theme, params string[] classes)
    {
        var engine = new KhamsinEngine(theme);
        var parsed = new List<ParsedClass>();
        foreach (var token in classes)
        {
            Assert.True(engine.TryParse(token, out var parsedClass, out var error), error?.Message);
            parsed.Add(parsedClass);
        }

        return CssEmitter.Emit(parsed, theme, includeTheme: false);
    }

    // Masks

    [Fact]
    public void Edge_masks_compose_from_and_to_positions()
    {
        var css = Css("mask-t-from-50%", "mask-t-to-80%");
        Assert.Contains(
            ".mask-t-from-50\\%{--khamsin-mask-top-from:50%;--khamsin-mask-top:linear-gradient(to bottom,transparent var(--khamsin-mask-top-from,0%),black var(--khamsin-mask-top-to,100%));mask-image:var(--khamsin-mask-top,)",
            css);
        Assert.Contains("--khamsin-mask-top-to:80%;", css);
    }

    [Fact]
    public void Axis_masks_drive_both_edges()
    {
        var css = Css("mask-x-from-25%");
        Assert.Contains("--khamsin-mask-left-from:25%;", css);
        Assert.Contains("--khamsin-mask-right-from:25%;", css);
        Assert.Contains("--khamsin-mask-left:linear-gradient(to right,", css);
        Assert.Contains("--khamsin-mask-right:linear-gradient(to left,", css);
    }

    [Fact]
    public void Radial_linear_and_conic_masks_bind()
    {
        var css = Css("mask-radial-from-30%", "mask-circle", "mask-linear-45", "mask-conic-from-10%");
        Assert.Contains("--khamsin-mask-radial-from:30%;", css);
        Assert.Contains("--khamsin-mask-radial-shape:circle;", css);
        Assert.Contains("radial-gradient(var(--khamsin-mask-radial-shape,circle)", css);
        Assert.Contains("--khamsin-mask-linear-angle:45deg;", css);
        Assert.Contains("--khamsin-mask-conic-from:10%;", css);
        Assert.Contains("conic-gradient(var(--khamsin-mask-conic-angle,0deg)", css);
    }

    [Fact]
    public void Mask_meta_and_arbitrary_bind()
    {
        var css = Css("mask-none", "mask-composite-subtract", "mask-mode-luminance", "mask-clip-padding", "mask-[url(/img/mask.svg)]");
        Assert.Contains(".mask-none{mask-image:none}", css);
        Assert.Contains(".mask-composite-subtract{mask-composite:subtract}", css);
        Assert.Contains(".mask-mode-luminance{mask-mode:luminance}", css);
        Assert.Contains(".mask-clip-padding{mask-clip:padding-box}", css);
        Assert.Contains("{mask-image:url(/img/mask.svg)}", css);
    }

    // 3D transforms and negatives

    [Fact]
    public void Three_dee_transforms_bind()
    {
        var css = Css("rotate-x-45", "perspective-dramatic", "perspective-origin-top", "backface-hidden", "transform-3d", "translate-z-8");
        Assert.Contains(".rotate-x-45{rotate:x 45deg}", css);
        Assert.Contains(".perspective-dramatic{perspective:100px}", css);
        Assert.Contains(".perspective-origin-top{perspective-origin:top}", css);
        Assert.Contains(".backface-hidden{backface-visibility:hidden}", css);
        Assert.Contains(".transform-3d{transform-style:preserve-3d}", css);
        Assert.Contains("translate:var(--khamsin-translate-x,0) var(--khamsin-translate-y,0) var(--khamsin-translate-z)", css);
    }

    [Fact]
    public void Negative_rotations_negate_through_calc()
    {
        Assert.Contains(".-rotate-45{rotate:calc(45deg * -1)}", Css("-rotate-45"));
        Assert.Contains(".-rotate-x-45{rotate:x calc(45deg * -1)}", Css("-rotate-x-45"));
        Assert.Contains(".-skew-x-6{transform:skewX(calc(6deg * -1))}", Css("-skew-x-6"));
    }

    // v4 shadows and small utilities

    [Fact]
    public void Inset_ring_and_inset_shadow_bind()
    {
        var css = Css("inset-ring-2", "inset-ring-ember-500", "inset-shadow-sm", "inset-shadow-ink-900/50");
        Assert.Contains("--khamsin-ring-inset:inset;--khamsin-ring-width:2px;box-shadow:var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-width) var(--khamsin-ring-color,currentColor)", css);
        Assert.Contains("--khamsin-ring-color:var(--color-ember-500)", css);
        Assert.Contains("--khamsin-inset-shadow:inset 0 2px 4px var(--khamsin-inset-shadow-color,rgb(0 0 0 / 0.05));box-shadow:var(--khamsin-inset-shadow)", css);
        Assert.Contains("--khamsin-inset-shadow-color:color-mix(in oklab,var(--color-ink-900) 50%,transparent)", css);
    }

    [Fact]
    public void Text_shadow_and_drop_shadow_colors_bind()
    {
        var css = Css("text-shadow-lg", "text-shadow-ocean-600", "drop-shadow-md", "drop-shadow-ember-500");
        Assert.Contains("text-shadow:0 4px 8px var(--khamsin-text-shadow-color,rgb(0 0 0 / 0.3))", css);
        Assert.Contains("--khamsin-text-shadow-color:var(--color-ocean-600)", css);
        Assert.Contains("drop-shadow(0 3px 3px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.12)))", css);
        Assert.Contains("--khamsin-drop-shadow-color:var(--color-ember-500)", css);
    }

    [Fact]
    public void Small_v4_utilities_bind()
    {
        Assert.Contains(".outline-hidden{outline:2px solid transparent;outline-offset:2px}", Css("outline-hidden"));
        Assert.Contains(".field-sizing-content{field-sizing:content}", Css("field-sizing-content"));
        Assert.Contains(".h-dvh{height:100dvh}", Css("h-dvh"));
        Assert.Contains(".col-end-3{grid-column-end:3}", Css("col-end-3"));
        Assert.Contains(".grid-cols-subgrid{grid-template-columns:subgrid}", Css("grid-cols-subgrid"));
        Assert.Contains("background-image:linear-gradient(to right,var(--khamsin-gradient-stops,transparent))", Css("bg-linear-to-r"));
        Assert.Contains("--khamsin-gradient-from-position:20%", Css("from-20%"));
    }

    // Variants

    [Fact]
    public void Form_and_state_pseudo_variants_bind()
    {
        var css = Css("visited:text-ink-500", "open:block", "placeholder-shown:border-ink-300", "autofill:bg-amber-100", "inert:opacity-50");
        Assert.Contains(".visited\\:text-ink-500:visited{", css);
        Assert.Contains(".open\\:block[open]{", css);
        Assert.Contains(".placeholder-shown\\:border-ink-300:placeholder-shown{", css);
        Assert.Contains(".autofill\\:bg-amber-100:is(:autofill,:-webkit-autofill){", css);
        Assert.Contains(".inert\\:opacity-50:is([inert],[inert] *){", css);
    }

    [Fact]
    public void Negation_variants_compile_to_not()
    {
        var css = Css("not-hover:underline", "not-first:text-ink-500", "not-[data-open]:hidden");
        Assert.Contains(".not-hover\\:underline:not(:hover){", css);
        Assert.Contains(".not-first\\:text-ink-500:not(:first-child){", css);
        Assert.Contains(".not-\\[data-open\\]\\:hidden:not([data-open]){", css);
        // Negation composes with media variants.
        var stacked = Css("md:not-active:hidden");
        Assert.Contains("@media (min-width: 48rem){.md\\:not-active\\:hidden:not(:active)", stacked);
    }

    [Fact]
    public void Nth_variants_bind()
    {
        var css = Css("nth-3:text-ocean-700", "nth-last-2:text-ember-700", "nth-odd:bg-ink-50", "nth-[2n+1]:block");
        Assert.Contains(".nth-3\\:text-ocean-700:nth-child(3){", css);
        Assert.Contains(".nth-last-2\\:text-ember-700:nth-last-child(2){", css);
        Assert.Contains(".nth-odd\\:bg-ink-50:nth-child(odd){", css);
        Assert.Contains(".nth-\\[2n\\+1\\]\\:block:nth-child(2n+1){", css);
    }

    [Fact]
    public void Attribute_shorthands_bind()
    {
        var css = Css("data-open:block", "aria-busy:opacity-60");
        Assert.Contains(".data-open\\:block[data-open]{", css);
        Assert.Contains(".aria-busy\\:opacity-60[aria-busy=\"true\"]{", css);
    }

    [Fact]
    public void Supports_and_arbitrary_breakpoint_shorthands_bind()
    {
        var css = Css("supports-[display:grid]:grid", "min-[600px]:flex", "max-[800px]:hidden");
        Assert.Contains("@supports(display:grid){.supports-\\[display\\:grid\\]\\:grid{display:grid}}", css);
        Assert.Contains("@media(min-width:600px){.min-\\[600px\\]\\:flex{display:flex}}", css);
        Assert.Contains("@media(max-width:800px){.max-\\[800px\\]\\:hidden{display:none}}", css);
    }

    // Container queries

    [Fact]
    public void Container_size_variants_wrap_in_at_container()
    {
        var css = Css("@sm:flex", "@max-lg:hidden", "@3xl:grid");
        Assert.Contains("@container (min-width:24rem){.\\@sm\\:flex{display:flex}}", css);
        Assert.Contains("@container (max-width:32rem){.\\@max-lg\\:hidden{display:none}}", css);
        Assert.Contains("@container (min-width:48rem){.\\@3xl\\:grid{display:grid}}", css);
    }

    [Fact]
    public void Named_container_variants_and_utility_target_the_label()
    {
        var css = Css("@sm/sidebar:flex", "@container", "@container/sidebar");
        Assert.Contains("@container sidebar (min-width:24rem){", css);
        Assert.Contains(".\\@container{container-type:inline-size}", css);
        Assert.Contains("container-type:inline-size;container-name:sidebar", css);
    }

    // Config

    [Fact]
    public void Dark_media_strategy_switches_to_prefers_color_scheme()
    {
        var theme = ParseConfig("""{ "dark": "media" }""").ToTheme();
        var css = CssWith(theme, "dark:bg-forest-600");
        Assert.Contains("@media (prefers-color-scheme: dark){.dark\\:bg-forest-600{background-color:var(--color-forest-600)}}", css);
        Assert.DoesNotContain(".dark .dark\\:bg-forest-600", css);
    }

    [Fact]
    public void Dark_media_rejects_invalid_values_but_accepts_media()
    {
        Assert.True(KhamsinConfig.TryParse("""{ "dark": "media" }""", out _, out var errors));
        Assert.Empty(errors);
        Assert.False(KhamsinConfig.TryParse("""{ "dark": "media; hack" }""", out _, out _));
    }

    private static KhamsinConfig ParseConfig(string json)
    {
        Assert.True(KhamsinConfig.TryParse(json, out var config, out var errors), string.Join("; ", errors));
        return config!;
    }
}
