namespace Compono.Benchmarks;

/// <summary>
/// The nested-composable-type + built-in + collection shape
/// <c>docs/plans/0002-milestone-2-core-composition-engine.md</c>'s Execution Flow section and
/// Phase 4 benchmark task use as "a representative graph" - the first point in that plan a type
/// exists that's worth benchmarking resolution against, rather than just construction dispatch
/// (<see cref="ArchitectureBenchmarks"/>'s <see cref="Leaf"/>).
/// </summary>
public sealed record Address(string Street, string City);

/// <summary>See <see cref="Address"/>'s remarks.</summary>
public sealed record Customer(string FirstName, string LastName, Address HomeAddress, List<string> Tags);
