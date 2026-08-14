using Compono.Generators.Diagnostics;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// A generated-test-double-eligible interface leaf (ADR-0043) reached in a discovered type's
/// transitive graph, plus everything needed to emit its double/configuration/<c>Configure()</c>
/// bridge/module initializer - or, if <see cref="Diagnostics"/> is non-empty, why it can't be
/// emitted (that leaf still defers to the unchanged runtime-provider path unchanged).
/// </summary>
/// <param name="InterfaceFullyQualifiedName">The interface leaf's fully qualified, <c>global::</c>-prefixed name.</param>
/// <param name="SafeIdentifier">
/// The identifier-safe, hash-suffixed name shared by every type this interface's double needs
/// (<c>{SafeIdentifier}_Double</c>, <c>{SafeIdentifier}_DoubleConfiguration</c>, ...) - produced by
/// <c>Emitters.TestDoubleIdentifierNaming</c>, ADR-0043 Amendment 5, Finding J.
/// </param>
/// <param name="Members">
/// Every supported member the double must implement - empty when <paramref name="Diagnostics"/> is
/// non-empty, since analysis stops at the first unsupported shape found (fail-fast, matching
/// <c>RequiredMemberCollector</c>/<c>ConstructorSelector</c>'s existing convention).
/// </param>
/// <param name="Diagnostics">
/// Non-empty only when this interface can't get a generated double at all (an inaccessible interface,
/// an unsupported member shape, a naming collision, ...) - the leaf still defers entirely to the
/// unchanged runtime-provider path in that case.
/// </param>
/// <param name="InfoDiagnostics">
/// Non-blocking, per-member/per-overload diagnostics reported alongside a double that still gets
/// emitted (ADR-0044 Amendment 3/5) - a diamond-colliding identity or an overload-set-internal
/// unsupported shape (<see langword="ref"/>/<see langword="out"/>/<see langword="in"/>) withholds
/// that one member's <c>Configure()</c>/<c>Verify()</c> surface without rejecting the whole
/// interface, unlike <paramref name="Diagnostics"/>.
/// </param>
internal sealed record DiscoveredTestDoubleInfo(
    string InterfaceFullyQualifiedName,
    string SafeIdentifier,
    EquatableArray<TestDoubleMemberInfo> Members,
    EquatableArray<DiagnosticInfo> Diagnostics,
    EquatableArray<DiagnosticInfo> InfoDiagnostics = default);
