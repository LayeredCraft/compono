namespace Compono.Benchmarks.Models;

/// <summary>
/// A flat, parameterless-dependency type - the simplest possible composition target, reused
/// across every ADR-0034 category that needs a "floor" model (no nested composable dependency,
/// no collection). Replaces the old suite's <c>Leaf</c>.
/// </summary>
public sealed record SimplePoco(string Name, int Count, bool IsActive);
