namespace Compono;

/// <summary>
/// Where one accumulated <see cref="CompositionBuilder"/> entry (a registration, a rule, or a scalar
/// configuration option) came from - a direct builder call, or the chain of profiles whose
/// <c>Configure</c> it ran inside of.
/// </summary>
/// <remarks>
/// A discriminated union, matching this codebase's existing <see cref="PathSegment"/>/
/// <see cref="CompositionResult"/> shape. Used only for diagnostics - naming every contributing source
/// of a <see cref="CompositionConfigurationError"/> - never for resolution behavior. <c>public</c>
/// because <see cref="CompositionConfigurationError"/>, which exposes it, is <c>public</c>. See
/// <c>docs/adr/0018-composition-profiles.md</c>'s provenance decision.
/// </remarks>
public abstract record ConfigurationSource
{
    private ConfigurationSource()
    {
    }

    /// <summary>The single, shared instance representing a builder call made outside any profile.</summary>
    public static readonly ConfigurationSource Direct = new DirectSource();

    /// <summary>
    /// A builder call made from inside a profile's <c>Configure</c>, or nested inside another
    /// profile's <c>Configure</c>.
    /// </summary>
    /// <param name="Profiles">The applied profile types, outermost first.</param>
    public sealed record ProfileChain(IReadOnlyList<Type> Profiles) : ConfigurationSource;

    private sealed record DirectSource : ConfigurationSource;
}
