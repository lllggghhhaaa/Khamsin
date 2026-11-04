using System.Text;

namespace Khamsin.Extraction;

public enum KxCallKind
{
    When,
    Variant,
    KhamsinClass,
    Cn,
}

public sealed class CSharpLiteral
{
    public CSharpLiteral(string value, int offset, KxCallKind kind)
    {
        Value = value;
        Offset = offset;
        Kind = kind;
    }

    public string Value { get; }

    public int Offset { get; }

    public KxCallKind Kind { get; }
}

/// <summary>
/// Lexical scanner for the literal-based helpers. The generator uses the semantic model for C#
/// diagnostics and extraction; this scanner exists so the MSBuild task can collect literals from
/// sources without a compilation. It deliberately ignores interpolated strings: those are already
/// rejected by KHM001.
/// </summary>
public static class CSharpSourceScanner
{
    public static IReadOnlyList<CSharpLiteral> FindKxLiterals(string text)
    {
        var results = new List<CSharpLiteral>();
        if (string.IsNullOrEmpty(text))
        {
            return results;
        }

        var index = 0;
        while (index < text.Length)
        {
            if (!TryFindCall(text, index, out var callStart, out var callKind, out var openParen))
            {
                break;
            }

            var closeParen = FindMatchingParenthesis(text, openParen);
            if (closeParen < 0)
            {
                break;
            }

            if (callKind == KxCallKind.Cn)
            {
                CollectLiterals(text, openParen + 1, closeParen, callKind, results);
                index = closeParen + 1;
                if (callStart > index)
                {
                    index = callStart;
                }

                continue;
            }

            var arguments = SplitArguments(text, openParen + 1, closeParen);
            if (arguments.Count > 1)
            {
                switch (callKind)
                {
                    case KxCallKind.When:
                        for (var i = 1; i < arguments.Count; i++)
                        {
                            CollectLiterals(text, arguments[i].Start, arguments[i].End, callKind, results);
                        }

                        break;

                    case KxCallKind.Variant:
                        for (var i = 1; i < arguments.Count; i++)
                        {
                            var argument = arguments[i];
                            if (IsTuple(text, argument.Start, argument.End))
                            {
                                // Split the tuple's contents, not the tuple itself: the commas inside
                                // the parentheses separate elements, and the first element is the key.
                                var tupleStart = SkipWhitespace(text, argument.Start, argument.End);
                                var tupleEnd = TrimEnd(text, tupleStart, argument.End);
                                var elements = SplitArguments(text, tupleStart + 1, tupleEnd - 1);
                                for (var e = 1; e < elements.Count; e++)
                                {
                                    CollectLiterals(text, elements[e].Start, elements[e].End, callKind, results);
                                }
                            }
                            else
                            {
                                CollectLiterals(text, argument.Start, argument.End, callKind, results);
                            }
                        }

                        break;
                }
            }

            index = closeParen + 1;
            if (callStart > index)
            {
                index = callStart;
            }
        }

        return results;
    }

    /// <summary>
    /// Finds literal assignments to <c>KhamsinClass</c> members and locals. These are component
    /// defaults: they are part of the closed class set even when they live inside a Razor
    /// <c>@code</c> block that the generator cannot see through the semantic model.
    /// </summary>
    public static IReadOnlyList<CSharpLiteral> FindKhamsinClassLiterals(string text)
    {
        var results = new List<CSharpLiteral>();
        if (string.IsNullOrEmpty(text))
        {
            return results;
        }

        var index = 0;
        while (index < text.Length)
        {
            var at = IndexOfIdentifier(text, "KhamsinClass", index);
            if (at < 0)
            {
                break;
            }

            index = at + "KhamsinClass".Length;
            var cursor = SkipTrivia(text, index);
            if (cursor >= text.Length || !IsIdentifierStart(text[cursor]))
            {
                continue;
            }

            while (cursor < text.Length && IsIdentifierPart(text[cursor]))
            {
                cursor++;
            }

            cursor = SkipTrivia(text, cursor);
            if (cursor >= text.Length || text[cursor] != '=')
            {
                continue;
            }

            cursor = SkipTrivia(text, cursor + 1);
            if (cursor < text.Length && MatchesWord(text, cursor, "new"))
            {
                cursor = SkipTrivia(text, cursor + 3);
                if (cursor < text.Length && IsIdentifierStart(text[cursor]))
                {
                    while (cursor < text.Length && IsIdentifierPart(text[cursor]))
                    {
                        cursor++;
                    }

                    cursor = SkipTrivia(text, cursor);
                }

                if (cursor >= text.Length || text[cursor] != '(')
                {
                    continue;
                }

                cursor = SkipTrivia(text, cursor + 1);
            }

            if (cursor >= text.Length || text[cursor] != '"')
            {
                continue;
            }

            if (TryReadString(text, cursor, text.Length, out var value, out var next))
            {
                results.Add(new CSharpLiteral(value, cursor, KxCallKind.KhamsinClass));
            }

            index = next;
        }

        return results;
    }

    private static int IndexOfIdentifier(string text, string identifier, int start)
    {
        var index = start;
        while (index < text.Length)
        {
            var at = text.IndexOf(identifier, index, StringComparison.Ordinal);
            if (at < 0)
            {
                return -1;
            }

            var before = at == 0 || !IsIdentifierPart(text[at - 1]);
            var end = at + identifier.Length;
            var after = end >= text.Length || !IsIdentifierPart(text[end]);
            if (before && after)
            {
                return at;
            }

            index = at + identifier.Length;
        }

        return -1;
    }

    private static bool TryFindCall(string text, int start, out int callStart, out KxCallKind kind, out int openParen)
    {
        callStart = -1;
        kind = KxCallKind.When;
        openParen = -1;

        for (var i = start; i < text.Length - 1; i++)
        {
            if (!IsIdentifierStart(text[i]))
            {
                continue;
            }

            if (i > 0 && IsIdentifierPart(text[i - 1]))
            {
                continue;
            }

            if (text[i] == 'K' && i + 1 < text.Length && text[i + 1] == 'x' && (i + 2 >= text.Length || !IsIdentifierPart(text[i + 2])))
            {
                var cursor = SkipTrivia(text, i + 2);
                if (cursor >= text.Length || text[cursor] != '.')
                {
                    continue;
                }

                cursor = SkipTrivia(text, cursor + 1);
                if (MatchesWord(text, cursor, "When"))
                {
                    cursor += 4;
                    kind = KxCallKind.When;
                }
                else if (MatchesWord(text, cursor, "Variant"))
                {
                    cursor += 7;
                    kind = KxCallKind.Variant;
                }
                else
                {
                    continue;
                }

                cursor = SkipTrivia(text, cursor);
                if (cursor >= text.Length || text[cursor] != '(')
                {
                    continue;
                }

                callStart = i;
                openParen = cursor;
                return true;
            }

            if (text[i] == 'C' && i + 1 < text.Length && text[i + 1] == 'n' && (i + 2 >= text.Length || !IsIdentifierPart(text[i + 2])))
            {
                var cursor = SkipTrivia(text, i + 2);
                if (cursor >= text.Length || text[cursor] != '.')
                {
                    continue;
                }

                cursor = SkipTrivia(text, cursor + 1);
                int methodNameLength;
                if (MatchesWord(text, cursor, "Of"))
                {
                    methodNameLength = 2;
                }
                else if (MatchesWord(text, cursor, "Merge"))
                {
                    methodNameLength = 5;
                }
                else
                {
                    continue;
                }

                cursor = SkipTrivia(text, cursor + methodNameLength);
                if (cursor >= text.Length || text[cursor] != '(')
                {
                    continue;
                }

                callStart = i;
                openParen = cursor;
                kind = KxCallKind.Cn;
                return true;
            }
        }

        return false;
    }

    private static int FindMatchingParenthesis(string text, int openParen)
    {
        var depth = 0;
        for (var i = openParen; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                i = SkipString(text, i) - 1;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = SkipLineComment(text, i) - 1;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = SkipBlockComment(text, i) - 1;
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static List<Span> SplitArguments(string text, int start, int end)
    {
        var spans = new List<Span>();
        var depth = 0;
        var argumentStart = start;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                i = SkipString(text, i) - 1;
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                spans.Add(new Span(argumentStart, i));
                argumentStart = i + 1;
            }
        }

        spans.Add(new Span(argumentStart, end));
        return spans;
    }

    private static bool IsTuple(string text, int start, int end)
    {
        start = SkipWhitespace(text, start, end);
        end = TrimEnd(text, start, end);
        return end - start >= 2 && text[start] == '(' && text[end - 1] == ')';
    }

    private static void CollectLiterals(string text, int start, int end, KxCallKind kind, List<CSharpLiteral> results)
    {
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c == '\'')
            {
                i = SkipChar(text, i) - 1;
                continue;
            }

            if (c == '/' && i + 1 < end && text[i + 1] == '/')
            {
                i = SkipLineComment(text, i) - 1;
                continue;
            }

            if (c == '/' && i + 1 < end && text[i + 1] == '*')
            {
                i = SkipBlockComment(text, i) - 1;
                continue;
            }

            if (c == '@' && i + 1 < end && text[i + 1] == '"')
            {
                if (TryReadVerbatimString(text, i, end, out var value, out var next))
                {
                    results.Add(new CSharpLiteral(value, i, kind));
                }

                i = next - 1;
                continue;
            }

            if (c == '$')
            {
                // Interpolated string: literal fragments are not a closed set.
                var next = SkipString(text, i + 1);
                i = next - 1;
                continue;
            }

            if (c == '"')
            {
                if (TryReadString(text, i, end, out var value, out var next))
                {
                    results.Add(new CSharpLiteral(value, i, kind));
                }

                i = next - 1;
            }
        }
    }

    private static bool TryReadString(string text, int start, int end, out string value, out int next)
    {
        value = string.Empty;
        next = start + 1;
        var builder = new StringBuilder();
        for (var i = start + 1; i < end; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < end)
            {
                var escaped = text[i + 1];
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '0' => '\0',
                    _ => escaped,
                });
                i++;
                continue;
            }

            if (c == '"')
            {
                value = builder.ToString();
                next = i + 1;
                return true;
            }

            builder.Append(c);
        }

        next = end;
        return false;
    }

    private static bool TryReadVerbatimString(string text, int start, int end, out string value, out int next)
    {
        value = string.Empty;
        var builder = new StringBuilder();
        next = start + 2;
        for (var i = start + 2; i < end; i++)
        {
            if (text[i] == '"')
            {
                if (i + 1 < end && text[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                    continue;
                }

                value = builder.ToString();
                next = i + 1;
                return true;
            }

            builder.Append(text[i]);
        }

        next = end;
        return false;
    }

    /// <summary>Skips a string or character literal and returns the index after its terminator.</summary>
    private static int SkipString(string text, int start)
    {
        if (start >= text.Length)
        {
            return text.Length;
        }

        if (text[start] == '\'')
        {
            return SkipChar(text, start);
        }

        var verbatim = false;
        var cursor = start;
        if (text[cursor] == '$')
        {
            cursor++;
            if (cursor < text.Length && text[cursor] == '@')
            {
                verbatim = true;
                cursor++;
            }
        }
        else if (text[cursor] == '@')
        {
            cursor++;
            verbatim = true;
        }

        if (cursor >= text.Length || text[cursor] != '"')
        {
            return cursor;
        }

        if (verbatim)
        {
            cursor++;
            while (cursor < text.Length)
            {
                if (text[cursor] == '"')
                {
                    if (cursor + 1 < text.Length && text[cursor + 1] == '"')
                    {
                        cursor += 2;
                        continue;
                    }

                    return cursor + 1;
                }

                cursor++;
            }

            return text.Length;
        }

        cursor++;
        while (cursor < text.Length)
        {
            if (text[cursor] == '\\')
            {
                cursor += 2;
                continue;
            }

            if (text[cursor] == '"')
            {
                return cursor + 1;
            }

            cursor++;
        }

        return text.Length;
    }

    private static int SkipChar(string text, int start)
    {
        var cursor = start + 1;
        while (cursor < text.Length)
        {
            if (text[cursor] == '\\')
            {
                cursor += 2;
                continue;
            }

            if (text[cursor] == '\'')
            {
                return cursor + 1;
            }

            cursor++;
        }

        return text.Length;
    }

    private static int SkipLineComment(string text, int start)
    {
        var newline = text.IndexOf('\n', start);
        return newline < 0 ? text.Length : newline + 1;
    }

    private static int SkipBlockComment(string text, int start)
    {
        var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
        return end < 0 ? text.Length : end + 2;
    }

    private static int SkipTrivia(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int SkipWhitespace(string text, int index, int end)
    {
        while (index < end && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int TrimEnd(string text, int start, int end)
    {
        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return end;
    }

    private static bool MatchesWord(string text, int start, string word)
    {
        if (start + word.Length > text.Length)
        {
            return false;
        }

        if (string.CompareOrdinal(text, start, word, 0, word.Length) != 0)
        {
            return false;
        }

        var end = start + word.Length;
        return end >= text.Length || !IsIdentifierPart(text[end]);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private readonly struct Span
    {
        public Span(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }
}
