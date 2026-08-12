namespace Compono.TUnit;

/// <summary>
/// Composes a TUnit test method's parameters through Compono, with <typeparamref name="TProfile"/>
/// applied to the underlying <see cref="Composer"/> - equivalent to
/// <c>Composer.Create(builder =&gt; builder.AddProfile&lt;TProfile&gt;())</c>. See
/// <see cref="ComposeAttribute"/> for the full binding algorithm.
/// </summary>
/// <typeparam name="TProfile">The profile to apply.</typeparam>
/// <remarks>
/// A profile type that doesn't implement <see cref="ICompositionProfile"/> or lacks a public
/// parameterless constructor is a compile error at the <c>[Compose&lt;TProfile&gt;]</c> use site
/// (C# enforces generic-attribute constraints there like any other generic type) - there is no
/// runtime "invalid profile type" diagnostic to design. Mirrors
/// <c>Compono.XunitV3.ComposeAttribute{TProfile}</c> exactly.
/// </remarks>
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
