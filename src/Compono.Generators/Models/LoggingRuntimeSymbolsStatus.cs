namespace Compono.Generators.Models;

/// <summary>
/// Whether logging activation generation should run at all for this compilation - computed once,
/// per docs/adr/0055-compono-logging-testing-support-package.md Amendment 3's gating flow:
/// <c>ComponoGeneratedLogging</c> is the sole enablement signal; <c>Compono.Logging</c>'s runtime
/// symbols are only checked afterward, purely for diagnostics.
/// </summary>
internal enum LoggingRuntimeSymbolsStatus
{
    /// <summary><c>ComponoGeneratedLogging</c> is disabled (the default for any consumer who never
    /// references <c>Compono.Logging</c>) - no symbol resolution was even attempted.</summary>
    Disabled,

    /// <summary><c>ComponoGeneratedLogging</c> is enabled and every required
    /// <c>Compono.Logging</c> runtime symbol resolved - discovery/emission proceeds normally.</summary>
    EnabledAndAvailable,

    /// <summary><c>ComponoGeneratedLogging</c> is enabled but at least one required
    /// <c>Compono.Logging</c> runtime symbol did not resolve - <see cref="Diagnostics.DiagnosticDescriptors.LoggingRuntimeSymbolsUnavailable"/>
    /// is reported and no logging registration source is emitted.</summary>
    EnabledButUnavailable,
}
