namespace Khamsin.Core.Tests;

/// <summary>Coverage for colored shadows, animations and filters.</summary>
public class EffectsTests
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

    private static KhamsinConfig ParseConfig(string json)
    {
        Assert.True(KhamsinConfig.TryParse(json, out var config, out var errors), string.Join("; ", errors));
        return config!;
    }

    // Colored shadows

    [Fact]
    public void Shadow_token_declares_its_colored_twin()
    {
        var css = Css("shadow-md");
        Assert.Contains(
            ".shadow-md{--khamsin-shadow-colored:0 4px 8px -2px var(--khamsin-shadow-color),0 2px 4px -2px var(--khamsin-shadow-color);box-shadow:var(--shadow-md)}",
            css);
    }

    [Fact]
    public void Shadow_color_utility_composes_with_the_token()
    {
        var css = Css("shadow-md", "shadow-ember-600/50");
        Assert.Contains(
            ".shadow-ember-600\\/50{--khamsin-shadow-color:color-mix(in oklab,var(--color-ember-600) 50%,transparent);box-shadow:var(--khamsin-shadow-colored,0 0 #0000)}",
            css);
        // The color rule sorts after the token rule so the colored shadow wins the cascade.
        Assert.True(css.IndexOf(".shadow-md{", StringComparison.Ordinal) < css.IndexOf(".shadow-ember-600", StringComparison.Ordinal));
    }

    [Fact]
    public void Shadow_none_and_tokenless_color_are_safe()
    {
        Assert.Contains("--khamsin-shadow-colored:none", Css("shadow-none"));
        var colorOnly = Css("shadow-ink-900");
        Assert.Contains("box-shadow:var(--khamsin-shadow-colored,0 0 #0000)", colorOnly);
        Assert.DoesNotContain("--khamsin-shadow-colored:", colorOnly.Replace("box-shadow:var(--khamsin-shadow-colored,0 0 #0000)", string.Empty));
    }

    // Animations

    [Fact]
    public void Animate_spin_emits_its_keyframes_once()
    {
        var css = Css("animate-spin", "animate-pulse");
        Assert.Contains(".animate-spin{animation:spin 1s linear infinite}", css);
        Assert.Contains("@keyframes spin{to{transform:rotate(360deg)}}", css);
        Assert.Contains("@keyframes pulse{50%{opacity:0.5}}", css);
        // Keyframes are appended after the utilities layer.
        Assert.True(css.IndexOf("@layer utilities{", StringComparison.Ordinal) < css.IndexOf("@keyframes spin", StringComparison.Ordinal));
    }

    [Fact]
    public void Unused_animations_are_tree_shaken()
    {
        var css = Css("animate-pulse");
        Assert.DoesNotContain("@keyframes spin", css);
        Assert.DoesNotContain("@keyframes bounce", css);
    }

    [Fact]
    public void Enter_composition_sets_the_variables_and_keyframes()
    {
        var css = Css("animate-in", "fade-in-50", "zoom-in-95", "slide-in-from-top-2");
        Assert.Contains(".animate-in{animation:khamsin-enter 0.15s var(--ease-out) both}", css);
        Assert.Contains(".fade-in-50{--khamsin-enter-opacity:0.5}", css);
        Assert.Contains(".zoom-in-95{--khamsin-enter-scale:.95}", css);
        Assert.Contains(".slide-in-from-top-2{--khamsin-enter-translate-y:calc(var(--spacing) * -2)}", css);
        Assert.Contains(
            "@keyframes khamsin-enter{from{opacity:var(--khamsin-enter-opacity,1);transform:translate3d(var(--khamsin-enter-translate-x,0),var(--khamsin-enter-translate-y,0),0) scale3d(var(--khamsin-enter-scale,1),var(--khamsin-enter-scale,1),1)}}",
            css);
        Assert.DoesNotContain("khamsin-exit", css);
    }

    [Fact]
    public void Exit_composition_and_merger_keep_the_pieces_together()
    {
        var css = Css("animate-out", "fade-out", "slide-out-to-right-4");
        Assert.Contains(".animate-out{animation:khamsin-exit 0.15s var(--ease-out) both}", css);
        Assert.Contains(".fade-out{--khamsin-exit-opacity:0}", css);
        Assert.Contains(".slide-out-to-right-4{--khamsin-exit-translate-x:calc(var(--spacing) * 4)}", css);
        Assert.Contains("@keyframes khamsin-exit{", css);

        // fade/zoom/slide compose with animate-in instead of conflicting with it.
        var merged = KhamsinMerger.Merge("animate-in", "fade-in-50 zoom-in-95");
        Assert.Contains("animate-in", merged);
        Assert.Contains("fade-in-50", merged);
        Assert.Contains("zoom-in-95", merged);
    }

    [Fact]
    public void Config_animations_extend_the_registry()
    {
        var theme = ParseConfig("""
            {
              "animations": {
                "pop": { "value": "pop 0.2s var(--ease-out)", "keyframes": { "from": "opacity:0;transform:scale(0.95)", "to": "opacity:1" } }
              }
            }
            """).ToTheme();
        var engine = new KhamsinEngine(theme);

        Assert.True(engine.TryParse("animate-pop", out var parsed, out var error), error?.Message);
        var css = CssEmitter.Emit([parsed], theme, includeTheme: false);
        Assert.Contains(".animate-pop{animation:pop 0.2s var(--ease-out)}", css);
        Assert.Contains("@keyframes pop{from{opacity:0;transform:scale(0.95)}to{opacity:1}}", css);
        // Built-in animations survive the merge and spin is still available.
        Assert.True(engine.TryParse("animate-spin", out _, out _));
    }

    // Filters

    [Fact]
    public void Blur_emits_the_composed_filter()
    {
        var css = Css("blur");
        Assert.Contains(".blur{--khamsin-blur:blur(8px);filter:var(--khamsin-blur,) var(--khamsin-brightness,) var(--khamsin-contrast,) var(--khamsin-grayscale,) var(--khamsin-hue-rotate,) var(--khamsin-invert,) var(--khamsin-saturate,) var(--khamsin-sepia,) var(--khamsin-drop-shadow,)}", css);
    }

    [Fact]
    public void Filter_utilities_stack()
    {
        var css = Css("blur-sm", "brightness-125", "drop-shadow-md");
        Assert.Contains("--khamsin-blur:blur(4px)", css);
        Assert.Contains("--khamsin-brightness:brightness(1.25)", css);
        Assert.Contains("--khamsin-drop-shadow:drop-shadow(0 3px 3px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.12)))", css);
        Assert.Equal(3, css.Split(new[] { "filter:var(--khamsin-blur,)" }, StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Backdrop_filters_use_their_own_variable_set()
    {
        var css = Css("backdrop-blur-md", "backdrop-brightness-75");
        Assert.Contains("--khamsin-backdrop-blur:blur(12px)", css);
        Assert.Contains("--khamsin-backdrop-brightness:brightness(.75)", css);
        Assert.Contains("backdrop-filter:var(--khamsin-backdrop-blur,)", css);
        Assert.DoesNotContain("filter:var(--khamsin-blur,)", css);
    }

    [Fact]
    public void Backdrop_variant_targets_the_pseudo_element()
    {
        var css = Css("backdrop:bg-ink-950/60");
        Assert.Contains(".backdrop\\:bg-ink-950\\/60::backdrop{", css);
    }

    [Fact]
    public void Arbitrary_animation_and_blur_values_work()
    {
        var css = Css("animate-[wiggle_1s_ease-in-out_infinite]", "blur-[2px]");
        Assert.Contains(".animate-\\[wiggle_1s_ease-in-out_infinite\\]{animation:wiggle 1s ease-in-out infinite}", css);
        Assert.Contains("--khamsin-blur:blur(2px)", css);
    }
}
