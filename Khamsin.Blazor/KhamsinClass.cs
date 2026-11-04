namespace Khamsin;

/// <summary>
/// A component parameter that accepts utility classes using Tailwind-compatible syntax. Implicitly
/// convertible from <see cref="string"/>; combining two values merges them with conflict
/// resolution, later winning.
/// </summary>
public readonly struct KhamsinClass : IEquatable<KhamsinClass>
{
    public static readonly KhamsinClass Empty = new(string.Empty);

    private readonly string? _value;

    public KhamsinClass(string? value)
    {
        _value = value;
    }

    /// <summary>Never null, including for <c>default(KhamsinClass)</c>.</summary>
    public string Value => _value ?? string.Empty;

    public bool IsEmpty => Value.Length == 0;

    public static implicit operator KhamsinClass(string? value) => new(value);

    /// <summary>Merges two class lists; <paramref name="right"/> wins on conflict.</summary>
    public static KhamsinClass operator +(KhamsinClass left, KhamsinClass right)
        => new(KhamsinMerger.Merge(left.Value, right.Value));

    /// <summary>Applies consumer overrides to a component's defaults.</summary>
    public static KhamsinClass Merge(KhamsinClass defaults, KhamsinClass overrides) => defaults + overrides;

    public static KhamsinClass Join(params KhamsinClass[] values) 
        => values.Aggregate(Empty, (current, t) => current + t);

    public bool Equals(KhamsinClass other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is KhamsinClass other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(KhamsinClass left, KhamsinClass right) => left.Equals(right);

    public static bool operator !=(KhamsinClass left, KhamsinClass right) => !left.Equals(right);
}
