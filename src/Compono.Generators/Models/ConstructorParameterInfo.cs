namespace Compono.Generators.Models;

/// <summary>
/// One parameter of the constructor selected for a discovered type, per
/// <c>docs/adr/0002-constructor-selection-algorithm.md</c>.
/// </summary>
internal sealed record ConstructorParameterInfo(string Name, string FullyQualifiedTypeName);
