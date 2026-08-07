namespace Compono.Benchmarks.Models;

/// <summary>
/// A model whose only member is a collection - per ADR-0034's Scalability category, its actual
/// element count is controlled at composition time via <c>WithCollectionSize(...)</c>, not fixed
/// on the type itself, so one model serves every collection-size data point in the matrix.
/// </summary>
public sealed record LargeCollection(List<string> Items);
