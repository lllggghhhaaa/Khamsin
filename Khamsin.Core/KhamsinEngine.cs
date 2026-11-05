namespace Khamsin;

/// <summary>Result of turning a class list into CSS, including anything the registry rejected.</summary>
public sealed class CssGenerationResult
{
    internal CssGenerationResult(string css, IReadOnlyList<string> unknownClasses)
    {
        Css = css;
        UnknownClasses = unknownClasses;
    }

    public string Css { get; }

    /// <summary>Tokens that are not part of the closed class set. Callers turn these into diagnostics.</summary>
    public IReadOnlyList<string> UnknownClasses { get; }
}

/// <summary>
/// The public engine facade: a theme, its registry and the emitter. Build-time generation and
/// runtime merging both go through the same registry instance shape.
/// </summary>
public sealed class KhamsinEngine(Theme theme)
{
    private static readonly Lazy<KhamsinEngine> LazyDefault = new(() => new KhamsinEngine(Theme.Default));

    public static KhamsinEngine Default => LazyDefault.Value;

    public Theme Theme { get; } = theme ?? throw new ArgumentNullException(nameof(theme));

    public UtilityRegistry Registry { get; } = new(theme);

    public bool TryParse(string raw, out ParsedClass parsed, out ClassParseError? error)
        => Registry.TryParse(raw, out parsed, out error);

    public CssGenerationResult Generate(IEnumerable<string> classNames, bool minify = true, string customCss = "")
    {
        if (classNames is null)
            throw new ArgumentNullException(nameof(classNames));

        var parsed = new List<ParsedClass>();
        var unknown = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in classNames)
        {
            foreach (var token in Tokenize(entry))
            {
                if (!seen.Add(token))
                    continue;

                if (TryParse(token, out var parsedClass, out _))
                    parsed.Add(parsedClass);
                else
                    unknown.Add(token);
            }
        }

        return new CssGenerationResult(CssEmitter.Emit(parsed, Theme, minify: minify, customCss: customCss), unknown);
    }

    public string GenerateCss(IEnumerable<string> classNames, bool minify = true) => Generate(classNames, minify).Css;

    public static IEnumerable<string> Tokenize(string? classList)
    {
        if (string.IsNullOrEmpty(classList))
            yield break;

        var value = classList!;
        var start = -1;
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                if (start < 0) continue;
                
                yield return value.Substring(start, i - start);
                start = -1;
            }
            else if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
            yield return value.Substring(start);
    }
}
