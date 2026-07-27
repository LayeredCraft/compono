using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Walks a discovered type's constructor parameters recursively, per Phase 1 of
/// docs/plans/0001-milestone-1-source-generation-foundation.md: a parameter type only gets its own
/// generated plan if <see cref="LeafTypeClassifier"/> says it's eligible for generated composition
/// (a concrete, non-abstract, non-delegate/enum/built-in type) - everything else is left as a bare
/// <c>context.Resolve&lt;TParam&gt;()</c> call for a future <c>ICompositionContext</c>/provider to
/// handle. A concrete type that IS eligible but fails constructor selection (ambiguous, no
/// accessible constructor, unsupported parameter kind, unassigned required members) is diagnosed at
/// the original <c>Composer.Create&lt;T&gt;()</c> call site rather than silently left as
/// <c>Resolve&lt;TParam&gt;()</c> - that would hide an invalid generated graph and turn a
/// compile-time composition failure into a runtime one.
/// </summary>
internal static class TransitiveClosureWalker
{
    public static EquatableArray<DiscoveredTypeInfo> Walk(INamedTypeSymbol rootType, Compilation compilation, LocationInfo? location)
    {
        var wellKnownTypes = WellKnownTypes.WellKnownTypes.GetOrCreate(compilation);
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { rootType };
        var results = new List<DiscoveredTypeInfo>();
        var queue = new Queue<(INamedTypeSymbol Type, string? Path)>();

        queue.Enqueue((rootType, null));

        while (queue.Count > 0)
        {
            var (type, path) = queue.Dequeue();
            var (info, constructor) = Analyze(type, compilation, location, path);
            results.Add(info);

            if (constructor is null)
                continue;

            foreach (var parameter in constructor.Parameters)
                EnqueueIfEligible(parameter, type, path, wellKnownTypes, visited, queue);
        }

        return results.ToEquatableArray();
    }

    private static void EnqueueIfEligible(
        IParameterSymbol parameter,
        INamedTypeSymbol parentType,
        string? parentPath,
        WellKnownTypes.WellKnownTypes wellKnownTypes,
        HashSet<INamedTypeSymbol> visited,
        Queue<(INamedTypeSymbol Type, string? Path)> queue)
    {
        if (parameter.Type is not INamedTypeSymbol parameterType)
            return;

        if (LeafTypeClassifier.IsProviderResolved(parameterType, wellKnownTypes))
            return;

        if (!visited.Add(parameterType))
            return;

        var childPath = parentPath is null ? $"{parentType.Name}.{parameter.Name}" : $"{parentPath}.{parameter.Name}";

        queue.Enqueue((parameterType, childPath));
    }

    private static (DiscoveredTypeInfo Info, IMethodSymbol? Constructor) Analyze(
        INamedTypeSymbol type, Compilation compilation, LocationInfo? location, string? path)
    {
        // ContainingNamespace.ToDisplayString() returns the literal text "<global namespace>" for
        // a type with no namespace, not an empty string - IsGlobalNamespace is the actual check.
        var @namespace = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();

        // FullyQualifiedFormat emits `global::`-prefixed names - required for every type reference
        // emitted into generated code, per the "Generated code" coding-standards section.
        var emittedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var selection = ConstructorSelector.Select(type, compilation, location, path);

        if (!selection.IsSuccess)
        {
            var failure = new DiscoveredTypeInfo(
                @namespace,
                type.Name,
                emittedName,
                EquatableArray<ConstructorParameterInfo>.Empty,
                new[] { selection.Diagnostic! }.ToEquatableArray());

            return (failure, null);
        }

        var parameters = selection.Constructor!.Parameters
            .Select(p => new ConstructorParameterInfo(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToEquatableArray();

        var success = new DiscoveredTypeInfo(
            @namespace,
            type.Name,
            emittedName,
            parameters,
            EquatableArray<DiagnosticInfo>.Empty);

        return (success, selection.Constructor);
    }
}
