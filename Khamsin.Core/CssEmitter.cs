using System.Text;

namespace Khamsin;

/// <summary>
/// Emits deterministic CSS for a set of parsed classes. Ordering is layer, variant weight, property
/// order, then class name; identical input always produces identical bytes. Output is minified by
/// default and can be written in an indented form for debugging.
/// </summary>
public static class CssEmitter
{
    public static string Emit(IEnumerable<ParsedClass> classes, Theme theme, bool includeTheme = true, bool minify = true, string customCss = "")
    {
        if (classes is null)
            throw new ArgumentNullException(nameof(classes));

        if (theme is null)
            throw new ArgumentNullException(nameof(theme));

        var rules = BuildRules(classes, theme);
        rules.Sort(CompareRules);
        var animations = UsedAnimations(classes, theme);

        var builder = new StringBuilder(minify ? 4096 : 8192);
        if (minify)
            EmitMinified(builder, rules, theme, includeTheme);
        else
            EmitReadable(builder, rules, theme, includeTheme);

        AppendCustomCss(builder, customCss, minify);
        AppendKeyframes(builder, animations, theme, minify);
        return builder.ToString();
    }

    /// <summary>Appends custom CSS from the library/app configuration in a base cascade layer.</summary>
    private static void AppendCustomCss(StringBuilder builder, string customCss, bool minify)
    {
        if (string.IsNullOrWhiteSpace(customCss))
            return;

        if (minify)
        {
            builder.Append("@layer base{").Append(customCss.Trim()).Append('}');
            return;
        }

        builder.Append("\n@layer base {\n").Append(customCss.Trim()).Append("\n}\n");
    }

    /// <summary>
    /// Animation names referenced by the emitted classes: theme animations by value, and the
    /// reserved enter/exit keyframes through animate-in/animate-out.
    /// </summary>
    private static List<string> UsedAnimations(IEnumerable<ParsedClass> classes, Theme theme)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parsed in classes)
        {
            if (!string.Equals(parsed.Entry.Group, "animation", StringComparison.Ordinal))
                continue;

            var name = parsed.Entry.Prefix switch
            {
                "animate-in" => "khamsin-enter",
                "animate-out" => "khamsin-exit",
                _ => parsed.Value ?? string.Empty,
            };

            if (name.Length > 0 && theme.Animations.ContainsKey(name) && seen.Add(name))
                names.Add(name);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static void AppendKeyframes(StringBuilder builder, List<string> names, Theme theme, bool minify)
    {
        foreach (var name in names)
        {
            var animation = theme.Animations[name];
            if (animation.Keyframes.Count == 0)
                continue;

            if (minify)
            {
                builder.Append("@keyframes ").Append(name).Append('{');
                foreach (var frame in animation.Keyframes)
                    builder.Append(frame.Key).Append('{').Append(frame.Value).Append('}');

                builder.Append('}');
                continue;
            }

            builder.Append("\n@keyframes ").Append(name).Append(" {\n");
            foreach (var frame in animation.Keyframes)
            {
                builder.Append("  ").Append(frame.Key).Append(" {\n");
                foreach (var declaration in frame.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    builder.Append("    ").Append(declaration.Trim()).Append(";\n");

                builder.Append("  }\n");
            }

            builder.Append("}\n");
        }
    }

    private static void EmitMinified(StringBuilder builder, List<CssRule> rules, Theme theme, bool includeTheme)
    {
        if (includeTheme)
        {
            builder.Append("@layer theme,base,utilities;");
            builder.Append("@layer theme{:root{");
            var variables = theme.CssVariables;
            for (var i = 0; i < variables.Count; i++)
            {
                if (i > 0) builder.Append(';');

                builder.Append(variables[i].Key).Append(':').Append(variables[i].Value);
            }

            builder.Append("}}");
            AppendPreflightMinified(builder);
        }

        builder.Append("@layer utilities{");
        WriteRules(builder, rules);
        builder.Append('}');
    }

    private static void EmitReadable(StringBuilder builder, List<CssRule> rules, Theme theme, bool includeTheme)
    {
        if (includeTheme)
        {
            builder.Append("@layer theme, base, utilities;\n\n");
            builder.Append("@layer theme {\n");
            builder.Append("  :root {\n");
            var variables = theme.CssVariables;
            foreach (var variable in variables) 
                builder.Append("    ").Append(variable.Key).Append(": ").Append(variable.Value).Append(";\n");

            builder.Append("  }\n");
            builder.Append("}\n\n");
            AppendPreflightReadable(builder);
        }

        builder.Append("@layer utilities {\n");
        WriteReadableRules(builder, rules);
        builder.Append('}');
    }

    /// <summary>
    /// Tailwind-compatible preflight. Components and application markup rely on the global reset
    /// (border-box sizing, zeroed margins, form controls inheriting typography), so it is emitted
    /// with the theme in the base layer. Utilities in the later layer still win.
    /// </summary>
    private static readonly (string Selector, string Declarations)[] PreflightRules =
    [
        ("*,::before,::after,::backdrop", "box-sizing:border-box;margin:0;padding:0;border:0 solid"),
        ("::file-selector-button", "box-sizing:border-box;margin:0;padding:0;border:0 solid"),
        ("html", "line-height:1.5;-webkit-text-size-adjust:100%;tab-size:4;-webkit-tap-highlight-color:transparent"),
        ("body", "line-height:inherit"),
        ("hr", "height:0;color:inherit;border-top-width:1px"),
        ("h1,h2,h3,h4,h5,h6", "font-size:inherit;font-weight:inherit"),
        ("a", "color:inherit;text-decoration:inherit"),
        ("b,strong", "font-weight:bolder"),
        ("code,kbd,samp,pre", "font-family:ui-monospace,SFMono-Regular,Menlo,Monaco,Consolas,\"Liberation Mono\",\"Courier New\",monospace;font-size:1em"),
        ("small", "font-size:80%"),
        ("sub,sup", "font-size:75%;line-height:0;position:relative;vertical-align:baseline"),
        ("sub", "bottom:-0.25em"),
        ("sup", "top:-0.5em"),
        ("table", "text-indent:0;border-color:inherit;border-collapse:collapse"),
        ("progress", "vertical-align:baseline"),
        ("summary", "display:list-item"),
        ("ol,ul,menu", "list-style:none"),
        ("img,svg,video,canvas,audio,iframe,embed,object", "display:block;vertical-align:middle"),
        ("img,video", "max-width:100%;height:auto"),
        ("button,input,select,optgroup,textarea", "font:inherit;font-feature-settings:inherit;font-variation-settings:inherit;letter-spacing:inherit;color:inherit;opacity:1;background-color:transparent;border-radius:0"),
        ("::placeholder", "opacity:1;color:color-mix(in oklab,currentcolor 50%,transparent)"),
        ("textarea", "resize:vertical"),
        ("::-webkit-search-decoration", "-webkit-appearance:none"),
        ("::-webkit-date-and-time-value", "min-height:1lh;text-align:inherit"),
        ("::-webkit-datetime-edit", "display:inline-flex"),
        ("::-webkit-datetime-edit-fields-wrapper", "padding:0"),
        ("::-webkit-datetime-edit-year-field,::-webkit-datetime-edit-month-field,::-webkit-datetime-edit-day-field,::-webkit-datetime-edit-hour-field,::-webkit-datetime-edit-minute-field,::-webkit-datetime-edit-second-field,::-webkit-datetime-edit-millisecond-field,::-webkit-datetime-edit-meridiem-field", "padding-block:0"),
        ("::-webkit-calendar-picker-indicator", "line-height:1"),
        ("button,input:where([type='button'],[type='reset'],[type='submit'])", "appearance:button"),
        ("::-webkit-inner-spin-button,::-webkit-outer-spin-button", "height:auto"),
        ("[hidden]:where(:not([hidden='until-found']))", "display:none !important"),
    ];

    private static void AppendPreflightMinified(StringBuilder builder)
    {
        builder.Append("@layer base{");
        foreach (var rule in PreflightRules)
            builder.Append(rule.Selector).Append('{').Append(rule.Declarations).Append('}');

        builder.Append('}');
    }

    private static void AppendPreflightReadable(StringBuilder builder)
    {
        builder.Append("@layer base {\n");
        foreach (var rule in PreflightRules)
        {
            builder.Append("  ").Append(rule.Selector).Append(" {\n");
            foreach (var declaration in SplitDeclarations(rule.Declarations))
                builder.Append("    ").Append(FormatDeclaration(declaration)).Append(";\n");

            builder.Append("  }\n");
        }

        builder.Append("}\n\n");
    }

    private static void WriteReadableRules(StringBuilder builder, List<CssRule> rules)
    {
        var index = 0;
        while (index < rules.Count)
        {
            var groupKey = rules[index].GroupKey;
            var end = index;
            while (end < rules.Count && string.Equals(rules[end].GroupKey, groupKey, StringComparison.Ordinal)) end++;

            var wrappers = rules[index].Wrappers;
            var indent = 2;
            foreach (var w in wrappers)
            {
                builder.Append(' ', indent).Append(w).Append(" {\n");
                indent += 2;
            }

            for (var i = index; i < end; i++) 
                AppendReadableRule(builder, rules[i], new string(' ', indent), new string(' ', indent + 2));

            for (var w = wrappers.Count - 1; w >= 0; w--)
            {
                indent -= 2;
                builder.Append(' ', indent).Append("}\n");
            }

            index = end;
        }
    }

    private static void AppendReadableRule(StringBuilder builder, CssRule rule, string selectorIndent, string declarationIndent)
    {
        builder.Append(selectorIndent).Append(rule.Selector).Append(" {\n");
        foreach (var declaration in SplitDeclarations(rule.Declarations)) 
            builder.Append(declarationIndent).Append(FormatDeclaration(declaration)).Append(";\n");

        builder.Append(selectorIndent).Append("}\n");
    }

    /// <summary>Splits declaration text into one declaration per entry, honouring nested values.</summary>
    private static IEnumerable<string> SplitDeclarations(string declarations)
    {
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < declarations.Length; i++)
        {
            var c = declarations[i];
            if (quote != '\0')
            {
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == quote) quote = '\0';

                continue;
            }

            switch (c)
            {
                case '"':
                case '\'':
                    quote = c;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    if (depth > 0) depth--;

                    break;
                case ';' when depth == 0:
                    if (i > start)
                        yield return declarations.Substring(start, i - start);

                    start = i + 1;
                    break;
            }
        }

        if (declarations.Length > start)
            yield return declarations.Substring(start);
    }

    private static string FormatDeclaration(string declaration)
    {
        var colon = declaration.IndexOf(':');
        var text = colon < 0
            ? declaration
            : declaration.Substring(0, colon) + ": " + declaration.Substring(colon + 1);

        if (text.EndsWith("!important", StringComparison.Ordinal)) 
            text = text.Substring(0, text.Length - "!important".Length) + " !important";

        return text;
    }

    private static void WriteRules(StringBuilder builder, List<CssRule> rules)
    {
        var index = 0;
        var first = true;
        while (index < rules.Count)
        {
            var groupKey = rules[index].GroupKey;
            var end = index;
            while (end < rules.Count && string.Equals(rules[end].GroupKey, groupKey, StringComparison.Ordinal)) end++;

            if (!first) builder.Append('\n');

            var wrappers = rules[index].Wrappers;
            foreach (var w in wrappers) builder.Append(w).Append('{');

            for (var i = index; i < end; i++) AppendRule(builder, rules[i]);
            for (var w = wrappers.Count - 1; w >= 0; w--) builder.Append('}');

            first = false;
            index = end;
        }
    }

    private static void AppendRule(StringBuilder builder, CssRule rule) => builder.Append(rule.Selector).Append('{').Append(rule.Declarations).Append('}');

    private static List<CssRule> BuildRules(IEnumerable<ParsedClass> classes, Theme theme)
    {
        var variants = VariantRegistry.Create(theme);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rules = new List<CssRule>();

        foreach (var parsed in classes)
        {
            if (!seen.Add(parsed.Raw))
                continue;

            var bound = new BoundValue(
                parsed.Value ?? string.Empty,
                parsed.Modifier,
                parsed.IsArbitrary,
                parsed.IsCssVariable);

            if (!UtilityCss.TryBuildDeclarations(parsed.Entry, bound, parsed.Negative, theme, out var declarations))
                continue;

            var selector = "." + CssEscaper.Escape(parsed.Raw);
            var wrappers = new List<string>(2);
            var weight = 0;

            foreach (var variantName in parsed.Variants)
            {
                if (!variants.TryGet(variantName, out var variant))
                    continue;

                if (variant.Weight > weight) weight = variant.Weight;

                switch (variant.Kind)
                {
                    case VariantKind.Prefix:
                        selector = variant.SelectorPrefix + selector;
                        break;
                    case VariantKind.Pseudo:
                        selector += variant.Pseudo;
                        break;
                    case VariantKind.Media:
                        wrappers.Add("@media " + variant.Media);
                        break;
                    case VariantKind.Supports:
                        wrappers.Add("@supports " + variant.Media);
                        break;
                    case VariantKind.AtRule:
                        wrappers.Add(variant.Media!);
                        break;
                    case VariantKind.Selector:
                        selector = variant.SelectorPrefix!.Replace("&", selector);
                        break;
                }
            }

            if (parsed.Entry.ChildSelector != null) selector += parsed.Entry.ChildSelector;

            if (NeedsGeneratedContent(parsed.Variants) &&
                declarations.IndexOf("content:", StringComparison.Ordinal) < 0) declarations += ";content:var(--khamsin-content,\"\")";

            var wrapperKey = wrappers.Count == 0 ? string.Empty : string.Join("|", wrappers);
            var variantKey = parsed.Variants.Count == 0 ? string.Empty : string.Join("|", parsed.Variants);
            var groupKey = wrapperKey.Length == 0 ? variantKey : wrapperKey + "#" + variantKey;

            rules.Add(new CssRule(
                parsed.Raw,
                selector,
                UtilityCss.ApplyImportant(declarations, parsed.Important),
                wrappers,
                groupKey,
                wrapperKey,
                weight,
                parsed.Entry.Order));
        }

        return rules;
    }

    private static bool NeedsGeneratedContent(IReadOnlyList<string> variants) =>
        variants.Any(t =>
            string.Equals(t, "before", StringComparison.Ordinal) ||
            string.Equals(t, "after", StringComparison.Ordinal));

    private static int CompareRules(CssRule left, CssRule right)
    {
        var byWeight = left.Weight.CompareTo(right.Weight);
        if (byWeight != 0)
            return byWeight;

        var byWrapper = string.CompareOrdinal(left.SortKey, right.SortKey);
        if (byWrapper != 0)
            return byWrapper;

        var byOrder = left.Order.CompareTo(right.Order);
        return byOrder != 0 ? byOrder : string.CompareOrdinal(left.Raw, right.Raw);
    }

    private sealed record CssRule(
        string Raw,
        string Selector,
        string Declarations,
        IReadOnlyList<string> Wrappers,
        string GroupKey,
        string SortKey,
        int Weight,
        int Order);
}
