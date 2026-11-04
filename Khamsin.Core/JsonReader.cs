using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Khamsin;

/// <summary>
/// A parsed JSON value. Objects keep insertion order so configuration errors can be reported
/// deterministically and duplicate keys are detectable.
/// </summary>
internal sealed class JsonValue
{
    internal JsonValueKind Kind { get; }
    internal string? Text { get; }
    internal List<KeyValuePair<string, JsonValue>>? Members { get; }
    internal List<JsonValue>? Items { get; }

    private JsonValue(JsonValueKind kind, string? text, List<KeyValuePair<string, JsonValue>>? members, List<JsonValue>? items)
    {
        Kind = kind;
        Text = text;
        Members = members;
        Items = items;
    }

    internal static JsonValue String(string value) => new(JsonValueKind.String, value, null, null);
    internal static JsonValue Number(string value) => new(JsonValueKind.Number, value, null, null);
    internal static JsonValue Literal(JsonValueKind kind) => new(kind, null, null, null);
    internal static JsonValue Object(List<KeyValuePair<string, JsonValue>> members) => new(JsonValueKind.Object, null, members, null);
    internal static JsonValue Array(List<JsonValue> items) => new(JsonValueKind.Array, null, null, items);

    public override string ToString() => Kind switch
    {
        JsonValueKind.String => Text ?? string.Empty,
        JsonValueKind.Number => Text ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => base.ToString() ?? string.Empty,
    };
}

internal enum JsonValueKind
{
    String,
    Number,
    True,
    False,
    Null,
    Object,
    Array,
}

/// <summary>
/// Strict minimal JSON parser for the configuration file. Hand-rolled so Khamsin.Core keeps zero
/// package dependencies on netstandard2.0; the grammar implemented is RFC 8259 minus comments and
/// trailing commas, which are rejected with their position.
/// </summary>
internal static class JsonReader
{
    internal static bool TryParse(string text, out JsonValue value, out string? error)
    {
        value = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "The configuration file is empty.";
            return false;
        }

        var reader = new Parser(text);
        var parsed = reader.ParseValue();
        if (parsed is null)
        {
            error = reader.Error;
            return false;
        }

        reader.SkipWhitespace();
        if (reader.Position < text.Length)
        {
            error = reader.Describe("unexpected content after the JSON document");
            return false;
        }

        value = parsed;
        return true;
    }

    private sealed class Parser
    {
        private readonly string _text;

        internal Parser(string text) => _text = text;

        internal int Position { get; private set; }

        internal string? Error { get; private set; }

        internal string Describe(string message)
        {
            var (line, column) = LinePosition(Position);
            return $"Line {line}, column {column}: {message}.";
        }

        internal (int Line, int Column) LinePosition(int position)
        {
            var line = 1;
            var column = 1;
            for (var i = 0; i < position && i < _text.Length; i++)
            {
                if (_text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return (line, column);
        }

        internal void SkipWhitespace()
        {
            while (Position < _text.Length && char.IsWhiteSpace(_text[Position])) Position++;
        }

        internal JsonValue? ParseValue()
        {
            SkipWhitespace();
            if (Position >= _text.Length)
            {
                Error = Describe("the JSON document ended before a value was complete");
                return null;
            }

            switch (_text[Position])
            {
                case '{':
                    return ParseObject();
                case '[':
                    return ParseArray();
                case '"':
                    var parsed = ParseString();
                    return parsed is null ? null : JsonValue.String(parsed);
                case 't':
                    return ParseWord("true", JsonValueKind.True);
                case 'f':
                    return ParseWord("false", JsonValueKind.False);
                case 'n':
                    return ParseWord("null", JsonValueKind.Null);
                case var c when c == '-' || (c >= '0' && c <= '9'):
                    return ParseNumber();
                default:
                    Error = Describe($"unexpected character '{_text[Position]}'");
                    return null;
            }
        }

        private JsonValue? ParseObject()
        {
            Position++; // '{'
            var members = new List<KeyValuePair<string, JsonValue>>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            SkipWhitespace();
            if (Peek() == '}')
            {
                Position++;
                return JsonValue.Object(members);
            }

            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"')
                {
                    Error = Describe(Peek() < 0 ? "expected a property name before the end of the file" : "expected a property name");
                    return null;
                }

                var name = ParseString();
                if (name is null) return null;

                SkipWhitespace();
                if (Peek() != ':')
                {
                    Error = Describe("expected ':' after a property name");
                    return null;
                }

                Position++;
                var value = ParseValue();
                if (value is null) return null;

                if (!seen.Add(name))
                {
                    var (line, column) = LinePosition(Position);
                    Error = $"Line {line}, column {column}: duplicate property '{name}'.";
                    return null;
                }

                members.Add(new KeyValuePair<string, JsonValue>(name, value));
                SkipWhitespace();
                var next = Peek();
                if (next == ',')
                {
                    Position++;
                    continue;
                }

                if (next == '}')
                {
                    Position++;
                    return JsonValue.Object(members);
                }

                Error = Describe("expected ',' or '}' in an object");
                return null;
            }
        }

        private JsonValue? ParseArray()
        {
            Position++; // '['
            var items = new List<JsonValue>();
            SkipWhitespace();
            if (Peek() == ']')
            {
                Position++;
                return JsonValue.Array(items);
            }

            while (true)
            {
                var value = ParseValue();
                if (value is null) return null;

                items.Add(value);
                SkipWhitespace();
                var next = Peek();
                if (next == ',')
                {
                    Position++;
                    continue;
                }

                if (next == ']')
                {
                    Position++;
                    return JsonValue.Array(items);
                }

                Error = Describe("expected ',' or ']' in an array");
                return null;
            }
        }

        private string? ParseString()
        {
            Position++; // opening quote
            var builder = new StringBuilder();
            while (Position < _text.Length)
            {
                var c = _text[Position];
                if (c == '"')
                {
                    Position++;
                    return builder.ToString();
                }

                if (c == '\\')
                {
                    Position++;
                    if (Position >= _text.Length)
                    {
                        Error = Describe("unterminated escape sequence");
                        return null;
                    }

                    var escape = _text[Position];
                    switch (escape)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escape);
                            break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (Position + 4 >= _text.Length ||
                                !ushort.TryParse(_text.Substring(Position + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            {
                                Error = Describe("invalid unicode escape sequence");
                                return null;
                            }

                            builder.Append((char)code);
                            Position += 4;
                            break;
                        default:
                            Error = Describe($"invalid escape sequence '\\{escape}'");
                            return null;
                    }

                    Position++;
                    continue;
                }

                if (c < ' ')
                {
                    Error = Describe("control character inside a string");
                    return null;
                }

                builder.Append(c);
                Position++;
            }

            Error = Describe("unterminated string");
            return null;
        }

        private JsonValue? ParseNumber()
        {
            var start = Position;
            if (Peek() == '-') Position++;

            while (Position < _text.Length && char.IsDigit(_text[Position])) Position++;

            if (Peek() == '.')
            {
                Position++;
                while (Position < _text.Length && char.IsDigit(_text[Position])) Position++;
            }

            if (Peek() is 'e' or 'E')
            {
                Position++;
                if (Peek() is '+' or '-') Position++;
                while (Position < _text.Length && char.IsDigit(_text[Position])) Position++;
            }

            var text = _text.Substring(start, Position - start);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                Error = Describe($"invalid number '{text}'");
                return null;
            }

            return JsonValue.Number(text);
        }

        private JsonValue? ParseWord(string word, JsonValueKind kind)
        {
            if (Position + word.Length > _text.Length || string.CompareOrdinal(_text, Position, word, 0, word.Length) != 0)
            {
                Error = Describe($"unexpected character '{_text[Position]}'");
                return null;
            }

            Position += word.Length;
            return JsonValue.Literal(kind);
        }

        private int Peek() => Position < _text.Length ? _text[Position] : -1;
    }
}
