using System.Diagnostics.CodeAnalysis;

namespace Compono.XunitV3;

/// <summary>
/// Composes an xUnit v3 theory row's parameters through Compono with <typeparamref name="TProfile"/>
/// applied - the Composed theory rows counterpart to <see cref="ComposeAttribute{TProfile}"/>. Rows are
/// read from the same public static member shape <see cref="ComposeDataAttribute"/> documents; each row
/// is composed against a fresh <typeparamref name="TProfile"/> instance. See
/// <c>docs/adr/0070-xunitv3-composedata-composed-theory-rows.md</c>.
/// </summary>
/// <typeparam name="TProfile">
/// The profile to apply. The <c>new()</c> constraint mirrors
/// <see cref="ComposeAttribute{TProfile}"/>'s own designed shape (a fixed, default-constructed profile).
/// </typeparam>
/// <remarks>
/// Construction of the profile is reflection-free (<c>new TProfile()</c>), identical to
/// <see cref="ComposeAttribute{TProfile}"/> - <see cref="DynamicallyAccessedMembersAttribute"/> is
/// carried on <typeparamref name="TProfile"/> for parity with
/// <see cref="ComposeAttribute{TProfile, TConfig}"/>'s generic parameters even though the closed
/// <c>new()</c> constraint would not itself strip the public constructors.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeDataAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProfile> : ComposeDataAttribute
    where TProfile : ICompositionProfile, new()
{
    /// <summary>
    /// Creates a <see cref="ComposeDataAttribute{TProfile}"/>.
    /// </summary>
    /// <param name="memberName">
    /// The name of the public static field, property, or parameterless method that supplies this
    /// theory's rows - see <see cref="ComposeDataAttribute.MemberName"/>.
    /// </param>
    public ComposeDataAttribute(string memberName) : base(memberName)
    {
    }

    /// <inheritdoc />
    internal override void ApplyProfile(CompositionBuilder builder, object? config) => builder.AddProfile<TProfile>();
}
