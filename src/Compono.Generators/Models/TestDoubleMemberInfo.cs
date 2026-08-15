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
    string ExtensionReceiverName = "self")
{
    /// <summary>The backing <c>ReturnConfig&lt;T&gt;</c> field name - never a reserved keyword once <c>__</c>-prefixed.</summary>
    public string FieldName => IsOverloaded ? $"__{OriginalName}{DiscriminatorSuffix}" : $"__{OriginalName}";

    /// <summary>The type argument for this member's backing <c>ReturnConfig&lt;T&gt;</c> field.</summary>
    public string SlotTypeFullyQualifiedName => IsVoid ? "global::Compono.Unit" : ReturnTypeFullyQualifiedName;
}
