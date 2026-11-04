namespace Khamsin;

/// <summary>
/// Build-time manifest attached to a component (or to an assembly for helper classes) by the
/// source generator. The MSBuild task reads these attributes from compiled metadata; it never
/// scrapes IL string literals.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class KhamsinManifestAttribute : Attribute
{
    /// <summary>Whitespace-separated utility classes that appear statically in this scope.</summary>
    public string Classes { get; set; } = string.Empty;

    /// <summary>Components this component renders, used for transitive tree-shaking.</summary>
    public Type[] Uses { get; set; } = Type.EmptyTypes;
}
