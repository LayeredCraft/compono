using Compono.Generators.Diagnostics;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// A method parameter type reached through a <c>[Compose]</c>-family attribute that needs a
/// <c>RowInvokerRegistry.Register(...)</c> dispatch registration, per ADR-0041 Amendment 2 - recorded
/// independent of <see cref="TransitiveClosureResult.Types"/>, since that walk deliberately excludes
/// exactly the provider-resolved leaf types a row-binding dispatch mechanism needs most.
/// </summary>
/// <remarks>
/// <see cref="Diagnostics"/> is non-empty only for a dispatch-eligible-by-shape parameter type that
/// fails <see cref="Discovery.ComposedTypeAnalyzer"/>'s accessibility check (CMP0013) - a shape-
/// ineligible parameter type (a <see langword="ref"/> struct, a pointer) is never recorded as a
/// <see cref="RowInvokerTypeInfo"/> at all, silently excluded before this type is ever constructed.
/// </remarks>
internal sealed record RowInvokerTypeInfo(string FullyQualifiedTypeName, EquatableArray<DiagnosticInfo> Diagnostics);
