using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Compono.Generators.WellKnownTypes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Compono.Generators.Discovery;

/// <summary>
/// Finds <c>Composer.Create&lt;T&gt;()</c> call sites and resolves each into a
/// <see cref="DiscoveredTypeInfo"/>, per <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c>'s
/// call-site discovery mechanism.
/// </summary>
internal static class CreateInvocationDiscovery
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken cancellationToken) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.ValueText: "Create", TypeArgumentList.Arguments.Count: 1 },
            },
        };

    public static DiscoveredTypeInfo? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
            return null;

        var wellKnownTypes = WellKnownTypes.WellKnownTypes.GetOrCreate(context.SemanticModel.Compilation);

        if (method.Name != "Create" || !wellKnownTypes.IsType(method.ContainingType, WellKnownTypeData.WellKnownType.Compono_Composer))
            return null;

        if (method.TypeArguments.Length != 1 || method.TypeArguments[0] is not INamedTypeSymbol composedType)
            return null;

        return Analyze(composedType);
    }

    private static DiscoveredTypeInfo Analyze(INamedTypeSymbol type)
    {
        var selection = ConstructorSelector.Select(type);

        if (!selection.IsSuccess)
            return new DiscoveredTypeInfo(
                type.ContainingNamespace.ToDisplayString(),
                type.Name,
                type.ToDisplayString(),
                EquatableArray<ConstructorParameterInfo>.Empty,
                new[] { selection.Diagnostic! }.ToEquatableArray());

        var parameters = selection.Constructor!.Parameters
            .Select(p => new ConstructorParameterInfo(p.Name, p.Type.ToDisplayString()))
            .ToEquatableArray();

        return new DiscoveredTypeInfo(
            type.ContainingNamespace.ToDisplayString(),
            type.Name,
            type.ToDisplayString(),
            parameters,
            EquatableArray<DiagnosticInfo>.Empty);
    }
}
