using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Compono.Options;

/// <summary>
/// The one hand-written, reflection-free source of truth for a settings type <typeparamref name="T"/> -
/// a test constructs this directly, configures/mutates it, and wires it into composition via
/// <see cref="CompositionBuilderExtensions.UseOptions{T}"/>. Implements
/// <see cref="IOptionsMonitor{TOptions}"/> directly, since Monitor's live, subscribable contract is
/// exactly what a mutable source naturally is; <see cref="IOptions{TOptions}"/> and
/// <see cref="IOptionsSnapshot{TOptions}"/> are satisfied by an internal frozen view captured from this
/// source, never by this type itself. See docs/adr/0061-compono-options-testing-support.md's "Public
/// object model" and "Concurrent access to one source instance" sections for the full rationale.
/// </summary>
/// <remarks>
/// Deliberately does not implement <see cref="IDisposable"/>/<see cref="IAsyncDisposable"/> - it owns no
/// disposable resource. A subscription returned by <see cref="OnChange"/> is the only thing a test
/// disposes. See the ADR's "Source disposal" section.
/// </remarks>
/// <typeparam name="T">The settings type this source is the source of truth for.</typeparam>
public sealed class TestOptionsSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IOptionsMonitor<T>
    where T : class
{
    private readonly object _gate = new();
    private readonly Dictionary<string, T> _values = new(StringComparer.Ordinal);
    private event Action<T, string?>? OnChanged;

    /// <summary>Creates a source whose default-named value (<see cref="Microsoft.Extensions.Options.Options.DefaultName"/>) is <paramref name="initialValue"/>.</summary>
    /// <param name="initialValue">The default value, established immediately - no separate setup call is required before resolving <see cref="CurrentValue"/>.</param>
    public TestOptionsSource(T initialValue)
    {
        ArgumentNullException.ThrowIfNull(initialValue);
        _values[MSOptions.DefaultName] = initialValue;
    }

    /// <inheritdoc />
    public T CurrentValue => Get(MSOptions.DefaultName);

    /// <inheritdoc />
    /// <exception cref="UnconfiguredNamedOptionException"><paramref name="name"/> (or the default name) was never established via <see cref="Change(T)"/>/<see cref="Change(string, T)"/>.</exception>
    public T Get(string? name)
    {
        var effectiveName = name ?? MSOptions.DefaultName;
        lock (_gate)
        {
            if (_values.TryGetValue(effectiveName, out var value))
            {
                return value;
            }
        }

        throw new UnconfiguredNamedOptionException(typeof(T), effectiveName);
    }

    /// <summary>
    /// Establishes or updates the default-named (<see cref="Microsoft.Extensions.Options.Options.DefaultName"/>) value, then
    /// synchronously notifies every current <see cref="OnChange"/> subscriber. The new value is visible
    /// via <see cref="CurrentValue"/>/<see cref="Get"/> before any subscriber is invoked, matching real
    /// <c>OptionsMonitor&lt;T&gt;</c>'s cache-then-invoke ordering.
    /// </summary>
    /// <param name="value">The new default value.</param>
    public void Change(T value) => Change(MSOptions.DefaultName, value);

    /// <summary>
    /// Establishes or updates <paramref name="name"/>'s value, then synchronously notifies every current
    /// <see cref="OnChange"/> subscriber for that name. This is also how a named value is first
    /// established - there is no separate "add" API. The new value is visible via <see cref="Get"/>
    /// before any subscriber is invoked. Matches real <c>OptionsMonitor&lt;T&gt;</c>: synchronous, no
    /// per-subscriber exception isolation (a throwing subscriber blocks subsequent ones).
    /// </summary>
    /// <param name="name">The option name - <see cref="Microsoft.Extensions.Options.Options.DefaultName"/> for the default.</param>
    /// <param name="value">The new value for <paramref name="name"/>.</param>
    public void Change(string name, T value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            _values[name] = value;
        }

        OnChanged?.Invoke(value, name);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The returned <see cref="IDisposable"/> unsubscribes <paramref name="listener"/> when disposed, and
    /// disposing it more than once is a no-op. Multiple independent subscriptions are supported and
    /// dispose independently of one another.
    /// </remarks>
    public IDisposable OnChange(Action<T, string?> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        OnChanged += listener;
        return new ChangeSubscription(this, listener);
    }

    private void Unsubscribe(Action<T, string?> listener) => OnChanged -= listener;

    // Captures a point-in-time copy of every currently-configured name/value pair - used by
    // FrozenOptionsView<T> to build a snapshot that never touches this source again after
    // construction (docs/adr/0061 "IOptions<T>"/"IOptionsSnapshot<T>" identity model).
    internal IReadOnlyDictionary<string, T> CaptureSnapshot()
    {
        lock (_gate)
        {
            return new Dictionary<string, T>(_values, StringComparer.Ordinal);
        }
    }

    private sealed class ChangeSubscription(TestOptionsSource<T> source, Action<T, string?> listener) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                source.Unsubscribe(listener);
            }
        }
    }
}
