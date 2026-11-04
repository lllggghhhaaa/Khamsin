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

    /// <summary>The engine used for parsing and emission; the config extends the default theme when present.</summary>
    private KhamsinEngine? _engine;

    private KhamsinEngine Engine
    {
        get
        {
            if (_engine is not null) return _engine;

            if (ConfigPath.Length > 0 && File.Exists(ConfigPath))
            {
                if (!Khamsin.KhamsinConfig.TryLoad(ConfigPath, out var config, out var errors) || config is null)
                {
                    foreach (var error in errors)
                        Log.LogError("Khamsin config ({0}): {1}", ConfigPath, error);

                    throw new InvalidOperationException("Khamsin: the configuration file is invalid; see the errors above.");
                }

                _engine = new KhamsinEngine(config.ToTheme());
                return _engine;
            }

            _engine = KhamsinEngine.Default;
            return _engine;
        }
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
        var manifests = new Dictionary<string, ManifestInfo>(StringComparer.Ordinal);
        var assemblyManifests = new Dictionary<string, List<ManifestInfo>>(StringComparer.OrdinalIgnoreCase);
        var contributingAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifestOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        }

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

        // Helper-class manifests are not attached to a component, so they cannot be tree-shaken.
        // Include them only for assemblies that contributed a used component.
        foreach (var manifest in assemblyManifests.Where(pair => contributingAssemblies.Contains(pair.Key)).SelectMany(pair => pair.Value))
            AddClasses(classes, manifest.Classes);

        foreach (var manifest in consumer.AssemblyManifests) 
            AddClasses(classes, manifest.Classes);

        // The consumer's own components always contribute: the spec treats the app's sources as one
        // closed set, whether or not every component is currently rendered.
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
                    if (Engine.TryParse(literal.Value, out _, out _)) AddClasses(classes, literal.Value);
            }
            else if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var literal in CSharpSourceScanner.FindKxLiterals(text)) AddClasses(classes, literal.Value);
                foreach (var literal in CSharpSourceScanner.FindKhamsinClassLiterals(text)) AddClasses(classes, literal.Value);
            }
        }

        var result = Engine.Generate(classes, Minify);
        foreach (var unknown in result.UnknownClasses) 
            Log.LogWarning(null, "KHM002", null, null, 0, 0, 0, 0, "Khamsin: '{0}' is not a known utility class and was not emitted.", unknown);

        Write(result.Css);
        Log.LogMessage(MessageImportance.Low, "Khamsin: {0} classes -> {1} bytes at '{2}'.", classes.Count, result.Css.Length, OutputPath);
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
                // The target's up-to-date check compares input timestamps with this file. Touching
                // an unchanged stylesheet lets the next build skip the task without rewriting it.
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
        {
            if (!classes.Contains(token))
            {
                classes.Add(token);
            }
        }
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static bool IsIntermediate(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private sealed class CachedScan
    {
        public CachedScan(long stamp, long length, AssemblyScanResult scan)
        {
            Stamp = stamp;
            Length = length;
            Scan = scan;
        }

        public long Stamp { get; }

        public long Length { get; }

        public AssemblyScanResult Scan { get; }
    }
}
