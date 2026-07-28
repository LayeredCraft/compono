namespace Compono.Generators.Models;

/// <summary>
/// One required property or field on a discovered type whose selected constructor doesn't already
/// satisfy it via <c>[SetsRequiredMembers]</c>, per
/// <c>docs/adr/0006-required-members-and-nullability-metadata.md</c> - emitted as an
/// object-initializer assignment after the constructor call.
/// </summary>
internal sealed record RequiredMemberInfo(string Name, string FullyQualifiedTypeName, bool IsNullable);
