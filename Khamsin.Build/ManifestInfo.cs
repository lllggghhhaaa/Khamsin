namespace Khamsin.Build;

internal sealed record ManifestInfo(string Classes, List<string> Uses);

/// <summary>A configuration document embedded in an assembly by the source generator.</summary>
internal sealed record ConfigInfo(string AssemblyName, string Json, string Css = "");

internal sealed class AssemblyScanResult
{
    public Dictionary<string, ManifestInfo> Manifests { get; } = new(StringComparer.Ordinal);

    public List<ManifestInfo> AssemblyManifests { get; } = [];

    public List<ConfigInfo> Configs { get; } = [];

    public HashSet<string> UsedComponents { get; } = new(StringComparer.Ordinal);

    /// <summary>Custom CSS content from the assembly's own khamsin.json <c>css</c> property.</summary>
    public string OwnCss { get; set; } = string.Empty;
}
