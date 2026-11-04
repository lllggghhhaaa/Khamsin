using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Khamsin.Generators;

/// <summary>
/// Semantic helpers for <c>Cn.Of</c>/<c>Cn.Merge</c> calls, the clsx-style composer. Strings that
/// can render must be literals, or come from ternaries, coalescing, tuples, collections, nested
/// <c>Cn</c> calls or constant fields; dynamic strings are rejected by the analyzer.
/// </summary>
internal static class CnSyntax
{
    internal static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return false;

        var name = access.Name.Identifier.ValueText;
        return name is "Of" or "Merge" && access.Expression is IdentifierNameSyntax { Identifier.ValueText: "Cn" };
    }

    internal static bool TryGetMethod(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken, out IMethodSymbol method)
    {
        method = null!;
        var info = model.GetSymbolInfo(invocation, cancellationToken);
        if (info.Symbol is not IMethodSymbol candidate)
            return false;

        var containing = candidate.ContainingType;
        if (containing is null || !string.Equals(containing.ToDisplayString(), "Khamsin.Cn", StringComparison.Ordinal))
            return false;

        method = candidate;
        return true;
    }

    /// <summary>
    /// Collects the class-carrying string values and the expressions the analyzer must reject.
    /// Literal positions: direct literals, ternary/coalesce branches, tuple elements, collection
    /// elements, nested Cn calls and constant fields. Conditions and booleans contribute nothing.
    /// </summary>
    internal static void CollectArguments(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out List<(string Value, Location Location)> literals,
        out List<(string Text, Location Location)> nonLiterals)
    {
        literals = new List<(string, Location)>();
        nonLiterals = new List<(string, Location)>();

        foreach (var argument in invocation.ArgumentList.Arguments)
            Visit(argument.Expression, model, cancellationToken, literals, nonLiterals);
    }

    private static void Visit(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken,
        List<(string Value, Location Location)> literals,
        List<(string Text, Location Location)> nonLiterals)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                literals.Add((literal.Token.ValueText, literal.GetLocation()));
                return;

            case LiteralExpressionSyntax nullLiteral when nullLiteral.IsKind(SyntaxKind.NullLiteralExpression):
                return;

            case ConditionalExpressionSyntax conditional:
                Visit(conditional.WhenTrue, model, cancellationToken, literals, nonLiterals);
                Visit(conditional.WhenFalse, model, cancellationToken, literals, nonLiterals);
                return;

            case BinaryExpressionSyntax coalesce when coalesce.IsKind(SyntaxKind.CoalesceExpression):
                Visit(coalesce.Left, model, cancellationToken, literals, nonLiterals);
                Visit(coalesce.Right, model, cancellationToken, literals, nonLiterals);
                return;

            case TupleExpressionSyntax tuple:
                foreach (var argument in tuple.Arguments)
                    Visit(argument.Expression, model, cancellationToken, literals, nonLiterals);

                return;

            case CollectionExpressionSyntax collection:
                foreach (var element in collection.Elements)
                {
                    if (element is ExpressionElementSyntax expressionElement)
                        Visit(expressionElement.Expression, model, cancellationToken, literals, nonLiterals);
                    else if (element is SpreadElementSyntax spread)
                        Visit(spread.Expression, model, cancellationToken, literals, nonLiterals);
                }

                return;

            case ObjectCreationExpressionSyntax creation when creation.Initializer is not null:
                VisitInitializer(creation.Initializer, model, cancellationToken, literals, nonLiterals);
                return;

            case ImplicitObjectCreationExpressionSyntax implicitCreation when implicitCreation.Initializer is not null:
                VisitInitializer(implicitCreation.Initializer, model, cancellationToken, literals, nonLiterals);
                return;

            case InvocationExpressionSyntax nested when IsCandidate(nested) && TryGetMethod(nested, model, cancellationToken, out _):
                CollectArguments(nested, model, cancellationToken, out var nestedLiterals, out var nestedNonLiterals);
                literals.AddRange(nestedLiterals);
                nonLiterals.AddRange(nestedNonLiterals);
                return;
        }

        // Constant fields resolve to their literal value; booleans contribute nothing; a
        // KhamsinClass is already extracted at its own declaration site. Everything else carries
        // classes the build cannot know.
        var constant = model.GetConstantValue(expression, cancellationToken);
        if (constant.HasValue && constant.Value is string constText)
        {
            literals.Add((constText, expression.GetLocation()));
            return;
        }

        var type = model.GetTypeInfo(expression, cancellationToken).Type;
        if (type is null || type.TypeKind == TypeKind.Error)
            return;

        if (type.SpecialType == SpecialType.System_Boolean)
            return;

        if (string.Equals(type.ToDisplayString(), "Khamsin.KhamsinClass", StringComparison.Ordinal))
            return;

        var text = expression.ToString();
        if (text.Length > 60)
            text = text.Substring(0, 60) + "...";

        nonLiterals.Add((text, expression.GetLocation()));
    }

    private static void VisitInitializer(
        InitializerExpressionSyntax initializer,
        SemanticModel model,
        CancellationToken cancellationToken,
        List<(string Value, Location Location)> literals,
        List<(string Text, Location Location)> nonLiterals)
    {
        foreach (var expression in initializer.Expressions)
            Visit(expression, model, cancellationToken, literals, nonLiterals);
    }

    internal static KxManifest? Extract(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        if (!TryGetMethod(invocation, context.SemanticModel, cancellationToken, out _))
            return null;

        CollectArguments(invocation, context.SemanticModel, cancellationToken, out var literals, out _);
        if (literals.Count == 0)
            return null;

        var symbol = context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken);
        while (symbol is not null && symbol is not INamedTypeSymbol)
            symbol = symbol.ContainingSymbol;

        if (symbol is not INamedTypeSymbol type)
            return null;

        return new KxManifest(
            KxSyntax.ToFullName(type),
            KxSyntax.AccessibilityOf(type),
            KxSyntax.IsComponent(type),
            KxSyntax.CanBeExtended(type),
            [.. literals.SelectMany(literal => KhamsinEngine.Tokenize(literal.Value))]);
    }
}
