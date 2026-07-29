using Compono.Generators.Discovery;

namespace Compono.Generators.Models;

/// <summary>
/// A closed collection type reached in a discovered type's transitive graph, needing its own
/// generated collection plan, per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s third amendment.
/// </summary>
internal sealed record DiscoveredCollectionInfo(
    CollectionShape Shape,
    string FullyQualifiedCollectionTypeName,
    string ElementFullyQualifiedTypeName,
    bool ElementIsNullable,
    string? KeyFullyQualifiedTypeName,
    bool KeyIsNullable);
