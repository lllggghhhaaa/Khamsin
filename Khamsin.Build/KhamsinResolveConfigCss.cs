using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Khamsin.Build;

/// <summary>
/// Resolves the stylesheet paths listed by the configuration into items the CSS target can declare
/// as inputs. This runs before MSBuild evaluates <c>KhamsinGenerateCss</c>, so editing a bundled
/// stylesheet invalidates the generated CSS without touching any source file.
/// </summary>
public sealed class KhamsinResolveConfigCss : Task
{
    /// <summary>Absolute path to the configuration file, or empty when the project has none.</summary>
    public string ConfigPath { get; set; } = string.Empty;

    /// <summary>
    /// Absolute paths from the <c>css</c> array. Paths are returned even when the file is missing so
    /// deleting a listed stylesheet still invalidates the target.
    /// </summary>
    [Output]
    public ITaskItem[] CssFiles { get; set; } = Array.Empty<ITaskItem>();

    public override bool Execute()
    {
        if (string.IsNullOrEmpty(ConfigPath) || !File.Exists(ConfigPath))
            return true;

        if (!KhamsinConfig.TryLoad(ConfigPath, out var config, out _) || config?.CssFiles is not { Count: > 0 })
            return true;

        var directory = Path.GetDirectoryName(ConfigPath) ?? string.Empty;
        var files = new List<ITaskItem>(config.CssFiles.Count);
        foreach (var cssPath in config.CssFiles)
        {
            if (string.IsNullOrWhiteSpace(cssPath))
                continue;

            try
            {
                files.Add(new TaskItem(Path.GetFullPath(cssPath, directory)));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Log.LogWarning("Khamsin: CSS path '{0}' in '{1}' is invalid and was ignored: {2}", cssPath, ConfigPath, exception.Message);
            }
        }

        CssFiles = files.ToArray();
        return true;
    }
}
