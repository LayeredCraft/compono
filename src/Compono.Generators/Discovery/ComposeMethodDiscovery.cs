using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Finds methods attributed <c>[Compose]</c>/<c>[Compose&lt;TProfile&gt;]</c> (<c>Compono.XunitV3</c>)
/// and generates a plan for each eligible parameter type in that method's signature, per ADR-0022's
/// Amendment (2026-07-30), fix #2. Separately, per ADR-0041 Amendment 2, records every
/// dispatch-eligible parameter type needing a <c>RowInvokerRegistry</c> registration - a distinct
/// concern from plan generation, since a provider-resolved leaf type (e.g. <see langword="string"/>)
/// never needs a plan but always needs a dispatch registration.
/// </summary>
/// <remarks>
/// A separate discovery component, deliberately not folded into <see cref="CreateInvocationDiscovery"/>
/// - <c>Compono.XunitV3</c>'s binding (its own <c>MethodInfo.MakeGenericMethod</c>-based invoker
/// caching) never emits a textual <c>row.Resolve&lt;T&gt;(...)</c> call site in the consumer's own
/// source for that mechanism to match against, so a type reached only as a <c>[Compose]</c> method's
/// own parameter needs its own discovery path here instead. "Eligible" mirrors
/// <c>Compono.XunitV3</c>'s own binding algorithm's supported-shape table: a generic test method is
/// excluded entirely (its parameter types can close over the method's own type parameter, the same
/// shape <see cref="ComposedTypeAnalyzer"/>'s open-generic check already rejects for any other
/// discovery path); a <c>ref</c>/<c>out</c>/<c>in</c>/<c>params</c> parameter is excluded individually
/// - the method's other, ordinary parameters are still discovered. Every eligible parameter gets a
/// plan generated unconditionally, even one that's always inline-supplied in practice - see the ADR
/// amendment for why statically predicting inline-vs-composed per call site isn't worth duplicating
/// the binding algorithm's own runtime inline-binding calculation inside the generator.
/// </remarks>
internal static class ComposeMethodDiscovery
{
    public const string AttributeMetadataName = "Compono.XunitV3.ComposeAttribute";

    /// <summary>
    /// The metadata name of the closed-over-nothing generic form, <c>[Compose&lt;TProfile&gt;]</c> -
    /// <see cref="Microsoft.CodeAnalysis.SyntaxValueProvider.ForAttributeWithMetadataName"/> matches
    /// an attribute usage only against its own <see cref="AttributeData.AttributeClass"/>'s exact
    /// fully-qualified metadata name, not against a base type's - so
    /// <c>[Compose&lt;TProfile&gt;]</c>, whose attribute class metadata name is
    /// <c>Compono.XunitV3.ComposeAttribute`1</c> (the CLR arity-suffixed name of the generic type, not
    /// its non-generic base <see cref="AttributeMetadataName"/>), is invisible to a provider
    /// registered against <see cref="AttributeMetadataName"/> alone and needs this second,
    /// independently-registered metadata name.
    /// </summary>
    public const string GenericAttributeMetadataName = "Compono.XunitV3.ComposeAttribute`1";

    /// <summary>
    /// The metadata name of the two-type-parameter form, <c>[Compose&lt;TProfile, TConfig&gt;]</c> -
    /// same reasoning as <see cref="GenericAttributeMetadataName"/>: its attribute class metadata
    /// name is the distinct, arity-suffixed <c>Compono.XunitV3.ComposeAttribute`2</c>, invisible to
    /// a provider registered against either of the other two metadata names, and needs its own
    /// independently-registered provider (PR #65 review - the packaged sample's only composed
    /// parameter type was a registered <c>string</c>, which never needs a generated plan and masked
    /// this gap; a concrete, undiscovered-elsewhere parameter type reached only through
    /// <c>[Compose&lt;TProfile, TConfig&gt;]</c> failed at <c>GetData</c> time with no plan found).
    /// </summary>
    public const string TwoTypeParameterAttributeMetadataName = "Compono.XunitV3.ComposeAttribute`2";

    /// <summary>
    /// <c>Compono.TUnit</c>'s own <c>[Compose]</c> - the identical discovery gap as
    /// <see cref="AttributeMetadataName"/>, for a second attribute family. See
    /// <c>docs/adr/0040-compono-tunit-package-design.md</c>'s "Generator discovery" section:
    /// <c>Compono.TUnit</c>'s binding is likewise entirely runtime reflection over
    /// <c>DataGeneratorMetadata</c>, no textual <c>Resolve&lt;T&gt;()</c> call site, so a parameter
    /// type reached only through a <c>Compono.TUnit</c>-attributed method needs this same dedicated
    /// discovery path. Feeds the identical, attribute-family-agnostic <see cref="TransformMethod"/>.
    /// </summary>
    public const string TUnitAttributeMetadataName = "Compono.TUnit.ComposeAttribute";

    /// <inheritdoc cref="GenericAttributeMetadataName"/>
    /// <remarks><c>Compono.TUnit.ComposeAttribute&lt;TProfile&gt;</c>'s own arity-suffixed form.</remarks>
    public const string TUnitGenericAttributeMetadataName = "Compono.TUnit.ComposeAttribute`1";

    /// <inheritdoc cref="TwoTypeParameterAttributeMetadataName"/>
    /// <remarks><c>Compono.TUnit.ComposeAttribute&lt;TProfile, TConfig&gt;</c>'s own arity-suffixed form.</remarks>
    public const string TUnitTwoTypeParameterAttributeMetadataName = "Compono.TUnit.ComposeAttribute`2";

    public static ComposeMethodDiscoveryResult TransformMethod(GeneratorAttributeSyntaxContext context, bool testDoublesEnabled, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not IMethodSymbol method || method.IsGenericMethod)
        {
            return new ComposeMethodDiscoveryResult(
                new TransitiveClosureResult(
                    EquatableArray<DiscoveredTypeInfo>.Empty, EquatableArray<DiscoveredCollectionInfo>.Empty, EquatableArray<DiscoveredTestDoubleInfo>.Empty),
                EquatableArray<RowInvokerTypeInfo>.Empty);
        }

        var compilation = context.SemanticModel.Compilation;
        var types = new List<DiscoveredTypeInfo>();
        var collections = new List<DiscoveredCollectionInfo>();
        var testDoubles = new List<DiscoveredTestDoubleInfo>();
        var rowInvokerTypes = new List<RowInvokerTypeInfo>();

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams)
                continue;

            var location = LocationOf(parameter, cancellationToken);
            var result = ComposedTypeAnalyzer.Analyze(parameter.Type, compilation, location, testDoublesEnabled);
            types.AddRange(result.Types);
            collections.AddRange(result.Collections);
            testDoubles.AddRange(result.TestDoubles);

            if (RowInvokerTypeOf(parameter.Type, compilation, location) is { } rowInvokerType)
                rowInvokerTypes.Add(rowInvokerType);
        }

        return new ComposeMethodDiscoveryResult(
            new TransitiveClosureResult(types.ToEquatableArray(), collections.ToEquatableArray(), testDoubles.ToEquatableArray()),
            rowInvokerTypes.ToEquatableArray());
    }

    // ADR-0041's dispatch-eligibility guard: a parameter type that can never legally be a generic type
    // argument to CompositionRow.Resolve<T>()/etc. at all (a ref struct, a pointer, an open type
    // parameter) is simply not recorded - it was never going to get a working generated plan either,
    // so no diagnostic. A shape-eligible type that isn't accessible from a top-level generated type
    // (CMP0012's collection-element-type problem, applied here to a bare parameter type - part 2 of
    // the guard) still needs recording, but carrying its own CMP0013 diagnostic instead of a
    // registration.
    private static RowInvokerTypeInfo? RowInvokerTypeOf(ITypeSymbol parameterType, Compilation compilation, LocationInfo? location)
    {
        if (!ComposedTypeAnalyzer.IsRowInvokerShapeEligible(parameterType, compilation))
            return null;

        var fullyQualifiedName = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (compilation.IsSymbolAccessibleWithin(parameterType, compilation.Assembly))
            return new RowInvokerTypeInfo(fullyQualifiedName, EquatableArray<DiagnosticInfo>.Empty);

        var diagnostic = new DiagnosticInfo(
            DiagnosticDescriptors.InaccessibleRowInvokerParameterType,
            location,
            parameterType.ToDisplayString(),
            fullyQualifiedName);

        return new RowInvokerTypeInfo(fullyQualifiedName, new[] { diagnostic }.ToEquatableArray());
    }

    // Points diagnostics at the parameter's own declaration - the closest thing to a "request site"
    // a method-parameter-only discovery has (there's no call-site expression to point at, unlike
    // CreateInvocationDiscovery).
    private static LocationInfo? LocationOf(IParameterSymbol parameter, CancellationToken cancellationToken) =>
        parameter.DeclaringSyntaxReferences is [var syntaxReference, ..]
            ? LocationInfo.From(syntaxReference.GetSyntax(cancellationToken))
            : LocationInfo.From(parameter);
}
