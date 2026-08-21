namespace Compono;

/// <summary>
/// The one public entry point of <c>Compono.DependencyInjection</c> - a configured-resolution
/// <see cref="IServiceProvider"/> bridge over a <see cref="CompositionRow"/>. See
/// <c>docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md</c>.
/// </summary>
public static class CompositionRowServiceProviderExtensions
{
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
        /// a later one, if the row's own configuration changes in between.
        /// </summary>
        /// <remarks>
        /// Do not configure a DIFFERENT row's <c>UseServiceProvider</c> with the result of this call on
        /// a row that itself (directly or transitively) resolves back into that same row - nothing in
        /// Compono detects a resolution cycle that crosses two rows, and it will overflow the stack
        /// rather than throw a diagnosed exception. See ADR-0047's Recursion section.
        /// </remarks>
        public IServiceProvider AsServiceProvider()
        {
            ArgumentNullException.ThrowIfNull(row);
            return new ComponoServiceProvider(row);
        }
    }
}
