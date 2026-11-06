namespace Khamsin.Build;

internal sealed record ManifestInfo(string Classes, List<string> Uses);

/// <summary>A configuration document embedded in an assembly by the source generator.</summary>
internal sealed record ConfigInfo(string AssemblyName, string Json, string Css = "");

internal sealed class AssemblyScanResult
{
    /// <summary>Simple assembly name, used to group assembly-level manifests under the same owner.</summary>
    public string AssemblyName { get; set; } = string.Empty;

    public Dictionary<string, ManifestInfo> Manifests { get; } = new(StringComparer.Ordinal);

    public List<ManifestInfo> AssemblyManifests { get; } = [];

    public List<ConfigInfo> Configs { get; } = [];

    public HashSet<string> UsedComponents { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Component usages discovered in each type's IL, keyed by metadata type name. This recovers
    /// transitive edges for generic components (and older libraries) whose manifests omit <c>Uses</c>.
    /// </summary>
    public Dictionary<string, HashSet<string>> TypeUsages { get; } = new(StringComparer.Ordinal);

    /// <summary>Custom CSS content from the assembly's own khamsin.json <c>css</c> property.</summary>
    public string OwnCss { get; set; } = string.Empty;
}
