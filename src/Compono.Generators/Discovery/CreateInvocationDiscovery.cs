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
            // MemberAccessExpressionSyntax covers plain `composer.Create<T>()`;
            // MemberBindingExpressionSyntax covers null-conditional `composer?.Create<T>()` - a
            // separate syntax shape (nested inside a ConditionalAccessExpressionSyntax) that the
            // Roslyn syntax walker visits as its own node, so both have to be matched here or the
            // conditional-access form is silently missed by discovery.
            Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Create", TypeArgumentList.Arguments.Count: 1 } }
                     or MemberBindingExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Create", TypeArgumentList.Arguments.Count: 1 } },
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

        return Analyze(composedType, context.SemanticModel.Compilation);
    }

    private static DiscoveredTypeInfo Analyze(INamedTypeSymbol type, Compilation compilation)
    {
        // ContainingNamespace.ToDisplayString() returns the literal text "<global namespace>" for
        // a type with no namespace, not an empty string - confirmed empirically (it briefly made it
        // into a generated `namespace <global namespace> { ... }`, which is obviously invalid C#).
        // IsGlobalNamespace is the actual, correct check.
        var @namespace = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();

        // FullyQualifiedFormat emits `global::`-prefixed names. Plain ToDisplayString() doesn't,
        // despite how it reads - and an unqualified `Acme.Customer` in generated code binds through
        // a type named `Acme` if one shadows the namespace segment in scope, breaking the
        // consumer's build. Everything emitted into generated code goes through this format;
        // diagnostic messages keep the plain form for readability.
        var emittedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var selection = ConstructorSelector.Select(type, compilation);

        if (!selection.IsSuccess)
            return new DiscoveredTypeInfo(
                @namespace,
                type.Name,
                emittedName,
                EquatableArray<ConstructorParameterInfo>.Empty,
                new[] { selection.Diagnostic! }.ToEquatableArray());

        var parameters = selection.Constructor!.Parameters
            .Select(p => new ConstructorParameterInfo(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToEquatableArray();

        return new DiscoveredTypeInfo(
            @namespace,
            type.Name,
            emittedName,
            parameters,
            EquatableArray<DiagnosticInfo>.Empty);
    }
}
