using Compono.Generators.Diagnostics;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// One <c>Compono.XunitV3.Aot.ComposeAttribute</c>-attributed test method - per ADR-0066/PLAN-0066,
/// this drives a generated <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c>
/// registration, one per method (not deduped by parameter type the way <see cref="RowInvokerTypeInfo"/>
/// is - each method needs its own factory closure, in its own declared parameter order).
/// </summary>
internal sealed record AotComposeMethodInfo(
    string FullyQualifiedTestClassName,
    string MethodName,
    EquatableArray<AotComposeParameterInfo> Parameters,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>
/// One parameter of an <see cref="AotComposeMethodInfo"/> method, in declaration order - everything
/// the generated factory closure needs to emit a <c>row.Resolve&lt;T&gt;(descriptor)</c> call with a
/// compile-time-known <c>T</c>.
/// </summary>
internal sealed record AotComposeParameterInfo(
    string Name,
    string FullyQualifiedTypeName,
    int Ordinal,
    bool IsNullable);
