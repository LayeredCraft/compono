using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Deterministic-default C# expressions for a generated test-double member's return type, per
/// ADR-0043's "Deterministic defaults": primitives, nullable references, <c>Task</c>/<c>Task&lt;T&gt;</c>,
/// <c>ValueTask</c>/<c>ValueTask&lt;T&gt;</c>, and empty collections (never <see langword="null"/>, even
/// when the collection type itself is nullable-annotated - <c>List&lt;int&gt;?</c> still gets <c>[]</c>,
/// not <see langword="null"/>).
/// A non-nullable reference return has no deterministic default at all (Amendment 5, Finding K) - the
/// caller diagnoses and rejects rather than emitting <see langword="null"/> or attempting real
/// composition.
/// </summary>
internal static class TestDoubleDefaults
{
    public static bool TryGetDefaultExpression(ITypeSymbol type, out string expression)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            expression = "default";
            return true;
        }

        // Task/ValueTask checked before the generic IsValueType fallback below - ValueTask/ValueTask<T>
        // are themselves structs, so IsValueType would otherwise short-circuit to a bare `default`
        // before this branch ever ran, silently returning a ValueTask wrapping `default(T)` (null for a
        // reference T) instead of respecting T's own deterministic default or the non-nullable-reference
        // diagnostic. PR #83 review round 2.
        if (type is INamedTypeSymbol named)
        {
            var isSystemThreadingTasks = named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";

            if (isSystemThreadingTasks && named.Name == "Task" && named.TypeArguments.Length == 0)
            {
                expression = "global::System.Threading.Tasks.Task.CompletedTask";
                return true;
            }

            if (isSystemThreadingTasks && named.Name == "Task" && named.TypeArguments.Length == 1)
            {
                if (!TryGetDefaultExpression(named.TypeArguments[0], out var inner))
                {
                    expression = "";
                    return false;
                }

                var typeArgument = named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                expression = $"global::System.Threading.Tasks.Task.FromResult<{typeArgument}>({inner})";
                return true;
            }

            if (isSystemThreadingTasks && named.Name == "ValueTask" && named.TypeArguments.Length == 0)
            {
                expression = "default";
                return true;
            }

            if (isSystemThreadingTasks && named.Name == "ValueTask" && named.TypeArguments.Length == 1)
            {
                if (!TryGetDefaultExpression(named.TypeArguments[0], out var inner))
                {
                    expression = "";
                    return false;
                }

                var typeArgument = named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                expression = $"new global::System.Threading.Tasks.ValueTask<{typeArgument}>({inner})";
                return true;
            }

            // A known enumerable/collection shape - "empty collections never null" (ADR-0043's
            // "Deterministic defaults"), even when the collection type itself is nullable-annotated
            // (`List<int>?`) - checked before the nullable-annotation fallback below, which would
            // otherwise emit `null` for one instead. A collection expression target-types to any of
            // these (array, List<T>, and the BCL collection interfaces), so one literal covers every
            // shape. PR #83 review round 2.
            if (IsKnownCollectionShape(named))
            {
                expression = "[]";
                return true;
            }
        }

        // Checked before the nullable-annotation fallback below for the same "empty collections never
        // null" reason as the named-collection-shape check above. Only a rank-1 array target-types to a
        // `[]` collection expression - a rank-2+ array (`int[,]`) has no such literal and falls through
        // to the unsupported-return-shape diagnostic instead of emitting invalid generated code.
        // PR #83 review round 2.
        if (type is IArrayTypeSymbol { Rank: 1 })
        {
            expression = "[]";
            return true;
        }

        if (type is IArrayTypeSymbol)
        {
            expression = "";
            return false;
        }

        if (type.IsValueType)
        {
            expression = "default";
            return true;
        }

        // A nullable-annotated reference (`string?`, `Customer?`) - `default` is `null`, and that's
        // a legal value for it.
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            expression = "default";
            return true;
        }

        // A non-nullable reference type with no recognized deterministic default - diagnose and
        // reject rather than emit `null` (violates the interface's own nullable annotation) or
        // attempt real composition (out of scope). See Amendment 5, Finding K.
        expression = "";
        return false;
    }

    private static bool IsKnownCollectionShape(INamedTypeSymbol type)
    {
        var originalDefinition = type.OriginalDefinition.ToDisplayString();

        return originalDefinition is
            "System.Collections.Generic.IEnumerable<T>" or
            "System.Collections.Generic.IReadOnlyCollection<T>" or
            "System.Collections.Generic.IReadOnlyList<T>" or
            "System.Collections.Generic.ICollection<T>" or
            "System.Collections.Generic.IList<T>" or
            "System.Collections.Generic.List<T>";
    }
}
