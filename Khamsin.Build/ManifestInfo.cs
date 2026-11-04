namespace Khamsin.Build;

internal sealed record ManifestInfo(string Classes, List<string> Uses);

/// <summary>A configuration document embedded in an assembly by the source generator.</summary>
internal sealed record ConfigInfo(string AssemblyName, string Json);

internal sealed class AssemblyScanResult
{
    public Dictionary<string, ManifestInfo> Manifests { get; } = new(StringComparer.Ordinal);

    public List<ManifestInfo> AssemblyManifests { get; } = [];

    public List<ConfigInfo> Configs { get; } = [];

    public HashSet<string> UsedComponents { get; } = new(StringComparer.Ordinal);
}
