namespace Compono.MSTest;

/// <summary>
/// Composes an MSTest data-driven test method's parameters through Compono, applying
/// <typeparamref name="TProfile"/> - matching <c>Compono.XunitV3</c>/<c>Compono.TUnit</c>'s own
/// identical generic form exactly (same Compono-facing attribute family and semantics).
/// </summary>
/// <typeparam name="TProfile">
/// The profile to apply, via <see cref="CompositionBuilder.AddProfile{TProfile}"/>. Default-
/// constructed - see <see cref="ComposeAttribute{TProfile, TConfig}"/> for the profile-
/// configuration-argument form instead.
/// </typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile> : ComposeAttribute
    where TProfile : ICompositionProfile, new()
{
    /// <summary>
    /// Creates a <see cref="ComposeAttribute{TProfile}"/>.
    /// </summary>
    /// <param name="inlineValues">
    /// Values supplied positionally, left-to-right from the test method's first parameter - see
    /// <see cref="ComposeAttribute(object?[])"/>.
    /// </param>
    public ComposeAttribute(params object?[] inlineValues) : base(inlineValues)
    {
    }

    internal override void ApplyProfile(CompositionBuilder builder) => builder.AddProfile<TProfile>();
}
