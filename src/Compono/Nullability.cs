namespace Compono;

/// <summary>
/// Whether a value requested from <see cref="ICompositionContext.Resolve{TValue}(in CompositionRequestDescriptor)"/> came from a
/// nullable-annotated parameter or required member (a <c>string?</c>, not a <c>string</c>).
/// </summary>
/// <remarks>
/// Reference-type nullable annotations are erased from the runtime generic type argument -
/// <c>Resolve&lt;string&gt;()</c> and a hypothetical <c>Resolve&lt;string?&gt;()</c> are the same
/// closed generic method at runtime - so a generated <see cref="ICompositionPlan{T}"/> passes this
/// explicitly rather than relying on the requested type argument to carry it. See
/// <c>docs/adr/0006-required-members-and-nullability-metadata.md</c>.
/// </remarks>
public enum Nullability
{
    /// <summary>The requested parameter or member is not nullable-annotated.</summary>
    NotNullable,

    /// <summary>The requested parameter or member is nullable-annotated.</summary>
    Nullable,
}
