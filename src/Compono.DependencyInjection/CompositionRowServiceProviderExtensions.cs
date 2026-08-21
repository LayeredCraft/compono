using System.Runtime.CompilerServices;

namespace Compono;

/// <summary>
/// The one public entry point of <c>Compono.DependencyInjection</c> - a configured-resolution
/// <see cref="IServiceProvider"/> bridge over a <see cref="CompositionRow"/>. See
/// <c>docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md</c>.
/// </summary>
public static class CompositionRowServiceProviderExtensions
{
    // One adapter per row, not one per call - PR #105 review: AsServiceProvider() originally
    // constructed a fresh ComponoServiceProvider (and fresh lock) on every call, so wrapping the same
    // row twice and using both providers concurrently could still race inside the row's shared
    // CompositionContext (each adapter serialized its own calls, but not against the other adapter's).
    // ConditionalWeakTable.GetValue is itself thread-safe and guarantees the same value for the same
    // key across concurrent callers, so this also fixes the underlying race without giving
    // CompositionRow/CompositionContext a general thread-safety promise they were never designed to
    // have - the scope stays exactly "IServiceProvider.GetService is safe to call concurrently,"
    // matching the BCL's own convention for that interface. Keying on the row (not caching inside
    // CompositionRow itself) keeps core Compono unaware this integration package exists.
    private static readonly ConditionalWeakTable<CompositionRow, IServiceProvider> Adapters = new();

    extension(CompositionRow row)
    {
        /// <summary>
        /// Wraps <paramref name="row"/> as an <see cref="IServiceProvider"/> backed by
        /// <see cref="CompositionRow.TryResolveConfigured"/>, with stable per-<see cref="Type"/>
        /// identity for the lifetime of the returned instance: the first successful resolution for a
        /// given type is cached, and every later <c>GetService</c> call for that same type returns the
        /// identical instance - this is what lets a test configure a double once and have a
        /// separately-rendered consumer (e.g. a bUnit component's <c>[Inject]</c>) observe the same
        /// value. A miss is never cached - a type unsatisfiable on one call can still be satisfied by
        /// a later one, if the row's own configuration changes in between. Calling this more than once
        /// for the same <paramref name="row"/> returns the identical <see cref="IServiceProvider"/>
        /// instance every time, not a fresh one - this is what makes the whole surface, including
        /// concurrent <c>GetService</c> calls made through two separately-obtained references, safe to
        /// use concurrently.
        /// </summary>
        /// <remarks>
        /// Wiring a DIFFERENT row's <c>UseServiceProvider</c> with the result of this call, where that
        /// row itself (directly or transitively) resolves back into this row, is discouraged but not
        /// unguarded: a <see cref="CompositionRow"/>'s underlying context is created once and reused for
        /// every call made on it, so a true cycle re-enters the same registration factory or provider on
        /// that context while it is still active, and Compono's existing reentrance guard raises a
        /// diagnosed <see cref="CompositionException"/> - it does not overflow the stack. See ADR-0047's
        /// Recursion section.
        /// <para>
        /// The returned <see cref="IServiceProvider"/> does not own or dispose anything it resolves and
        /// caches - if a resolved value implements <see cref="IDisposable"/>/<see cref="IAsyncDisposable"/>,
        /// disposing it is the caller's responsibility, exactly as it would be for a value the caller
        /// constructed by hand. This matches <see cref="CompositionRow"/>/<see cref="Composer"/>'s own
        /// lack of any disposal contract - this bridge does not introduce one.
        /// </para>
        /// </remarks>
        public IServiceProvider AsServiceProvider()
        {
            ArgumentNullException.ThrowIfNull(row);
            return Adapters.GetValue(row, static r => new ComponoServiceProvider(r));
        }
    }
}
