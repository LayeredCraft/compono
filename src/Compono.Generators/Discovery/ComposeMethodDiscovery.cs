using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Finds methods attributed <c>[Compose]</c>/<c>[Compose&lt;TProfile&gt;]</c> (<c>Compono.XunitV3</c>)
/// and generates a plan for each eligible parameter type in that method's signature, per ADR-0022's
/// Amendment (2026-07-30), fix #2.
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

    public static TransitiveClosureResult TransformMethod(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not IMethodSymbol method || method.IsGenericMethod)
            return new TransitiveClosureResult(EquatableArray<DiscoveredTypeInfo>.Empty, EquatableArray<DiscoveredCollectionInfo>.Empty);

        var types = new List<DiscoveredTypeInfo>();
        var collections = new List<DiscoveredCollectionInfo>();

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams)
                continue;

            var location = LocationOf(parameter, cancellationToken);
            var result = ComposedTypeAnalyzer.Analyze(parameter.Type, context.SemanticModel.Compilation, location);
            types.AddRange(result.Types);
            collections.AddRange(result.Collections);
        }

        return new TransitiveClosureResult(types.ToEquatableArray(), collections.ToEquatableArray());
    }

    // Points diagnostics at the parameter's own declaration - the closest thing to a "request site"
    // a method-parameter-only discovery has (there's no call-site expression to point at, unlike
    // CreateInvocationDiscovery).
    private static LocationInfo? LocationOf(IParameterSymbol parameter, CancellationToken cancellationToken) =>
        parameter.DeclaringSyntaxReferences is [var syntaxReference, ..]
            ? LocationInfo.From(syntaxReference.GetSyntax(cancellationToken))
            : LocationInfo.From(parameter);
}
