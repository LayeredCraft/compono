using Compono.Generators.Diagnostics;
using Compono.Generators.Discovery;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// A closed collection type reached in a discovered type's transitive graph, needing its own
/// generated collection plan, per
/// <c>docs/adr/0014-generator-emitted-collection-plans.md</c>.
/// </summary>
/// <remarks>
/// <see cref="Diagnostics"/> is always empty for an ordinary walker-discovered entry - it's only ever
/// populated by <c>ComponoIncrementalGenerator</c>'s own merge step, for a synthetic CMP0011 entry
/// reporting that the same closed collection type was discovered with disagreeing element/key
/// nullability, mirroring how <see cref="DiscoveredTypeInfo.Diagnostics"/>'s CMP0010 conflict entry
/// is synthesized at merge time rather than carried from discovery.
/// </remarks>
internal sealed record DiscoveredCollectionInfo(
    CollectionShape Shape,
    string FullyQualifiedCollectionTypeName,
    string ElementFullyQualifiedTypeName,
    bool ElementIsNullable,
    string? KeyFullyQualifiedTypeName,
    bool KeyIsNullable,
    EquatableArray<DiagnosticInfo> Diagnostics);
