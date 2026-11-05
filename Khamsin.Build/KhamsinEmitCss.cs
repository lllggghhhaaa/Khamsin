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
        var assemblyManifests = new Dictionary<string, List<ManifestInfo>>(StringComparer.OrdinalIgnoreCase);
        var contributingAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifestOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var referencedConfigs = new Dictionary<string, ConfigInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in ReferenceAssemblies)
        {
            var path = reference.GetMetadata("FullPath");
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || PathsEqual(path, AssemblyPath))
                continue;

            AssemblyScanResult scan;
            try
            {
                scan = ScanCached(path);
            }
            catch (Exception exception) when (exception is BadImageFormatException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var manifest in scan.Manifests.Where(manifest => !manifests.ContainsKey(manifest.Key)))
            {
                manifests.Add(manifest.Key, manifest.Value);
                manifestOwners[manifest.Key] = path;
            }

            if (scan.AssemblyManifests.Count > 0) assemblyManifests[path] = scan.AssemblyManifests;

            foreach (var config in scan.Configs)
                referencedConfigs[config.AssemblyName] = config;
        }

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

            if (!manifests.TryGetValue(typeName, out var manifest))
                continue;

            if (manifestOwners.TryGetValue(typeName, out var owner)) contributingAssemblies.Add(owner);

            AddClasses(classes, manifest.Classes);
            foreach (var use in manifest.Uses.Where(use => !visited.Contains(use))) queue.Enqueue(use);
        }

        foreach (var manifest in assemblyManifests.Where(pair => contributingAssemblies.Contains(pair.Key)).SelectMany(pair => pair.Value))
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
        var builder = new System.Text.StringBuilder();
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

    private static AssemblyScanResult ScanCached(string path)
    {
        var info = new FileInfo(path);
        var stamp = info.LastWriteTimeUtc.Ticks;
        var length = info.Length;

        if (Cache.TryGetValue(path, out var cached) && cached.Stamp == stamp && cached.Length == length)
            return cached.Scan;

        var scan = AssemblyMetadataReader.Scan(path, scanUsage: false);
        Cache[path] = new CachedScan(stamp, length, scan);
        return scan;
    }

    private static void AddClasses(List<string> classes, string classList)
    {
        foreach (var token in KhamsinEngine.Tokenize(classList))
            if (!classes.Contains(token)) classes.Add(token);
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static bool IsIntermediate(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CachedScan(long Stamp, long Length, AssemblyScanResult Scan);
}
