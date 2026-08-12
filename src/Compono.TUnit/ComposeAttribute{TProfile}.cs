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

    internal override void ApplyProfile(CompositionBuilder builder)
    {
        // A negative configured seed must be rejected before any profile work is attempted -
        // otherwise Seed = -1 combined with a throwing TProfile.Configure would report the profile
        // failure below with "Seed: -1" embedded instead of the documented negative-seed diagnostic
        // the base class's own ComposeRow enforces. Matches ComposeAttribute<TProfile, TConfig>'s
        // identical early check (Codex review).
        if (SeedAsNullable is { } configuredSeed && configuredSeed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.TUnit requires a non-negative seed, but the configured seed was {configuredSeed}.",
                configuredSeed));
        }

        try
        {
            builder.AddProfile<TProfile>();
        }
        catch (CompositionException exception)
        {
            // ApplyProfile runs while the base class's Lazy<Composer> is still being built - before
            // ComposeRow ever calls Composer.CreateRow, so no CompositionRow/row.Seed exists yet at
            // this point. TProfile.Configure throwing here (e.g. a bad registration) must still end
            // with the "Seed: {value}" convention every Compono.TUnit-owned pre-composition failure
            // uses, matching ComposeAttribute<TProfile, TConfig>'s identical wrapping for its own
            // ApplyProfile failures - otherwise even a configured Seed goes unreported for this
            // profile form specifically (Codex review).
            var seed = SeedAsNullable ?? Random.Shared.Next(0, int.MaxValue);
            throw CompositionException.WithSeedInMessage(exception, seed);
        }
    }
}
