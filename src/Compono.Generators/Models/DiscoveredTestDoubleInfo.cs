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
internal sealed record DiscoveredTestDoubleInfo(
    string InterfaceFullyQualifiedName,
    string SafeIdentifier,
    EquatableArray<TestDoubleMemberInfo> Members,
    EquatableArray<DiagnosticInfo> Diagnostics);
