using System.Globalization;

namespace Khamsin;

public enum VariantKind
{
    /// <summary>Adds a pseudo-class or pseudo-element to the utility selector.</summary>
    Pseudo,

    /// <summary>Wraps the rule in a media query.</summary>
    Media,

    /// <summary>Wraps the rule in a supports query.</summary>
    Supports,

    /// <summary>Wraps the rule in an arbitrary at-rule, e.g. <c>@container</c>.</summary>
    AtRule,

    /// <summary>Prepends a selector fragment, e.g. <c>.dark </c> or <c>.group:hover </c>.</summary>
    Prefix,

    /// <summary>Replaces <c>&amp;</c> in an arbitrary selector template.</summary>
    Selector
}

/// <summary>Declarative description of one variant.</summary>
public sealed record VariantDefinition(
    string Name,
    int Weight,
    VariantKind Kind,
    string? Pseudo = null,
    string? Media = null,
    string? SelectorPrefix = null);

/// <summary>
/// The closed set of variants the engine understands, plus the dynamic forms that are still
/// statically readable: arbitrary selectors, arbitrary at-rules, <c>has-</c>, <c>data-</c> and
/// <c>aria-</c> variants. Unknown variants are diagnostics, never silent output.
/// </summary>
public sealed class VariantRegistry
{
    private readonly Dictionary<string, VariantDefinition> _byName;
    private readonly Theme _theme;

    private VariantRegistry(Dictionary<string, VariantDefinition> byName, Theme theme)
    {
        _byName = byName;
        _theme = theme;
    }

    public static VariantRegistry Default => DefaultVariants.Instance;

    public IEnumerable<VariantDefinition> All => _byName.Values;

    public bool TryGet(string name, out VariantDefinition definition) => 
        _byName.TryGetValue(name, out definition!) || TryCreateDynamic(name, out definition!);

    internal static Dictionary<string, VariantDefinition> Build(Theme theme)
    {
        var variants = new List<VariantDefinition>
        {
            new("first", 60, VariantKind.Pseudo, Pseudo: ":first-child"),
            new("last", 61, VariantKind.Pseudo, Pseudo: ":last-child"),
            new("odd", 62, VariantKind.Pseudo, Pseudo: ":nth-child(odd)"),
            new("even", 63, VariantKind.Pseudo, Pseudo: ":nth-child(even)"),

            // Pseudo-elements
            new("first-line", 60, VariantKind.Pseudo, Pseudo: "::first-line"),
            new("first-letter", 60, VariantKind.Pseudo, Pseudo: "::first-letter"),
            new("placeholder", 78, VariantKind.Pseudo, Pseudo: "::placeholder"),
            new("file", 78, VariantKind.Pseudo, Pseudo: "::file-selector-button"),
            new("marker", 78, VariantKind.Pseudo, Pseudo: "::marker"),
            new("selection", 79, VariantKind.Pseudo, Pseudo: "::selection"),
            new("before", 88, VariantKind.Pseudo, Pseudo: "::before"),
            new("after", 89, VariantKind.Pseudo, Pseudo: "::after"),
            new("backdrop", 89, VariantKind.Pseudo, Pseudo: "::backdrop"),
            new("details-content", 78, VariantKind.Pseudo, Pseudo: "::details-content"),

            new("hover", 80, VariantKind.Pseudo, Pseudo: ":hover"),
            new("focus", 82, VariantKind.Pseudo, Pseudo: ":focus"),
            new("focus-visible", 84, VariantKind.Pseudo, Pseudo: ":focus-visible"),
            new("focus-within", 83, VariantKind.Pseudo, Pseudo: ":focus-within"),
            new("active", 86, VariantKind.Pseudo, Pseudo: ":active"),
            new("disabled", 90, VariantKind.Pseudo, Pseudo: ":disabled"),
            new("checked", 87, VariantKind.Pseudo, Pseudo: ":checked"),
            new("enabled", 88, VariantKind.Pseudo, Pseudo: ":enabled"),
            new("visited", 86, VariantKind.Pseudo, Pseudo: ":visited"),
            new("target", 86, VariantKind.Pseudo, Pseudo: ":target"),
            new("open", 86, VariantKind.Pseudo, Pseudo: "[open]"),
            new("empty", 86, VariantKind.Pseudo, Pseudo: ":empty"),
            new("read-only", 86, VariantKind.Pseudo, Pseudo: ":read-only"),
            new("read-write", 86, VariantKind.Pseudo, Pseudo: ":read-write"),
            new("placeholder-shown", 86, VariantKind.Pseudo, Pseudo: ":placeholder-shown"),
            new("autofill", 86, VariantKind.Pseudo, Pseudo: ":is(:autofill,:-webkit-autofill)"),
            new("invalid", 86, VariantKind.Pseudo, Pseudo: ":invalid"),
            new("valid", 86, VariantKind.Pseudo, Pseudo: ":valid"),
            new("user-invalid", 86, VariantKind.Pseudo, Pseudo: ":user-invalid"),
            new("user-valid", 86, VariantKind.Pseudo, Pseudo: ":user-valid"),
            new("required", 86, VariantKind.Pseudo, Pseudo: ":required"),
            new("optional", 86, VariantKind.Pseudo, Pseudo: ":optional"),
            new("in-range", 86, VariantKind.Pseudo, Pseudo: ":in-range"),
            new("out-of-range", 86, VariantKind.Pseudo, Pseudo: ":out-of-range"),
            new("indeterminate", 86, VariantKind.Pseudo, Pseudo: ":indeterminate"),
            new("default", 86, VariantKind.Pseudo, Pseudo: ":default"),
            new("inert", 86, VariantKind.Pseudo, Pseudo: ":is([inert],[inert] *)"),
            new("rtl", 59, VariantKind.Prefix, SelectorPrefix: "[dir=\"rtl\"] "),
            new("ltr", 59, VariantKind.Prefix, SelectorPrefix: "[dir=\"ltr\"] "),

            new("group-hover", 70, VariantKind.Prefix, SelectorPrefix: ".group:hover "),
            new("group-focus", 71, VariantKind.Prefix, SelectorPrefix: ".group:focus "),
            new("group-focus-visible", 72, VariantKind.Prefix, SelectorPrefix: ".group:focus-visible "),
            new("group-focus-within", 73, VariantKind.Prefix, SelectorPrefix: ".group:focus-within "),
            new("group-active", 73, VariantKind.Prefix, SelectorPrefix: ".group:active "),
            new("peer-hover", 74, VariantKind.Prefix, SelectorPrefix: ".peer:hover ~ "),
            new("peer-focus", 75, VariantKind.Prefix, SelectorPrefix: ".peer:focus ~ "),
            new("peer-focus-visible", 76, VariantKind.Prefix, SelectorPrefix: ".peer:focus-visible ~ "),
            new("peer-focus-within", 76, VariantKind.Prefix, SelectorPrefix: ".peer:focus-within ~ "),
            new("peer-checked", 77, VariantKind.Prefix, SelectorPrefix: ".peer:checked ~ "),
            new("peer-disabled", 78, VariantKind.Prefix, SelectorPrefix: ".peer:disabled ~ "),
            new("peer-first", 64, VariantKind.Prefix, SelectorPrefix: ".peer:first-child ~ "),
            new("peer-last", 65, VariantKind.Prefix, SelectorPrefix: ".peer:last-child ~ "),
            new("peer-odd", 66, VariantKind.Prefix, SelectorPrefix: ".peer:nth-child(odd) ~ "),
            new("peer-even", 67, VariantKind.Prefix, SelectorPrefix: ".peer:nth-child(even) ~ "),

            // Media states that do not depend on the theme
            new("print", 58, VariantKind.Media, Media: "print"),
            new("motion-safe", 57, VariantKind.Media, Media: "(prefers-reduced-motion: no-preference)"),
            new("motion-reduce", 57, VariantKind.Media, Media: "(prefers-reduced-motion: reduce)"),
            new("contrast-more", 57, VariantKind.Media, Media: "(prefers-contrast: more)"),
            new("contrast-less", 57, VariantKind.Media, Media: "(prefers-contrast: less)"),
            new("portrait", 57, VariantKind.Media, Media: "(orientation: portrait)"),
            new("landscape", 57, VariantKind.Media, Media: "(orientation: landscape)"),
            new("forced-colors", 57, VariantKind.Media, Media: "(forced-colors: active)"),
            new("pointer-fine", 57, VariantKind.Media, Media: "(pointer: fine)"),
            new("pointer-coarse", 57, VariantKind.Media, Media: "(pointer: coarse)"),
            new("any-pointer-fine", 57, VariantKind.Media, Media: "(any-pointer: fine)"),
            new("any-pointer-coarse", 57, VariantKind.Media, Media: "(any-pointer: coarse)"),
            new("noscript", 57, VariantKind.Media, Media: "(scripting: none)"),
            new("inverted-colors", 57, VariantKind.Media, Media: "(inverted-colors: inverted)"),
            new("starting", 45, VariantKind.AtRule, Media: "@starting-style"),
        };

        var map = new Dictionary<string, VariantDefinition>(StringComparer.Ordinal);
        foreach (var variant in variants) map.Add(variant.Name, variant);

        // Dark strategy: the configured selector prefix by default, or the v4 media query when the
        // configuration sets dark to the sentinel "media".
        if (string.Equals(theme.DarkSelector, "media", StringComparison.Ordinal))
            map.Add("dark", new VariantDefinition("dark", 55, VariantKind.Media, Media: "(prefers-color-scheme: dark)"));
        else
            map.Add("dark", new VariantDefinition("dark", 55, VariantKind.Prefix, SelectorPrefix: theme.DarkSelector + " "));

        var breakpointWeight = 10;
        foreach (var breakpoint in OrderedBreakpoints(theme))
        {
            map.Add(breakpoint.Key, new VariantDefinition(breakpoint.Key, breakpointWeight, VariantKind.Media, Media: "(min-width: " + breakpoint.Value + ")"));
            map.Add("max-" + breakpoint.Key, new VariantDefinition("max-" + breakpoint.Key, breakpointWeight, VariantKind.Media, Media: "(max-width: " + breakpoint.Value + ")"));
            breakpointWeight += 10;
        }

        return map;
    }

    /// <summary>
    /// Breakpoints in ascending width order so cascade weights match viewport size. Unparsable
    /// units sort after the known ones, alphabetically, but still become usable variants.
    /// </summary>
    private static List<KeyValuePair<string, string>> OrderedBreakpoints(Theme theme)
    {
        var known = new List<(KeyValuePair<string, string> Entry, double Width)>();
        var unknown = new List<KeyValuePair<string, string>>();
        foreach (var entry in theme.Breakpoints)
        {
            if (TryParseWidth(entry.Value, out var width)) known.Add((entry, width));
            else unknown.Add(entry);
        }

        known.Sort(static (left, right) =>
        {
            var byWidth = left.Width.CompareTo(right.Width);
            return byWidth != 0 ? byWidth : string.CompareOrdinal(left.Entry.Key, right.Entry.Key);
        });
        unknown.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

        var ordered = new List<KeyValuePair<string, string>>(known.Count + unknown.Count);
        foreach (var (entry, _) in known) ordered.Add(entry);
        ordered.AddRange(unknown);
        return ordered;
    }

    private static bool TryParseWidth(string value, out double width)
    {
        width = 0;
        if (value.Length < 3) return false;

        var unit = value.Length >= 3 ? value.Substring(value.Length - 3) : string.Empty;
        var numberPart = unit is "rem" or "em" ? value.Substring(0, value.Length - 3)
            : value.EndsWith("px", StringComparison.Ordinal) ? value.Substring(0, value.Length - 2)
            : null;

        if (numberPart is null || !double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return false;

        var pixels = unit switch
        {
            "rem" => number * 16,
            "em" => number * 16,
            _ => number, // px
        };
        width = pixels;
        return true;
    }

    internal static VariantRegistry Create(Theme theme) => new(Build(theme), theme);

    /// <summary>States available to named group and peer variants, e.g. <c>group-hover/card:</c>.</summary>
    private static readonly Dictionary<string, string> NamedStates = new(StringComparer.Ordinal)
    {
        ["hover"] = ":hover",
        ["focus"] = ":focus",
        ["focus-visible"] = ":focus-visible",
        ["focus-within"] = ":focus-within",
        ["active"] = ":active",
        ["disabled"] = ":disabled",
        ["checked"] = ":checked",
        ["first"] = ":first-child",
        ["last"] = ":last-child",
        ["odd"] = ":nth-child(odd)",
        ["even"] = ":nth-child(even)",
    };

    /// <summary>
    /// Recognises variants whose selector cannot be listed up front but is still a literal:
    /// <c>[&amp;&gt;li]</c>, <c>[@supports(display:grid)]</c>, <c>has-[:checked]</c>,
    /// <c>data-[state=open]</c>, <c>aria-[sort=ascending]</c>, their group/peer forms and named
    /// group/peer states such as <c>group-hover/card</c>.
    /// </summary>
    private bool TryCreateDynamic(string name, out VariantDefinition definition)
    {
        definition = null!;

        if (TryCreateNamed(name, out definition))
            return true;

        if (TryCreateContainerQuery(name, out definition))
            return true;

        if (TryCreateNegation(name, out definition))
            return true;

        if (TryCreateNth(name, out definition))
            return true;

        if (name.Length > 2 && name[0] == '[' && name[name.Length - 1] == ']')
        {
            var inner = name.Substring(1, name.Length - 2).Trim();
            if (!IsSafeFragment(inner))
                return false;

            if (inner[0] == '@')
            {
                if (!IsSupportedAtRule(inner))
                    return false;

                definition = new VariantDefinition(name, 45, VariantKind.AtRule, Media: inner);
                return true;
            }

            if (inner.IndexOf('&') < 0)
                return false;

            definition = new VariantDefinition(name, 45, VariantKind.Selector, SelectorPrefix: inner);
            return true;
        }

        if (TryBracket(name, "has-[", out var has))
        {
            definition = new VariantDefinition(name, 85, VariantKind.Pseudo, Pseudo: ":has(" + has + ")");
            return true;
        }

        if (TryBracket(name, "data-[", out var data))
        {
            definition = new VariantDefinition(name, 85, VariantKind.Pseudo, Pseudo: "[data-" + data + "]");
            return true;
        }

        if (TryBracket(name, "aria-[", out var aria))
        {
            definition = new VariantDefinition(name, 85, VariantKind.Pseudo, Pseudo: "[aria-" + aria + "]");
            return true;
        }

        if (TryBracket(name, "group-has-[", out var groupHas))
        {
            definition = new VariantDefinition(name, 74, VariantKind.Prefix, SelectorPrefix: ".group:has(" + groupHas + ") ");
            return true;
        }

        if (TryBracket(name, "group-data-[", out var groupData))
        {
            definition = new VariantDefinition(name, 74, VariantKind.Prefix, SelectorPrefix: ".group[data-" + groupData + "] ");
            return true;
        }

        if (TryBracket(name, "group-aria-[", out var groupAria))
        {
            definition = new VariantDefinition(name, 74, VariantKind.Prefix, SelectorPrefix: ".group[aria-" + groupAria + "] ");
            return true;
        }

        if (TryBracket(name, "peer-has-[", out var peerHas))
        {
            definition = new VariantDefinition(name, 74, VariantKind.Prefix, SelectorPrefix: ".peer:has(" + peerHas + ") ~ ");
            return true;
        }

        if (TryBracket(name, "peer-data-[", out var peerData))
        {
            definition = new VariantDefinition(name, 74, VariantKind.Prefix, SelectorPrefix: ".peer[data-" + peerData + "] ~ ");
            return true;
        }

        if (TryBracket(name, "peer-aria-[", out var peerAria))
        {
            definition = new VariantDefinition(name, 74, VariantKind.Prefix, SelectorPrefix: ".peer[aria-" + peerAria + "] ~ ");
            return true;
        }

        if (TryCreateAttributeShorthand(name, out definition))
            return true;

        if (TryCreateShorthandAtRule(name, out definition))
            return true;

        return false;
    }

    /// <summary>
    /// Container query variants: <c>@sm</c>, <c>@max-md</c> and their named forms
    /// (<c>@sm/card</c>) resolve against the theme's container tokens and wrap in
    /// <c>@container (min-width/max-width: …)</c>.
    /// </summary>
    private bool TryCreateContainerQuery(string name, out VariantDefinition definition)
    {
        definition = null!;
        if (name.Length < 2 || name[0] != '@')
            return false;

        var rest = name.Substring(1);
        string? label = null;
        var slash = rest.IndexOf('/');
        if (slash >= 0)
        {
            label = rest.Substring(slash + 1);
            rest = rest.Substring(0, slash);
        }

        string comparison;
        string width;
        if (rest.StartsWith("min-[", StringComparison.Ordinal) && rest.EndsWith("]", StringComparison.Ordinal))
        {
            width = rest.Substring(5, rest.Length - 6).Trim();
            if (!IsSafeFragment(width))
                return false;

            comparison = "min-width";
        }
        else if (rest.StartsWith("max-[", StringComparison.Ordinal) && rest.EndsWith("]", StringComparison.Ordinal))
        {
            width = rest.Substring(5, rest.Length - 6).Trim();
            if (!IsSafeFragment(width))
                return false;

            comparison = "max-width";
        }
        else
        {
            var isMax = rest.StartsWith("max-", StringComparison.Ordinal);
            var sizeName = isMax ? rest.Substring(4) : rest;
            if (!UtilityEntry.IsValidUtilityLabel(sizeName) || (label != null && !UtilityEntry.IsValidUtilityLabel(label)))
                return false;

            if (!_theme.Containers.TryGetValue(sizeName, out var tokenWidth))
                return false;

            width = tokenWidth;
            comparison = isMax ? "max-width" : "min-width";
        }
        var container = label is null ? string.Empty : " " + label;
        definition = new VariantDefinition(name, 20, VariantKind.AtRule, Media: "@container" + container + " (" + comparison + ":" + width + ")");
        return true;
    }

    /// <summary>
    /// The v4 negation variant: <c>not-hover:</c>, structural states such as <c>not-first:</c>,
    /// and arbitrary payloads (<c>not-[data-open]</c>) compile to <c>:not(…)</c>.
    /// </summary>
    private static bool TryCreateNegation(string name, out VariantDefinition definition)
    {
        definition = null!;
        if (!name.StartsWith("not-", StringComparison.Ordinal))
            return false;

        var rest = name.Substring(4);
        if (rest.Length > 2 && rest[0] == '[' && rest[rest.Length - 1] == ']')
        {
            var inner = rest.Substring(1, rest.Length - 2).Trim();
            if (!IsSafeFragment(inner))
                return false;

            // A bare attribute name implies its presence selector; anything else is inserted
            // verbatim as the :not() argument.
            var argument = IsAttributeName(inner) ? "[" + inner + "]" : inner;
            definition = new VariantDefinition(name, 88, VariantKind.Pseudo, Pseudo: ":not(" + argument + ")");
            return true;
        }

        if (NamedStates.TryGetValue(rest, out var state))
        {
            definition = new VariantDefinition(name, 88, VariantKind.Pseudo, Pseudo: ":not(" + state + ")");
            return true;
        }

        return false;
    }

    /// <summary>v4.1 positional variants: <c>nth-3:</c>, <c>nth-last-2:</c>, <c>nth-odd:</c> and arbitrary <c>nth-[2n+1]:</c>.</summary>
    private static bool TryCreateNth(string name, out VariantDefinition definition)
    {
        definition = null!;
        string kind;
        string rest;
        if (name.StartsWith("nth-last-", StringComparison.Ordinal))
        {
            kind = "nth-last-child";
            rest = name.Substring("nth-last-".Length);
        }
        else if (name.StartsWith("nth-", StringComparison.Ordinal))
        {
            kind = "nth-child";
            rest = name.Substring(4);
        }
        else
        {
            return false;
        }

        if (rest.Length > 2 && rest[0] == '[' && rest[rest.Length - 1] == ']')
        {
            var formula = rest.Substring(1, rest.Length - 2).Trim();
            if (!IsSafeFragment(formula))
                return false;

            definition = new VariantDefinition(name, 64, VariantKind.Pseudo, Pseudo: ":" + kind + "(" + formula + ")");
            return true;
        }

        var valid = rest == "odd" || rest == "even" || (rest.Length > 0 && rest.Length <= 4 && rest.All(char.IsDigit));
        if (!valid)
            return false;

        definition = new VariantDefinition(name, 64, VariantKind.Pseudo, Pseudo: ":" + kind + "(" + rest + ")");
        return true;
    }

    /// <summary>v4 attribute shorthands: <c>data-open:</c> and the named <c>aria-busy:</c>-style set.</summary>
    private static bool TryCreateAttributeShorthand(string name, out VariantDefinition definition)
    {
        definition = null!;

        if (name.StartsWith("data-", StringComparison.Ordinal) && name.IndexOf('[') < 0)
        {
            var label = name.Substring("data-".Length);
            if (UtilityEntry.IsValidUtilityLabel(label))
            {
                definition = new VariantDefinition(name, 85, VariantKind.Pseudo, Pseudo: "[data-" + label + "]");
                return true;
            }

            return false;
        }

        if (name.StartsWith("aria-", StringComparison.Ordinal) && name.IndexOf('[') < 0 && AriaNamed.TryGetValue(name, out var value))
        {
            definition = new VariantDefinition(name, 85, VariantKind.Pseudo, Pseudo: "[aria-" + name.Substring("aria-".Length) + "=\"" + value + "\"]");
            return true;
        }

        return false;
    }

    private static bool IsAttributeName(string candidate)
    {
        if (candidate.Length == 0)
            return false;

        foreach (var c in candidate)
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                return false;

        return true;
    }

    /// <summary>Named v4 aria states with their fixed values.</summary>
    private static readonly Dictionary<string, string> AriaNamed = new(StringComparer.Ordinal)
    {
        ["aria-busy"] = "true",
        ["aria-checked"] = "true",
        ["aria-disabled"] = "true",
        ["aria-expanded"] = "true",
        ["aria-hidden"] = "true",
        ["aria-pressed"] = "true",
        ["aria-readonly"] = "true",
        ["aria-required"] = "true",
        ["aria-selected"] = "true",
        ["aria-sort"] = "ascending",
    };

    /// <summary>Shorthand wrappers: <c>supports-[display:grid]:</c> and arbitrary <c>min-[600px]:</c>/<c>max-[600px]:</c> breakpoints.</summary>
    private static bool TryCreateShorthandAtRule(string name, out VariantDefinition definition)
    {
        definition = null!;

        if (name.StartsWith("supports-[", StringComparison.Ordinal) && name.EndsWith("]", StringComparison.Ordinal))
        {
            var inner = name.Substring("supports-[".Length, name.Length - "supports-[".Length - 1).Trim();
            if (IsSafeFragment(inner))
            {
                definition = new VariantDefinition(name, 45, VariantKind.AtRule, Media: "@supports(" + inner + ")");
                return true;
            }

            return false;
        }

        foreach (var (prefix, comparison) in new[] { ("min-[", "min-width"), ("max-[", "max-width") })
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith("]", StringComparison.Ordinal))
                continue;

            var inner = name.Substring(prefix.Length, name.Length - prefix.Length - 1).Trim();
            if (!IsSafeFragment(inner))
                return false;

            definition = new VariantDefinition(name, 10, VariantKind.AtRule, Media: "@media(" + comparison + ":" + inner + ")");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Named group and peer variants: <c>group-hover/card</c> targets
    /// <c>.group\/card:hover</c> descendants, <c>peer-focus/dialog</c> targets
    /// <c>.peer\/dialog:focus ~</c> siblings. The sentinel class <c>group/name</c> needs no CSS.
    /// </summary>
    private static bool TryCreateNamed(string name, out VariantDefinition definition)
    {
        definition = null!;

        var slash = name.LastIndexOf('/');
        if (slash <= 0 || slash == name.Length - 1)
            return false;

        var kind = name.Substring(0, slash);
        var label = name.Substring(slash + 1);
        if (!IsValidLabel(label))
            return false;

        var isPeer = kind.StartsWith("peer-", StringComparison.Ordinal);
        var isGroup = kind.StartsWith("group-", StringComparison.Ordinal);
        if (!isPeer && !isGroup)
            return false;

        var state = kind.Substring(isPeer ? "peer-".Length : "group-".Length);

        // Negated named states: group-not-hover/card targets .group\/card:not(:hover).
        var negated = false;
        if (state.StartsWith("not-", StringComparison.Ordinal))
        {
            state = state.Substring(4);
            negated = true;
        }
        if (!NamedStates.TryGetValue(state, out var statePseudo))
            return false;

        var pseudo = negated ? ":not(" + statePseudo + ")" : statePseudo;

        var sentinel = (isPeer ? ".peer" : ".group") + "\\/" + label;
        definition = new VariantDefinition(
            name,
            isPeer ? 76 : 74,
            VariantKind.Prefix,
            SelectorPrefix: sentinel + pseudo + (isPeer ? " ~ " : " "));
        return true;
    }

    private static bool IsValidLabel(string label)
    {
        if (label.Length == 0 || label.Length > 64)
            return false;

        foreach (var c in label)
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                return false;

        return true;
    }

    private static bool TryBracket(string name, string prefix, out string inner)
    {
        inner = string.Empty;
        if (name.Length <= prefix.Length + 1 || !name.StartsWith(prefix, StringComparison.Ordinal) || name[name.Length - 1] != ']')
            return false;

        inner = name.Substring(prefix.Length, name.Length - prefix.Length - 1).Trim();
        return IsSafeFragment(inner);
    }

    private static bool IsSupportedAtRule(string prelude)
    {
        if (!(prelude.StartsWith("@media", StringComparison.Ordinal) ||
              prelude.StartsWith("@supports", StringComparison.Ordinal) ||
              prelude.StartsWith("@container", StringComparison.Ordinal)))
            return false;

        return IsSafeFragment(prelude);
    }

    internal static bool IsSafeFragment(string fragment)
    {
        if (fragment.Length == 0)
            return false;

        var depth = 0;
        foreach (var c in fragment)
        {
            if (c is '{' or '}' or ';' or '\r' or '\n')
                return false;

            if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
                if (depth < 0)
                    return false;
            }
        }

        return depth == 0 && fragment.IndexOf("/*", StringComparison.Ordinal) < 0;
    }

    private static class DefaultVariants
    {
        internal static readonly VariantRegistry Instance = Create(Theme.Default);
    }
}
