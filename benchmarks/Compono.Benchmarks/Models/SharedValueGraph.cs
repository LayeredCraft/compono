namespace Compono.Benchmarks.Models;

/// <summary>A value shared across <see cref="ConsumerOne"/>/<see cref="ConsumerTwo"/> in one composition row - see <see cref="ConsumerOne"/>'s remarks.</summary>
public sealed record SharedContext(string CorrelationId);

/// <summary>
/// One of two sibling row values in ADR-0034's shared-value representative shape - each nests
/// <see cref="SharedContext"/> as an ordinary, unmarked constructor parameter. Composed via
/// <c>Composer.CreateRow</c>/<c>ResolveShared</c> (mirroring <c>Compono.XunitV3</c>'s
/// <c>[Shared]</c> mechanism at the core API level, without depending on that package): the row's
/// shared <see cref="SharedContext"/> instance is transparently reused by both consumers' nested
/// constructor parameter, per ADR-0021's unconditional-read-side scope check. Replaces the old
/// suite's complete lack of shared-value coverage.
/// </summary>
public sealed record ConsumerOne(SharedContext Context, string Label);

/// <summary>See <see cref="ConsumerOne"/>'s remarks.</summary>
public sealed record ConsumerTwo(SharedContext Context, int Sequence);
