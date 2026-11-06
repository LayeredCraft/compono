using Compono.Generators.Diagnostics;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// The kind of member a <c>[ComposeData]</c> member name resolved to (ADR-0070). Recorded separately
/// from the member's own call target so the later emission phase (B5) can pick the right C# access
/// syntax; discovery itself never emits code.
/// </summary>
internal enum AotComposeDataMemberKind
{
    Field,
    Property,
    Method,
}

/// <summary>
/// The shape of a resolved <c>[ComposeData]</c> member's declared return type (ADR-0070), mirroring
/// <c>Compono.XunitV3.ComposeDataAttribute</c>'s runtime <c>MemberAccessor</c> classification.
/// </summary>
internal enum AotComposeDataReturnShape
{
    /// <summary>A synchronous <see cref="System.Collections.IEnumerable"/> result.</summary>
    Sync,

    /// <summary>A <c>Task&lt;T&gt;</c> whose awaited value is the collection.</summary>
    TaskOf,

    /// <summary>A <c>ValueTask&lt;T&gt;</c> whose awaited value is the collection.</summary>
    ValueTaskOf,

    /// <summary>An <c>IAsyncEnumerable&lt;T&gt;</c> result.</summary>
    AsyncEnumerable,
}

/// <summary>
/// One parameter of an <see cref="AotComposeDataMethodInfo"/> test method, in declaration order -
/// everything the (B5) generated factory closure needs to bind a row's leading-positional value to a
/// parameter with a compile-time-known type.
/// </summary>
/// <param name="Name">The parameter's own name, as declared.</param>
/// <param name="FullyQualifiedTypeName">The parameter type, <c>global::</c>-qualified.</param>
/// <param name="Ordinal">The parameter's zero-based position in the method signature.</param>
/// <param name="IsNullable">
/// Whether the parameter type is nullable in the ADR-0070 sense (<c>Nullable&lt;T&gt;</c>, or a
/// reference type annotated <c>?</c>) - drives strict row-value assignability at runtime.
/// </param>
/// <param name="IsShared">
/// Whether the parameter carries <c>[Shared]</c> - two parameters of the same type both marked
/// <c>[Shared]</c> is rejected (CMP0040 twin), since a single row cannot supply two distinct shared
/// instances.
/// </param>
internal sealed record AotComposeDataParameterInfo(
    string Name,
    string FullyQualifiedTypeName,
    int Ordinal,
    bool IsNullable,
    bool IsShared);

/// <summary>
/// The presence-tracked xUnit metadata a <c>[ComposeData]</c> attribute carries, inherited from
/// <c>Xunit.v3.DataAttribute</c> (ADR-0070). Every field is <see langword="null"/> (or empty for
/// <see cref="Traits"/>) exactly when the corresponding named argument was not supplied, matching the
/// JIT side's own presence semantics - so a later emission phase can tell "not set" from "set to the
/// type's default".
/// </summary>
/// <param name="Seed">
/// The effective <c>Seed</c> named argument, or <see langword="null"/> when absent. An explicit
/// <c>Seed = 0</c> is preserved as <c>0</c>; a negative seed is stored as-is (the negative-seed
/// rejection is a runtime concern, not a compile-time diagnostic - ADR-0070).
/// </param>
/// <param name="Label">The <c>Label</c> named argument, or <see langword="null"/> when absent.</param>
/// <param name="Skip">The <c>Skip</c> named argument, or <see langword="null"/> when absent.</param>
/// <param name="TestDisplayName">The <c>TestDisplayName</c> named argument, or <see langword="null"/> when absent.</param>
/// <param name="Explicit">The <c>Explicit</c> named argument, or <see langword="null"/> when absent.</param>
/// <param name="Timeout">The <c>Timeout</c> named argument, or <see langword="null"/> when absent.</param>
/// <param name="Traits">
/// The <c>Traits</c> named argument's flattened key/value pairs, or empty when absent/empty (matching
/// the JIT side's own "null when absent or empty" normalization).
/// </param>
/// <param name="DisableParallelization">The <c>DisableParallelization</c> named argument, or <see langword="null"/> when absent.</param>
internal sealed record AotComposeDataAttributeMetadata(
    int? Seed,
    string? Label,
    string? Skip,
    string? TestDisplayName,
    bool? Explicit,
    int? Timeout,
    EquatableArray<string> Traits,
    bool? DisableParallelization);

/// <summary>
/// One validated, active condition-skip binding (<c>SkipUnless</c>/<c>SkipWhen</c>) for a
/// <c>[ComposeData]</c> method: the property's own declaring holder and name, resolved against the
/// attribute's <c>SkipType</c> or the test method's declaring class (ADR-0070 / ADR-0071's B4.9
/// active-only rule).
/// </summary>
/// <param name="FullyQualifiedHolderTypeName">The holder type declaring the static <see langword="bool"/> property.</param>
/// <param name="PropertyName">The property's own name.</param>
internal sealed record AotComposeDataSkipCondition(string FullyQualifiedHolderTypeName, string PropertyName);

/// <summary>
/// One <c>Compono.XunitV3.Aot.ComposeDataAttribute</c>-family-attributed test method, fully validated
/// for Native AOT (ADR-0070/ADR-0071's B4). A method with a non-empty <see cref="Diagnostics"/>
/// produces no AOT theory-data-row registration - the execution emission itself is the later B5 phase;
/// B4 owns only discovery, validation, and this model.
/// </summary>
/// <remarks>
/// Every property is a string/bool/int?/<see cref="EquatableArray{T}"/> - no retained Roslyn
/// <c>ISymbol</c>, <c>Location</c>, or <c>Compilation</c> - so the whole model is value-equatable and
/// incremental-caching-friendly. Fully-qualified type names are <c>global::</c>-prefixed; the runtime
/// display names a B5 diagnostic/failure message needs (e.g. <c>{M}</c>/<c>{T}</c>) are reconstructed
/// there from these names, not stored here.
/// </remarks>
/// <param name="AttributeArity">
/// <c>0</c> for the non-generic <c>[ComposeData]</c> form, <c>1</c> for <c>[ComposeData&lt;TProfile&gt;]</c>,
/// <c>2</c> for <c>[ComposeData&lt;TProfile, TConfig&gt;]</c>.
/// </param>
/// <param name="FullyQualifiedTestClassName">The test method's own declaring type, <c>global::</c>-qualified.</param>
/// <param name="MethodName">The test method's own name.</param>
/// <param name="FullyQualifiedLookupTypeName">
/// The type the member was resolved against: the attribute's <c>MemberType</c> when supplied, otherwise
/// <see cref="FullyQualifiedTestClassName"/>.
/// </param>
/// <param name="FullyQualifiedProfileTypeName"><c>TProfile</c>, or <see langword="null"/> for the non-generic form.</param>
/// <param name="FullyQualifiedConfigTypeName"><c>TConfig</c>, or <see langword="null"/> for the <c>0</c>-/<c>1</c>-arity forms.</param>
/// <param name="MemberName">
/// The attribute's <c>MemberName</c> value. Always present for the marker attributes' own constructor,
/// but retained as nullable because an unresolved or empty name is itself a diagnostic case.
/// </param>
/// <param name="MemberKind">The resolved member's kind, or <see langword="null"/> when unresolved.</param>
/// <param name="FullyQualifiedMemberContainingTypeName">
/// The type that actually declares the selected member - the generated call target, which may differ
/// from <see cref="FullyQualifiedLookupTypeName"/> (an inherited member's call binds to its declaring
/// base). <see langword="null"/> when unresolved.
/// </param>
/// <param name="ReturnShape">The resolved member's return shape, or <see langword="null"/> when unresolved/rejected.</param>
/// <param name="RowsAreTyped">
/// Whether the row element type is (or derives from) <c>TheoryData&lt;...&gt;</c>/<c>TheoryDataRow&lt;...&gt;</c>,
/// so the row layout is known statically and typed-row assignability (CMP0055/CMP0056) can be validated.
/// </param>
/// <param name="RowElementTypeNames">
/// For a typed row, the ordered element type names carried by the row (the <c>TheoryData</c>/
/// <c>TheoryDataRow</c> generic arguments); empty for an untyped row.
/// </param>
/// <param name="Parameters">The method's ordinary parameters, in declaration order.</param>
/// <param name="AttributeMetadata">The presence-tracked xUnit attribute metadata.</param>
/// <param name="SkipUnless">The validated active <c>SkipUnless</c> binding, or <see langword="null"/> when inactive/absent.</param>
/// <param name="SkipWhen">The validated active <c>SkipWhen</c> binding, or <see langword="null"/> when inactive/absent.</param>
/// <param name="Diagnostics">
/// Every diagnostic produced for this method. Non-empty means no AOT registration is emitted for it.
/// </param>
internal sealed record AotComposeDataMethodInfo(
    int AttributeArity,
    string FullyQualifiedTestClassName,
    string MethodName,
    string FullyQualifiedLookupTypeName,
    string? FullyQualifiedProfileTypeName,
    string? FullyQualifiedConfigTypeName,
    string? MemberName,
    AotComposeDataMemberKind? MemberKind,
    string? FullyQualifiedMemberContainingTypeName,
    AotComposeDataReturnShape? ReturnShape,
    bool RowsAreTyped,
    EquatableArray<string> RowElementTypeNames,
    EquatableArray<AotComposeDataParameterInfo> Parameters,
    AotComposeDataAttributeMetadata AttributeMetadata,
    AotComposeDataSkipCondition? SkipUnless,
    AotComposeDataSkipCondition? SkipWhen,
    EquatableArray<DiagnosticInfo> Diagnostics);
