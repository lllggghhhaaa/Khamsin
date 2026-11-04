namespace Khamsin;

/// <summary>
/// A font-size token. Sizes carry their paired line height so both declarations stay in sync.
/// </summary>
public sealed class TextSize
{
    public TextSize(string size, string lineHeight)
    {
        Size = size ?? throw new ArgumentNullException(nameof(size));
        LineHeight = lineHeight ?? throw new ArgumentNullException(nameof(lineHeight));
    }

    public string Size { get; }

    public string LineHeight { get; }
}

/// <summary>
/// An animation token: the <c>animation</c> shorthand emitted by <c>animate-&lt;name&gt;</c> plus
/// the keyframes it needs. Keyframes are emitted next to the utilities that reference them, so
/// unused animations never reach the stylesheet.
/// </summary>
public sealed class AnimationDefinition
{
    public AnimationDefinition(string value, params KeyValuePair<string, string>[] keyframes)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Keyframes = keyframes ?? throw new ArgumentNullException(nameof(keyframes));
    }

    /// <summary>The <c>animation</c> shorthand, including the keyframes name.</summary>
    public string Value { get; }

    /// <summary>Keyframe selectors (<c>from</c>, <c>to</c>, percentages) with their declarations.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Keyframes { get; }
}

/// <summary>
/// Design tokens emitted as CSS custom properties. The theme is immutable; the registry and the
/// emitter both read from the same instance so build-time CSS and runtime merging cannot drift.
/// </summary>
public sealed class Theme
{
    private readonly Dictionary<string, string> _colors;
    private readonly Dictionary<string, string> _radius;
    private readonly Dictionary<string, string> _shadows;
    private readonly Dictionary<string, TextSize> _textSizes;
    private readonly Dictionary<string, string> _eases;
    private readonly Dictionary<string, string> _breakpoints;
    private readonly Dictionary<string, string> _containers;
    private readonly Dictionary<string, string> _fonts;
    private readonly Dictionary<string, AnimationDefinition> _animations;
    private readonly List<KeyValuePair<string, string>> _variables;

    public Theme(
        string spacing,
        Dictionary<string, string> colors,
        Dictionary<string, string> radius,
        Dictionary<string, string> shadows,
        Dictionary<string, TextSize> textSizes,
        Dictionary<string, string> eases,
        Dictionary<string, string> breakpoints,
        Dictionary<string, string> containers,
        Dictionary<string, string>? fonts = null,
        string? darkSelector = null,
        Dictionary<string, AnimationDefinition>? animations = null)
    {
        Spacing = spacing ?? throw new ArgumentNullException(nameof(spacing));
        _colors = colors ?? throw new ArgumentNullException(nameof(colors));
        _radius = radius ?? throw new ArgumentNullException(nameof(radius));
        _shadows = shadows ?? throw new ArgumentNullException(nameof(shadows));
        _textSizes = textSizes ?? throw new ArgumentNullException(nameof(textSizes));
        _eases = eases ?? throw new ArgumentNullException(nameof(eases));
        _breakpoints = breakpoints ?? throw new ArgumentNullException(nameof(breakpoints));
        _containers = containers ?? throw new ArgumentNullException(nameof(containers));
        _fonts = fonts ?? DefaultTheme.DefaultFonts;
        _animations = animations ?? DefaultTheme.DefaultAnimations;
        DarkSelector = darkSelector ?? ".dark";
        _variables = BuildVariables();
    }

    public static Theme Default => DefaultTheme.Instance;

    /// <summary>The value of <c>--spacing</c>; spacing utilities are multipliers of it.</summary>
    public string Spacing { get; }

    public IReadOnlyDictionary<string, string> Colors => _colors;

    public IReadOnlyDictionary<string, string> Radius => _radius;

    public IReadOnlyDictionary<string, string> Shadows => _shadows;

    public IReadOnlyDictionary<string, TextSize> TextSizes => _textSizes;

    public IReadOnlyDictionary<string, string> Eases => _eases;

    public IReadOnlyDictionary<string, string> Breakpoints => _breakpoints;

    public IReadOnlyDictionary<string, string> Containers => _containers;

    /// <summary>Font family tokens; <c>font-&lt;name&gt;</c> resolves against this set.</summary>
    public IReadOnlyDictionary<string, string> Fonts => _fonts;

    /// <summary>Animation tokens; <c>animate-&lt;name&gt;</c> resolves against this set.</summary>
    public IReadOnlyDictionary<string, AnimationDefinition> Animations => _animations;

    /// <summary>Selector prefix for the <c>dark:</c> variant, without the trailing combinator space.</summary>
    public string DarkSelector { get; }

    /// <summary>All tokens as <c>name=value</c> pairs, sorted by name for deterministic output.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> CssVariables => _variables;

    public bool IsKnownColor(string token) => _colors.ContainsKey(token);

    private List<KeyValuePair<string, string>> BuildVariables()
    {
        var variables = new List<KeyValuePair<string, string>>
        {
            new("--spacing", Spacing),
        };
        variables.AddRange(_colors.Select(pair => new KeyValuePair<string, string>("--color-" + pair.Key, pair.Value)));
        variables.AddRange(_radius.Select(pair => new KeyValuePair<string, string>("--radius-" + pair.Key, pair.Value)));
        variables.AddRange(_shadows.Select(pair => new KeyValuePair<string, string>("--shadow-" + pair.Key, pair.Value)));

        foreach (var pair in _textSizes)
        {
            variables.Add(new KeyValuePair<string, string>("--text-" + pair.Key, pair.Value.Size));
            variables.Add(new KeyValuePair<string, string>("--text-" + pair.Key + "--line-height", pair.Value.LineHeight));
        }

        variables.AddRange(_eases.Select(pair => new KeyValuePair<string, string>("--ease-" + pair.Key, pair.Value)));
        variables.AddRange(_breakpoints.Select(pair => new KeyValuePair<string, string>("--breakpoint-" + pair.Key, pair.Value)));
        variables.AddRange(_containers.Select(pair => new KeyValuePair<string, string>("--container-" + pair.Key, pair.Value)));
        variables.AddRange(_fonts.Select(pair => new KeyValuePair<string, string>("--font-" + pair.Key, pair.Value)));

        variables.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));
        return variables;
    }
}
