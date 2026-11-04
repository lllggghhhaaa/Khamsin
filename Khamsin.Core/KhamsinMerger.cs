using System.Collections.Concurrent;
using System.Text;

namespace Khamsin;

/// <summary>
/// Runtime class merge. Later classes win inside the same conflict group and variant set; a group
/// shadows its descendants (for example <c>p-4</c> removes <c>px-2</c>, but <c>px-2</c> does not
/// remove an earlier <c>p-4</c>). The method runs on every component render, so results are cached
/// and tokenization avoids LINQ.
/// </summary>
public static class KhamsinMerger
{
    private const int MaxCacheEntries = 4096;

    private static readonly ConcurrentDictionary<MergeKey, string> Cache = new();

    private static KhamsinEngine? _configured;

    /// <summary>
    /// Registers a configuration-aware engine so merging resolves conflict groups for custom theme
    /// tokens (for example <c>bg-primary</c>). The first call wins: libraries generated against
    /// their own configuration must not fight the application's registration, so generated
    /// registrations only run in the entry assembly.
    /// </summary>
    public static void Configure(KhamsinConfig config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config));

        if (Interlocked.CompareExchange(ref _configured, new KhamsinEngine(config.ToTheme()), null) != null)
            return;

        Cache.Clear();
    }

    public static string Merge(string? defaults, string? overrides)
    {
        var lower = defaults ?? string.Empty;
        var upper = overrides ?? string.Empty;
        if (lower.Length == 0 && upper.Length == 0)
            return string.Empty;

        var key = new MergeKey(lower, upper);
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var merged = MergeCore(lower, upper);
        if (Cache.Count >= MaxCacheEntries) Cache.Clear();

        Cache[key] = merged;
        return merged;
    }

    public static string Merge(IReadOnlyList<string?> classLists) =>
        classLists == null ? throw new ArgumentNullException(nameof(classLists)) : classLists.Aggregate(string.Empty, Merge);

    private static string MergeCore(string defaults, string overrides)
    {
        var engine = _configured ?? KhamsinEngine.Default;
        var tokens = new List<string>();
        var parsed = new List<Entry?>();

        AppendTokens(engine, defaults, tokens, parsed);
        AppendTokens(engine, overrides, tokens, parsed);

        var removed = new bool[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            var current = parsed[i];
            if (current is null)
                continue;

            for (var j = 0; j < i; j++)
            {
                var previous = parsed[j];
                if (previous is null || removed[j])
                    continue;

                if (!string.Equals(previous.Value.VariantKey, current.Value.VariantKey, StringComparison.Ordinal))
                    continue;

                if (GroupsConflict(previous.Value.Group, current.Value.Group)) removed[j] = true;
            }
        }

        var builder = new StringBuilder(defaults.Length + overrides.Length + 1);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (removed[i])
                continue;

            if (builder.Length > 0) builder.Append(' ');

            builder.Append(tokens[i]);
        }

        return builder.ToString();
    }

    private static void AppendTokens(KhamsinEngine engine, string classList, List<string> tokens, List<Entry?> parsed)
    {
        foreach (var token in KhamsinEngine.Tokenize(classList))
        {
            tokens.Add(token);
            if (engine.TryParse(token, out var parsedClass, out _))
                parsed.Add(new Entry(parsedClass.Entry.Group, ClassParser.BuildVariantKey(parsedClass)));
            else
                parsed.Add(null);
        }
    }

    /// <summary>An existing group is replaced when it is the incoming group or one of its descendants.</summary>
    private static bool GroupsConflict(string existing, string incoming)
    {
        if (string.Equals(existing, incoming, StringComparison.Ordinal))
            return true;

        return existing.Length > incoming.Length &&
               existing[incoming.Length] == '/' &&
               existing.StartsWith(incoming, StringComparison.Ordinal);
    }

    private readonly record struct Entry(string Group, string VariantKey);

    private readonly struct MergeKey : IEquatable<MergeKey>
    {
        private readonly string _defaults;
        private readonly string _overrides;
        private readonly int _hash;

        public MergeKey(string defaults, string overrides)
        {
            _defaults = defaults;
            _overrides = overrides;
            var hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(defaults);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(overrides);
            _hash = hash;
        }

        public bool Equals(MergeKey other)
            => string.Equals(_defaults, other._defaults, StringComparison.Ordinal) &&
               string.Equals(_overrides, other._overrides, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is MergeKey other && Equals(other);

        public override int GetHashCode() => _hash;
    }
}
