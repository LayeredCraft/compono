using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// The result of walking one requested type's transitive composition graph
/// (<see cref="Discovery.TransitiveClosureWalker"/>): every composable type reached, plus every
/// closed collection shape reached (<c>docs/adr/0014-generator-emitted-collection-plans.md</c>), plus
/// every closed <c>ILogger&lt;T&gt;</c> category reached
/// (docs/adr/0055-compono-logging-testing-support-package.md Amendments 1/3).
/// </summary>
internal sealed record TransitiveClosureResult(
    EquatableArray<DiscoveredTypeInfo> Types,
    EquatableArray<DiscoveredCollectionInfo> Collections,
    EquatableArray<DiscoveredTestDoubleInfo> TestDoubles,
    EquatableArray<DiscoveredLoggingCategoryInfo> LoggingCategories = default);
