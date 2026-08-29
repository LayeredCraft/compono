namespace Compono.Generators.Models;

/// <summary>
/// The compile-time opt-in switches threaded through every discovery path, read once per
/// compilation by <see cref="ComponoIncrementalGenerator"/>: <c>ComponoGeneratedTestDoubles</c>
/// (ADR-0043) and <c>ComponoGeneratedLogging</c>
/// (docs/adr/0055-compono-logging-testing-support-package.md Amendment 3). Both default to
/// <see langword="false"/> when unset - <see cref="LoggingEnabled"/>'s own default-to-<see langword="true"/>
/// behavior for a consumer who references <c>Compono.Logging</c> comes from that package's own
/// packed MSBuild props asset, not from this type or this generator.
/// </summary>
internal readonly record struct GeneratorFeatureFlags(bool TestDoublesEnabled, bool LoggingEnabled);
