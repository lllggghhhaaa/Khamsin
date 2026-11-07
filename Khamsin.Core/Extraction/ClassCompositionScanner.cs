using System;
using System.Collections.Generic;

namespace Khamsin.Extraction
{
    /// <summary>
    /// Best-effort extraction for classes that are composed rather than written as one literal:
    /// interpolated class attributes, ternaries, concatenation, expression-bodied members and class
    /// attributes embedded in markup strings. These positions are an open namespace, so callers keep
    /// only the tokens the utility registry knows.
    /// </summary>
    public static class ClassCompositionScanner
    {
        /// <summary>
        /// Matches <c>class</c> and attribute names carrying it, so component parameter
        /// parameters such as <c>Class</c>, <c>ClassName</c> or <c>AvatarClass</c> are read too.
        /// </summary>
        public static bool TryReadAttributeName(string text, int start, out int length)
        {
            length = 0;
            var cursor = start;
            while (cursor < text.Length && (char.IsLetterOrDigit(text[cursor]) || text[cursor] == '_'))
                cursor++;

            if (cursor == start)
                return false;

            length = cursor - start;
            return length >= 5 && text.Substring(start, length).IndexOf("class", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Splits a class-attribute value into literal fragments, <c>@(…)</c> expressions and bare
        /// <c>@Member</c> references. Fragments are best effort: the caller keeps only known tokens.
        /// </summary>
        public static void CollectClassValue(
            string value,
            int valueOffset,
            List<RazorClassLiteral> literals,
            List<RazorClassReference> references,
            int group)
        {
            if (value.IndexOf('@') < 0)
            {
                AddClassTokens(literals, value, valueOffset, group);
                return;
            }

            var index = 0;
            var literalStart = -1;
            while (index < value.Length)
            {
                var c = value[index];
                if (c != '@')
                {
                    if (literalStart < 0)
                        literalStart = index;

                    index++;
                    continue;
                }

                if (literalStart >= 0 && index > literalStart)
                    AddClassTokens(literals, value.Substring(literalStart, index - literalStart), valueOffset + literalStart, group);

                literalStart = -1;
                var after = index + 1;
                if (after >= value.Length)
                    break;

                if (value[after] == '(')
                {
                    var close = FindClosingParenthesis(value, after);
                    if (close < 0)
                        break;

                    CollectExpressionLiterals(
                        value.Substring(after + 1, close - after - 1), valueOffset + after + 1, literals, group);
                    index = close + 1;
                    continue;
                }

                if (char.IsLetter(value[after]) || value[after] == '_')
                {
                    var start = after;
                    while (after < value.Length && (char.IsLetterOrDigit(value[after]) || value[after] == '_'))
                        after++;

                    var name = value.Substring(start, after - start);

                    if (after >= value.Length || value[after] != '.')
                        references.Add(new RazorClassReference(name, valueOffset + start));
                }

                index = after;
            }

            if (literalStart >= 0 && index > literalStart)
                AddClassTokens(literals, value.Substring(literalStart, index - literalStart), valueOffset + literalStart, group);
        }

        /// <summary>Collects every string literal and interpolated fragment of a C# expression.</summary>
        public static void CollectExpressionLiterals(
            string expression, int baseOffset, List<RazorClassLiteral> literals, int group)
        {
            var index = 0;
            while (index < expression.Length)
            {
                var c = expression[index];
                if (c == '/' && index + 1 < expression.Length && expression[index + 1] == '/')
                {
                    var newline = expression.IndexOf('\n', index);
                    index = newline < 0 ? expression.Length : newline + 1;
                    continue;
                }

                if (c == '/' && index + 1 < expression.Length && expression[index + 1] == '*')
                {
                    var close = expression.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    index = close < 0 ? expression.Length : close + 2;
                    continue;
                }

                if (c == '\'')
                {
                    index = SkipCharLiteral(expression, index);
                    continue;
                }

                if ((c == '@' || c == '$') && index + 1 < expression.Length &&
                    (expression[index + 1] == '"' || expression[index + 1] == '@' || expression[index + 1] == '$'))
                {
                    index++;
                    continue;
                }

                if (c == '"')
                {
                    CollectStringFragments(expression, ref index, baseOffset, literals, group);
                    continue;
                }

                index++;
            }
        }

        /// <summary>
        /// Resolves a <c>class="@Member"</c> reference against a declaration in the same file and
        /// returns the references no declaration in that file could satisfy.
        /// </summary>
        public static IReadOnlyList<RazorClassReference> ResolveReferences(
            string text, List<RazorClassReference> references, List<RazorClassLiteral> literals)
        {
            if (references.Count == 0)
                return [];

            var unresolved = new List<RazorClassReference>();
            var reported = new HashSet<string>(StringComparer.Ordinal);
            var group = 2_500_000;
            foreach (var reference in references)
            {
                if (!TryFindMemberExpression(text, reference.Name, out var start, out var end))
                {
                    if (reported.Add(reference.Name))
                        unresolved.Add(reference);

                    continue;
                }

                group++;
                CollectExpressionLiterals(text.Substring(start, end - start), start, literals, group);
            }

            return unresolved;
        }

        /// <summary>Finds <c>name = …</c> or <c>name => …</c> and returns the initializer span.</summary>
        public static bool TryFindMemberExpression(string text, string name, out int start, out int end)
        {
            start = 0;
            end = 0;
            var index = 0;
            while (index < text.Length)
            {
                var at = text.IndexOf(name, index, StringComparison.Ordinal);
                if (at < 0)
                    return false;

                index = at + name.Length;
                if (at > 0 && IsIdentifierChar(text[at - 1]))
                    continue;

                if (index < text.Length && IsIdentifierChar(text[index]))
                    continue;

                var cursor = SkipWhitespace(text, index);
                if (cursor >= text.Length)
                    continue;

                if (text[cursor] != '=')
                    continue;

                if (cursor + 1 < text.Length && text[cursor + 1] == '>')
                {
                    cursor = SkipWhitespace(text, cursor + 2);
                    start = cursor;
                    end = FindStatementEnd(text, cursor);
                    return end > start;
                }

                cursor = SkipWhitespace(text, cursor + 1);
                if (cursor >= text.Length || text[cursor] == '=')
                    continue;

                start = cursor;
                end = FindStatementEnd(text, cursor);
                return end > start;
            }

            return false;
        }

        /// <summary>Collects the literal parts of one string, skipping interpolation holes.</summary>
        public static void CollectStringFragments(
            string text, ref int index, int baseOffset, List<RazorClassLiteral> literals, int group)
        {
            var start = index;
            var quotes = 1;
            while (start + quotes < text.Length && text[start + quotes] == '"')
                quotes++;

            if (quotes >= 3)
            {
                var contentStart = start + quotes;
                var close = text.IndexOf(new string('"', quotes), contentStart, StringComparison.Ordinal);
                if (close < 0)
                {
                    index = text.Length;
                    return;
                }

                AddClassTokens(literals, text.Substring(contentStart, close - contentStart), baseOffset + contentStart, group);
                index = close + quotes;
                return;
            }

            var cursor = start + 1;
            var fragmentStart = cursor;
            while (cursor < text.Length)
            {
                var c = text[cursor];
                if (c == '\\' && cursor + 1 < text.Length)
                {
                    cursor += 2;
                    continue;
                }

                if (c == '{')
                {
                    if (cursor + 1 < text.Length && text[cursor + 1] == '{')
                    {
                        cursor += 2;
                        continue;
                    }

                    if (cursor > fragmentStart)
                        AddClassTokens(literals, text.Substring(fragmentStart, cursor - fragmentStart), baseOffset + fragmentStart, group);

                    var holeEnd = SkipInterpolationHole(text, cursor);
                    if (holeEnd > cursor + 1)
                        CollectExpressionLiterals(
                            text.Substring(cursor + 1, holeEnd - cursor - 1), baseOffset + cursor + 1, literals, group);

                    cursor = holeEnd + 1;
                    fragmentStart = cursor;
                    continue;
                }

                if (c == '"')
                    break;

                cursor++;
            }

            if (cursor > fragmentStart)
                AddClassTokens(literals, text.Substring(fragmentStart, cursor - fragmentStart), baseOffset + fragmentStart, group);

            index = Math.Min(cursor + 1, text.Length);
        }

        public static void AddClassTokens(List<RazorClassLiteral> literals, string value, int offset, int group)
        {
            var index = 0;
            while (index < value.Length)
            {
                while (index < value.Length && char.IsWhiteSpace(value[index]))
                    index++;

                var start = index;
                while (index < value.Length && !char.IsWhiteSpace(value[index]))
                    index++;

                if (index > start)
                    literals.Add(new RazorClassLiteral(value.Substring(start, index - start), offset + start, group));
            }
        }

        private static int SkipInterpolationHole(string text, int openBrace)
        {
            var depth = 0;
            for (var i = openBrace; i < text.Length; i++)
            {
                var c = text[i];
                if (c is '"' or '\'')
                {
                    i = SkipStringLiteral(text, i) - 1;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return text.Length - 1;
        }

        private static int SkipStringLiteral(string text, int start)
        {
            if (text[start] == '\'')
                return SkipCharLiteral(text, start);

            var cursor = start + 1;
            while (cursor < text.Length)
            {
                if (text[cursor] == '\\')
                {
                    cursor += 2;
                    continue;
                }

                if (text[cursor] == '"')
                    return cursor + 1;

                cursor++;
            }

            return text.Length;
        }

        private static int SkipCharLiteral(string text, int start)
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
                    return cursor + 1;

                cursor++;
            }

            return text.Length;
        }

        private static int FindClosingParenthesis(string text, int openParen)
        {
            var depth = 0;
            for (var i = openParen; i < text.Length; i++)
            {
                var c = text[i];
                if (c is '"' or '\'')
                {
                    i = SkipStringLiteral(text, i) - 1;
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
                        return i;
                }
            }

            return -1;
        }

        private static int FindStatementEnd(string text, int start)
        {
            var depth = 0;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"' || c == '\'')
                {
                    i = SkipStringLiteral(text, i) - 1;
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
                else if (c == ';' && depth <= 0)
                {
                    return i;
                }
            }

            return text.Length;
        }

        private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.';

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            return index;
        }
    }
}
