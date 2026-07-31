namespace Compono;

/// <summary>
/// Activates <see cref="NSubstituteProvider"/> on a <see cref="CompositionBuilder"/>. See
/// <c>docs/adr/0025-compono-nsubstitute-package-design.md</c>.
/// </summary>
public static class CompositionBuilderExtensions
{
    extension(CompositionBuilder builder)
    {
        /// <summary>
        /// Registers an <see cref="NSubstituteProvider"/> with default <see cref="NSubstituteOptions"/>.
        /// </summary>
        public CompositionBuilder UseNSubstitute() => builder.UseNSubstitute(static _ => { });

        /// <summary>
        /// Registers an <see cref="NSubstituteProvider"/>, configured by <paramref name="configure"/>.
        /// </summary>
        /// <param name="configure">Sets the provider's <see cref="NSubstituteOptions"/>.</param>
        public CompositionBuilder UseNSubstitute(Action<NSubstituteOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var options = new NSubstituteOptions();
            configure(options);
            return builder.AddTestDoubleProvider(new NSubstituteProvider(options));
        }
    }
}
