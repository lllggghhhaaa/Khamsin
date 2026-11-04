namespace Khamsin.Core.Tests;

/// <summary>
/// Property tests with a fixed seed so failures are reproducible. Each test states an invariant
/// that must hold for every generated input, rather than checking one example.
/// </summary>
public class PropertyBasedTests
{
    private static readonly string[] Pool =
    {
        "p-4", "px-2", "py-3", "m-1", "mx-auto", "w-4", "h-6", "size-8",
        "bg-ocean-600", "hover:bg-ember-500", "text-sm", "font-bold", "rounded-lg",
        "border-2", "shadow-md", "opacity-50", "md:flex", "dark:bg-ink-900",
        "gap-2", "z-10", "cursor-pointer", "transition", "duration-200",
    };

    [Fact]
    public void Parser_round_trips_raw_token()
    {
        var random = new Random(1337);
        for (var i = 0; i < 200; i++)
        {
            var token = Pool[random.Next(Pool.Length)];
            Assert.True(KhamsinEngine.Default.TryParse(token, out var parsed, out var error), error?.Message);
            Assert.Equal(token, parsed.Raw);
        }
    }

    [Fact]
    public void Emitter_is_permutation_invariant()
    {
        var random = new Random(4242);
        for (var i = 0; i < 50; i++)
        {
            var classes = Pick(random, 8);
            var shuffled = classes.ToArray();
            Shuffle(random, shuffled);

            Assert.Equal(Generate(classes), Generate(shuffled));
        }
    }

    [Fact]
    public void Merge_is_idempotent_and_keeps_last_conflict()
    {
        var random = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            var defaults = string.Join(" ", Pick(random, 4));
            var overrides = string.Join(" ", Pick(random, 4));

            var merged = KhamsinMerger.Merge(defaults, overrides);
            Assert.Equal(merged, KhamsinMerger.Merge(merged, string.Empty));
            Assert.Equal(merged, KhamsinMerger.Merge(merged, merged));

            var mergedTokens = merged.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var inputTokens = (defaults + " " + overrides).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.All(mergedTokens, token => Assert.Contains(token, inputTokens));
        }
    }

    [Fact]
    public void Merge_never_changes_a_single_class_list()
    {
        var random = new Random(99);
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 100; i++)
        {
            var classes = Pick(random, 6);
            var input = string.Join(" ", classes);

            // Keep only sequences with no intra-list conflicts (including parent/child groups):
            // the merge must then be a no-op.
            var parsedClasses = classes
                .Select(c => KhamsinEngine.Default.TryParse(c, out var p, out _) ? p : null)
                .Where(p => p != null)
                .Select(p => p!)
                .ToArray();
            var conflictFree = true;
            for (var a = 0; a < parsedClasses.Length && conflictFree; a++)
            {
                for (var b = a + 1; b < parsedClasses.Length; b++)
                {
                    if (Norm(parsedClasses[a]) != Norm(parsedClasses[b]))
                    {
                        continue;
                    }

                    var left = parsedClasses[a].Entry.Group;
                    var right = parsedClasses[b].Entry.Group;
                    if (left == right || left.StartsWith(right + "/", StringComparison.Ordinal) || right.StartsWith(left + "/", StringComparison.Ordinal))
                    {
                        conflictFree = false;
                        break;
                    }
                }
            }
            if (!conflictFree)
            {
                continue;
            }

            var merged = KhamsinMerger.Merge(input, string.Empty);
            foreach (var token in merged.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                distinct.Add(token);
            }

            Assert.Equal(string.Join(" ", classes), merged);
        }

        Assert.NotEmpty(distinct);
    }

    private static string Norm(ParsedClass parsed)
    {
        var variants = parsed.Variants.OrderBy(v => v, StringComparer.Ordinal);
        return string.Join(":", variants) + (parsed.Important ? "!" : string.Empty);
    }

    private static List<string> Pick(Random random, int count)
    {
        var result = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(Pool[random.Next(Pool.Length)]);
        }

        return result;
    }

    private static void Shuffle(Random random, string[] values)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static string Generate(IEnumerable<string> classes)
    {
        var parsed = new List<ParsedClass>();
        foreach (var token in classes)
        {
            Assert.True(KhamsinEngine.Default.TryParse(token, out var parsedClass, out _));
            parsed.Add(parsedClass);
        }

        return CssEmitter.Emit(parsed, Theme.Default, includeTheme: false);
    }
}
