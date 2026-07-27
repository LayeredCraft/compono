using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Diagnostics;

/// <summary>
/// A cacheable stand-in for a reported <see cref="Diagnostic"/> - travels through the incremental
/// pipeline as data, materialized into a real <see cref="Diagnostic"/> only inside
/// <see cref="Microsoft.CodeAnalysis.SourceProductionContext"/>. See
/// <c>docs/adr/0005-generator-implementation-conventions.md</c>.
/// </summary>
internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo? Location = null, params object?[] MessageArgs)
{
    public bool Equals(DiagnosticInfo? other) =>
        other is not null
        && Descriptor.Id == other.Descriptor.Id
        && Equals(Location, other.Location)
        // MessageArgs has to participate in equality, not just Descriptor.Id/Location - otherwise
        // an ambiguous type going from 2 to 3 constructors (same location, same descriptor, different
        // {1} message argument) reads as "unchanged" to Roslyn's incremental caching, and a stale
        // constructor count keeps being reported.
        && MessageArgs.SequenceEqual(other.MessageArgs);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Descriptor.Id);
        hash.Add(Location);

        foreach (var arg in MessageArgs)
            hash.Add(arg);

        return hash.ToHashCode();
    }

    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location?.ToLocation(), MessageArgs);

    public void Report(SourceProductionContext context) => context.ReportDiagnostic(ToDiagnostic());
}
