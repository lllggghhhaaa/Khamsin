using System.Collections.Concurrent;
using System.Text;
using Khamsin.Extraction;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Khamsin.Build;

/// <summary>
/// Emits the pruned stylesheet for one executable or web project. Roots are the components the
/// consumer actually renders (read from metadata), then manifests are followed transitively. The
/// task is incremental twice: MSBuild skips it when declared inputs are unchanged, and the task
/// itself does not rewrite an identical file.
/// </summary>
public sealed class KhamsinEmitCss : Task
{
    private static readonly ConcurrentDictionary<string, CachedScan> Cache = new(StringComparer.OrdinalIgnoreCase);

    [Required]
    public string AssemblyPath { get; set; } = string.Empty;

    public ITaskItem[] ReferenceAssemblies { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Copy-local implementation assemblies. Reference assemblies preserve manifests but strip the
    /// IL bodies that reveal <c>OpenComponent&lt;T&gt;</c> usage, so these are scanned for usage too.
    /// </summary>
    public ITaskItem[] ImplementationAssemblies { get; set; } = Array.Empty<ITaskItem>();

    public ITaskItem[] SourceFiles { get; set; } = Array.Empty<ITaskItem>();

    [Required]
    public string OutputPath { get; set; } = string.Empty;

    public string ProjectDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Path to the configuration file (<c>khamsin.json</c> by convention). When it exists, its
    /// tokens extend the built-in theme; parse errors fail the build.
    /// </summary>
    public string ConfigPath { get; set; } = string.Empty;

    /// <summary>Minified output is the default; readable output exists for debugging a build.</summary>
    public bool Minify { get; set; } = true;

    [Output]
    public string CssFile { get; set; } = string.Empty;

    public override bool Execute()
    {
        try
        {
            ExecuteCore();
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
        }

        return !Log.HasLoggedErrors;
    }

    /// <summary>
    /// Builds the engine from the app's configuration layered over the configurations embedded in
    /// referenced assemblies. Referenced configurations merge in assembly-name order; the app's
    /// configuration is applied last so it always wins.
    /// </summary>
    private KhamsinEngine CreateEngine(AssemblyScanResult consumer, IEnumerable<ConfigInfo> referenced)
    {
        var configurations = new List<KhamsinConfig>();
        foreach (var info in referenced.OrderBy(static info => info.AssemblyName, StringComparer.Ordinal))
            AddConfig(configurations, info);

        if (ConfigPath.Length > 0 && File.Exists(ConfigPath))
        {
            if (!KhamsinConfig.TryLoad(ConfigPath, out var config, out var errors) || config is null)
            {
                foreach (var error in errors)
                    Log.LogError("Khamsin config ({0}): {1}", ConfigPath, error);

                throw new InvalidOperationException("Khamsin: the configuration file is invalid; see the errors above.");
            }

            configurations.Add(config);
        }
        else
        {
            foreach (var info in consumer.Configs.OrderBy(static info => info.AssemblyName, StringComparer.Ordinal))
                AddConfig(configurations, info);
        }

        return configurations.Count == 0
            ? KhamsinEngine.Default
            : new KhamsinEngine(KhamsinConfig.Merge(configurations).ToTheme());
    }

    private void AddConfig(List<KhamsinConfig> configurations, ConfigInfo info)
    {
        if (KhamsinConfig.TryParse(info.Json, out var config, out var errors) && config is not null)
        {
            configurations.Add(config);
            return;
        }

        Log.LogWarning(null, "KHM006", null, null, 0, 0, 0, 0,
            "Khamsin: configuration embedded in referenced assembly '{0}' is invalid and was ignored: {1}",
            info.AssemblyName,
            string.Join(" ", errors));
    }

    private void ExecuteCore()
    {
        CssFile = OutputPath;

        if (!File.Exists(AssemblyPath))
        {
            Log.LogError("Khamsin: compiled assembly '{0}' was not found.", AssemblyPath);
            return;
        }

        var consumer = AssemblyMetadataReader.Scan(AssemblyPath, scanUsage: true);

        // Resolve the app's own CSS from the khamsin.json's css file paths.
        if (ConfigPath.Length > 0 && File.Exists(ConfigPath) &&
            KhamsinConfig.TryLoad(ConfigPath, out var ownConfig, out _) && ownConfig is not null)
        {
            var cssBuilder = new StringBuilder();
            var configDir = Path.GetDirectoryName(ConfigPath) ?? string.Empty;
            foreach (var cssPath in ownConfig.CssFiles ?? [])
            {
                var fullPath = Path.GetFullPath(cssPath, configDir);
                if (File.Exists(fullPath))
                    cssBuilder.AppendLine(File.ReadAllText(fullPath));
            }

            consumer.OwnCss = cssBuilder.ToString();
        }

        var manifests = new Dictionary<string, ManifestInfo>(StringComparer.Ordinal);
        var manifestOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var assemblyManifests = new Dictionary<string, List<ManifestInfo>>(StringComparer.OrdinalIgnoreCase);
        var typeUsages = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var contributingAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencedConfigs = new Dictionary<string, ConfigInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in consumer.TypeUsages)
            AddUsages(typeUsages, pair.Key, pair.Value);

        // Implementation assemblies carry the IL that reference assemblies omit. Scanning them
        // first recovers both manifests and transitive OpenComponent<T> edges from old packages.
        var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(AssemblyPath) };
        var scannedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { consumer.AssemblyName };

        void AddScan(string path, bool scanUsage)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !IsAssemblyFile(path))
                return;

            var fullPath = Path.GetFullPath(path);
            if (!scannedPaths.Add(fullPath))
                return;

            AssemblyScanResult scan;
            try
            {
                scan = ScanCached(fullPath, scanUsage);
            }
            catch (Exception exception) when (exception is BadImageFormatException or IOException or UnauthorizedAccessException)
            {
                return;
            }

            if (scan.AssemblyName.Length == 0 || !scannedAssemblies.Add(scan.AssemblyName))
                return;

            foreach (var manifest in scan.Manifests)
            {
                // First wins: the consumer's own manifests are applied last, and implementation
                // assemblies are scanned before the reference assemblies that mirror them.
                if (manifests.TryAdd(manifest.Key, manifest.Value))
                    manifestOwners[manifest.Key] = scan.AssemblyName;
            }

            foreach (var pair in scan.TypeUsages)
                AddUsages(typeUsages, pair.Key, pair.Value);

            if (scan.AssemblyManifests.Count > 0)
            {
                if (!assemblyManifests.TryGetValue(scan.AssemblyName, out var assembly))
                {
                    assembly = [];
                    assemblyManifests.Add(scan.AssemblyName, assembly);
                }

                assembly.AddRange(scan.AssemblyManifests);
            }

            foreach (var config in scan.Configs)
                referencedConfigs[config.AssemblyName] = config;
        }

        foreach (var implementation in ImplementationAssemblies)
            AddScan(implementation.GetMetadata("FullPath"), scanUsage: true);

        foreach (var reference in ReferenceAssemblies)
            AddScan(reference.GetMetadata("FullPath"), scanUsage: false);

        var engine = CreateEngine(consumer, referencedConfigs.Values);

        foreach (var manifest in consumer.Manifests) manifests[manifest.Key] = manifest.Value;

        var classes = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        foreach (var component in consumer.UsedComponents) queue.Enqueue(component);

        while (queue.Count > 0)
        {
            var typeName = queue.Dequeue();
            if (!visited.Add(typeName))
                continue;

            // Usage edges are recorded per declaring type, including the real generic definition
            // when an older manifest sits on a non-generic stub.
            if (typeUsages.TryGetValue(typeName, out var usages))
                foreach (var use in usages)
                    queue.Enqueue(use);

            var manifestName = typeName;
            if (!manifests.TryGetValue(typeName, out var manifest))
            {
                manifestName = TypeNames.StripGenericArity(typeName);
                if (manifestName == typeName || !manifests.TryGetValue(manifestName, out manifest))
                    continue;

                if (typeUsages.TryGetValue(manifestName, out var stubUsages))
                    foreach (var use in stubUsages)
                        queue.Enqueue(use);
            }

            if (manifestOwners.TryGetValue(manifestName, out var owner)) contributingAssemblies.Add(owner);

            AddClasses(classes, manifest.Classes);
            foreach (var use in manifest.Uses.Where(use => !visited.Contains(use))) queue.Enqueue(use);
        }

        foreach (var assembly in contributingAssemblies)
            if (assemblyManifests.TryGetValue(assembly, out var assemblyManifestList))
                foreach (var manifest in assemblyManifestList)
                    AddClasses(classes, manifest.Classes);

        foreach (var manifest in consumer.AssemblyManifests) 
            AddClasses(classes, manifest.Classes);

        foreach (var manifest in consumer.Manifests.Values) 
            AddClasses(classes, manifest.Classes);

        foreach (var file in SourceFiles)
        {
            var path = file.GetMetadata("FullPath");
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || IsIntermediate(path))
                continue;

            var text = File.ReadAllText(path);
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase))
            {
                var scan = RazorSourceScanner.Scan(text);
                foreach (var literal in scan.Literals) AddClasses(classes, literal.Value);

                foreach (var literal in scan.AuthorLiterals)
                    if (engine.TryParse(literal.Value, out _, out _)) AddClasses(classes, literal.Value);
            }
            else if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var literal in CSharpSourceScanner.FindKxLiterals(text)) AddClasses(classes, literal.Value);
                foreach (var literal in CSharpSourceScanner.FindKhamsinClassLiterals(text)) AddClasses(classes, literal.Value);
            }
        }

        var customCss = CollectCustomCss(consumer, referencedConfigs.Values);
        var result = engine.Generate(classes, Minify, customCss);
        foreach (var unknown in result.UnknownClasses) 
            Log.LogWarning(null, "KHM002", null, null, 0, 0, 0, 0, "Khamsin: '{0}' is not a known utility class and was not emitted.", unknown);

        Write(result.Css);
        Log.LogMessage(MessageImportance.Low, "Khamsin: {0} classes -> {1} bytes at '{2}'.", classes.Count, result.Css.Length, OutputPath);
    }

    /// <summary>Collects custom CSS from the app config and referenced library configs.</summary>
    private static string CollectCustomCss(
        AssemblyScanResult consumer, IEnumerable<ConfigInfo> referencedConfigs)
    {
        var builder = new StringBuilder();
        foreach (var config in referencedConfigs)
            if (!string.IsNullOrWhiteSpace(config.Css))
                builder.AppendLine(config.Css);

        if (!string.IsNullOrWhiteSpace(consumer.OwnCss))
            builder.AppendLine(consumer.OwnCss);

        return builder.ToString();
    }

    private void Write(string css)
    {
        var directory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        if (File.Exists(OutputPath))
        {
            var existing = File.ReadAllText(OutputPath, Encoding.UTF8);
            if (string.Equals(existing, css, StringComparison.Ordinal))
            {
                File.SetLastWriteTimeUtc(OutputPath, DateTime.UtcNow);
                Log.LogMessage(MessageImportance.Low, "Khamsin: stylesheet is up to date.");
                return;
            }
        }

        File.WriteAllText(OutputPath, css, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static AssemblyScanResult ScanCached(string path, bool scanUsage)
    {
        var info = new FileInfo(path);
        var stamp = info.LastWriteTimeUtc.Ticks;
        var length = info.Length;
        var key = (scanUsage ? "usage:" : "manifest:") + Path.GetFullPath(path);

        if (Cache.TryGetValue(key, out var cached) && cached.Stamp == stamp && cached.Length == length)
            return cached.Scan;

        var scan = AssemblyMetadataReader.Scan(path, scanUsage);
        Cache[key] = new CachedScan(stamp, length, scan);
        return scan;
    }

    private static void AddUsages(Dictionary<string, HashSet<string>> target, string typeName, HashSet<string> usages)
    {
        if (target.TryGetValue(typeName, out var existing))
        {
            existing.UnionWith(usages);
            return;
        }

        target.Add(typeName, new HashSet<string>(usages, StringComparer.Ordinal));
    }

    private static bool IsAssemblyFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddClasses(List<string> classes, string classList)
    {
        foreach (var token in KhamsinEngine.Tokenize(classList))
            if (!classes.Contains(token)) classes.Add(token);
    }

    private static bool IsIntermediate(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CachedScan(long Stamp, long Length, AssemblyScanResult Scan);
}
