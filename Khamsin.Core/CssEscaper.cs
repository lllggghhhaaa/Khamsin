using System.Text;

namespace Khamsin;

/// <summary>Escapes class names for use in CSS selectors.</summary>
public static class CssEscaper
{
    public static string Escape(string className)
    {
        if (className.Length == 0)
            return string.Empty;

        var builder = new StringBuilder(className.Length + 8);
        for (var i = 0; i < className.Length; i++)
        {
            var c = className[i];
            var safe = char.IsLetter(c) || c == '-' || c == '_';
            if (safe)
            {
                builder.Append(c);
                continue;
            }

            if (char.IsDigit(c))
            {
                // An identifier may not start with a digit; a hex escape needs a terminator so
                // that \2xl is not read as the code point 0x2 followed by "xl".
                if (i == 0)
                {
                    builder.Append('\\').Append(((int)c).ToString("x", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
                    continue;
                }

                builder.Append(c);
                continue;
            }

            builder.Append('\\').Append(c);
        }

        return builder.ToString();
    }
}
