namespace Khamsin;

/// <summary>
/// Derives the color-swapped variant of a box-shadow value for the shadow-color utilities.
/// A shadow token rule declares both the plain shadow and its colored twin; the color utility
/// then re-issues <c>box-shadow</c> from the twin, which substitutes the element's
/// <c>--khamsin-shadow-color</c>. Color functions become that variable; shadows without a color
/// function (for example <c>none</c>) keep their plain text.
/// </summary>
internal static class ShadowColors
{
    private static readonly string[] Functions =
    [
        "color-mix(",
        "color(",
        "oklch(",
        "oklab(",
        "hwb(",
        "rgb(",
        "rgba(",
        "hsl(",
        "hsla(",
        "lab(",
        "lch(",
        "light-dark(",
    ];

    internal const string ColorVariable = "var(--khamsin-shadow-color)";

    /// <summary>Replaces every color function in the shadow value with the shadow-color variable.</summary>
    internal static string DeriveColored(string value)
    {
        var result = value;
        foreach (var function in Functions)
        {
            var searchIndex = 0;
            while (searchIndex < result.Length)
            {
                var at = result.IndexOf(function, searchIndex, StringComparison.Ordinal);
                if (at < 0)
                    break;

                // Only a value position starts a color: after the value start, a space or a comma.
                if (at > 0 && result[at - 1] is not ' ' and not ',' and not '(')
                {
                    searchIndex = at + function.Length;
                    continue;
                }

                var close = MatchingParenthesis(result, at + function.Length - 1);
                if (close < 0)
                    return result; // Unbalanced input was rejected by validation; leave it alone.

                result = result.Substring(0, at) + ColorVariable + result.Substring(close + 1);
                searchIndex = at + ColorVariable.Length;
            }
        }

        return result;
    }

    private static int MatchingParenthesis(string text, int openParen)
    {
        var depth = 0;
        for (var i = openParen; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }
}
