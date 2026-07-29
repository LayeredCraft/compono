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
    /// Every conflict found - always at least one. An immutable snapshot taken at construction, never
    /// the caller-supplied list itself - it can never drift from the already-rendered
    /// <see cref="Exception.Message"/>.
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
        : base(BuildMessage(RequireErrors(errors)))
    {
        // Snapshotted, not the caller-supplied list itself - Message above is rendered once, from
        // this same base-initializer call; if Errors instead aliased a caller-retained mutable list
        // (Build()'s own accumulator is a plain List<T> under the IReadOnlyList<T> parameter type),
        // a later mutation would make Errors and the already-rendered Message silently disagree.
        // errors has already passed RequireErrors' validation by the time this line runs - the base
        // initializer above would have thrown first otherwise - so no need to validate it again here.
        Errors = [.. errors];
    }

    private static IReadOnlyList<CompositionConfigurationError> RequireErrors(IReadOnlyList<CompositionConfigurationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            throw new ArgumentException("At least one error is required.", nameof(errors));

        return errors;
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
