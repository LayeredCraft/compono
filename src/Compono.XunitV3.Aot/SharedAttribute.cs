namespace Compono.XunitV3.Aot;

/// <summary>
/// Marks a <c>[ComposeData]</c>-attributed test method parameter as shared under xUnit v3's Native
/// AOT pipeline - the AOT counterpart to <c>Compono.XunitV3.SharedAttribute</c>. Its generated value
/// is stored in the row's composition scope, so any other composed parameter or nested generated
/// dependency that structurally requests the same type in the same row reuses this exact value
/// instead of composing its own independent one.
/// </summary>
/// <remarks>
/// Like the other <c>Compono.XunitV3.Aot</c> marker attributes, this is a thin marker only - it is
/// discovered by <c>Compono.Generators</c> at compile time (matched on this type's own fully
/// qualified metadata name, never on assignability/inheritance) and carries no runtime composition
/// logic of its own. AOT consumers cannot reference <c>Compono.XunitV3.SharedAttribute</c> because
/// that package depends on the reflection-mode xUnit extensibility package this package exists to
/// avoid (ADR-0066), so this marker is defined in the AOT namespace instead (ADR-0070 Amendment 3).
/// <para>
/// Type-based only, matching the engine's existing shared-value scope key
/// (<c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>) - no
/// name/qualifier-based sharing. See ADR-0022's "[Shared] semantics" section for the full binding
/// rules.
/// </para>
/// <para>
/// Referencing this marker alone is not enough to make sharing work: the actual generated
/// <c>ShareExplicit&lt;T&gt;</c>/<c>ResolveShared&lt;T&gt;</c> behavior for AOT <c>[ComposeData]</c>
/// rows is produced by <c>Compono.Generators</c>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SharedAttribute : Attribute;
