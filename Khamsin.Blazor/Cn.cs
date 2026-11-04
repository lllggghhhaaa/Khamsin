using System.Text;

namespace Khamsin;

/// <summary>
/// A clsx-style class composer, the Khamsin counterpart of React's <c>cn()</c>. Accepts strings,
/// <see cref="KhamsinClass"/> values, booleans (ignored), condition tuples and nested collections;
/// filters to the truthy parts and resolves conflicts last-wins through
/// <see cref="KhamsinMerger"/>. Every string that can render must still be a literal at the call
/// site (or come from a <c>KhamsinClass</c> whose initializer was a literal) so the build can
/// extract it; the analyzer reports dynamic strings as KHM001.
/// </summary>
public static class Cn
{
    /// <summary>Composes the inputs into one conflict-resolved class value.</summary>
    public static KhamsinClass Of(params object?[]? inputs) => Merge(KhamsinClass.Empty, inputs);

    /// <summary>Applies the composed inputs over component defaults, then resolves conflicts.</summary>
    public static KhamsinClass Merge(KhamsinClass defaults, params object?[]? inputs)
    {
        var builder = new StringBuilder(defaults.Value);
        AppendInputs(builder, inputs);
        return new KhamsinClass(KhamsinMerger.Merge(builder.ToString(), string.Empty));
    }

    private static void AppendInputs(StringBuilder builder, object?[]? inputs)
    {
        if (inputs is null)
            return;

        foreach (var input in inputs)
            AppendValue(builder, input);
    }

    private static void AppendValue(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
            case bool:
                break;

            case string text:
                AppendToken(builder, text);
                break;

            case KhamsinClass cls:
                AppendToken(builder, cls.Value);
                break;

            case ValueTuple<bool, string> pair:
                if (pair.Item1)
                    AppendToken(builder, pair.Item2);
                break;

            case ValueTuple<bool, string, string> triple:
                AppendToken(builder, triple.Item1 ? triple.Item2 : triple.Item3);
                break;

            case ValueTuple<bool, KhamsinClass> classPair:
                if (classPair.Item1)
                    AppendToken(builder, classPair.Item2.Value);
                break;

            case ValueTuple<bool, KhamsinClass, KhamsinClass> classTriple:
                AppendToken(builder, (classTriple.Item1 ? classTriple.Item2 : classTriple.Item3).Value);
                break;

            case System.Collections.IEnumerable enumerable:
                foreach (var item in enumerable)
                    AppendValue(builder, item);
                break;
        }
    }

    private static void AppendToken(StringBuilder builder, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return;

        if (builder.Length > 0 && builder[^1] != ' ')
            builder.Append(' ');

        builder.Append(trimmed);
    }
}
