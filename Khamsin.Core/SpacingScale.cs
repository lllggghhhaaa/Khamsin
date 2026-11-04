using System.Globalization;

namespace Khamsin;

/// <summary>
/// The numeric spacing scale. Values are multipliers of <c>--spacing</c>; <c>px</c> is the single
/// physical-pixel exception.
/// </summary>
public static class SpacingScale
{
    private static readonly Dictionary<string, string> Multipliers = new(StringComparer.Ordinal)
    {
        ["0"] = "0",
        ["0.5"] = "0.5",
        ["1"] = "1",
        ["1.5"] = "1.5",
        ["2"] = "2",
        ["2.5"] = "2.5",
        ["3"] = "3",
        ["3.5"] = "3.5",
        ["4"] = "4",
        ["5"] = "5",
        ["6"] = "6",
        ["7"] = "7",
        ["8"] = "8",
        ["9"] = "9",
        ["10"] = "10",
        ["11"] = "11",
        ["12"] = "12",
        ["14"] = "14",
        ["16"] = "16",
        ["20"] = "20",
        ["24"] = "24",
        ["28"] = "28",
        ["32"] = "32",
        ["36"] = "36",
        ["40"] = "40",
        ["44"] = "44",
        ["48"] = "48",
        ["52"] = "52",
        ["56"] = "56",
        ["60"] = "60",
        ["64"] = "64",
        ["72"] = "72",
        ["80"] = "80",
        ["96"] = "96",
    };

    public static bool IsKnown(string key) => key == "px" || Multipliers.ContainsKey(key);

    /// <summary>Scale keys available to utilities that re-use the numeric steps.</summary>
    internal static IEnumerable<string> Steps => Multipliers.Keys;

    /// <summary>Formats the spacing expression for a scale key, negating when requested.</summary>
    public static bool TryFormat(string key, bool negative, out string value)
    {
        if (key == "px")
        {
            value = negative ? "-1px" : "1px";
            return true;
        }

        if (!Multipliers.TryGetValue(key, out var multiplier))
        {
            value = string.Empty;
            return false;
        }

        if (multiplier == "0")
        {
            value = "0";
            return true;
        }

        value = "calc(var(--spacing) * " + (negative ? "-" : string.Empty) + multiplier + ")";
        return true;
    }

    /// <summary>Formats a fraction such as <c>1/3</c> as a percentage, or fails when malformed.</summary>
    public static bool TryFormatFraction(string key, out string value)
    {
        value = string.Empty;
        var slash = key.IndexOf('/');
        if (slash <= 0 || slash == key.Length - 1)
            return false;

        if (!int.TryParse(key.Substring(0, slash), NumberStyles.None, CultureInfo.InvariantCulture, out var numerator) ||
            !int.TryParse(key.Substring(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var denominator))
            return false;

        if (numerator <= 0 || denominator is < 2 or > 12 || numerator >= denominator)
            return false;

        var percentage = numerator * 100d / denominator;
        value = FormatNumber(percentage) + "%";
        return true;
    }

    internal static string FormatNumber(double number)
    {
        var rounded = Math.Round(number, 6, MidpointRounding.AwayFromZero);
        var text = rounded.ToString("0.######", CultureInfo.InvariantCulture);
        return text;
    }
}
