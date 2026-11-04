namespace Khamsin;

/// <summary>
/// Literal-based helpers. Every class passed here is a compile-time literal, so the build can
/// extract it; only the selection happens at runtime.
/// </summary>
public static class Kx
{
    /// <summary>Returns <paramref name="whenTrue"/> or <paramref name="whenFalse"/>.</summary>
    public static KhamsinClass When(bool condition, string whenTrue, string whenFalse = "")
        => new(condition ? whenTrue : whenFalse);

    /// <summary>
    /// Selects the classes attached to <paramref name="value"/>. The value may be non-literal; the
    /// classes may not.
    /// </summary>
    public static KhamsinClass Variant<T>(T value, params (T Value, string Classes)[]? options)
    {
        if (options is null)
            return KhamsinClass.Empty;

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < options.Length; i++)
            if (comparer.Equals(value, options[i].Value))
                return new KhamsinClass(options[i].Classes);

        return KhamsinClass.Empty;
    }
}
