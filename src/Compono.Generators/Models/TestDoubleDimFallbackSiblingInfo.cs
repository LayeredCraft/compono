using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// One member an <see cref="TestDoubleMemberInfo.DimFallbackHelperClassName"/> helper class must
/// itself implement, purely as a forward back to the owning double's own explicit implementation -
/// everything the helper's declaring interface (and its own transitive base interfaces) requires,
/// except the one <see cref="TestDoubleMemberInfo.IsDimFallbackTarget"/> member the helper
/// deliberately leaves unoverridden so C#'s own default-interface-member dispatch resolves it. Never
/// independently recorded - the owner's own explicit implementation already owns this member's
/// call-recording state (ADR-0044 Amendment 20's call-recording invariant).
/// </summary>
internal sealed record TestDoubleDimFallbackSiblingInfo(
    string EscapedName,
    string DeclaringInterfaceFullyQualifiedName,
    TestDoubleMemberKind Kind,
    TestDoublePropertyAccessorKind AccessorKind,
    string ReturnTypeFullyQualifiedName,
    bool IsVoid,
    EquatableArray<TestDoubleParameterInfo> Parameters,
    bool IsGenericMethod = false,
    EquatableArray<string> TypeParameterNames = default)
{
    /// <summary><c>"&lt;T, U&gt;"</c> when <see cref="IsGenericMethod"/>, otherwise empty.</summary>
    public string GenericSuffix => IsGenericMethod ? $"<{string.Join(", ", TypeParameterNames)}>" : "";
}
