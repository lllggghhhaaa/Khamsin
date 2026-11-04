using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Khamsin.Generators;

internal sealed class RazorInput(string path, string text) : IEquatable<RazorInput>
{
    public string Path { get; } = path;

    public string Text { get; } = text;

    public bool Equals(RazorInput? other)
        => other != null && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase) && string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as RazorInput);

    public override int GetHashCode()
    {
        var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
        return hash * 31 + Text.Length;
    }
}

internal sealed class KxManifest(
    string typeFullName,
    string accessibility,
    bool isComponent,
    bool canBeExtended,
    ImmutableArray<string> classes)
    : IEquatable<KxManifest>
{
    public string TypeFullName { get; } = typeFullName;

    public string Accessibility { get; } = accessibility;

    public bool IsComponent { get; } = isComponent;

    /// <summary>True when every declaration of the type is partial, so a manifest part is legal.</summary>
    public bool CanBeExtended { get; } = canBeExtended;

    public ImmutableArray<string> Classes { get; } = classes;

    public bool Equals(KxManifest? other)
    {
        if (other is null ||
            !string.Equals(TypeFullName, other.TypeFullName, StringComparison.Ordinal) ||
            !string.Equals(Accessibility, other.Accessibility, StringComparison.Ordinal) ||
            IsComponent != other.IsComponent ||
            CanBeExtended != other.CanBeExtended ||
            Classes.Length != other.Classes.Length)
            return false;

        return !Classes.Where((t, i) => !string.Equals(t, other.Classes[i], StringComparison.Ordinal)).Any();
    }

    public override bool Equals(object? obj) => Equals(obj as KxManifest);

    public override int GetHashCode()
    {
        var hash = StringComparer.Ordinal.GetHashCode(TypeFullName);
        hash = hash * 31 + Classes.Length;
        return hash;
    }
}

internal sealed class ProjectSettings(string rootNamespace, string projectDirectory, string configFileName) : IEquatable<ProjectSettings>
{
    public string RootNamespace { get; } = rootNamespace;

    public string ProjectDirectory { get; } = projectDirectory;

    /// <summary>Configured configuration file name; empty when no <c>KhamsinConfig</c> property is set.</summary>
    public string ConfigFileName { get; } = configFileName;

    public bool Equals(ProjectSettings? other)
        => other is not null &&
           string.Equals(RootNamespace, other.RootNamespace, StringComparison.Ordinal) &&
           string.Equals(ProjectDirectory, other.ProjectDirectory, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(ConfigFileName, other.ConfigFileName, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as ProjectSettings);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(RootNamespace);
}

/// <summary>The parsed configuration for one compilation, or the reason it is unusable.</summary>
internal sealed class ConfigInput
{
    internal static readonly ConfigInput None = new(null, [], string.Empty);

    private ConfigInput(KhamsinConfig? config, ImmutableArray<string> errors, string filePath)
    {
        Config = config;
        Errors = errors;
        FilePath = filePath;
    }

    internal static ConfigInput From(KhamsinConfig? config, ImmutableArray<string> errors, string filePath = "")
        => config is null && errors.Length == 0 ? None : new ConfigInput(config, errors, filePath);

    /// <summary>Null when the file was missing or could not be parsed.</summary>
    internal KhamsinConfig? Config { get; }

    internal ImmutableArray<string> Errors { get; }

    internal string FilePath { get; }
}

internal sealed class GeneratorInputs(
    ImmutableArray<RazorInput> razorFiles,
    ImmutableArray<KxManifest> kxManifests,
    ProjectSettings settings,
    Compilation compilation,
    ConfigInput config)
{
    public ImmutableArray<RazorInput> RazorFiles { get; } = razorFiles;

    public ImmutableArray<KxManifest> KxManifests { get; } = kxManifests;

    public ProjectSettings Settings { get; } = settings;

    public Compilation Compilation { get; } = compilation;

    public ConfigInput Config { get; } = config;
}
