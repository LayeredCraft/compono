namespace Compono.TUnit;

/// <summary>
/// Marks a <c>[Compose]</c>-attributed test method parameter as shared: its composed (or
/// inline-supplied) value is stored in the row's <see cref="CompositionRow"/> scope, so any other
/// composed parameter or nested generated dependency that structurally requests the same type in
/// the same row reuses this exact value instead of composing its own independent one.
/// </summary>
/// <remarks>
/// Type-based only, matching the engine's existing shared-value scope key
/// (<c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>) - no
/// name/qualifier-based sharing. See <c>docs/adr/0040-compono-tunit-package-design.md</c>'s
/// "Package shape" section - mirrors <c>Compono.XunitV3.SharedAttribute</c>'s binding rules
/// exactly (declaration order among <c>[Shared]</c> parameters, duplicate-type rejection,
/// visibility scoped to the current row only), duplicated rather than shared per that ADR's
/// binding-logic decision.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SharedAttribute : Attribute;
