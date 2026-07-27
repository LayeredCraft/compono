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

    public static EquatableArray<DiscoveredTypeInfo>? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
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

        // Anything that isn't an INamedTypeSymbol - an array (`Customer[]`), a pointer, a function
        // pointer - has no constructors for ConstructorSelector to select from, and `new T(...)`
        // isn't even the right syntax to construct one. Report it instead of silently doing
        // nothing: without this, `composer.Create<Customer[]>()` compiles clean, generates no plan
        // and no diagnostic, and only fails at runtime via Composer's generic
        // "no plan registered" message - which gives no hint that this type shape was never
        // supported in the first place.
        if (typeArgument is not INamedTypeSymbol composedType)
            return UnsupportedTypeArgumentShapeFailure(typeArgument, location);

        // Walks the requested type's constructor parameters recursively (Phase 1) - the returned
        // array holds the requested type itself plus every type in its transitive closure that's
        // eligible for its own generated plan (LeafTypeClassifier), not just the top-level type.
        return TransitiveClosureWalker.Walk(composedType, context.SemanticModel.Compilation, location);
    }

    private static TypeSyntax? GetTypeArgumentSyntax(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.TypeArgumentList.Arguments[0],
            MemberBindingExpressionSyntax { Name: GenericNameSyntax generic } => generic.TypeArgumentList.Arguments[0],
            _ => null,
        };

    private static EquatableArray<DiscoveredTypeInfo> OpenGenericTypeArgumentFailure(ITypeSymbol type, LocationInfo? location) =>
        TypeArgumentFailure(DiagnosticDescriptors.OpenGenericTypeArgument, type, location);

    private static EquatableArray<DiscoveredTypeInfo> UnsupportedTypeArgumentShapeFailure(ITypeSymbol type, LocationInfo? location) =>
        TypeArgumentFailure(DiagnosticDescriptors.UnsupportedTypeArgumentShape, type, location);

    // Shared by every "the type argument itself is unusable" failure - none of these have a
    // constructor to select, so there's no DiscoveredTypeInfo.Parameters to populate, just a
    // diagnostic. `type.ContainingNamespace`/`type.Name` are empty for shapes like arrays and
    // pointers, which is fine here: the Namespace/TypeName fields go unused once Diagnostics is
    // non-empty (ComponoIncrementalGenerator skips codegen for any type with diagnostics).
    private static EquatableArray<DiscoveredTypeInfo> TypeArgumentFailure(DiagnosticDescriptor descriptor, ITypeSymbol type, LocationInfo? location)
    {
        var @namespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "";
        var emittedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var failure = new DiscoveredTypeInfo(
            @namespace,
            type.Name,
            emittedName,
            EquatableArray<ConstructorParameterInfo>.Empty,
            new[] { new DiagnosticInfo(descriptor, location, type.ToDisplayString()) }.ToEquatableArray());

        return new[] { failure }.ToEquatableArray();
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
