namespace Compono;

/// <summary>
/// A structural-position-keyed source of deterministic pseudo-random values for one node of a
/// composition operation.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>'s
/// reproducibility contract, a resolved value's random identity is derived exclusively from its
/// structural position in the composition graph - the chain of <see cref="PathSegment"/> tags and
/// ordinals/indices from the root - and never from <see cref="CompositionRequest.RequestedType"/> or
/// any other type identity.
/// </remarks>
internal interface IRandomSource
{
    /// <summary>
    /// Derives the child node's independent random source from this node's fork state and the given
    /// path segment's structural identity (its per-kind tag plus <c>Ordinal</c>/<c>Index</c> - never
    /// <c>Name</c>).
    /// </summary>
    IRandomSource Fork(PathSegment segment);

    /// <summary>Produces the next pseudo-random 64-bit value for this node's own value stream.</summary>
    ulong NextUInt64();

    /// <summary>
    /// Derives a stable 64-bit value from this node's own fork state and a fixed salt distinct from
    /// <see cref="Fork"/>'s own per-<see cref="PathSegment"/>-kind tags. Never reads or mutates the
    /// value-state <see cref="NextUInt64"/> advances, so repeated calls for the same node return the
    /// same result and never perturb that node's own value stream. Backs
    /// <see cref="ICompositionContext.DeriveSeed"/> - see
    /// <c>docs/adr/0026-deterministic-seed-derivation-for-providers.md</c>.
    /// </summary>
    ulong DeriveSeed();
}
