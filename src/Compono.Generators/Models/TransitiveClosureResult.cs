using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// The result of walking one requested type's transitive composition graph
/// (<see cref="Discovery.TransitiveClosureWalker"/>): every composable type reached, plus every
/// closed collection shape reached (<c>docs/adr/0014-generator-emitted-collection-plans.md</c>).
/// </summary>
internal sealed record TransitiveClosureResult(
    EquatableArray<DiscoveredTypeInfo> Types,
    EquatableArray<DiscoveredCollectionInfo> Collections,
    EquatableArray<DiscoveredTestDoubleInfo> TestDoubles);
