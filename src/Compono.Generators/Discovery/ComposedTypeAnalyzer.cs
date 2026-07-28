using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Shared tail of every discovery path: validates that a requested type is a genuinely closed,
/// named type, then hands it to <see cref="TransitiveClosureWalker"/>. Both discovery mechanisms
/// from <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c> — <c>Create&lt;T&gt;()</c>
/// call sites and <c>[Composable]</c> requests — funnel through here so an invalid requested type
/// gets the same diagnostic regardless of how it was discovered.
/// </summary>
internal static class ComposedTypeAnalyzer
{
    public static EquatableArray<DiscoveredTypeInfo> Analyze(ITypeSymbol requestedType, Compilation compilation, LocationInfo? location)
    {
        // Three ways a requested type can fail to be a genuine closed type, all needing the same
        // diagnostic: `composer.Create<T>()` where `T` is the enclosing generic method's own type
        // parameter (requestedType is directly an ITypeParameterSymbol - not even an
        // INamedTypeSymbol, so it wouldn't survive the cast below); `composer.Create<Box<T>>()`
        // where `T` is nested inside a constructed generic type's arguments; and
        // `composer.Create<Outer<T>.Inner>()` where `Inner` isn't itself generic but its
        // *containing* type still closes over the method's `T`. The same shapes reach here via
        // `[Composable]` on a generic type declaration, whose type parameters are unresolved the
        // same way. ContainsTypeParameter walks all three shapes before anything downstream assumes
        // the requested type is fully closed.
        if (ContainsTypeParameter(requestedType))
            return TypeArgumentFailure(DiagnosticDescriptors.OpenGenericTypeArgument, requestedType, location);

        // Anything that isn't an INamedTypeSymbol - an array (`Customer[]`), a pointer, a function
        // pointer - has no constructors for ConstructorSelector to select from, and `new T(...)`
        // isn't even the right syntax to construct one. Report it instead of silently doing
        // nothing: without this, `composer.Create<Customer[]>()` compiles clean, generates no plan
        // and no diagnostic, and only fails at runtime via Composer's generic
        // "no plan registered" message - which gives no hint that this type shape was never
        // supported in the first place.
        if (requestedType is not INamedTypeSymbol composedType)
            return TypeArgumentFailure(DiagnosticDescriptors.UnsupportedTypeArgumentShape, requestedType, location);

        // A ref struct constructor PARAMETER is already rejected by ConstructorSelector's
        // ValidateParameterKinds (CMP0004) when validating whichever type declares it - but nothing
        // stops the requested type itself from being ref-like, since that path never goes through
        // parameter validation. ICompositionPlan<T>/PlanCache<T> both declare a bare `T` with no
        // `allows ref struct` constraint, so emitting `ICompositionPlan<global::N.SomeRefStruct>`
        // would fail to compile (CS9244) - reject it here instead, before any codegen happens.
        if (composedType.IsRefLikeType)
            return TypeArgumentFailure(DiagnosticDescriptors.RefLikeTypeArgument, composedType, location);

        // Walks the requested type's constructor parameters recursively (Phase 1) - the returned
        // array holds the requested type itself plus every type in its transitive closure that's
        // eligible for its own generated plan (LeafTypeClassifier), not just the top-level type.
        return TransitiveClosureWalker.Walk(composedType, compilation, location);
    }

    // Shared by every "the requested type itself is unusable" failure - none of these have a
    // constructor to select, so there's no DiscoveredTypeInfo.Parameters to populate, just a
    // diagnostic. `type.ContainingNamespace`/`type.Name` are empty for shapes like arrays and
    // pointers, which is fine here: the Namespace/TypeName fields go unused once Diagnostics is
    // non-empty (ComponoIncrementalGenerator skips codegen for any type with diagnostics).
    public static EquatableArray<DiscoveredTypeInfo> TypeArgumentFailure(DiagnosticDescriptor descriptor, ITypeSymbol type, LocationInfo? location)
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
