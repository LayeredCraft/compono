using Compono.Generators.Diagnostics;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// A closed <c>ILogger&lt;T&gt;</c> category reached in a discovered type's transitive graph (or
/// itself the composed root), per
/// docs/adr/0055-compono-logging-testing-support-package.md Amendments 1/3 - recorded independent of
/// <see cref="TransitiveClosureResult.Types"/>, since that walk deliberately excludes exactly the
/// provider-resolved interface leaves this feature needs (<c>ILogger&lt;T&gt;</c> is never itself
/// walked structurally). Only recorded when <c>ComponoGeneratedLogging</c> is enabled - see
/// <see cref="GeneratorFeatureFlags"/>.
/// </summary>
/// <param name="CategoryFullyQualifiedName">
/// The category type <c>T</c>'s fully qualified, <c>global::</c>-prefixed name (not
/// <c>ILogger&lt;T&gt;</c> itself) - what
/// <c>Compono.Logging.LoggingFactoryRegistry.Register&lt;TCategory&gt;</c>'s generated call closes
/// over.
/// </param>
/// <param name="Diagnostics">
/// Non-empty only when this category can't get a generated activation at all - currently, a
/// private/protected category type inaccessible from a top-level generated registration class
/// (mirroring <c>DiscoveredCollectionInfo</c>'s identical accessibility check for element/key
/// types). The category still defers entirely to <c>LoggingProvider</c>'s runtime
/// missing-generated-activation diagnostic in that case, exactly as if it had never been discovered.
/// </param>
internal sealed record DiscoveredLoggingCategoryInfo(
    string CategoryFullyQualifiedName,
    EquatableArray<DiagnosticInfo> Diagnostics);
