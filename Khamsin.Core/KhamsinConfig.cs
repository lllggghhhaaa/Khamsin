using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Khamsin;

/// <summary>
/// The Khamsin configuration file model, loaded from <c>khamsin.json</c>. Token entries merge over
/// the built-in defaults by name, so a config can add semantic colors such as
/// <c>primary</c> without restating the palette, or deliberately re-theme a built-in family.
/// </summary>
public sealed class KhamsinConfig
{
    /// <summary>Selector prefix for the <c>dark:</c> variant; a space is appended when emitted.</summary>
    public string? Dark { get; set; }

    /// <summary>Overrides <c>--spacing</c>, the base of the numeric scale.</summary>
    public string? Spacing { get; set; }

    /// <summary>Color names mapped to CSS values; <c>var()</c> references enable runtime theming.</summary>
    public Dictionary<string, string>? Colors { get; set; }

    /// <summary>Font family tokens used by <c>font-&lt;name&gt;</c>.</summary>
    public Dictionary<string, string>? Fonts { get; set; }

    public Dictionary<string, string>? Radius { get; set; }

    public Dictionary<string, string>? Shadows { get; set; }

    /// <summary>Text size tokens; each carries a size and a paired line height.</summary>
    public Dictionary<string, TextSize>? TextSizes { get; set; }

    public Dictionary<string, string>? Eases { get; set; }

    /// <summary>Breakpoints feed the responsive variants; extra names become extra variants.</summary>
    public Dictionary<string, string>? Breakpoints { get; set; }

    public Dictionary<string, string>? Containers { get; set; }

    /// <summary>Animation tokens: shorthand plus optional keyframes, used by <c>animate-&lt;name&gt;</c>.</summary>
    public Dictionary<string, AnimationDefinition>? Animations { get; set; }

    /// <summary>Parses configuration JSON. Returns false and fills <paramref name="errors"/> when the document or any entry is invalid.</summary>
    public static bool TryParse(string json, out KhamsinConfig? config, out IReadOnlyList<string> errors)
    {
        config = null;
        var collected = new List<string>();

        if (!JsonReader.TryParse(json, out var document, out var parseError))
        {
            collected.Add(parseError ?? "The configuration could not be parsed.");
            errors = collected;
            return false;
        }

        if (document.Kind != JsonValueKind.Object)
        {
            collected.Add("The configuration root must be a JSON object.");
            errors = collected;
            return false;
        }

        var parsed = new KhamsinConfig();
        foreach (var member in document.Members!)
        {
            switch (member.Key)
            {
                case "dark":
                    parsed.Dark = ReadString(member, "dark", collected);
                    break;
                case "spacing":
                    parsed.Spacing = ReadString(member, "spacing", collected);
                    break;
                case "colors":
                    parsed.Colors = ReadColors(member, collected);
                    break;
                case "fonts":
                case "radius":
                case "shadows":
                case "eases":
                case "breakpoints":
                case "containers":
                    AssignDictionary(parsed, member, collected);
                    break;
                case "textSizes":
                    parsed.TextSizes = ReadTextSizes(member, collected);
                    break;
                case "animations":
                    parsed.Animations = ReadAnimations(member, collected);
                    break;
                default:
                    collected.Add($"Unknown configuration key '{member.Key}'.");
                    break;
            }
        }

        parsed.Validate(collected);
        errors = collected;
        if (collected.Count > 0) return false;

        config = parsed;
        return true;
    }

    /// <summary>Loads and parses a configuration file.</summary>
    public static bool TryLoad(string path, out KhamsinConfig? config, out IReadOnlyList<string> errors)
    {
        if (!File.Exists(path))
        {
            config = null;
            errors = new[] { $"Configuration file '{path}' was not found." };
            return false;
        }

        return TryParse(File.ReadAllText(path), out config, out errors);
    }

    /// <summary>Builds a Theme from the built-in defaults with this config's entries merged over them.</summary>
    public Theme ToTheme()
    {
        var baseTheme = Theme.Default;
        return new Theme(
            spacing: Spacing ?? baseTheme.Spacing,
            colors: Merge(baseTheme.Colors, Colors),
            radius: Merge(baseTheme.Radius, Radius),
            shadows: Merge(baseTheme.Shadows, Shadows),
            textSizes: MergeSizes(baseTheme.TextSizes, TextSizes),
            eases: Merge(baseTheme.Eases, Eases),
            breakpoints: Merge(baseTheme.Breakpoints, Breakpoints),
            containers: Merge(baseTheme.Containers, Containers),
            fonts: Merge(baseTheme.Fonts, Fonts),
            darkSelector: string.IsNullOrEmpty(Dark) ? baseTheme.DarkSelector : Dark!,
            animations: MergeAnimations(baseTheme.Animations, Animations));
    }

    private static Dictionary<string, AnimationDefinition> MergeAnimations(
        IReadOnlyDictionary<string, AnimationDefinition> baseMap, Dictionary<string, AnimationDefinition>? overrides)
    {
        var merged = new Dictionary<string, AnimationDefinition>(StringComparer.Ordinal);
        foreach (var pair in baseMap) merged[pair.Key] = pair.Value;

        if (overrides != null)
            foreach (var pair in overrides) merged[pair.Key] = pair.Value;

        return merged;
    }

    private static Dictionary<string, AnimationDefinition> ReadAnimations(KeyValuePair<string, JsonValue> member, List<string> errors)
    {
        var animations = new Dictionary<string, AnimationDefinition>(StringComparer.Ordinal);
        if (member.Value.Kind != JsonValueKind.Object)
        {
            errors.Add("'animations' must be an object.");
            return animations;
        }

        foreach (var entry in member.Value.Members!)
        {
            var name = entry.Key.ToLowerInvariant();
            if (!IsValidName(name))
            {
                errors.Add($"Animation name '{entry.Key}' is invalid; use lowercase letters, digits and dashes.");
                continue;
            }

            if (entry.Value.Kind == JsonValueKind.String)
            {
                animations[name] = new AnimationDefinition(entry.Value.Text!);
                continue;
            }

            if (entry.Value.Kind == JsonValueKind.Object)
            {
                string? value = null;
                List<KeyValuePair<string, string>>? keyframes = null;
                foreach (var property in entry.Value.Members!)
                {
                    if (string.Equals(property.Key, "value", StringComparison.OrdinalIgnoreCase) && property.Value.Kind == JsonValueKind.String)
                    {
                        value = property.Value.Text;
                    }
                    else if (string.Equals(property.Key, "keyframes", StringComparison.OrdinalIgnoreCase) && property.Value.Kind == JsonValueKind.Object)
                    {
                        keyframes = new List<KeyValuePair<string, string>>();
                        foreach (var frame in property.Value.Members!)
                        {
                            if (frame.Value.Kind != JsonValueKind.String)
                            {
                                errors.Add($"Animation '{entry.Key}' keyframe '{frame.Key}' must be a string of declarations.");
                                continue;
                            }

                            keyframes.Add(new KeyValuePair<string, string>(frame.Key, frame.Value.Text!));
                        }
                    }
                    else
                    {
                        errors.Add($"Animation '{entry.Key}' supports 'value' and 'keyframes' only.");
                    }
                }

                if (value is null)
                {
                    errors.Add($"Animation '{entry.Key}' is missing its 'value' shorthand.");
                    continue;
                }

                animations[name] = keyframes is null
                    ? new AnimationDefinition(value)
                    : new AnimationDefinition(value, [.. keyframes]);
                continue;
            }

            errors.Add($"Animation '{entry.Key}' must be a string or an object with 'value' and 'keyframes'.");
        }

        return animations;
    }

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> baseMap, Dictionary<string, string>? overrides)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in baseMap) merged[pair.Key] = pair.Value;

        if (overrides != null)
            foreach (var pair in overrides) merged[pair.Key] = pair.Value;

        return merged;
    }

    private static Dictionary<string, TextSize> MergeSizes(
        IReadOnlyDictionary<string, TextSize> baseMap, Dictionary<string, TextSize>? overrides)
    {
        var merged = new Dictionary<string, TextSize>(StringComparer.Ordinal);
        foreach (var pair in baseMap) merged[pair.Key] = pair.Value;

        if (overrides != null)
            foreach (var pair in overrides) merged[pair.Key] = pair.Value;

        return merged;
    }

    private static string? ReadString(KeyValuePair<string, JsonValue> member, string key, List<string> errors)
    {
        if (member.Value.Kind != JsonValueKind.String)
        {
            errors.Add($"'{key}' must be a string.");
            return null;
        }

        return member.Value.Text;
    }

    private static Dictionary<string, string> ReadColors(KeyValuePair<string, JsonValue> member, List<string> errors)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (member.Value.Kind != JsonValueKind.Object)
        {
            errors.Add("'colors' must be an object.");
            return colors;
        }

        foreach (var entry in member.Value.Members!)
        {
            var name = entry.Key.ToLowerInvariant();
            if (!IsValidName(name))
            {
                errors.Add($"Color name '{entry.Key}' is invalid; use lowercase letters, digits and dashes.");
                continue;
            }

            if (entry.Value.Kind == JsonValueKind.String)
            {
                colors[name] = entry.Value.Text!;
                continue;
            }

            if (entry.Value.Kind == JsonValueKind.Object)
            {
                foreach (var shade in entry.Value.Members!)
                {
                    if (shade.Value.Kind != JsonValueKind.String)
                    {
                        errors.Add($"Color '{entry.Key}.{shade.Key}' must be a string value.");
                        continue;
                    }

                    var shadeName = shade.Key.ToLowerInvariant();
                    if (!IsValidName(shadeName))
                    {
                        errors.Add($"Color shade '{entry.Key}.{shade.Key}' is invalid; use lowercase letters, digits and dashes.");
                        continue;
                    }

                    colors[shadeName == "default" ? name : name + "-" + shadeName] = shade.Value.Text!;
                }

                continue;
            }

            errors.Add($"Color '{entry.Key}' must be a string or an object of shades.");
        }

        return colors;
    }

    private static Dictionary<string, TextSize> ReadTextSizes(KeyValuePair<string, JsonValue> member, List<string> errors)
    {
        var sizes = new Dictionary<string, TextSize>(StringComparer.Ordinal);
        if (member.Value.Kind != JsonValueKind.Object)
        {
            errors.Add("'textSizes' must be an object.");
            return sizes;
        }

        foreach (var entry in member.Value.Members!)
        {
            var name = entry.Key.ToLowerInvariant();
            if (!IsValidName(name))
            {
                errors.Add($"Text size name '{entry.Key}' is invalid; use lowercase letters, digits and dashes.");
                continue;
            }

            if (entry.Value.Kind == JsonValueKind.String)
            {
                sizes[name] = new TextSize(entry.Value.Text!, entry.Value.Text!);
                continue;
            }

            if (entry.Value.Kind == JsonValueKind.Array && entry.Value.Items!.Count is 1 or 2)
            {
                var items = entry.Value.Items;
                if (items.All(static item => item.Kind == JsonValueKind.String))
                {
                    var size = items[0].Text!;
                    var lineHeight = items.Count == 2 ? items[1].Text! : size;
                    sizes[name] = new TextSize(size, lineHeight);
                    continue;
                }
            }

            errors.Add($"Text size '{entry.Key}' must be a string or a [size, lineHeight] pair.");
        }

        return sizes;
    }

    private static void AssignDictionary(KhamsinConfig config, KeyValuePair<string, JsonValue> member, List<string> errors)
    {
        var target = member.Key switch
        {
            "fonts" => config.Fonts ??= new Dictionary<string, string>(StringComparer.Ordinal),
            "radius" => config.Radius ??= new Dictionary<string, string>(StringComparer.Ordinal),
            "shadows" => config.Shadows ??= new Dictionary<string, string>(StringComparer.Ordinal),
            "eases" => config.Eases ??= new Dictionary<string, string>(StringComparer.Ordinal),
            "breakpoints" => config.Breakpoints ??= new Dictionary<string, string>(StringComparer.Ordinal),
            _ => config.Containers ??= new Dictionary<string, string>(StringComparer.Ordinal),
        };

        if (member.Value.Kind != JsonValueKind.Object)
        {
            errors.Add($"'{member.Key}' must be an object.");
            return;
        }

        foreach (var entry in member.Value.Members!)
        {
            if (entry.Value.Kind != JsonValueKind.String)
            {
                errors.Add($"'{member.Key}.{entry.Key}' must be a string value.");
                continue;
            }

            var name = entry.Key.ToLowerInvariant();
            if (!IsValidName(name))
            {
                errors.Add($"'{member.Key}.{entry.Key}' is invalid; use lowercase letters, digits and dashes.");
                continue;
            }

            target[name] = entry.Value.Text!;
        }
    }

    private void Validate(List<string> errors)
    {
        if (Dark is { Length: > 0 })
        {
            // "media" selects the v4 prefers-color-scheme strategy instead of a class prefix.
            if (!string.Equals(Dark, "media", StringComparison.Ordinal) && !VariantRegistry.IsSafeFragment(Dark))
                errors.Add("'dark' must be a safe CSS selector fragment without braces, semicolons or comments, or the value 'media'.");
        }

        if (Spacing is { Length: > 0 } && !IsSafeValue(Spacing))
            errors.Add("'spacing' contains characters that are not allowed in a CSS value.");

        ValidateValues(Colors, "colors", errors);
        ValidateValues(Fonts, "fonts", errors);
        ValidateValues(Radius, "radius", errors);
        ValidateValues(Shadows, "shadows", errors);
        ValidateValues(Eases, "eases", errors);
        ValidateValues(Breakpoints, "breakpoints", errors);
        ValidateValues(Containers, "containers", errors);

        if (TextSizes != null)
            foreach (var pair in TextSizes)
            {
                if (!IsSafeValue(pair.Value.Size)) errors.Add($"'textSizes.{pair.Key}' has an invalid size value.");
                if (!IsSafeValue(pair.Value.LineHeight)) errors.Add($"'textSizes.{pair.Key}' has an invalid line-height value.");
            }

        if (Animations != null)
            foreach (var pair in Animations)
            {
                if (!IsSafeValue(pair.Value.Value)) errors.Add($"'animations.{pair.Key}' has an invalid animation shorthand.");
                foreach (var frame in pair.Value.Keyframes)
                    if (!IsSafeSelector(frame.Key) || !IsSafeDeclarations(frame.Value))
                        errors.Add($"'animations.{pair.Key}' has an invalid keyframe '{frame.Key}'.");
            }
    }

    private static bool IsSafeSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return false;

        foreach (var c in selector)
            if (c is '{' or '}' or '@' || char.IsControl(c))
                return false;

        return true;
    }

    private static bool IsSafeDeclarations(string declarations)
    {
        if (string.IsNullOrWhiteSpace(declarations)) return false;

        foreach (var c in declarations)
            if (c is '{' or '}' or '@' || char.IsControl(c))
                return false;

        return true;
    }

    private static void ValidateValues(Dictionary<string, string>? entries, string key, List<string> errors)
    {
        if (entries == null) return;

        foreach (var pair in entries)
            if (!IsSafeValue(pair.Value))
                errors.Add($"'{key}.{pair.Key}' contains characters that are not allowed in a CSS value.");
    }

    private static bool IsSafeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Double quotes are legal inside CSS values (font names); semicolons would split the
        // minified token list, braces would break the :root block, and control characters and
        // at-signs have no business in a token value.
        foreach (var c in value)
            if (c is ';' or '{' or '}' or '@' || char.IsControl(c))
                return false;

        return true;
    }

    private static bool IsValidName(string name)
    {
        if (name.Length == 0) return false;

        foreach (var c in name)
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
                return false;

        return true;
    }
}
