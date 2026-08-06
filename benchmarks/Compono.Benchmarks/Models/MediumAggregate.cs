namespace Compono.Benchmarks.Models;

/// <summary>
/// A nested composable dependency of <see cref="MediumAggregate"/> - see that type's remarks.
/// </summary>
public sealed record Address(string Street, string City);

/// <summary>
/// The canonical "representative graph" model, per ADR-0034: one nested composable dependency
/// (<see cref="Address"/>), every built-in kind via <c>string</c>, and a collection member -
/// reused across every ADR-0034 category that needs a realistic, moderately-nested type, instead
/// of each category inventing its own. Replaces the old suite's <c>Customer</c>/<c>Address</c>.
/// </summary>
public sealed record MediumAggregate(string FirstName, string LastName, Address HomeAddress, List<string> Tags);
