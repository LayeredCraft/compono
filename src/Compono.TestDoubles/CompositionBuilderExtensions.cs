namespace Compono;

/// <summary>
/// Activates <see cref="GeneratedTestDoubleProvider"/> on a <see cref="CompositionBuilder"/>. See
/// ADR-0043's "Runtime activation and precedence".
/// </summary>
public static class CompositionBuilderExtensions
{
    extension(CompositionBuilder builder)
    {
        /// <summary>
        /// Registers a <see cref="GeneratedTestDoubleProvider"/>. Call this <b>before</b>
        /// <c>UseNSubstitute()</c> (or any other test-double provider) when both are installed - stage
        /// 6 providers are tried in registration order, and a generated double should win over a
        /// generic substitute whenever both could satisfy the same interface. See ADR-0043's "Runtime
        /// activation and precedence".
        /// </summary>
        public CompositionBuilder UseGeneratedTestDoubles() => builder.AddTestDoubleProvider(new GeneratedTestDoubleProvider());
    }
}
