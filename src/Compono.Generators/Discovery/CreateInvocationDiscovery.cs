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

        if (method.TypeArguments.Length != 1)
            return null;

        var typeArgument = method.TypeArguments[0];

        // Points diagnostics at the `T` in `composer.Create<T>()` itself, not at T's declaration
        // (which may not even exist - see the ContainsTypeParameter branch below - or may just be
        // a less useful place to highlight than the call site that actually triggered discovery).
        var location = LocationInfo.From(GetTypeArgumentSyntax(invocation) ?? (SyntaxNode)invocation);

        // Three ways a type argument can fail to be a genuine closed type, all needing the same
        // diagnostic: `composer.Create<T>()` where `T` is the enclosing generic method's own type
        // parameter (typeArgument is directly an ITypeParameterSymbol - not even an
        // INamedTypeSymbol, so it wouldn't survive the cast below); `composer.Create<Box<T>>()`
        // where `T` is nested inside a constructed generic type's arguments; and
        // `composer.Create<Outer<T>.Inner>()` where `Inner` isn't itself generic but its
        // *containing* type still closes over the method's `T`. ContainsTypeParameter walks all
        // three shapes before anything downstream assumes the type argument is fully closed.
        if (ContainsTypeParameter(typeArgument))
            return OpenGenericTypeArgumentFailure(typeArgument, location);

        if (typeArgument is not INamedTypeSymbol composedType)
            return null;

        return Analyze(composedType, context.SemanticModel.Compilation, location);
    }

    private static TypeSyntax? GetTypeArgumentSyntax(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.TypeArgumentList.Arguments[0],
            MemberBindingExpressionSyntax { Name: GenericNameSyntax generic } => generic.TypeArgumentList.Arguments[0],
            _ => null,
        };

    private static DiscoveredTypeInfo Analyze(INamedTypeSymbol type, Compilation compilation, LocationInfo? location)
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

        var selection = ConstructorSelector.Select(type, compilation, location);

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

    private static DiscoveredTypeInfo OpenGenericTypeArgumentFailure(ITypeSymbol type, LocationInfo? location)
    {
        var @namespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "";
        var emittedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new DiscoveredTypeInfo(
            @namespace,
            type.Name,
            emittedName,
            EquatableArray<ConstructorParameterInfo>.Empty,
            new[]
            {
                new DiagnosticInfo(
                    DiagnosticDescriptors.OpenGenericTypeArgument,
                    location,
                    type.ToDisplayString()),
            }.ToEquatableArray());
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        INamedTypeSymbol named =>
            (named.IsGenericType && named.TypeArguments.Any(ContainsTypeParameter)) ||
            (named.ContainingType is not null && ContainsTypeParameter(named.ContainingType)),
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
        _ => false,
    };
}
