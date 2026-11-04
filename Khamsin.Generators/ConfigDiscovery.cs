using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Khamsin.Generators;

/// <summary>The configuration document embedded in one referenced assembly, or why it is unusable.</summary>
internal sealed class ReferencedConfig
{
    internal ReferencedConfig(string assemblyName, KhamsinConfig? config, string? error)
    {
        AssemblyName = assemblyName;
        Config = config;
        Error = error;
    }

    internal string AssemblyName { get; }

    internal KhamsinConfig? Config { get; }

    internal string? Error { get; }
}

/// <summary>
/// Reads <c>Khamsin.KhamsinConfigAttribute</c> from referenced assemblies. The attribute carries
/// the library's own configuration document, so the consuming build can merge it under its own.
/// Configurations are returned in assembly-name order to make the merge deterministic.
/// </summary>
internal static class ConfigDiscovery
{
    private const string AttributeName = "Khamsin.KhamsinConfigAttribute";

    internal static ImmutableArray<ReferencedConfig> Read(Compilation compilation)
    {
        var byAssembly = new Dictionary<string, ReferencedConfig>(StringComparer.OrdinalIgnoreCase);
        var currentAssembly = compilation.AssemblyName;

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                continue;

            var name = assembly.Identity.Name;
            if (name.Length == 0 || string.Equals(name, currentAssembly, StringComparison.OrdinalIgnoreCase))
                continue;

            var json = ReadJson(assembly);
            if (json is null)
                continue;

            byAssembly[name] = KhamsinConfig.TryParse(json, out var config, out var errors)
                ? new ReferencedConfig(name, config, null)
                : new ReferencedConfig(name, null, string.Join(" ", errors));
        }

        return [.. byAssembly.Values.OrderBy(static item => item.AssemblyName, StringComparer.Ordinal)];
    }

    private static string? ReadJson(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null ||
                !string.Equals(attributeClass.ToDisplayString(), AttributeName, StringComparison.Ordinal))
                continue;

            foreach (var argument in attribute.NamedArguments)
                if (string.Equals(argument.Key, "Json", StringComparison.Ordinal) && argument.Value.Value is string json)
                    return json;

            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is string positional)
                return positional;
        }

        return null;
    }
}
