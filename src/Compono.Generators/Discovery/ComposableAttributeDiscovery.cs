using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Compono.Generators.WellKnownTypes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Compono.Generators.Discovery;

/// <summary>
/// Finds <c>[Composable]</c> plan-generation requests, per
/// <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c>'s second discovery mechanism —
/// the opt-in marker for a type with no local <c>Create&lt;T&gt;()</c> call site. Two placements:
/// directly on a type declaration this compilation owns, or assembly-level
/// (<c>[assembly: Composable(typeof(SomeType))]</c>) for a type in a referenced assembly that
/// can't be annotated. Both feed <see cref="ComposedTypeAnalyzer"/>, so a <c>[Composable]</c>
/// request goes through exactly the same validation/selection/emission pipeline as a call site.
/// </summary>
internal static class ComposableAttributeDiscovery
{
    public const string AttributeMetadataName = "Compono.ComposableAttribute";

    public static TransitiveClosureResult TransformTypeLevel(
        GeneratorAttributeSyntaxContext context, bool testDoublesEnabled, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol annotatedType)
        {
            return new TransitiveClosureResult(
                EquatableArray<DiscoveredTypeInfo>.Empty, EquatableArray<DiscoveredCollectionInfo>.Empty, EquatableArray<DiscoveredTestDoubleInfo>.Empty);
        }

        var types = new List<DiscoveredTypeInfo>();
        var collections = new List<DiscoveredCollectionInfo>();
        var testDoubles = new List<DiscoveredTestDoubleInfo>();

        foreach (var attribute in context.Attributes)
        {
            // `[Composable(typeof(Other))]` on a type is a request for `Other`, same as the
            // assembly-level form; the bare `[Composable]` marker requests the annotated type
            // itself. An explicit `typeof` argument that doesn't resolve to a type symbol (e.g. a
            // literal null) also falls back to the annotated type - there's still an unambiguous
            // request here, unlike the assembly-level form where the argument is the only target.
            var requestedType = ExtractTypeArgument(attribute) ?? annotatedType;
            var location = LocationOf(attribute, annotatedType, cancellationToken);

            var result = ComposedTypeAnalyzer.Analyze(requestedType, context.SemanticModel.Compilation, location, testDoublesEnabled);
            types.AddRange(result.Types);
            collections.AddRange(result.Collections);
            testDoubles.AddRange(result.TestDoubles);
        }

        return new TransitiveClosureResult(types.ToEquatableArray(), collections.ToEquatableArray(), testDoubles.ToEquatableArray());
    }

    // Deliberately no name filter here - a syntax-only check can't see through an alias
    // (`using Marker = Compono.ComposableAttribute;` then `[assembly: Marker(typeof(Customer))]`),
    // and rejecting on the literal syntax name would silently drop that valid usage before
    // TransformAssemblyLevel's semantic check (which resolves aliases correctly via GetSymbolInfo)
    // ever runs. Assembly-level attributes are rare, so admitting every candidate here and letting
    // the semantic check downstream do the real filtering costs nothing meaningful.
    public static bool IsAssemblyCandidate(SyntaxNode node, CancellationToken cancellationToken) =>
        node is AttributeSyntax
        {
            Parent: AttributeListSyntax { Target.Identifier.RawKind: (int)SyntaxKind.AssemblyKeyword },
        };

    public static TransitiveClosureResult? TransformAssemblyLevel(
        GeneratorSyntaxContext context, bool testDoublesEnabled, CancellationToken cancellationToken)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol is not IMethodSymbol constructor)
            return null;

        var wellKnownTypes = WellKnownTypes.WellKnownTypes.GetOrCreate(context.SemanticModel.Compilation);

        if (!wellKnownTypes.IsType(constructor.ContainingType, WellKnownTypeData.WellKnownType.Compono_ComposableAttribute))
            return null;

        var location = LocationInfo.From(attribute);
        var requestedType = ExtractTypeArgument(attribute, context.SemanticModel, cancellationToken);

        // At assembly level there's no annotated type to fall back to - the `typeof` argument IS
        // the request, so its absence leaves nothing to generate a plan for and gets its own
        // diagnostic rather than a silent no-op.
        if (requestedType is null)
            return AssemblyComposableMissingTypeFailure(location);

        return ComposedTypeAnalyzer.Analyze(requestedType, context.SemanticModel.Compilation, location, testDoublesEnabled);
    }

    private static ITypeSymbol? ExtractTypeArgument(AttributeData attribute) =>
        attribute.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: ITypeSymbol type }] ? type : null;

    private static ITypeSymbol? ExtractTypeArgument(
        AttributeSyntax attribute, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var expression = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;

        // `[assembly: Composable((typeof(Customer)))]` (legal, redundant parens) wraps the argument
        // in a ParenthesizedExpressionSyntax - matching only a bare TypeOfExpressionSyntax would
        // silently miss this and report CMP0008 for an otherwise perfectly valid request.
        while (expression is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;

        return expression is TypeOfExpressionSyntax typeOf
            ? semanticModel.GetTypeInfo(typeOf.Type, cancellationToken).Type
            : null;
    }

    // Points diagnostics at the attribute application itself (`[Composable]`/
    // `[assembly: Composable(typeof(...))]`) - the request site, mirroring how call-site discovery
    // points at the `T` in `Create<T>()` rather than at T's declaration.
    private static LocationInfo? LocationOf(
        AttributeData attribute, INamedTypeSymbol annotatedType, CancellationToken cancellationToken) =>
        attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is { } syntax
            ? LocationInfo.From(syntax)
            : LocationInfo.From(annotatedType);

    private static TransitiveClosureResult AssemblyComposableMissingTypeFailure(LocationInfo? location)
    {
        // No requested type means there's nothing real to put in the type-identity fields - they go
        // unused anyway once Diagnostics is non-empty (ComponoIncrementalGenerator skips codegen
        // for any entry with diagnostics).
        var failure = new DiscoveredTypeInfo(
            Namespace: "",
            TypeName: "",
            FullyQualifiedName: "",
            EquatableArray<ConstructorParameterInfo>.Empty,
            EquatableArray<RequiredMemberInfo>.Empty,
            new[] { new DiagnosticInfo(DiagnosticDescriptors.AssemblyComposableMissingType, location) }.ToEquatableArray());

        return new TransitiveClosureResult(
            new[] { failure }.ToEquatableArray(), EquatableArray<DiscoveredCollectionInfo>.Empty, EquatableArray<DiscoveredTestDoubleInfo>.Empty);
    }
}
