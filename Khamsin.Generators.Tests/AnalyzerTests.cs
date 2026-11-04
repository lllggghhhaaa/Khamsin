using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Khamsin.Generators.Tests;

public sealed class AnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var compilation = GeneratorHarness.CreateCompilation(source);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new KhamsinAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Literal_kx_calls_are_clean()
    {
        const string source = """
            using Khamsin;

            namespace Sample
            {
                internal static class Styles
                {
                    internal static KhamsinClass Card(bool compact)
                    {
                        Kx.When(compact, "p-2", "p-4");
                        return Kx.Variant(compact, ("sm", "px-2"), ("lg", "px-6"));
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Reports_non_literal_argument()
    {
        const string source = """
            using Khamsin;

            namespace Sample
            {
                internal static class Styles
                {
                    private static string Build() => "p-2";

                    internal static KhamsinClass Card() => Kx.When(true, Build(), "p-4");
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "KHM001");
        Assert.Contains("Build()", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_unknown_class_in_kx_literal()
    {
        const string source = """
            using Khamsin;

            namespace Sample
            {
                internal static class Styles
                {
                    internal static KhamsinClass Card() => Kx.When(true, "p-2 wat-is-this", "p-4");
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "KHM002");
        Assert.Contains("wat-is-this", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_unknown_class_in_khamsin_class_initializer()
    {
        const string source = """
            namespace Sample
            {
                internal static class Styles
                {
                    internal static readonly Khamsin.KhamsinClass Card = "p-2 still-not-real";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "KHM002" && d.GetMessage().Contains("still-not-real", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reports_conflicting_classes()
    {
        const string source = """
            using Khamsin;

            namespace Sample
            {
                internal static class Styles
                {
                    internal static KhamsinClass Card() => Kx.When(true, "px-2 px-4", "");
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "KHM003");
    }

    [Fact]
    public async Task Code_fix_removes_unknown_class()
    {
        const string source = """
            namespace Sample
            {
                internal static class Styles
                {
                    internal static readonly Khamsin.KhamsinClass Card = "p-2 wat-is-this";
                }
            }
            """;

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Sample", LanguageNames.CSharp)
            .WithCompilationOptions(new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(GeneratorHarness.ParseOptions());
        GeneratorHarness.References().ToList().ForEach(reference => project = project.AddMetadataReference(reference));
        var document = workspace.AddDocument(project.Id, "Styles.cs", SourceText.From(source));

        var compilation = await document.Project.GetCompilationAsync();
        Assert.NotNull(compilation);
        var diagnostics = await compilation!.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new KhamsinAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
        var diagnostic = diagnostics.Single(d => d.Id == "KHM002");

        CodeAction? action = null;
        var context = new CodeFixContext(document, diagnostic, (candidate, _) => action = candidate, CancellationToken.None);
        await new KhamsinCodeFixProvider().RegisterCodeFixesAsync(context);

        Assert.NotNull(action);
        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id);
        Assert.NotNull(changed);
        var text = (await changed!.GetTextAsync()).ToString();

        Assert.Contains("\"p-2\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("wat-is-this", text, StringComparison.Ordinal);
    }
}
