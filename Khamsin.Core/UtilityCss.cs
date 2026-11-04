using System.Text;

namespace Khamsin;

/// <summary>Turns a bound utility into declaration text. All output is minified CSS.</summary>
internal static class UtilityCss
{
    internal static bool TryBuildDeclarations(UtilityEntry entry, in BoundValue value, bool negative, Theme theme, out string declarations)
    {
        declarations = string.Empty;

        // Negative enum values (for example -rotate-45) negate the token through calc against the
        // value template; the fixed declaration map only knows the positive form. The unit suffix
        // that follows {v} in the template moves inside the calc.
        if (negative && entry.Kind == UtilityKind.Enum && entry.Template != null && !string.IsNullOrEmpty(value.Value))
        {
            var placeholder = entry.Template.IndexOf("{v}", StringComparison.Ordinal);
            var prefix = placeholder >= 0 ? entry.Template.Substring(0, placeholder) : entry.Template;
            var suffix = placeholder >= 0 ? entry.Template.Substring(placeholder + 3) : string.Empty;

            // Structural closers that belong to the template (skewX's parenthesis) wrap the calc
            // instead of living inside it; unit text stays in.
            var trailing = string.Empty;
            while (suffix.EndsWith(")"))
            {
                trailing = ")" + trailing;
                suffix = suffix.Substring(0, suffix.Length - 1);
            }

            declarations = prefix + "calc(" + value.Value + suffix + " * -1)" + trailing;
            return true;
        }

        if (value is { Arbitrary: false, CssVariable: false } && entry.Values != null && entry.Values.TryGetValue(value.Value, out var fixedDeclarations))
        {
            declarations = fixedDeclarations;
            return true;
        }

        if (entry.Template is null && entry.ArbitraryTemplate is null)
            return false;

        // Spacing and color utilities always carry a base template; enum/arbitrary utilities may
        // rely on the arbitrary template alone.
        var baseTemplate = entry.Template!;

        // Labeled utilities (for example @container/{name}) substitute the label into the template.
        if (entry.AcceptsLabel && !string.IsNullOrEmpty(value.Value) && !value.Arbitrary && !value.CssVariable)
        {
            declarations = baseTemplate.Replace("{v}", value.Value);
            return true;
        }

        switch (entry.Kind)
        {
            case UtilityKind.Spacing:
            {
                if (value.Arbitrary)
                {
                    if (!TryUnwrapArbitrary(value.Value, out var raw))
                    {
                        return false;
                    }

                    var arbitraryValue = negative ? "calc(" + raw + " * -1)" : raw;
                    declarations = baseTemplate.Replace("{v}", arbitraryValue);
                    return true;
                }

                if (value.CssVariable)
                {
                    if (!TryUnwrapCssVariable(value.Value, out var name))
                    {
                        return false;
                    }

                    var reference = "var(" + name + ")";
                    declarations = baseTemplate.Replace("{v}", negative ? "calc(" + reference + " * -1)" : reference);
                    return true;
                }

                if (entry.Keywords != null && entry.Keywords.TryGetValue(value.Value, out var keyword))
                {
                    declarations = baseTemplate.Replace("{v}", keyword);
                    return true;
                }

                if (SpacingScale.TryFormatFraction(value.Value, out var fraction))
                {
                    declarations = baseTemplate.Replace("{v}", fraction);
                    return true;
                }

                if (!SpacingScale.TryFormat(value.Value, negative, out var spacing))
                {
                    return false;
                }

                declarations = baseTemplate.Replace("{v}", spacing);
                return true;
            }

            case UtilityKind.Color:
            {
                // The background-color utility is polymorphic for arbitrary values: urls, gradients
                // and type hints target background-image/size/position like Tailwind's bg-[…].
                if (value.Arbitrary &&
                    string.Equals(entry.Group, "bg-color", StringComparison.Ordinal) &&
                    TryBuildBackgroundUtility(value.Value, value.Modifier, theme, out var backgroundDeclarations))
                {
                    declarations = backgroundDeclarations;
                    return true;
                }

                if (!TryBuildColor(value, theme, out var color))
                    return false;

                declarations = baseTemplate.Replace("{v}", color);
                return true;
            }

            case UtilityKind.Enum:
            case UtilityKind.Arbitrary:
            {
                var template = (entry.ArbitraryTemplate ?? entry.Template)!;
                string raw;
                if (value.CssVariable)
                {
                    if (!TryUnwrapCssVariable(value.Value, out var name))
                        return false;

                    raw = "var(" + name + ")";
                }
                else if (value.Arbitrary)
                {
                    if (!TryUnwrapArbitrary(value.Value, out raw))
                        return false;
                }
                else
                {
                    return false;
                }

                declarations = template.Replace("{v}", raw);
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Builds non-color background declarations for arbitrary <c>bg-[…]</c> values: a leading
    /// <c>url(</c> or gradient function becomes background-image, and the Tailwind type hints
    /// <c>image:</c>, <c>length:</c>, <c>position:</c> select the property explicitly;
    /// <c>color:</c> forces the color path. Everything else is a plain color.
    /// </summary>
    private static bool TryBuildBackgroundUtility(string token, string? modifier, Theme theme, out string declarations)
    {
        declarations = string.Empty;
        if (!TryUnwrapArbitrary(token, out var raw))
            return false;

        if (raw.StartsWith("url(", StringComparison.Ordinal) || StartsWithGradient(raw))
        {
            declarations = "background-image:" + raw;
            return true;
        }

        if (raw.StartsWith("image:", StringComparison.Ordinal))
        {
            declarations = "background-image:" + raw.Substring("image:".Length).Trim();
            return true;
        }

        if (raw.StartsWith("length:", StringComparison.Ordinal))
        {
            declarations = "background-size:" + raw.Substring("length:".Length).Trim();
            return true;
        }

        if (raw.StartsWith("position:", StringComparison.Ordinal))
        {
            declarations = "background-position:" + raw.Substring("position:".Length).Trim();
            return true;
        }

        if (raw.StartsWith("color:", StringComparison.Ordinal))
        {
            var forced = new BoundValue("[" + raw.Substring("color:".Length).Trim() + "]", modifier, true, false);
            if (!TryBuildColor(forced, theme, out var color))
                return false;

            declarations = "background-color:" + color;
            return true;
        }

        return false;
    }

    private static bool StartsWithGradient(string raw)
    {
        return raw.StartsWith("linear-gradient(", StringComparison.Ordinal) ||
               raw.StartsWith("radial-gradient(", StringComparison.Ordinal) ||
               raw.StartsWith("conic-gradient(", StringComparison.Ordinal) ||
               raw.StartsWith("repeating-linear-gradient(", StringComparison.Ordinal) ||
               raw.StartsWith("repeating-radial-gradient(", StringComparison.Ordinal) ||
               raw.StartsWith("repeating-conic-gradient(", StringComparison.Ordinal);
    }

    private static bool TryBuildColor(in BoundValue value, Theme theme, out string color)
    {
        color = string.Empty;

        if (value.CssVariable)
        {
            if (!TryUnwrapCssVariable(value.Value, out var name))
                return false;

            color = "var(" + name + ")";
        }
        else if (value.Arbitrary)
        {
            if (!TryUnwrapArbitrary(value.Value, out var raw))
                return false;

            color = raw;
        }
        else if (theme.IsKnownColor(value.Value))
        {
            color = "var(--color-" + value.Value + ")";
        }
        else
        {
            return false;
        }

        if (value.Modifier is null)
        {
            return true;
        }

        string opacity;
        if (value.Modifier.Length > 2 && value.Modifier[0] == '[' && value.Modifier[value.Modifier.Length - 1] == ']')
        {
            if (!TryUnwrapArbitrary(value.Modifier, out var raw))
                return false;

            opacity = raw;
        }
        else
        {
            opacity = value.Modifier + "%";
        }

        color = "color-mix(in oklab," + color + " " + opacity + ",transparent)";
        return true;
    }

    internal static bool TryUnwrapArbitrary(string token, out string raw)
    {
        raw = string.Empty;
        if (token.Length < 3 || token[0] != '[' || token[token.Length - 1] != ']')
            return false;

        var inner = token.Substring(1, token.Length - 2).Trim();
        if (inner.Length == 0)
            return false;

        var builder = new StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c is ';' or '{' or '}' || c == '\r' || c == '\n')
                return false;

            // An escaped underscore keeps itself; a bare one stands in for a space.
            if (c == '\\' && i + 1 < inner.Length && inner[i + 1] == '_')
            {
                builder.Append('_');
                i++;
                continue;
            }

            builder.Append(c == '_' ? ' ' : c);
        }

        if (builder.ToString().Contains("/*"))
            return false;

        if (!HasBalancedDelimiters(builder.ToString()))
            return false;

        raw = builder.ToString();
        return true;
    }

    internal static bool TryUnwrapCssVariable(string token, out string name)
    {
        name = string.Empty;
        if (token.Length < 4 || token[0] != '(' || token[token.Length - 1] != ')')
            return false;

        var inner = token.Substring(1, token.Length - 2).Trim();
        if (!inner.StartsWith("--", StringComparison.Ordinal) || inner.Length == 2)
            return false;

        for (var i = 2; i < inner.Length; i++)
        {
            var c = inner[i];
            var valid = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_';
            if (!valid)
                return false;
        }

        name = inner;
        return true;
    }

    private static bool HasBalancedDelimiters(string text)
    {
        var depth = 0;
        foreach (var c in text)
        {
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

        return depth == 0;
    }

    internal static string ApplyImportant(string declarations, bool important)
    {
        if (!important)
            return declarations;

        var parts = declarations.Split(';');
        var builder = new StringBuilder(declarations.Length + parts.Length * 10);
        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;

            if (builder.Length > 0) builder.Append(';');

            builder.Append(part).Append("!important");
        }

        return builder.ToString();
    }
}
