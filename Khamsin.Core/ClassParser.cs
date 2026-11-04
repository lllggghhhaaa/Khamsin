using System.Text;

namespace Khamsin;

public enum ClassParseErrorKind
{
    Empty,
    Malformed,
    UnknownVariant,
    UnknownUtility,
}

/// <summary>Diagnostic information for a class token the engine cannot understand.</summary>
public sealed class ClassParseError
{
    public ClassParseError(ClassParseErrorKind kind, string token, string message)
    {
        Kind = kind;
        Token = token ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public ClassParseErrorKind Kind { get; }

    public string Token { get; }

    public string Message { get; }
}

/// <summary>
/// A parsed utility class. Variants are resolved against the registry, which also decides how the
/// slash in a value is read: opacity for color utilities, a fraction for sizing utilities. That
/// disambiguation is why a registry instance participates in parsing.
/// </summary>
public sealed class ParsedClass
{
    internal ParsedClass(
        string raw,
        IReadOnlyList<string> variants,
        bool important,
        bool negative,
        UtilityEntry entry,
        string? value,
        string? modifier,
        bool isArbitrary,
        bool isCssVariable,
        int variantWeight)
    {
        Raw = raw;
        Variants = variants;
        Important = important;
        Negative = negative;
        Entry = entry;
        Value = value;
        Modifier = modifier;
        IsArbitrary = isArbitrary;
        IsCssVariable = isCssVariable;
        VariantWeight = variantWeight;
    }

    public string Raw { get; }

    public IReadOnlyList<string> Variants { get; }

    public bool Important { get; }

    public bool Negative { get; }

    public UtilityEntry Entry { get; }

    public string Utility => Entry.Prefix;

    public string? Value { get; }

    public string? Modifier { get; }

    public bool IsArbitrary { get; }

    public bool IsCssVariable { get; }

    /// <summary>Highest weight among the variants; drives emission order.</summary>
    public int VariantWeight { get; }
}

/// <summary>Parses class tokens into variants plus a registry-backed utility reference.</summary>
public static class ClassParser
{
    public static bool TryParse(string raw, UtilityRegistry registry, out ParsedClass parsed, out ClassParseError? error)
    {
        parsed = null!;
        error = null;

        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        if (string.IsNullOrEmpty(raw))
        {
            error = new ClassParseError(ClassParseErrorKind.Empty, raw, "Class token is empty.");
            return false;
        }

        var segments = SplitVariants(raw);
        if (segments is null)
        {
            error = new ClassParseError(ClassParseErrorKind.Malformed, raw, "Class '" + raw + "' has unbalanced brackets or an empty segment.");
            return false;
        }

        var variants = new List<string>(segments.Count - 1);
        var weight = 0;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var variant = segments[i];
            if (!registry.Variants.TryGet(variant, out var definition))
            {
                error = new ClassParseError(ClassParseErrorKind.UnknownVariant, raw, "Unknown variant '" + variant + "' in class '" + raw + "'.");
                return false;
            }

            variants.Add(variant);
            if (definition.Weight > weight) weight = definition.Weight;
        }

        var body = segments[segments.Count - 1];
        var important = false;
        if (body.Length > 0 && body[0] == '!')
        {
            important = true;
            body = body.Substring(1);
        }

        var negative = false;
        if (body.Length > 0 && body[0] == '-')
        {
            negative = true;
            body = body.Substring(1);
        }

        // The v3 important form trails the utility (p-2!); the v4 leading form was handled above.
        if (body.Length > 0 && body[body.Length - 1] == '!')
        {
            important = true;
            body = body.Substring(0, body.Length - 1);
        }

        if (body.Length == 0)
        {
            error = new ClassParseError(ClassParseErrorKind.Malformed, raw, "Class '" + raw + "' has no utility.");
            return false;
        }

        if (!registry.TryResolveBody(body, negative, out var entry, out var bound))
        {
            if (!negative && TryResolveArbitraryProperty(body, out var arbitraryProperty))
            {
                parsed = new ParsedClass(
                    raw,
                    variants,
                    important,
                    false,
                    arbitraryProperty,
                    null,
                    null,
                    false,
                    false,
                    weight);
                return true;
            }

            error = new ClassParseError(ClassParseErrorKind.UnknownUtility, raw, "Unknown utility class '" + raw + "'.");
            return false;
        }

        parsed = new ParsedClass(
            raw,
            variants,
            important,
            negative,
            entry,
            bound.Value.Length == 0 ? null : bound.Value,
            bound.Modifier,
            bound.Arbitrary,
            bound.CssVariable,
            weight);
        return true;
    }

    /// <summary>Splits on top-level colons, honouring <c>[...]</c> and <c>(...)</c> payloads.</summary>
    internal static List<string>? SplitVariants(string raw)
    {
        var segments = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            switch (c)
            {
                case '[':
                case '(':
                    depth++;
                    break;
                case ']':
                case ')':
                    depth--;
                    if (depth < 0)
                    {
                        return null;
                    }

                    break;
                case ':':
                    if (depth == 0)
                    {
                        segments.Add(raw.Substring(start, i - start));
                        start = i + 1;
                    }

                    break;
            }
        }

        if (depth != 0)
            return null;

        segments.Add(raw.Substring(start));
        
        return segments.Any(t => t.Length == 0 || HasWhitespace(t)) ? null : segments;
    }

    /// <summary>
    /// Recognises an arbitrary property such as <c>[mask-type:luminance]</c> or
    /// <c>[--panel-gap:2rem]</c>. It is a complete utility on its own and never carries a value
    /// prefix, so it is only tried after the registry fails to resolve the token.
    /// </summary>
    private static bool TryResolveArbitraryProperty(string body, out UtilityEntry entry)
    {
        entry = null!;
        if (body.Length < 5 || body[0] != '[' || body[body.Length - 1] != ']')
            return false;

        var inner = body.Substring(1, body.Length - 2);
        var colon = inner.IndexOf(':');
        if (colon <= 0 || colon == inner.Length - 1)
            return false;

        var property = inner.Substring(0, colon).Trim();
        var value = inner.Substring(colon + 1).Trim();
        if (!IsValidPropertyName(property) || value.Length == 0)
            return false;

        if (!UtilityCss.TryUnwrapArbitrary("[" + value + "]", out var raw))
            return false;

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [string.Empty] = property + ":" + raw,
        };

        entry = new UtilityEntry(body, UtilityKind.Static, "arbitrary/" + property, 1200, values: values);
        return true;
    }

    private static bool IsValidPropertyName(string property)
    {
        if (property.Length == 0)
            return false;

        var start = property[0] == '-' && property.Length > 1 && property[1] == '-' ? 2 : 0;
        if (start == 2 && property.Length == 2)
            return false;

        if (!char.IsLetter(property[start]) && property[start] != '-')
            return false;

        for (var i = start; i < property.Length; i++)
        {
            var c = property[i];
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                return false;
        }

        return true;
    }

    private static bool HasWhitespace(string text) => text.Any(char.IsWhiteSpace);

    public static string BuildVariantKey(ParsedClass parsed)
    {
        if (parsed.Variants.Count == 0)
            return parsed.Important ? "!" : string.Empty;

        var variants = new string[parsed.Variants.Count];
        for (var i = 0; i < variants.Length; i++) variants[i] = parsed.Variants[i];

        Array.Sort(variants, StringComparer.Ordinal);
        var builder = new StringBuilder();
        for (var i = 0; i < variants.Length; i++)
        {
            if (i > 0) builder.Append(':');

            builder.Append(variants[i]);
        }

        if (parsed.Important) builder.Append('!');

        return builder.ToString();
    }
}
