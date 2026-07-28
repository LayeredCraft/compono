namespace Compono.Generators.Models;

/// <summary>
/// One required property or field on a discovered type whose selected constructor doesn't already
/// satisfy it via <c>[SetsRequiredMembers]</c>, per
/// <c>docs/adr/0006-required-members-and-nullability-metadata.md</c> - emitted as an
/// object-initializer assignment after the constructor call.
/// </summary>
/// <param name="Name">
/// The emitted object-initializer target - <c>@</c>-escaped if the member is named with a reserved
/// keyword (<see cref="RequiredMemberInfo"/> collection's <c>EscapeIdentifier</c>), since this value
/// is used as literal C# identifier syntax (<c>{Name} = ...</c>).
/// </param>
/// <param name="FullyQualifiedTypeName">The member's type, fully qualified for generated code.</param>
/// <param name="IsNullable">Whether the member's type is nullable-annotated.</param>
/// <param name="DisplayName">
/// The member's real, unescaped symbol name - used only as the
/// <c>CompositionRequestDescriptor</c>'s diagnostic-display string literal, never as identifier
/// syntax. Deliberately distinct from <see cref="Name"/>: reusing the escaped form here would bake
/// a stray <c>@</c> into a string a consumer might read in a failure message, even though nothing
/// about that string needs to be valid C# syntax.
/// </param>
internal sealed record RequiredMemberInfo(string Name, string FullyQualifiedTypeName, bool IsNullable, string DisplayName);
