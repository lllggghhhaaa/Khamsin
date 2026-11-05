namespace Khamsin;

public enum UtilityKind
{
    Static,
    Spacing,
    Color,
    Enum,
    Arbitrary
}

/// <summary>
/// One declarative registry entry. An entry describes how to bind a value and which conflict group
/// it belongs to; the CSS shape is a template with a single <c>{v}</c> placeholder.
/// </summary>
public sealed class UtilityEntry
{
    internal UtilityEntry(
        string prefix,
        UtilityKind kind,
        string group,
        int order,
        string? template = null,
        Dictionary<string, string>? values = null,
        Dictionary<string, string>? keywords = null,
        bool allowNegative = false,
        bool acceptsArbitrary = true,
        bool acceptsFraction = false,
        string? childSelector = null,
        string? arbitraryTemplate = null,
        bool acceptsLabel = false)
    {
        Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        Kind = kind;
        Group = group ?? throw new ArgumentNullException(nameof(group));
        Order = order;
        Template = template;
        Values = values;
        Keywords = keywords;
        AllowNegative = allowNegative;
        AcceptsArbitrary = acceptsArbitrary;
        AcceptsFraction = acceptsFraction;
        ChildSelector = childSelector;
        ArbitraryTemplate = arbitraryTemplate;
        AcceptsLabel = acceptsLabel;
    }

    public string Prefix { get; }

    public UtilityKind Kind { get; }

    /// <summary>Conflict path such as <c>padding/x</c>; a parent group shadows its descendants.</summary>
    public string Group { get; }

    /// <summary>Emission order inside a layer/variant bucket.</summary>
    public int Order { get; }

    public string? Template { get; }

    /// <summary>Declaration template used for arbitrary and CSS-variable values.</summary>
    public string? ArbitraryTemplate { get; }

    public IReadOnlyDictionary<string, string>? Values { get; }

    /// <summary>Extra literal values (for example <c>auto</c>, <c>full</c> or <c>max-w-md</c> tokens).</summary>
    public IReadOnlyDictionary<string, string>? Keywords { get; }

    public bool AllowNegative { get; }

    public bool AcceptsArbitrary { get; }

    public bool AcceptsFraction { get; }

    /// <summary>Accepts an open-ended label remainder (used by <c>@container/{name}</c>).</summary>
    public bool AcceptsLabel { get; }

    /// <summary>Optional child selector used by space utilities, e.g. <c>&gt;*+*</c>.</summary>
    public string? ChildSelector { get; }

    internal bool TryBind(string remainder, bool negative, Theme theme, out BoundValue bound)
    {
        bound = default;
        if (negative && !AllowNegative)
            return false;

        if (remainder.Length == 0)
        {
            if (negative)
                return false;

            if (Values is null || !Values.TryGetValue(string.Empty, out _)) return false;
            
            bound = new BoundValue(string.Empty, null, false, false);
            return true;
        }

        if (TryReadArbitrary(remainder, out var arbitrary, out var isCssVariable))
        {
            if (!AcceptsArbitrary)
                return false;

            bound = new BoundValue(remainder, null, arbitrary, isCssVariable);
            return true;
        }

        switch (Kind)
        {
            case UtilityKind.Static:
                if (AcceptsLabel && IsValidUtilityLabel(remainder))
                {
                    bound = new BoundValue(remainder, null, false, false);
                    return true;
                }

                return false;

            case UtilityKind.Spacing:
                if (negative && Keywords != null && Keywords.ContainsKey(remainder))
                    return false;

                if (Keywords is not null && Keywords.TryGetValue(remainder, out _) ||
                    AcceptsFraction && SpacingScale.TryFormatFraction(remainder, negative, out _))
                {
                    bound = new BoundValue(remainder, null, false, false);
                    return true;
                }

                if (!SpacingScale.IsKnown(remainder))
                    return false;

                bound = new BoundValue(remainder, null, false, false);
                return true;

            case UtilityKind.Enum:
                if (Values is null || !Values.ContainsKey(remainder))
                    return false;

                if (negative && !AllowNegative)
                    return false;

                bound = new BoundValue(remainder, null, false, false);
                return true;

            case UtilityKind.Color:
                return TryBindColor(remainder, theme, out bound);

            case UtilityKind.Arbitrary:
            default:
                return false;
        }
    }

    private bool TryBindColor(string remainder, Theme theme, out BoundValue bound)
    {
        bound = default;
        var slash = IndexOfTopLevelSlash(remainder);
        var token = slash < 0 ? remainder : remainder.Substring(0, slash);
        var modifier = slash < 0 ? null : remainder.Substring(slash + 1);

        if (modifier is not null && !IsValidOpacity(modifier))
            return false;

        if (token.Length == 0)
            return false;

        var isArbitrary = token[0] == '[' && token[token.Length - 1] == ']';
        var isCssVariable = token[0] == '(' && token[token.Length - 1] == ')';
        if (!isArbitrary && !isCssVariable && !theme.IsKnownColor(token))
            return false;

        bound = new BoundValue(token, modifier, isArbitrary, isCssVariable);
        return true;
    }

    private static bool IsValidOpacity(string modifier)
    {
        if (modifier.Length > 2 && modifier[0] == '[' && modifier[modifier.Length - 1] == ']')
            return true;

        if (modifier.Length == 0)
            return false;

        if (modifier.Any(t => !char.IsDigit(t)))
            return false;

        return int.TryParse(modifier, out var value) && value is >= 0 and <= 100;
    }

    internal static bool IsValidUtilityLabel(string label)
    {
        if (label.Length == 0 || label.Length > 64)
            return false;

        foreach (var c in label)
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                return false;

        return true;
    }

    internal static bool TryReadArbitrary(string text, out bool isArbitrary, out bool isCssVariable)
    {
        if (text.Length > 2 && text[0] == '[' && text[text.Length - 1] == ']')
        {
            isArbitrary = true;
            isCssVariable = false;
            return true;
        }

        if (text.Length > 2 && text[0] == '(' && text[text.Length - 1] == ')')
        {
            isArbitrary = false;
            isCssVariable = true;
            return true;
        }

        isArbitrary = false;
        isCssVariable = false;
        return false;
    }

    /// <summary>Finds the last slash that is not inside brackets or parentheses.</summary>
    private static int IndexOfTopLevelSlash(string text)
    {
        var depth = 0;
        var result = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '[' or '(')
                depth++;
            else if (c is ']' or ')')
                depth--;
            else if (c == '/' && depth == 0) 
                result = i;
        }

        return result;
    }
}

internal readonly record struct BoundValue(string Value, string? Modifier, bool Arbitrary, bool CssVariable);