namespace Compono;

/// <summary>
/// Tracks one scalar (singleton-valued) configuration option's value and every source that set it -
/// the shared mechanism every <see cref="CompositionBuilder"/> scalar verb (<c>WithSeed</c>,
/// <c>WithCollectionSize</c>'s global default, <c>UseServiceProvider</c>) uses to detect a duplicate
/// call, rather than each verb implementing its own ad hoc check.
/// </summary>
/// <remarks>
/// See <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>'s Amendment. Only
/// <c>WithSeed</c> uses this so far (Phase 0); <c>WithCollectionSize</c>/<c>UseServiceProvider</c>
/// reuse it, unchanged, when their own phases add them.
/// </remarks>
/// <typeparam name="TValue">The option's value type.</typeparam>
internal sealed class ConfigurationOptionSlot<TValue>
{
    private readonly string _optionName;
    private readonly List<ConfigurationSource> _sources = [];

    /// <summary>Creates an empty slot for the option named <paramref name="optionName"/>.</summary>
    /// <param name="optionName">The builder verb's name, e.g. <c>"WithSeed"</c> - used only in diagnostics.</param>
    internal ConfigurationOptionSlot(string optionName)
    {
        _optionName = optionName;
    }

    /// <summary>
    /// This option's most recently set value, or <see langword="default"/> if it was never set.
    /// </summary>
    internal TValue? Value { get; private set; }

    /// <summary>Records a call that set this option to <paramref name="value"/>, from <paramref name="source"/>.</summary>
    internal void Set(TValue value, ConfigurationSource source)
    {
        Value = value;
        _sources.Add(source);
    }

    /// <summary>
    /// Returns whether this option was set more than once and, if so, the conflict describing every
    /// contributing source.
    /// </summary>
    internal bool TryGetConflict(out CompositionConfigurationError.DuplicateConfigurationOption conflict)
    {
        if (_sources.Count > 1)
        {
            conflict = new CompositionConfigurationError.DuplicateConfigurationOption(_optionName, [.. _sources]);
            return true;
        }

        // Never dereferenced by a caller when this method returns false - the out parameter has no
        // meaningful value in the no-conflict case, same convention as the BCL's own TryGetValue.
        conflict = null!;
        return false;
    }
}
