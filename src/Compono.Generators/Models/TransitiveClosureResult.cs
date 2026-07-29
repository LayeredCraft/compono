using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// The result of walking one requested type's transitive composition graph
/// (<see cref="Discovery.TransitiveClosureWalker"/>): every composable type reached, plus every
/// closed collection shape reached (<c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s
/// third amendment).
/// </summary>
internal sealed record TransitiveClosureResult(
    EquatableArray<DiscoveredTypeInfo> Types,
    EquatableArray<DiscoveredCollectionInfo> Collections);
