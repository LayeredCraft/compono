namespace Compono;

/// <summary>
/// Mutable configuration accumulator for a <see cref="Composer"/>, live only for the duration of the
/// <see cref="Composer.Create(Action{CompositionBuilder})"/> callback that receives it.
/// </summary>
/// <remarks>
/// Every accumulated setting is validated and frozen into an immutable <see cref="CompositionConfiguration"/>
/// once the callback returns - nothing about a later <see cref="Composer.Create{T}"/>/
/// <see cref="Composer.CreateMany{T}"/> call can observe a mutation made after that point, since this
/// instance is never reachable again. See
/// <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>.
/// </remarks>
public sealed class CompositionBuilder
{
    private readonly ConfigurationOptionSlot<CompositionSeed> _seed = new("WithSeed");

    internal CompositionBuilder()
    {
    }

    /// <summary>
    /// Sets this composer's explicit root seed - the same seed produces the same composed output
    /// (for a given <c>Compono</c> package version) across every <see cref="Composer.Create{T}"/>/
    /// <see cref="Composer.CreateMany{T}"/> call this composer ever serves. Without this call, each
    /// root composition operation generates its own seed.
    /// </summary>
    /// <remarks>
    /// Calling this more than once (directly, or once directly and once from a profile, or from two
    /// different profiles) is a configuration conflict, not last-write-wins - surfaced as a
    /// <see cref="CompositionConfigurationException"/> once <see cref="Composer.Create(Action{CompositionBuilder})"/>'s
    /// validation runs, not immediately. See
    /// <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>'s Amendment for why:
    /// two different seeds configured for the same composer has no coherent "effective" value the way
    /// a typical options-builder's last-write-wins convention would assume.
    /// </remarks>
    /// <param name="seed">The explicit root seed.</param>
    public CompositionBuilder WithSeed(int seed)
    {
        _seed.Set(new CompositionSeed(unchecked((ulong)seed)), ConfigurationSource.Direct);
        return this;
    }

    // Validates every accumulated setting and freezes it into an immutable CompositionConfiguration -
    // called exactly once, by Composer.Create, immediately after the configuration callback returns.
    // Collects every conflict in one pass rather than throwing at the first one found, so a single
    // Composer.Create(...) call reports everything wrong with it at once.
    internal CompositionConfiguration Build()
    {
        var errors = new List<CompositionConfigurationError>();

        if (_seed.TryGetConflict(out var seedConflict))
            errors.Add(seedConflict);

        if (errors.Count > 0)
            throw new CompositionConfigurationException(errors);

        return new CompositionConfiguration
        {
            Seed = _seed.Value,
        };
    }
}
