namespace Khamsin;

/// <summary>
/// The utility table shared by the build-time emitter and the runtime merger. Lookup tries the
/// longest matching prefix first, then each entry declared for that prefix, so one prefix can host
/// several shapes (for example <c>text-sm</c>, <c>text-center</c> and <c>text-ocean-600</c>).
/// </summary>
public sealed class UtilityRegistry
{
    private readonly Theme _theme;
    private readonly Dictionary<string, List<UtilityEntry>> _byPrefix;
    private readonly string[] _prefixes;

    public UtilityRegistry(Theme theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        Variants = VariantRegistry.Create(theme);

        var entries = BuiltInUtilities.Create(theme);
        _byPrefix = new Dictionary<string, List<UtilityEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!_byPrefix.TryGetValue(entry.Prefix, out var list))
            {
                list = new List<UtilityEntry>();
                _byPrefix.Add(entry.Prefix, list);
            }

            list.Add(entry);
        }

        _prefixes = new string[_byPrefix.Count];
        _byPrefix.Keys.CopyTo(_prefixes, 0);
        Array.Sort(_prefixes, static (left, right) =>
        {
            var byLength = right.Length.CompareTo(left.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(left, right);
        });
    }

    public static UtilityRegistry Default => DefaultRegistry.Instance;

    public Theme Theme => _theme;

    public VariantRegistry Variants { get; }

    public IEnumerable<UtilityEntry> Entries => _byPrefix.Values.SelectMany(list => list);

    public bool TryParse(string raw, out ParsedClass parsed, out ClassParseError? error)
        => ClassParser.TryParse(raw, this, out parsed, out error);

    public bool IsKnown(string raw) => TryParse(raw, out _, out _);

    internal bool TryResolveBody(string body, bool negative, out UtilityEntry entry, out BoundValue bound)
    {
        entry = null!;
        bound = default;

        foreach (var prefix in _prefixes)
        {
            if (body.Length < prefix.Length)
                continue;

            string remainder;
            if (body.Length == prefix.Length)
            {
                if (!string.Equals(body, prefix, StringComparison.Ordinal))
                    continue;

                remainder = string.Empty;
            }
            else
            {
                // A prefix ending in '/' already carries its separator (for example "@container/");
                // every other prefix is followed by the conventional dash.
                if (!body.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (prefix[prefix.Length - 1] == '/')
                {
                    remainder = body.Substring(prefix.Length);
                }
                else
                {
                    if (body[prefix.Length] != '-')
                        continue;

                    remainder = body.Substring(prefix.Length + 1);
                }
            }

            var candidates = _byPrefix[prefix];
            foreach (var candidate in candidates)
            {
                if (!candidate.TryBind(remainder, negative, _theme, out var candidateBound))
                    continue;

                entry = candidate;
                bound = candidateBound;
                return true;
            }
        }

        return false;
    }

    internal bool TryResolveBody(string body, out UtilityEntry entry, out BoundValue bound)
        => TryResolveBody(body, false, out entry, out bound);

    private static class DefaultRegistry
    {
        internal static readonly UtilityRegistry Instance = new(Theme.Default);
    }
}
