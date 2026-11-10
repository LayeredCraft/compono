using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Compono.XunitV3.Binding;

namespace Compono.XunitV3;

/// <summary>
/// Composes an xUnit v3 theory row's parameters through Compono with a profile built, once per row,
/// from that row's own configuration - the Composed theory rows counterpart to
/// <see cref="ComposeAttribute{TProfile, TConfig}"/>. Rows are read from the same public static member
/// shape <see cref="ComposeDataAttribute"/> documents. Unlike
/// <see cref="ComposeAttribute{TProfile, TConfig}"/>, this attribute's own constructor argument is the
/// member name, not a config-argument array: the configuration comes from the row itself - element 0
/// of <em>every</em> row is that row's <typeparamref name="TConfig"/> (a non-null instance assignable
/// to <typeparamref name="TConfig"/>), and elements 1..n are that row's values. See
/// <c>docs/adr/0070-xunitv3-composedata-composed-theory-rows.md</c>.
/// </summary>
/// <typeparam name="TProfile">
/// The profile to construct and apply once per row from that row's <typeparamref name="TConfig"/>. Must
/// have exactly one public constructor accepting exactly one <typeparamref name="TConfig"/>-typed
/// parameter - no <c>new()</c> constraint, matching <see cref="ComposeAttribute{TProfile, TConfig}"/>.
/// </typeparam>
/// <typeparam name="TConfig">
/// The configuration type each row supplies as its element 0. Compono neither constructs it nor
/// resolves it from the profile; the row provides an already-built, non-null instance assignable to
/// <typeparamref name="TConfig"/>.
/// </typeparam>
/// <remarks>
/// <typeparamref name="TProfile"/> and <typeparamref name="TConfig"/> both carry
/// <see cref="DynamicallyAccessedMembersAttribute"/>(<see cref="DynamicallyAccessedMemberTypes.PublicConstructors"/>),
/// matching <see cref="ComposeAttribute{TProfile, TConfig}"/>'s shape and the ADR-0070 public API. The
/// annotation on <typeparamref name="TProfile"/> is load-bearing: the per-row profile is built by
/// reflection (<c>new TProfile(rowConfig)</c>) and the trimmer strips a closed generic argument's
/// public constructors without it. <typeparamref name="TConfig"/> is never constructed by Compono - the
/// row supplies the instance - so its annotation is carried for shape parity, not because Compono
/// reflects over it.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeDataAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProfile,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConfig> : ComposeDataAttribute
    where TProfile : ICompositionProfile
{
    /// <summary>
    /// Creates a <see cref="ComposeDataAttribute{TProfile, TConfig}"/>.
    /// </summary>
    /// <param name="memberName">
    /// The name of the public static field, property, or parameterless method that supplies this
    /// theory's rows - see <see cref="ComposeDataAttribute.MemberName"/>.
    /// </param>
    public ComposeDataAttribute(string memberName) : base(memberName)
    {
    }

    /// <inheritdoc />
    internal override Type? ConfigurationType => typeof(TConfig);

    /// <inheritdoc />
    internal override void ApplyProfile(CompositionBuilder builder, object? config)
    {
        // The profile constructor lookup does not depend on the row's config, so resolve it once per
        // attribute instance (thread-safe) and reuse it for every row (ADR-0070 B3 remediation F5).
        // The row-specific config value itself is never cached.
        var constructor = LazyInitializer.EnsureInitialized(
            ref _profileConstructor,
            ref _profileConstructorLock,
            static () => ConfigProfileBinder.ResolveProfileConstructor(typeof(TProfile), typeof(TConfig)));

        builder.AddProfile(ConfigProfileBinder.InvokeProfile<TProfile>(constructor, config!));
    }

    private ConstructorInfo? _profileConstructor;
    private object? _profileConstructorLock;
}
