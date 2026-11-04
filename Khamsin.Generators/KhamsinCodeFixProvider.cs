using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Khamsin.Generators;

/// <summary>Removes an unknown class token from the literal that produced KHM002.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(KhamsinCodeFixProvider))]
[Shared]
public sealed class KhamsinCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("KHM002");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("token", out var token) || token is null)
                continue;

            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var literal = node.AncestorsAndSelf().OfType<LiteralExpressionSyntax>()
                .FirstOrDefault(expression => expression.IsKind(SyntaxKind.StringLiteralExpression));
            if (literal is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove unknown class '" + token + "'",
                    cancellationToken => RemoveAsync(context.Document, literal, token, cancellationToken),
                    equivalenceKey: "KHM002.RemoveUnknownClass"),
                diagnostic);
        }
    }

    private static async Task<Document> RemoveAsync(Document document, LiteralExpressionSyntax literal, string token, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var classes = KhamsinEngine.Tokenize(literal.Token.ValueText).ToList();
        var removed = false;
        classes.RemoveAll(value =>
        {
            if (removed || !string.Equals(value, token, StringComparison.Ordinal)) return false;
            
            removed = true;
            return true;
        });

        if (!removed)
            return document;

        var replacement = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(string.Join(" ", classes)))
            .WithTriviaFrom(literal);

        return document.WithSyntaxRoot(root.ReplaceNode(literal, replacement));
    }
}
