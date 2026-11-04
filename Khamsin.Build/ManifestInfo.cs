namespace Khamsin.Build;

internal sealed class ManifestInfo
{
    public ManifestInfo(string classes, List<string> uses)
    {
        Classes = classes;
        Uses = uses;
    }

    public string Classes { get; }

    public List<string> Uses { get; }
}

internal sealed class AssemblyScanResult
{
    public AssemblyScanResult()
    {
        Manifests = new Dictionary<string, ManifestInfo>(StringComparer.Ordinal);
        AssemblyManifests = new List<ManifestInfo>();
        UsedComponents = new HashSet<string>(StringComparer.Ordinal);
    }

    public Dictionary<string, ManifestInfo> Manifests { get; }

    public List<ManifestInfo> AssemblyManifests { get; }

    public HashSet<string> UsedComponents { get; }
}
