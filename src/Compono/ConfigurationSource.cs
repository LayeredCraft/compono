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
    public sealed record ProfileChain : ConfigurationSource
    {
        /// <summary>
        /// The applied profile types, outermost first. A genuinely immutable snapshot
        /// (<see cref="ImmutableSnapshot"/>) taken at construction, never the caller-supplied list
        /// itself - the same mutation-after-construction concern
        /// <see cref="CompositionConfigurationException.Errors"/> guards against.
        /// </summary>
        public IReadOnlyList<Type> Profiles { get; }

        /// <summary>Creates a <see cref="ProfileChain"/> source.</summary>
        /// <param name="profiles">
        /// The applied profile types, outermost first - always at least one; this source represents
        /// "made inside a profile," so an empty chain has no coherent meaning. Copied into an
        /// immutable snapshot - mutating a list passed here after this constructor returns has no
        /// effect on <see cref="Profiles"/>.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="profiles"/> is empty.</exception>
        public ProfileChain(IReadOnlyList<Type> profiles)
        {
            ArgumentNullException.ThrowIfNull(profiles);

            // Snapshot before validating, not after - see CompositionConfigurationError's identical
            // pattern for why (a custom/concurrently-mutated IReadOnlyList<T> could otherwise let an
            // empty snapshot past a check performed against the original list).
            var snapshot = ImmutableSnapshot.Of(profiles);
            if (snapshot.Count == 0)
                throw new ArgumentException("A profile chain requires at least one profile.", nameof(profiles));

            Profiles = snapshot;
        }
    }

    private sealed record DirectSource : ConfigurationSource;
}
