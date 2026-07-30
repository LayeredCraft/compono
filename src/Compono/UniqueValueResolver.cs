namespace Compono;

/// <summary>
/// The bounded, deterministic duplicate-value retry helper a generated <c>HashSet&lt;T&gt;</c>/
/// <c>Dictionary&lt;TKey, TValue&gt;</c> collection plan calls once per element/key position, per
/// <c>docs/adr/0013-collection-generation-semantics.md</c> (bounded retry, then diagnosable failure)
/// and <c>docs/adr/0014-generator-emitted-collection-plans.md</c>
/// (generated code, not a runtime provider, builds collections).
/// </summary>
/// <remarks>
/// <see langword="public"/> for the same reason <see cref="CompositionRequestDescriptor"/>/
/// <see cref="CompositionRequestKind"/>/<see cref="ICompositionContext"/> already are: it's part of
/// the generated-code call surface, not the internal engine.
/// </remarks>
public static class UniqueValueResolver
{
    /// <summary>The bounded number of attempts before giving up on a unique value at one position.</summary>
    public const int MaxAttempts = 10;

    /// <summary>
    /// Attempts to resolve a value for <paramref name="position"/> that isn't already present in
    /// <paramref name="alreadyResolved"/>, retrying with a distinct, deterministic fork per attempt.
    /// A successful call both returns the unique value and leaves it already added to
    /// <paramref name="alreadyResolved"/>.
    /// </summary>
    /// <param name="context">The active composition context.</param>
    /// <param name="kind">
    /// <see cref="CompositionRequestKind.CollectionElement"/> for a <c>HashSet&lt;T&gt;</c> element,
    /// or <see cref="CompositionRequestKind.DictionaryKey"/> for a <c>Dictionary&lt;TKey, TValue&gt;</c>
    /// key.
    /// </param>
    /// <param name="position">The element/key's logical position in the collection being built.</param>
    /// <param name="nullability">Whether the requested value is nullable-annotated.</param>
    /// <param name="alreadyResolved">The set of values already resolved for this collection.</param>
    /// <param name="value">The resolved unique value, if this call returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="false"/> if <see cref="MaxAttempts"/> attempts were exhausted without producing
    /// a value not already in <paramref name="alreadyResolved"/> - the caller reports this as a
    /// <see cref="CompositionException"/> naming the value type and requested count, per ADR-0013.
    /// </returns>
    public static bool TryResolve<TValue>(
        ICompositionContext context,
        CompositionRequestKind kind,
        int position,
        Nullability nullability,
        HashSet<TValue> alreadyResolved,
        out TValue value)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = context.Resolve<TValue>(
                new CompositionRequestDescriptor(kind, RetryIndex(position, attempt), "", declaringType: null, nullability));

            if (alreadyResolved.Add(candidate))
            {
                value = candidate;
                return true;
            }
        }

        value = default!;
        return false;
    }

    // Attempt 0 forks identically to a plain, non-retried resolution at this position - tuning
    // MaxAttempts never perturbs a value that never collides. Every retry (attempt >= 1) forks from a
    // negative index, a space that never overlaps any position's own non-negative base index, so a
    // retry never coincidentally reproduces a sibling position's exact fork state.
    private static int RetryIndex(int position, int attempt) =>
        attempt == 0 ? position : -((position * MaxAttempts) + attempt);
}
