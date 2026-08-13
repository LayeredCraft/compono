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
internal sealed record TestDoubleMemberInfo(
    string OriginalName,
    string EscapedName,
    string DeclaringInterfaceFullyQualifiedName,
    TestDoubleMemberKind Kind,
    TestDoublePropertyAccessorKind AccessorKind,
    string ReturnTypeFullyQualifiedName,
    bool IsVoid,
    string DefaultExpression,
    EquatableArray<TestDoubleParameterInfo> Parameters)
{
    /// <summary>The backing <c>ReturnConfig&lt;T&gt;</c> field name - never a reserved keyword once <c>__</c>-prefixed.</summary>
    public string FieldName => $"__{OriginalName}";

    /// <summary>The type argument for this member's backing <c>ReturnConfig&lt;T&gt;</c> field.</summary>
    public string SlotTypeFullyQualifiedName => IsVoid ? "global::Compono.Unit" : ReturnTypeFullyQualifiedName;
}
