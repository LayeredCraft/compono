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
    // SymbolDisplayFormat.FullyQualifiedFormat alone omits the `?` nullable-reference-type modifier -
    // every type reference emitted into generated code needs it too, or a member declared to return
    // e.g. `Task<string?>` gets emitted as `Task<string>`, and the generated default-value expression
    // (a legitimate `null` for the nullable case) ends up assigned to a declared-non-nullable slot,
    // producing spurious nullable warnings in the consumer's own build. Used anywhere a *type argument*
    // gets interpolated into emitted code text (TestDoubleAnalyzer uses the same format for member
    // return/parameter/slot types). PR #83 review round 5.
    internal static readonly SymbolDisplayFormat NullableAwareFullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static bool TryGetDefaultExpression(ITypeSymbol type, Compilation compilation, out string expression)
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
            // Verified by identity (TaskWellKnownTypes), not by namespace/simple-name/arity alone - a
            // consumer's own type reopening the "System.Threading.Tasks" namespace with their own
            // same-named Task/Task<T>/ValueTask<T> (nested, top-level, whatever) would otherwise be
            // misidentified as the real BCL type here too, exactly like the closed-instantiation
            // eligibility check this same helper resolves for. This was flagged as a pre-existing,
            // out-of-scope latent issue in an earlier round of this PR's own review responses - Codex
            // review, PR #107 round 7 correctly pushed back on that framing: this code path is reached
            // by ANY member (not just a closed-instantiation-eligible one) whose declared return type
            // is Task/Task<T>/ValueTask<T>, so it was never actually gated behind ADR-0049's own new
            // eligibility check at all, and is exactly as real and reachable as the identical bug in
            // TestDoubleAnalyzer's own IsClosedInstantiationEligibleReturnShape. The non-generic
            // ValueTask branch below needs no equivalent check - its own default expression is the
            // bare "default" literal, which references no type by name and target-types correctly
            // against whatever the explicit implementation's own declared return type is, real or
            // shadowed alike.
            var taskTypes = TaskWellKnownTypes.GetOrCreate(compilation);

            if (taskTypes.IsTask(named) && named.TypeArguments.Length == 0)
            {
                expression = "global::System.Threading.Tasks.Task.CompletedTask";
                return true;
            }

            if (taskTypes.IsTaskOfT(named) && named.TypeArguments.Length == 1)
            {
                if (!TryGetDefaultExpression(named.TypeArguments[0], compilation, out var inner))
                {
                    expression = "";
                    return false;
                }

                var typeArgument = named.TypeArguments[0].ToDisplayString(NullableAwareFullyQualifiedFormat);
                expression = $"global::System.Threading.Tasks.Task.FromResult<{typeArgument}>({inner})";
                return true;
            }

            if (named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
                named.Name == "ValueTask" && named.TypeArguments.Length == 0)
            {
                expression = "default";
                return true;
            }

            if (taskTypes.IsValueTaskOfT(named) && named.TypeArguments.Length == 1)
            {
                if (!TryGetDefaultExpression(named.TypeArguments[0], compilation, out var inner))
                {
                    expression = "";
                    return false;
                }

                // ValueTask.FromResult<TResult>(TResult), not `new ValueTask<TResult>(inner)` - the
                // constructor is overloaded (ValueTask<TResult>(TResult result) and
                // ValueTask<TResult>(Task<TResult> task)), and `inner` is frequently the bare
                // `default` literal (any nullable-annotated reference or value-type TResult), which
                // converts to BOTH parameter types with no better-conversion tie-breaker between them
                // - a real CS0121 ambiguous-call compiler error in generated code, not a runtime bug.
                // The static factory method has only one applicable overload for any TResult, so it's
                // never ambiguous regardless of what `inner` evaluates to. Mirrors the Task<T> branch
                // above, which already uses the equivalent unambiguous Task.FromResult<T>(...) shape.
                // Codex review, PR #107 (round 5) - this was latent, pre-existing, reachable by any
                // defaultable ValueTask<T> member, but never exercised by an existing test until
                // ADR-0049 made ValueTask<T>/ValueTask<T?> the return type of a self-referencing
                // generic member for the first time.
                var typeArgument = named.TypeArguments[0].ToDisplayString(NullableAwareFullyQualifiedFormat);
                expression = $"global::System.Threading.Tasks.ValueTask.FromResult<{typeArgument}>({inner})";
                return true;
            }

            // IDictionary<TKey, TValue>/IReadOnlyDictionary<TKey, TValue> aren't "constructible
            // collection types" under C#'s collection-expression rules (unlike concrete
            // Dictionary<TKey, TValue> and the other interfaces below) - `[]` targeting either
            // produces CS9174, "type is not constructible". A concrete empty Dictionary is assignable
            // to both, so construct one explicitly instead of using the shared `[]` literal below.
            // Verified directly with a real compile spike before fixing. PR #83 review round 5.
            if (IsDictionaryInterfaceShape(named))
            {
                var keyType = named.TypeArguments[0].ToDisplayString(NullableAwareFullyQualifiedFormat);
                var valueType = named.TypeArguments[1].ToDisplayString(NullableAwareFullyQualifiedFormat);
                expression = $"new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>()";
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

    private static bool IsDictionaryInterfaceShape(INamedTypeSymbol type)
    {
        var originalDefinition = type.OriginalDefinition.ToDisplayString();

        return originalDefinition is
            "System.Collections.Generic.IDictionary<TKey, TValue>" or
            "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>";
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
            "System.Collections.Generic.List<T>" or
            "System.Collections.Generic.HashSet<T>" or
            "System.Collections.Generic.Dictionary<TKey, TValue>";
    }
}
