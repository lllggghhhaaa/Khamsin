using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Khamsin.Generators;

/// <summary>Stable analyzer rule ids and their descriptors.</summary>
internal static class Diagnostics
{
    internal const string Category = "Khamsin";

    internal static readonly DiagnosticDescriptor NonLiteralInput = new(
        id: "KHM001",
        title: "Khamsin class input must be a literal",
        messageFormat: "Khamsin classes must be string literals; '{0}' cannot be evaluated at build time",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The set of possible classes is closed at build time. Use literals, or the literal-based helpers Kx.When and Kx.Variant, instead of interpolation, concatenation or variables.");

    internal static readonly DiagnosticDescriptor UnknownClass = new(
        id: "KHM002",
        title: "Unknown Khamsin class",
        messageFormat: "'{0}' is not a known Khamsin utility class",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The utility registry defines the closed set of supported classes. Check docs/utilities.md for the supported set.");

    internal static readonly DiagnosticDescriptor ConflictingClasses = new(
        id: "KHM003",
        title: "Conflicting Khamsin classes in one literal",
        messageFormat: "'{0}' conflicts with '{1}' in the same literal; the later class wins",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Two classes in one literal target the same conflict group and variant set. The merge keeps the later one, so the earlier class can be removed.");

    internal static readonly DiagnosticDescriptor InvalidConfiguration = new(
        id: "KHM005",
        title: "Invalid Khamsin configuration",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Khamsin configuration file could not be read or contains invalid entries. The build falls back to the default theme until the file is fixed.");

    internal static readonly DiagnosticDescriptor ComponentNotResolvable = new(
        id: "KHM004",
        title: "Component referenced in markup could not be resolved",
        messageFormat: "Component tag '<{0}>' could not be resolved to a type; its Khamsin classes will not be discovered",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The manifest cannot list this component under Uses, so Khamsin cannot include its classes when the component is used.");

    internal static Location CreateLocation(string path, string text, int offset, int length)
    {
        if (offset < 0) offset = 0;

        if (offset > text.Length) offset = text.Length;

        if (length < 0 || offset + length > text.Length) length = Math.Max(0, text.Length - offset);

        var line = 0;
        var character = 0;
        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                character = 0;
            }
            else if (text[i] != '\r')
            {
                character++;
            }
        }

        var span = new TextSpan(offset, length);
        var lineSpan = new LinePositionSpan(new LinePosition(line, character), new LinePosition(line, character + length));
        return Location.Create(path, span, lineSpan);
    }

    internal static IReadOnlyList<string> Tokenize(string literal) => KhamsinEngine.Tokenize(literal).ToList();
}
