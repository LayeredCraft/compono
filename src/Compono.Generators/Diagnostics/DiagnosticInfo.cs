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
        && Equals(Location, other.Location);

    public override int GetHashCode() => HashCode.Combine(Descriptor.Id, Location);

    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location?.ToLocation(), MessageArgs);

    public void Report(SourceProductionContext context) => context.ReportDiagnostic(ToDiagnostic());
}
