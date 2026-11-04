namespace Khamsin.Extraction;

public sealed class RazorClassLiteral
{
    public RazorClassLiteral(string value, int offset, int group)
    {
        Value = value;
        Offset = offset;
        Group = group;
    }

    public string Value { get; }

    /// <summary>Offset of the token in the original file, used for diagnostics.</summary>
    public int Offset { get; }

    /// <summary>Identifies the literal this token came from, for conflict diagnostics.</summary>
    public int Group { get; }
}

public sealed class RazorComponentTag
{
    public RazorComponentTag(string name, int offset)
    {
        Name = name;
        Offset = offset;
    }

    public string Name { get; }

    public int Offset { get; }
}

public sealed class RazorNonLiteralUsage
{
    public RazorNonLiteralUsage(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }

    public int Offset { get; }

    public int Length { get; }
}

/// <summary>Facts extracted from a <c>.razor</c> file without invoking the Razor compiler.</summary>
public sealed class RazorScanResult
{
    internal RazorScanResult(
        string? namespaceOverride,
        IReadOnlyList<string> imports,
        IReadOnlyList<RazorClassLiteral> literals,
        IReadOnlyList<RazorClassLiteral> authorLiterals,
        IReadOnlyList<RazorComponentTag> usedComponents,
        IReadOnlyList<RazorNonLiteralUsage> nonLiteralUsages)
    {
        NamespaceOverride = namespaceOverride;
        Imports = imports;
        Literals = literals;
        AuthorLiterals = authorLiterals;
        UsedComponents = usedComponents;
        NonLiteralUsages = nonLiteralUsages;
    }

    public string? NamespaceOverride { get; }

    public IReadOnlyList<string> Imports { get; }

    public IReadOnlyList<RazorClassLiteral> Literals { get; }

    /// <summary>
    /// Tokens from ordinary <c>class</c> attributes. They are collected as a best effort only when
    /// they are known utilities: author class names are an open namespace and are never diagnosed.
    /// </summary>
    public IReadOnlyList<RazorClassLiteral> AuthorLiterals { get; }

    /// <summary>Tag names of rendered components, in source order.</summary>
    public IReadOnlyList<RazorComponentTag> UsedComponents { get; }

    public IReadOnlyList<RazorNonLiteralUsage> NonLiteralUsages { get; }
}

/// <summary>
/// Lightweight Razor scanner used by the generator and by the MSBuild task. It understands the two
/// things Khamsin needs: the <c>Khamsin="..."</c> attribute and component tags. Razor comments,
/// HTML comments and <c>@code</c> blocks are blanked before scanning so their contents cannot
/// contribute false positives.
/// </summary>
public static class RazorSourceScanner
{
    public static RazorScanResult Scan(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var blanked = BlankNonMarkup(text);
        var literals = new List<RazorClassLiteral>();
        var authorLiterals = new List<RazorClassLiteral>();
        var nonLiteral = new List<RazorNonLiteralUsage>();
        var components = new List<RazorComponentTag>();
        var imports = new List<string>();
        string? namespaceOverride = null;

        ScanDirectives(blanked, imports, ref namespaceOverride);
        ScanKhamsinAttributes(blanked, literals, nonLiteral);
        ScanClassAttributes(blanked, authorLiterals);
        ScanComponentTags(blanked, components);

        // Literal-based helpers inside @code blocks and inline expressions. Scan the original text
        // because code blocks were blanked for tag/attribute discovery.
        var group = 1_000_000;
        foreach (var literal in CSharpSourceScanner.FindKxLiterals(text))
        {
            group++;
            AddClassTokens(literals, literal.Value, literal.Offset, group);
        }

        // KhamsinClass constants declared in @code are component defaults and part of the closed set.
        foreach (var literal in CSharpSourceScanner.FindKhamsinClassLiterals(text))
        {
            group++;
            AddClassTokens(literals, literal.Value, literal.Offset, 3_000_000 + group % 100_000);
        }

        return new RazorScanResult(namespaceOverride, imports, literals, authorLiterals, components, nonLiteral);
    }

    private static void ScanDirectives(string text, List<string> imports, ref string? namespaceOverride)
    {
        var index = 0;
        while (index < text.Length)
        {
            var at = text.IndexOf('@', index);
            if (at < 0)
                return;

            if (MatchesWord(text, at + 1, "using"))
            {
                var start = SkipWhitespace(text, at + 6);
                var end = start;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '.' or '_')) end++;

                if (end > start)
                {
                    imports.Add(text.Substring(start, end - start));
                    index = end;
                    continue;
                }
            }
            else if (MatchesWord(text, at + 1, "namespace"))
            {
                var start = SkipWhitespace(text, at + 10);
                var end = start;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '.' or '_')) end++;

                if (end > start)
                {
                    namespaceOverride = text.Substring(start, end - start);
                    index = end;
                    continue;
                }
            }

            index = at + 1;
        }
    }

    private static void ScanKhamsinAttributes(string text, List<RazorClassLiteral> literals, List<RazorNonLiteralUsage> nonLiteral)
    {
        var index = 0;
        var group = 0;
        while (index < text.Length)
        {
            var at = text.IndexOf("Khamsin", index, StringComparison.Ordinal);
            if (at < 0)
                return;

            index = at + 7;
            if (at > 0 && IsIdentifierChar(text[at - 1]))
                continue;

            var cursor = SkipWhitespace(text, index);
            if (cursor >= text.Length || text[cursor] != '=')
                continue;

            cursor = SkipWhitespace(text, cursor + 1);
            if (cursor >= text.Length || (text[cursor] != '"' && text[cursor] != '\''))
                continue;

            var quote = text[cursor];
            var valueStart = cursor + 1;
            var valueEnd = FindAttributeValueEnd(text, valueStart, quote);

            if (valueEnd >= text.Length)
                return;

            var value = text.Substring(valueStart, valueEnd - valueStart);
            index = valueEnd + 1;

            group++;
            var trimmed = value.TrimStart();
            if (value.IndexOf('@') >= 0)
            {
                // Kx.* calls are literal-based; a literal expression such as @("px-4") is also
                // statically known. Everything else is a runtime expression we cannot read.
                if (value.IndexOf("Kx.", StringComparison.Ordinal) >= 0 || value.IndexOf("Cn.", StringComparison.Ordinal) >= 0)
                    continue;

                if (TryExtractLiteralExpression(value, valueStart, out var literalValue, out var literalOffset))
                {
                    AddClassTokens(literals, literalValue, literalOffset, group);
                    continue;
                }

                nonLiteral.Add(new RazorNonLiteralUsage(valueStart, value.Length));
                continue;
            }

            if (trimmed.Length == 0)
            {
                continue;
            }

            AddClassTokens(literals, trimmed, valueStart + (value.Length - trimmed.Length), group);
        }
    }

    /// <summary>
    /// Finds the closing quote, allowing C# string literals and balanced <c>@(...)</c> expressions
    /// inside the value. A naive quote search would stop at the opening quote of <c>@("px-4")</c>.
    /// </summary>
    private static int FindAttributeValueEnd(string text, int start, char quote)
    {
        var index = start;
        var depth = 0;
        var inString = false;
        var verbatim = false;

        while (index < text.Length)
        {
            var c = text[index];
            if (inString)
            {
                if (verbatim)
                {
                    if (c == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            index += 2;
                            continue;
                        }

                        inString = false;
                    }
                }
                else
                {
                    if (c == '\\')
                    {
                        index += 2;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }
                }

                index++;
                continue;
            }

            if (c == '@' && index + 1 < text.Length && text[index + 1] == '(')
            {
                depth++;
                index += 2;
                continue;
            }

            if (depth > 0)
            {
                if (c == '@' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    inString = true;
                    verbatim = true;
                    index += 2;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    verbatim = false;
                    index++;
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                    index++;
                    continue;
                }

                if (c == ')')
                {
                    depth--;
                    index++;
                    continue;
                }

                index++;
                continue;
            }

            if (c == quote)
                return index;

            index++;
        }

        return text.Length;
    }

    private static bool TryExtractLiteralExpression(string value, int valueOffset, out string literal, out int literalOffset)
    {
        literal = string.Empty;
        literalOffset = valueOffset;

        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index])) index++;

        if (index < value.Length && value[index] == '@')
        {
            index++;
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
        }

        if (index < value.Length && value[index] == '(')
        {
            var close = value.Length - 1;
            while (close > index && char.IsWhiteSpace(value[close])) close--;

            if (close <= index || value[close] != ')')
                return false;

            return TryExtractLiteralExpression(value.Substring(index + 1, close - index - 1), valueOffset + index + 1, out literal, out literalOffset);
        }

        var verbatim = false;
        if (index < value.Length && value[index] == '@')
        {
            verbatim = true;
            index++;
        }

        if (index >= value.Length || value[index] != '"')
            return false;

        index++;
        var contentStart = index;
        var builder = new System.Text.StringBuilder();
        while (index < value.Length)
        {
            var c = value[index];
            if (c == '"')
            {
                if (verbatim && index + 1 < value.Length && value[index + 1] == '"')
                {
                    builder.Append('"');
                    index += 2;
                    continue;
                }

                var trailing = index + 1;
                while (trailing < value.Length && char.IsWhiteSpace(value[trailing])) trailing++;

                if (trailing != value.Length)
                    return false;

                literal = builder.ToString();
                literalOffset = valueOffset + contentStart;
                return true;
            }

            if (!verbatim && c == '\\' && index + 1 < value.Length)
            {
                builder.Append(value[index + 1]);
                index += 2;
                continue;
            }

            builder.Append(c);
            index++;
        }

        return false;
    }

    private static void ScanClassAttributes(string text, List<RazorClassLiteral> literals)
    {
        var index = 0;
        var group = 2_000_000;
        while (index < text.Length - 5)
        {
            var at = text.IndexOf("class", index, StringComparison.Ordinal);
            if (at < 0)
                return;

            index = at + 5;
            if (at > 0 && IsIdentifierChar(text[at - 1]))
                continue;

            var cursor = SkipWhitespace(text, index);
            if (cursor >= text.Length || text[cursor] != '=')
                continue;

            cursor = SkipWhitespace(text, cursor + 1);
            if (cursor >= text.Length || (text[cursor] != '"' && text[cursor] != '\''))
                continue;

            var quote = text[cursor];
            var start = cursor + 1;
            var end = start;
            while (end < text.Length && text[end] != quote) end++;

            if (end >= text.Length)
                return;

            var value = text.Substring(start, end - start);
            index = end + 1;

            var expression = value.IndexOf('@');
            if (expression == 0)
                continue;

            var literalPart = expression < 0 ? value : value.Substring(0, expression);
            if (literalPart.Length == 0)
                continue;

            group++;
            AddClassTokens(literals, literalPart, start, group);
        }
    }

    private static void AddClassTokens(List<RazorClassLiteral> literals, string value, int offset, int group)
    {
        var index = 0;
        while (index < value.Length)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;

            var start = index;
            while (index < value.Length && !char.IsWhiteSpace(value[index])) index++;

            if (index > start) literals.Add(new RazorClassLiteral(value.Substring(start, index - start), offset + start, group));
        }
    }

    private static void ScanComponentTags(string text, List<RazorComponentTag> components)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '<')
                continue;

            var cursor = i + 1;
            if (cursor < text.Length && (text[cursor] == '/' || text[cursor] == '!' || text[cursor] == '?'))
                continue;

            cursor = SkipWhitespace(text, cursor);
            if (cursor >= text.Length || !char.IsUpper(text[cursor]))
                continue;

            var start = cursor;
            while (cursor < text.Length && (char.IsLetterOrDigit(text[cursor]) || text[cursor] is '.' or '_')) cursor++;

            if (cursor > start) components.Add(new RazorComponentTag(text.Substring(start, cursor - start), start));
        }
    }

    private static string BlankNonMarkup(string text)
    {
        var chars = text.ToCharArray();
        BlankRazorComments(chars);
        BlankHtmlComments(chars);
        BlankCodeBlocks(chars, "@code");
        BlankCodeBlocks(chars, "@functions");
        return new string(chars);
    }

    private static void BlankRazorComments(char[] chars)
    {
        var index = 0;
        while (index < chars.Length - 1)
        {
            if (chars[index] == '@' && chars[index + 1] == '*')
            {
                var end = IndexOf(chars, index + 2, "*@");
                if (end < 0) end = chars.Length - 2;

                for (var i = index; i < Math.Min(end + 2, chars.Length); i++) Blank(chars, i);

                index = end + 2;
                continue;
            }

            index++;
        }
    }

    private static void BlankHtmlComments(char[] chars)
    {
        var index = 0;
        while (index < chars.Length - 3)
        {
            if (chars[index] == '<' && chars[index + 1] == '!' && chars[index + 2] == '-' && chars[index + 3] == '-')
            {
                var end = IndexOf(chars, index + 4, "-->");
                if (end < 0) end = chars.Length;

                for (var i = index; i < Math.Min(end + 3, chars.Length); i++) Blank(chars, i);

                index = end + 3;
                continue;
            }

            index++;
        }
    }

    private static void BlankCodeBlocks(char[] chars, string directive)
    {
        var index = 0;
        while (index < chars.Length)
        {
            var at = IndexOf(chars, index, directive);
            if (at < 0)
                return;

            var cursor = at + directive.Length;
            while (cursor < chars.Length && char.IsWhiteSpace(chars[cursor])) cursor++;

            if (cursor >= chars.Length || chars[cursor] != '{')
            {
                index = at + directive.Length;
                continue;
            }

            var depth = 0;
            var end = cursor;
            for (; end < chars.Length; end++)
            {
                if (chars[end] == '{')
                {
                    depth++;
                }
                else if (chars[end] == '}')
                {
                    depth--;
                    if (depth != 0) continue;
                    end++;
                    break;
                }
            }

            for (var i = at; i < Math.Min(end, chars.Length); i++) Blank(chars, i);

            index = end;
        }
    }

    private static void Blank(char[] chars, int index)
    {
        if (chars[index] != '\r' && chars[index] != '\n') chars[index] = ' ';
    }

    private static int IndexOf(char[] chars, int start, string value)
    {
        var limit = chars.Length - value.Length;
        for (var i = start; i <= limit; i++)
        {
            var j = 0;
            while (j < value.Length && chars[i + j] == value[j]) j++;
            if (j == value.Length) return i;
        }
        return -1;
    }

    private static bool MatchesWord(string text, int start, string word)
    {
        if (start + word.Length > text.Length)
            return false;

        if (string.CompareOrdinal(text, start, word, 0, word.Length) != 0)
            return false;

        var end = start + word.Length;
        return end >= text.Length || !IsIdentifierChar(text[end]);
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.';

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;

        return index;
    }
}
