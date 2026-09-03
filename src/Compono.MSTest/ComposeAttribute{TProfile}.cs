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

    internal override void ApplyProfile(CompositionBuilder builder)
    {
        // A negative configured seed must be rejected before any profile work is attempted -
        // otherwise Seed = -1 combined with a throwing TProfile.Configure would report the profile
        // failure below with "Seed: -1" embedded instead of the documented negative-seed diagnostic
        // the base class's own composition-row construction enforces. Matches
        // ComposeAttribute<TProfile, TConfig>'s identical early check (PLAN-0061 Phase 1: this
        // attribute's own one-generic-argument form had been copied from an XunitV3 revision that
        // predated that fix).
        if (SeedAsNullable is { } configuredSeed && configuredSeed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.MSTest requires a non-negative seed, but the configured seed was {configuredSeed}.",
                configuredSeed));
        }

        try
        {
            builder.AddProfile<TProfile>();
        }
        catch (CompositionException exception)
        {
            // ApplyProfile runs while the base class's Lazy<Composer> is still being built - before
            // any CompositionRow exists yet at this point. TProfile.Configure throwing here (e.g. a
            // bad registration) must still end with the "Seed: {value}" convention every
            // Compono.MSTest-owned pre-composition failure uses, matching
            // ComposeAttribute<TProfile, TConfig>'s identical wrapping for its own ApplyProfile
            // failures.
            var seed = SeedAsNullable ?? Random.Shared.Next(0, int.MaxValue);
            throw CompositionException.WithSeedInMessage(exception, seed);
        }
    }
}
