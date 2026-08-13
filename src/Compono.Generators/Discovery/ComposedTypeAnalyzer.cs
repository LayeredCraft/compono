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
    public static TransitiveClosureResult Analyze(ITypeSymbol requestedType, Compilation compilation, LocationInfo? location, bool testDoublesEnabled)
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

        // A rank-1 array root (`composer.Create<Customer[]>()`) is one of ADR-0013's five supported
        // collection shapes - it must reach TransitiveClosureWalker's root collection-classification
        // (same as any other collection root) before the INamedTypeSymbol check below, which would
        // otherwise reject it (IArrayTypeSymbol is never an INamedTypeSymbol) with CMP0006 even
        // though arrays are a genuinely supported root shape. PR #11 review caught this - the
        // previous root-type fix only covered List<T>/HashSet<T>/Dictionary<TKey, TValue> roots
        // (all INamedTypeSymbol), missing arrays entirely.
        if (CollectionWellKnownTypes.GetOrCreate(compilation).TryClassify(requestedType, out _))
            return TransitiveClosureWalker.Walk(requestedType, compilation, location, testDoublesEnabled);

        // Anything that isn't an INamedTypeSymbol - a pointer, a function pointer, an unsupported
        // array rank - has no constructors for ConstructorSelector to select from, and `new T(...)`
        // isn't even the right syntax to construct one. Report it instead of silently doing
        // nothing: without this, `composer.Create<Customer[,]>()` compiles clean, generates no plan
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
        return TransitiveClosureWalker.Walk(composedType, compilation, location, testDoublesEnabled);
    }

    // Shared by every "the requested type itself is unusable" failure - none of these have a
    // constructor to select, so there's no DiscoveredTypeInfo.Parameters to populate, just a
    // diagnostic. `type.ContainingNamespace`/`type.Name` are empty for shapes like arrays and
    // pointers, which is fine here: the Namespace/TypeName fields go unused once Diagnostics is
    // non-empty (ComponoIncrementalGenerator skips codegen for any type with diagnostics).
    public static TransitiveClosureResult TypeArgumentFailure(DiagnosticDescriptor descriptor, ITypeSymbol type, LocationInfo? location)
    {
        var @namespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "";
        var emittedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var failure = new DiscoveredTypeInfo(
            @namespace,
            type.Name,
            emittedName,
            EquatableArray<ConstructorParameterInfo>.Empty,
            EquatableArray<RequiredMemberInfo>.Empty,
            new[] { new DiagnosticInfo(descriptor, location, type.ToDisplayString()) }.ToEquatableArray());

        return new TransitiveClosureResult(
            new[] { failure }.ToEquatableArray(), EquatableArray<DiscoveredCollectionInfo>.Empty, EquatableArray<DiscoveredTestDoubleInfo>.Empty);
    }

    /// <summary>
    /// Whether <paramref name="type"/> could ever legally be a generic type argument to
    /// <c>CompositionRow.Resolve&lt;T&gt;()</c>/<c>ResolveShared&lt;T&gt;()</c>/<c>ShareExplicit&lt;T&gt;()</c>
    /// at all - reused by <see cref="ComposeMethodDiscovery"/> for its <c>RowInvokerRegistry</c>
    /// dispatch-eligibility guard (ADR-0041), sharing the same three root-validity checks
    /// <see cref="Analyze"/> already applies before ever reaching <see cref="TransitiveClosureWalker.Walk"/>:
    /// an open type parameter (<see cref="ContainsTypeParameter"/>), a <see langword="ref"/> struct (no
    /// <c>allows ref struct</c> constraint on any of those three methods), or a shape with no
    /// <see cref="INamedTypeSymbol"/> identity that isn't one of ADR-0013's recognized collection shapes
    /// (a pointer, function pointer, or unsupported array rank). A type that fails this check was never
    /// going to get a working generated plan either - CMP0005/CMP0006/CMP0009 already cover the ordinary
    /// composed-type-argument case; this is the same rejection applied to a bare method-parameter type
    /// instead, silently (no diagnostic - see <see cref="ComposeMethodDiscovery"/> for why).
    /// </summary>
    public static bool IsRowInvokerShapeEligible(ITypeSymbol type, Compilation compilation)
    {
        if (ContainsTypeParameter(type))
            return false;

        // A recognized collection shape (List<T>, an array, ...) is an ordinary named/array type as
        // far as being a legal generic type argument goes - the collection-specific accessibility
        // concern (element/key type) is handled separately, by the accessibility check
        // ComposeMethodDiscovery applies via Compilation.IsSymbolAccessibleWithin.
        if (CollectionWellKnownTypes.GetOrCreate(compilation).TryClassify(type, out _))
            return true;

        return type is INamedTypeSymbol named && !named.IsRefLikeType;
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
