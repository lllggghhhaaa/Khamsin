using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Khamsin.Generators;

/// <summary>Shared Roslyn helpers for the literal-based <c>Kx</c> APIs.</summary>
internal static class KxSyntax
{
    internal static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return false;

        var name = access.Name.Identifier.ValueText;
        if (name is not ("When" or "Variant"))
            return false;

        if (access.Expression is IdentifierNameSyntax { Identifier.ValueText: "Kx" })
            return true;

        return access.Expression is MemberAccessExpressionSyntax member &&
               member.Name.Identifier.ValueText == "Kx" &&
               member.Expression is IdentifierNameSyntax { Identifier.ValueText: "Khamsin" };
    }

    internal static bool TryGetMethod(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken, out IMethodSymbol method)
    {
        method = null!;
        var info = model.GetSymbolInfo(invocation, cancellationToken);
        if (info.Symbol is not IMethodSymbol candidate)
            return false;

        var containing = candidate.ContainingType;
        if (containing is null || !string.Equals(containing.ToDisplayString(), "Khamsin.Kx", StringComparison.Ordinal))
            return false;

        method = candidate;
        return true;
    }

    /// <summary>
    /// Returns the string-literal expressions that carry classes, plus the spans of expressions that
    /// are not literals. For <c>When</c> every argument after the condition counts; for
    /// <c>Variant</c> the second element of each option tuple counts.
    /// </summary>
    internal static void CollectClassArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out List<LiteralExpressionSyntax> literals,
        out List<SyntaxNode> nonLiterals)
    {
        literals = [];
        nonLiterals = [];

        var isWhen = string.Equals(method.Name, "When", StringComparison.Ordinal);
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 1; i < arguments.Count; i++)
        {
            var expression = arguments[i].Expression;
            if (isWhen)
            {
                CollectExpression(expression, literals, nonLiterals);
                continue;
            }

            if (expression is TupleExpressionSyntax tuple)
            {
                for (var e = 1; e < tuple.Arguments.Count; e++) CollectExpression(tuple.Arguments[e].Expression, literals, nonLiterals);

                continue;
            }

            CollectExpression(expression, literals, nonLiterals);
        }
    }

    private static void CollectExpression(ExpressionSyntax expression, List<LiteralExpressionSyntax> literals, List<SyntaxNode> nonLiterals)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            literals.Add(literal);
            return;
        }

        nonLiterals.Add(expression);
    }

    internal static bool IsClassLiteralCandidate(SyntaxNode node)
    {
        var initializer = Initializer(node);
        return initializer is LiteralExpressionSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax or CastExpressionSyntax;
    }

    internal static ExpressionSyntax? Initializer(SyntaxNode node)
    {
        switch (node)
        {
            case VariableDeclaratorSyntax variable:
                return variable.Initializer?.Value;
            case PropertyDeclarationSyntax property:
                return property.Initializer?.Value;
            default:
                return null;
        }
    }

    internal static KxManifest? ExtractClassLiteral(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var node = context.Node;
        var initializer = Initializer(node);
        if (initializer is null)
            return null;

        var declared = context.SemanticModel.GetDeclaredSymbol(node, cancellationToken);
        INamedTypeSymbol? type;
        switch (declared)
        {
            case IFieldSymbol field:
                type = field.Type as INamedTypeSymbol;
                break;
            case IPropertySymbol property:
                type = property.Type as INamedTypeSymbol;
                break;
            case ILocalSymbol local:
                type = local.Type as INamedTypeSymbol;
                break;
            default:
                return null;
        }

        if (type is null || !string.Equals(type.ToDisplayString(), "Khamsin.KhamsinClass", StringComparison.Ordinal))
            return null;

        var literal = UnwrapInitializerLiteral(initializer);
        if (literal is null)
            return null;

        var containing = declared!.ContainingType;
        if (containing is null)
            return null;

        return new KxManifest(
            ToFullName(containing),
            AccessibilityOf(containing),
            IsComponent(containing),
            CanBeExtended(containing),
            [.. KhamsinEngine.Tokenize(literal.Token.ValueText)]);
    }

    internal static LiteralExpressionSyntax? UnwrapInitializerLiteral(ExpressionSyntax expression) =>
        expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal,
            ParenthesizedExpressionSyntax parenthesized => UnwrapInitializerLiteral(parenthesized.Expression),
            CastExpressionSyntax cast => UnwrapInitializerLiteral(cast.Expression),
            ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } creation => UnwrapInitializerLiteral(
                creation.ArgumentList.Arguments[0].Expression),
            ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } implicitCreation =>
                UnwrapInitializerLiteral(implicitCreation.ArgumentList.Arguments[0].Expression),
            _ => null
        };

    internal static KxManifest? Extract(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        if (!TryGetMethod(invocation, context.SemanticModel, cancellationToken, out var method))
            return null;

        CollectClassArguments(invocation, method, out var literals, out _);
        if (literals.Count == 0)
            return null;

        var symbol = context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken);
        while (symbol is not null && symbol is not INamedTypeSymbol) symbol = symbol.ContainingSymbol;

        if (symbol is not INamedTypeSymbol type)
            return null;

        return new KxManifest(
            ToFullName(type),
            AccessibilityOf(type),
            IsComponent(type),
            CanBeExtended(type),
            [.. literals.SelectMany(literal => KhamsinEngine.Tokenize(literal.Token.ValueText))]);
    }

    internal static bool CanBeExtended(INamedTypeSymbol type)
    {
        var references = type.DeclaringSyntaxReferences;
        if (references.Length == 0)
            return false;

        foreach (var reference in references)
            if (reference.GetSyntax() is not TypeDeclarationSyntax declaration ||
                !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                return false;

        return true;
    }

    internal static string ToFullName(INamedTypeSymbol type)
    {
        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return name.StartsWith("global::", StringComparison.Ordinal) ? name.Substring(8) : name;
    }

    internal static string AccessibilityOf(INamedTypeSymbol type) =>
        type.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => type.DeclaredAccessibility.ToString().ToLowerInvariant()
        };

    internal static bool IsComponent(INamedTypeSymbol type) => Enumerable.Any(type.AllInterfaces,
        @interface => string.Equals(@interface.ToDisplayString(), "Microsoft.AspNetCore.Components.IComponent",
            StringComparison.Ordinal));
}
