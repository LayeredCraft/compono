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

    public static EquatableArray<DiscoveredTypeInfo> TransformTypeLevel(
        GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol annotatedType)
            return EquatableArray<DiscoveredTypeInfo>.Empty;

        var results = new List<DiscoveredTypeInfo>();

        foreach (var attribute in context.Attributes)
        {
            // `[Composable(typeof(Other))]` on a type is a request for `Other`, same as the
            // assembly-level form; the bare `[Composable]` marker requests the annotated type
            // itself. An explicit `typeof` argument that doesn't resolve to a type symbol (e.g. a
            // literal null) also falls back to the annotated type - there's still an unambiguous
            // request here, unlike the assembly-level form where the argument is the only target.
            var requestedType = ExtractTypeArgument(attribute) ?? annotatedType;
            var location = LocationOf(attribute, annotatedType, cancellationToken);

            results.AddRange(ComposedTypeAnalyzer.Analyze(requestedType, context.SemanticModel.Compilation, location));
        }

        return results.ToEquatableArray();
    }

    public static bool IsAssemblyCandidate(SyntaxNode node, CancellationToken cancellationToken) =>
        node is AttributeSyntax
        {
            Parent: AttributeListSyntax { Target.Identifier.RawKind: (int)SyntaxKind.AssemblyKeyword },
        } attribute
        && UnqualifiedName(attribute.Name) is "Composable" or "ComposableAttribute";

    public static EquatableArray<DiscoveredTypeInfo>? TransformAssemblyLevel(
        GeneratorSyntaxContext context, CancellationToken cancellationToken)
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

        return ComposedTypeAnalyzer.Analyze(requestedType, context.SemanticModel.Compilation, location);
    }

    private static ITypeSymbol? ExtractTypeArgument(AttributeData attribute) =>
        attribute.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: ITypeSymbol type }] ? type : null;

    private static ITypeSymbol? ExtractTypeArgument(
        AttributeSyntax attribute, SemanticModel semanticModel, CancellationToken cancellationToken) =>
        attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf
            ? semanticModel.GetTypeInfo(typeOf.Type, cancellationToken).Type
            : null;

    // Points diagnostics at the attribute application itself (`[Composable]`/
    // `[assembly: Composable(typeof(...))]`) - the request site, mirroring how call-site discovery
    // points at the `T` in `Create<T>()` rather than at T's declaration.
    private static LocationInfo? LocationOf(
        AttributeData attribute, INamedTypeSymbol annotatedType, CancellationToken cancellationToken) =>
        attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is { } syntax
            ? LocationInfo.From(syntax)
            : LocationInfo.From(annotatedType);

    private static EquatableArray<DiscoveredTypeInfo> AssemblyComposableMissingTypeFailure(LocationInfo? location)
    {
        // No requested type means there's nothing real to put in the type-identity fields - they go
        // unused anyway once Diagnostics is non-empty (ComponoIncrementalGenerator skips codegen
        // for any entry with diagnostics).
        var failure = new DiscoveredTypeInfo(
            Namespace: "",
            TypeName: "",
            FullyQualifiedName: "",
            EquatableArray<ConstructorParameterInfo>.Empty,
            new[] { new DiagnosticInfo(DiagnosticDescriptors.AssemblyComposableMissingType, location) }.ToEquatableArray());

        return new[] { failure }.ToEquatableArray();
    }

    private static string? UnqualifiedName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
        _ => null,
    };
}
