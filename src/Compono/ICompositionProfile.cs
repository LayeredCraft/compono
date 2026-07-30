namespace Compono;

/// <summary>
/// A reusable, named grouping of <see cref="CompositionBuilder"/> configuration - registrations,
/// service-provider fallback, configuration rules, and other profiles - applied via
/// <see cref="CompositionBuilder.AddProfile{TProfile}()"/>/<see cref="CompositionBuilder.AddProfile(ICompositionProfile)"/>.
/// </summary>
/// <remarks>
/// A profile introduces no new engine concept of its own - <see cref="Configure"/> calls the exact same
/// builder verbs a direct <see cref="Composer.Create(Action{CompositionBuilder})"/> caller would. See
/// <c>docs/adr/0018-composition-profiles.md</c>.
/// </remarks>
public interface ICompositionProfile
{
    /// <summary>
    /// Applies this profile's configuration to <paramref name="builder"/> - called synchronously,
    /// immediately, during the <c>AddProfile</c> call that applies this profile.
    /// </summary>
    /// <param name="builder">
    /// The same <see cref="CompositionBuilder"/> instance the surrounding
    /// <see cref="Composer.Create(Action{CompositionBuilder})"/> callback is already configuring.
    /// </param>
    void Configure(CompositionBuilder builder);
}
