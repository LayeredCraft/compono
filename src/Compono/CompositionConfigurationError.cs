namespace Compono;

/// <summary>
/// One conflict found while validating a <see cref="CompositionBuilder"/>'s accumulated configuration -
/// the structured detail behind a <see cref="CompositionConfigurationException"/>.
/// </summary>
/// <remarks>
/// A discriminated union - the same shape this codebase already uses for <see cref="PathSegment"/> and
/// <see cref="CompositionResult"/> - so each conflict kind carries only the fields relevant to it,
/// rather than one flat type with a <c>Kind</c> enum and fields only some kinds use. New cases are
/// added by whichever Milestone 3 phase introduces the conflict they describe; only
/// <see cref="DuplicateConfigurationOption"/> exists so far (Phase 0). See
/// <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>'s Amendment.
/// </remarks>
public abstract record CompositionConfigurationError
{
    private CompositionConfigurationError()
    {
    }

    /// <summary>
    /// The same scalar (singleton-valued) configuration option - <c>WithSeed</c>,
    /// <c>WithCollectionSize</c>'s global default, or <c>UseServiceProvider</c> - was set more than
    /// once across a single <see cref="Composer.Create(Action{CompositionBuilder})"/> callback.
    /// </summary>
    /// <remarks>
    /// Deliberately fail-fast rather than last-wins, unlike a typical "options builder" convention -
    /// see <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>'s Amendment for
    /// why a contradictory scalar configuration (e.g. two different seeds) has no coherent effective
    /// value to fall back to.
    /// </remarks>
    public sealed record DuplicateConfigurationOption : CompositionConfigurationError
    {
        /// <summary>The builder verb's name, e.g. <c>"WithSeed"</c>.</summary>
        public string OptionName { get; }

        /// <summary>
        /// Every call that set this option, in call order - always at least two. A genuinely
        /// immutable snapshot (<see cref="ImmutableSnapshot"/>) taken at construction, never the
        /// caller-supplied list itself and never a plain array a caller could cast back to and
        /// mutate - the same mutation-after-construction concern
        /// <see cref="CompositionConfigurationException.Errors"/> guards against, one level deeper.
        /// </summary>
        public IReadOnlyList<ConfigurationSource> Sources { get; }

        /// <summary>Creates a <see cref="DuplicateConfigurationOption"/> error.</summary>
        /// <param name="optionName">The builder verb's name, e.g. <c>"WithSeed"</c>.</param>
        /// <param name="sources">
        /// Every call that set this option, in call order. Copied into an immutable snapshot -
        /// mutating a list passed here after this constructor returns has no effect on
        /// <see cref="Sources"/>.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="sources"/> has fewer than two entries.</exception>
        public DuplicateConfigurationOption(string optionName, IReadOnlyList<ConfigurationSource> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);

            // Snapshot before validating, not after - sources.Count alone could disagree with what
            // actually gets enumerated into the snapshot for a custom/concurrently-mutated
            // IReadOnlyList<T> implementation, letting an under-count sneak past a check performed
            // against the original list.
            var snapshot = ImmutableSnapshot.Of(sources);
            if (snapshot.Count < 2)
            {
                throw new ArgumentException(
                    "A duplicate-configuration-option error requires at least two contributing sources.",
                    nameof(sources));
            }

            OptionName = optionName;
            Sources = snapshot;
        }
    }

    /// <summary>
    /// The same exact type was registered (via <c>Register&lt;T&gt;</c>) more than once across a
    /// single <see cref="Composer.Create(Action{CompositionBuilder})"/> callback - directly, or from a
    /// profile, per <c>docs/adr/0019-registrations-and-service-provider-injection.md</c>'s deliberately
    /// strict throw-on-duplicate decision.
    /// </summary>
    public sealed record DuplicateRegistration : CompositionConfigurationError
    {
        /// <summary>The type registered more than once.</summary>
        public Type RegisteredType { get; }

        /// <summary>
        /// Every call that registered this type, in call order - always at least two. A genuinely
        /// immutable snapshot (<see cref="ImmutableSnapshot"/>), same guarantee as
        /// <see cref="DuplicateConfigurationOption.Sources"/>.
        /// </summary>
        public IReadOnlyList<ConfigurationSource> Sources { get; }

        /// <summary>Creates a <see cref="DuplicateRegistration"/> error.</summary>
        /// <param name="registeredType">The type registered more than once.</param>
        /// <param name="sources">Every call that registered this type, in call order.</param>
        /// <exception cref="ArgumentException"><paramref name="sources"/> has fewer than two entries.</exception>
        public DuplicateRegistration(Type registeredType, IReadOnlyList<ConfigurationSource> sources)
        {
            ArgumentNullException.ThrowIfNull(registeredType);
            ArgumentNullException.ThrowIfNull(sources);

            var snapshot = ImmutableSnapshot.Of(sources);
            if (snapshot.Count < 2)
            {
                throw new ArgumentException(
                    "A duplicate-registration error requires at least two contributing sources.",
                    nameof(sources));
            }

            RegisteredType = registeredType;
            Sources = snapshot;
        }
    }

    /// <summary>
    /// A profile, directly or transitively (via <c>AddProfile</c> called from inside another profile's
    /// <c>Configure</c>), applied itself again while it was already applying - detected and thrown
    /// immediately from <c>AddProfile</c>, never aggregated into a <see cref="CompositionConfigurationException"/>
    /// alongside any other conflict.
    /// </summary>
    /// <remarks>
    /// Identity for cycle purposes is a profile's declared CLR type (<c>profile.GetType()</c>),
    /// regardless of whether it reached the builder via <c>AddProfile&lt;T&gt;()</c> or
    /// <c>AddProfile(instance)</c> - see <c>docs/adr/0018-composition-profiles.md</c>.
    /// </remarks>
    public sealed record ProfileCycle : CompositionConfigurationError
    {
        /// <summary>
        /// The full cycle, in application order, with the repeated profile type at both ends (e.g.
        /// <c>[ProfileA, ProfileB, ProfileA]</c>) - always at least two entries. A genuinely immutable
        /// snapshot (<see cref="ImmutableSnapshot"/>), same guarantee as
        /// <see cref="DuplicateRegistration.Sources"/>.
        /// </summary>
        public IReadOnlyList<Type> Chain { get; }

        /// <summary>Creates a <see cref="ProfileCycle"/> error.</summary>
        /// <param name="chain">The full cycle, in application order, with the repeated profile type at both ends.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="chain"/> has fewer than two entries, contains a <see langword="null"/> entry,
        /// or its first and last entries differ.
        /// </exception>
        public ProfileCycle(IReadOnlyList<Type> chain)
        {
            ArgumentNullException.ThrowIfNull(chain);

            var snapshot = ImmutableSnapshot.Of(chain);
            if (snapshot.Count < 2)
            {
                throw new ArgumentException(
                    "A profile-cycle error requires a chain of at least two entries.",
                    nameof(chain));
            }

            if (snapshot.Any(type => type is null))
            {
                throw new ArgumentException(
                    "A profile-cycle error requires every chain entry to be a non-null profile type.",
                    nameof(chain));
            }

            if (snapshot[0] != snapshot[^1])
            {
                throw new ArgumentException(
                    "A profile-cycle error requires the chain's first and last entries to be the same profile type.",
                    nameof(chain));
            }

            Chain = snapshot;
        }
    }
}
