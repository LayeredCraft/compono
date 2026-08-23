using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// One method or property member of a generated test double, already validated as a supported shape
/// (an unsupported shape produces a <see cref="DiscoveredTestDoubleInfo.Diagnostics"/> entry instead
/// and never reaches this record). See ADR-0043's "Generated code shape".
/// </summary>
/// <param name="OriginalName">
/// The member's real, unescaped symbol name - used to derive <see cref="FieldName"/> (never a
/// reserved keyword once prefixed with <c>__</c>) and to compare against <c>System.Object</c>'s own
/// member names for the object-shadowing diagnostic.
/// </param>
/// <param name="EscapedName">
/// <c>@</c>-escaped per <c>RequiredMemberCollector.EscapeIdentifier</c>'s existing convention -
/// used everywhere this name is emitted as identifier syntax: the explicit interface implementation's
/// own member name (ADR-0043 Amendment 9, Finding V) and the configuration extension's method name
/// (Amendment 6, Finding O).
/// </param>
/// <param name="DeclaringInterfaceFullyQualifiedName">
/// The interface that actually declares this member - the leaf interface for its own members, or a
/// base interface for one only inherited through it (ADR-0043 Amendment 11, Finding Z). The explicit
/// interface implementation is qualified against this, not the leaf interface requested.
/// </param>
/// <param name="Kind">Whether this member came from an interface method or property.</param>
/// <param name="AccessorKind">
/// A property's write-accessor shape (<see langword="set"/> vs. <see langword="init"/> vs. get-only) -
/// not meaningful when <paramref name="Kind"/> is <see cref="TestDoubleMemberKind.Method"/>.
/// </param>
/// <param name="ReturnTypeFullyQualifiedName">
/// The member's return type (a method) or property type, fully qualified - unused when
/// <paramref name="IsVoid"/> is <see langword="true"/>.
/// </param>
/// <param name="IsVoid">
/// Whether the member is a <see langword="void"/>-returning method - its backing slot is
/// <c>ReturnConfig&lt;global::Compono.Unit&gt;</c> rather than <c>ReturnConfig&lt;ReturnTypeFullyQualifiedName&gt;</c>,
/// and dispatch never needs a default-value expression, only the configured-exception check.
/// </param>
/// <param name="DefaultExpression">
/// The deterministic-default C# expression for this member's return type - empty/unused when
/// <see cref="IsVoid"/> is <see langword="true"/>.
/// </param>
/// <param name="Parameters">
/// The member's method parameters - always empty for a <see cref="TestDoubleMemberKind.Property"/>.
/// </param>
/// <param name="HasConfigurationSurface">
/// Whether this member/overload actually gets a backing <c>ReturnConfig&lt;T&gt;</c> field and a
/// <c>Configure()</c>/<c>Verify()</c> extension (ADR-0044 Amendment 4). <see langword="false"/> for an
/// overload-set-internal-unsupported shape (a <see langword="ref"/>/<see langword="out"/>/
/// <see langword="in"/> parameter, ADR-0044 Amendment 5) or a diamond-colliding identity (two
/// different base interfaces independently declaring the same-named, same-shaped member, ADR-0044
/// Amendment 3 Finding 8) - either way the dispatch body still exists (an inline deterministic
/// default), it just has nothing to configure.
/// </param>
/// <param name="IsOverloaded">
/// Whether <paramref name="OriginalName"/> is shared by more than one member reaching this
/// interface's emitted double - drives whether <see cref="FieldName"/> needs
/// <paramref name="DiscriminatorSuffix"/> and whether a configuration extension takes real,
/// value-discarded parameters (mirroring the real overload, for ordinary C# overload resolution to
/// pick the right one) instead of the non-overloaded zero-argument form.
/// </param>
/// <param name="DiscriminatorSuffix">
/// An <c>_</c>-prefixed, hash-suffixed discriminator identifying this specific overload
/// (<see cref="Emitters.TestDoubleOverloadIdentity"/>) - empty when <paramref name="IsOverloaded"/> is
/// <see langword="false"/>.
/// </param>
/// <param name="OutParameterAssignments">
/// Full <c>name = defaultExpression;</c> assignment statements for every <see langword="out"/>
/// parameter, in declaration order - only non-empty for a <see cref="HasConfigurationSurface"/>
/// <see langword="false"/> method with an <see langword="out"/> parameter (ADR-0044 Amendment 8
/// Finding 20); every <see langword="out"/> parameter in a fallback body must be definitely assigned
/// before every return path (<c>CS0177</c> otherwise).
/// </param>
/// <param name="ExtensionReceiverName">
/// The configuration extension's <see langword="this"/>-receiver parameter name - <c>"self"</c>
/// unless <paramref name="IsOverloaded"/> is <see langword="true"/>, in which case it's chosen to
/// avoid colliding with any of this overload's own real parameter names (which, unlike synthetic
/// identifiers, are never guaranteed to avoid a leading-underscore convention - a real parameter can
/// be named <c>self</c> or even <c>__self</c>). Codex review, PR #88.
/// </param>
/// <param name="IsGenericMethod">
/// Whether this method declares its own type parameters (ADR-0044 Requirement 2) - never true for a
/// <see cref="TestDoubleMemberKind.Property"/>. Drives whether the explicit interface implementation
/// carries a <see cref="GenericSuffix"/> at all.
/// </param>
/// <param name="TypeParameterNames">
/// This method's own type parameter names, escaped, in declaration order - emitted on the explicit
/// interface implementation (<see cref="GenericSuffix"/>) whenever <see cref="IsGenericMethod"/> is
/// <see langword="true"/>, and, additionally, on the generated configuration/verification extension
/// only when <paramref name="IsOverloaded"/> is also <see langword="true"/> (ADR-0044 Amendment 1) -
/// a solo generic method's extension stays non-generic (Requirement 2's own rule: the slot type never
/// depends on the method's own type parameter).
/// </param>
/// <param name="ConstraintClauses">
/// Full <c>where T : ...</c> clause text, one per constrained type parameter - copied verbatim onto
/// the generated generic *extension* method only (ADR-0044 Amendment 2 Finding 2). Never emitted on
/// the explicit interface implementation, which inherits its constraints automatically and cannot
/// redeclare them (<c>CS0460</c>).
/// </param>
/// <param name="IsConfigurationRequired">
/// Whether this member has a real <see cref="HasConfigurationSurface"/> but no deterministic
/// <see cref="DefaultExpression"/> for its return type (ADR-0045) - its dispatch body's final
/// fallback throws <c>Compono.TestDoubleNotConfiguredException</c> instead of returning a computed
/// default. Never <see langword="true"/> when <see cref="HasConfigurationSurface"/> is
/// <see langword="false"/> or <see cref="IsVoid"/> is <see langword="true"/> - a combined-shape
/// member (a diamond collision, a zero-argument-extension collision, an overloaded
/// <see langword="ref"/>/<see langword="out"/>/<see langword="in"/> parameter, or a method-shaped
/// object-member collision) keeps the unchanged whole-interface <c>CMP0025</c> rejection instead.
/// </param>
/// <param name="IsEligibleForMatching">
/// Whether this member gets ADR-0048's argument-aware <c>Configure()</c>/<c>Verify()</c> surface
/// (<c>Compono.Match&lt;T&gt;</c>-typed parameters, per-parameter matcher fields, a call log) instead of v1/v2's
/// argument-independent one. Requires <see cref="HasConfigurationSurface"/>, at least one real
/// parameter (a zero-parameter member has nothing to match), not <see cref="IsOverloaded"/> (a real
/// compiler spike proved wrapping every overload's parameters in <c>Arg&lt;T&gt;</c> breaks C#
/// overload resolution unpredictably - ADR-0048's "Overload-discriminator interaction"), not
/// <see cref="IsClosedInstantiationEligible"/> (a mutually exclusive third classification - see that
/// parameter), no ref-like (<c>Span&lt;T&gt;</c>-shaped) real parameter (can never be used as a
/// generic type argument - <c>CS0306</c> - even though it dispatches fine via the argument-independent
/// path), no derived field-name collision (its own <c>_calls</c>/<c>_lock</c>/per-parameter matcher
/// field names would collide with another member's own field name), and - when
/// <see cref="IsGenericMethod"/> - no real parameter referencing the method's own open type parameter
/// (a per-member call log can't hold an open type parameter's value; the
/// <c>ILogger&lt;TState&gt;.Log</c> shape). An ineligible member generates its existing v1/v2/ADR-0044
/// shape, byte-for-byte unchanged.
/// </param>
/// <param name="IsClosedInstantiationEligible">
/// Whether this member gets ADR-0049's per-closed-<c>T</c> <c>Configure&lt;T&gt;()</c>/<c>Verify&lt;T&gt;()</c>
/// surface - a generic method whose return type is exactly its own sole type parameter <c>T</c>, or the
/// sole type argument of <c>Task&lt;T&gt;</c>/<c>Task&lt;T?&gt;</c>/<c>ValueTask&lt;T&gt;</c>/
/// <c>ValueTask&lt;T?&gt;</c>. A third classification, distinct from and mutually exclusive with
/// <see cref="IsEligibleForMatching"/> (ADR-0048's argument-matching surface, for a member whose
/// return type doesn't depend on its own type parameter at all) - both require
/// <see cref="HasConfigurationSurface"/>, but this one backs its state with a generator-emitted,
/// generic-in-<c>T</c> nested state class plus a <c>Dictionary&lt;Type, object&gt;</c> bucket keyed by
/// <c>typeof(T)</c> instead of a single <c>ReturnConfig&lt;T&gt;</c> field, since the storage location
/// itself varies per closed <c>T</c> a caller closes to at runtime. Unlike
/// <see cref="IsEligibleForMatching"/>, <see cref="IsOverloaded"/> does <b>not</b> exclude eligibility
/// here - an overloaded closed-instantiation-eligible member reuses ADR-0044's existing
/// overload-discriminator machinery unchanged (real, un-wrapped parameter types as the discriminator,
/// no <c>Compono.Match&lt;T&gt;</c> wrapping), the same disposition every other overloaded member
/// already has.
/// </param>
internal sealed record TestDoubleMemberInfo(
    string OriginalName,
    string EscapedName,
    string DeclaringInterfaceFullyQualifiedName,
    TestDoubleMemberKind Kind,
    TestDoublePropertyAccessorKind AccessorKind,
    string ReturnTypeFullyQualifiedName,
    bool IsVoid,
    string DefaultExpression,
    EquatableArray<TestDoubleParameterInfo> Parameters,
    bool HasConfigurationSurface = true,
    bool IsOverloaded = false,
    string DiscriminatorSuffix = "",
    EquatableArray<string> OutParameterAssignments = default,
    string ExtensionReceiverName = "self",
    bool IsGenericMethod = false,
    EquatableArray<string> TypeParameterNames = default,
    EquatableArray<string> ConstraintClauses = default,
    bool IsConfigurationRequired = false,
    bool IsEligibleForMatching = false,
    bool IsClosedInstantiationEligible = false)
{
    /// <summary>The backing <c>ReturnConfig&lt;T&gt;</c> field name - never a reserved keyword once <c>__</c>-prefixed.</summary>
    public string FieldName => IsOverloaded ? $"__{OriginalName}{DiscriminatorSuffix}" : $"__{OriginalName}";

    /// <summary>The type argument for this member's backing <c>ReturnConfig&lt;T&gt;</c> field.</summary>
    public string SlotTypeFullyQualifiedName => IsVoid ? "global::Compono.Unit" : ReturnTypeFullyQualifiedName;

    /// <summary><c>"&lt;T, U&gt;"</c> when <see cref="IsGenericMethod"/>, otherwise empty.</summary>
    public string GenericSuffix => IsGenericMethod ? $"<{string.Join(", ", TypeParameterNames)}>" : "";

    /// <summary>
    /// Whether the generated extension (not the explicit interface implementation) is itself generic -
    /// true for an overloaded generic member (ADR-0044 Amendment 1) or a
    /// <see cref="IsClosedInstantiationEligible"/> member (ADR-0049 - its <c>Configure&lt;T&gt;()</c>/
    /// <c>Verify&lt;T&gt;()</c> is generic whether or not it's also overloaded). A solo generic method
    /// that is neither stays non-generic per Requirement 2 (the slot type never depends on the method's
    /// own type parameter).
    /// </summary>
    public bool ExtensionIsGeneric => IsGenericMethod && (IsOverloaded || IsClosedInstantiationEligible);

    /// <summary>
    /// The generated nested state class name for this <see cref="IsClosedInstantiationEligible"/>
    /// member - <c>internal sealed class {FieldName}_State&lt;T&gt;</c> - holding a per-closed-<c>T</c>
    /// <c>ReturnConfig&lt;TSlot&gt;</c>, one matcher field per real parameter, and a lock-guarded call
    /// log (ADR-0049's Decision Outcome).
    /// </summary>
    public string ClosedInstantiationStateClassName => $"{FieldName}_State";

    /// <summary>
    /// The generated <c>Dictionary&lt;System.Type, object&gt;</c> bucket field name for this
    /// <see cref="IsClosedInstantiationEligible"/> member, keyed by <c>typeof(T)</c>.
    /// </summary>
    public string ClosedInstantiationBucketFieldName => $"{FieldName}_buckets";

    /// <summary>
    /// The generated lock-guarded bucket-lookup-or-create method name for this
    /// <see cref="IsClosedInstantiationEligible"/> member.
    /// </summary>
    public string ClosedInstantiationBucketMethodName => $"{FieldName}_Bucket";

    /// <summary>Space-joined <c>where</c> clauses, ready to splice after the extension's parameter list.</summary>
    public string ConstraintClausesText => ConstraintClauses.Count == 0 ? "" : " " + string.Join(" ", ConstraintClauses);
}
