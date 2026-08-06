namespace Compono.Benchmarks.Models;

/// <summary>An 8-level-deep chain of composable types, per ADR-0034's Scalability category (shallow vs. deep graphs). See <see cref="DeepGraph"/>'s remarks.</summary>
public sealed record DeepLevel8(string Value);

/// <summary>See <see cref="DeepGraph"/>'s remarks.</summary>
public sealed record DeepLevel7(DeepLevel8 Child);

/// <summary>See <see cref="DeepGraph"/>'s remarks.</summary>
public sealed record DeepLevel6(DeepLevel7 Child);

/// <summary>See <see cref="DeepGraph"/>'s remarks.</summary>
public sealed record DeepLevel5(DeepLevel6 Child);

/// <summary>See <see cref="DeepGraph"/>'s remarks.</summary>
public sealed record DeepLevel4(DeepLevel5 Child);

/// <summary>See <see cref="DeepGraph"/>'s remarks.</summary>
public sealed record DeepLevel3(DeepLevel4 Child);

/// <summary>See <see cref="DeepGraph"/>'s remarks.</summary>
public sealed record DeepLevel2(DeepLevel3 Child);

/// <summary>
/// The deep-graph representative model's root, per ADR-0034: an 8-level chain of single-field
/// composable types, deep enough (~48 trace entries at its deepest point) to exceed
/// <c>CompositionTraceBuffer</c>'s 32-entry initial capacity and trigger a real
/// <c>Array.Resize</c> - unlike <see cref="MediumAggregate"/>'s shallow, 2-level graph. Replaces
/// the old suite's <c>DeepLevel1</c>-<c>DeepLevel8</c> one-off benchmark types.
/// </summary>
public sealed record DeepGraph(DeepLevel2 Child);
