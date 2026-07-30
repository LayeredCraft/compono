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
        public DuplicateConfigurationOption(string optionName, IReadOnlyList<ConfigurationSource> sources)
        {
            OptionName = optionName;
            Sources = ImmutableSnapshot.Of(sources);
        }
    }
}
