namespace Compono;

/// <summary>
/// Thrown when <see cref="Composer.Create(Action{CompositionBuilder})"/>'s validation finds one or
/// more conflicts in the accumulated configuration.
/// </summary>
/// <remarks>
/// Distinct from <see cref="CompositionException"/>: this is a configuration-time failure, thrown once
/// while building a <see cref="Composer"/>, never from a running <see cref="Composer.Create{T}"/>/
/// <see cref="Composer.CreateMany{T}"/> call. See
/// <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>'s Amendment.
/// </remarks>
public sealed class CompositionConfigurationException : Exception
{
    /// <summary>
    /// Every conflict found - always at least one. A genuinely immutable snapshot
    /// (<see cref="ImmutableSnapshot"/>) taken at construction, never the caller-supplied list itself
    /// and never a plain array a caller could cast back to and mutate - it can never drift from the
    /// already-rendered <see cref="Exception.Message"/>, which is derived from this exact same
    /// snapshot.
    /// </summary>
    public IReadOnlyList<CompositionConfigurationError> Errors { get; }

    /// <summary>
    /// Creates a <see cref="CompositionConfigurationException"/> from one or more structured errors.
    /// Its <see cref="Exception.Message"/> is rendered from <paramref name="errors"/>, not the other
    /// way around - inspect <see cref="Errors"/> directly rather than parsing the message.
    /// </summary>
    /// <param name="errors">
    /// Every conflict found. Copied into an immutable snapshot - mutating a list passed here after
    /// this constructor returns has no effect on <see cref="Errors"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public CompositionConfigurationException(IReadOnlyList<CompositionConfigurationError> errors)
        : this(Prepare(errors))
    {
    }

    // A constructor's base-initializer and body can't share a local variable directly, but Message
    // and Errors both need to be derived from the exact same snapshot - enumerating errors twice
    // (once to build the message, once to snapshot Errors) would let a mutable or lazily-computed
    // custom IReadOnlyList<T> implementation observe different contents between those two passes.
    // Preparing both values once, together, in a static helper - then threading them through this
    // private tuple-taking constructor - is what guarantees a single enumeration and a single,
    // genuinely immutable (ImmutableSnapshot, not just an array under an IReadOnlyList<T> a caller
    // could cast back to and mutate) source of truth for both.
    private CompositionConfigurationException((string Message, IReadOnlyList<CompositionConfigurationError> Errors) prepared)
        : base(prepared.Message)
    {
        Errors = prepared.Errors;
    }

    private static (string Message, IReadOnlyList<CompositionConfigurationError> Errors) Prepare(
        IReadOnlyList<CompositionConfigurationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        // Snapshot before validating, not after - errors.Count alone could disagree with what
        // actually gets enumerated into the snapshot for a custom/concurrently-mutated
        // IReadOnlyList<T> implementation, letting an empty snapshot (and an empty Message) sneak
        // past a check performed against the original list.
        var snapshot = ImmutableSnapshot.Of(errors);
        if (snapshot.Count == 0)
            throw new ArgumentException("At least one error is required.", nameof(errors));

        return (BuildMessage(snapshot), snapshot);
    }

    private static string BuildMessage(IReadOnlyList<CompositionConfigurationError> errors) =>
        string.Join(Environment.NewLine, errors.Select(DescribeError));

    private static string DescribeError(CompositionConfigurationError error) => error switch
    {
        CompositionConfigurationError.DuplicateConfigurationOption duplicate =>
            $"'{duplicate.OptionName}' was configured more than once ({DescribeSources(duplicate.Sources)}).",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unrecognized composition configuration error."),
    };

    private static string DescribeSources(IReadOnlyList<ConfigurationSource> sources) =>
        string.Join(", ", sources.Select(DescribeSource));

    private static string DescribeSource(ConfigurationSource source) => source switch
    {
        ConfigurationSource.ProfileChain chain => string.Join(" -> ", chain.Profiles.Select(type => type.Name)),
        _ => "direct",
    };
}
