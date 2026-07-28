using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Collects a discovered type's required properties/fields for object-initializer emission, per
/// <c>docs/adr/0006-required-members-and-nullability-metadata.md</c>. A type whose selected
/// constructor is <c>[SetsRequiredMembers]</c>-annotated already satisfies every required member
/// itself, so it contributes none here - everything else walks the type and its base types the same
/// way <see cref="ConstructorSelector"/> already did before Phase 3, but instead of rejecting the
/// type outright, each required member is validated the same way a constructor parameter is
/// (<see cref="ConstructorSelector"/>'s ref-like/pointer checks) and handed back for emission.
/// </summary>
internal static class RequiredMemberCollector
{
    public static Result Collect(INamedTypeSymbol type, IMethodSymbol constructor, LocationInfo? location, string? path)
    {
        if (constructor.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
            return Result.Success(EquatableArray<RequiredMemberInfo>.Empty, []);

        var requiredMembers = EnumerateTypeAndBases(type)
            .SelectMany(t => t.GetMembers())
            .Where(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
            .ToArray();

        if (requiredMembers.Length == 0)
            return Result.Success(EquatableArray<RequiredMemberInfo>.Empty, []);

        var infos = new List<RequiredMemberInfo>(requiredMembers.Length);
        var memberTypes = new List<(ITypeSymbol Type, string Name)>(requiredMembers.Length);

        foreach (var member in requiredMembers)
        {
            var memberType = member switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => throw new InvalidOperationException("Unreachable: filtered to properties/fields above."),
            };

            // Same rationale as ConstructorSelector.ValidateParameterKinds - a ref-like or pointer
            // type can't be used as Resolve<T>()'s generic type argument (CS0306/CS0611), so a
            // required member of either shape would emit generated code that fails to compile.
            if (memberType.IsRefLikeType)
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedRequiredMemberKind,
                    location,
                    DisplayName(type, path),
                    member.Name,
                    "as a ref struct (ref-like type), which cannot be used as a generic type argument"));

            if (memberType.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedRequiredMemberKind,
                    location,
                    DisplayName(type, path),
                    member.Name,
                    "as a pointer type, which cannot be used as a generic type argument"));

            infos.Add(new RequiredMemberInfo(
                member.Name,
                memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                memberType.NullableAnnotation == NullableAnnotation.Annotated));
            memberTypes.Add((memberType, member.Name));
        }

        return Result.Success(infos.ToEquatableArray(), memberTypes);
    }

    private static string DisplayName(INamedTypeSymbol type, string? path) =>
        path is null
            ? $"'{type.ToDisplayString()}'"
            : $"'{type.ToDisplayString()}' (reached via {path})";

    private static IEnumerable<INamedTypeSymbol> EnumerateTypeAndBases(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }

    internal readonly struct Result
    {
        private Result(EquatableArray<RequiredMemberInfo> members, IReadOnlyList<(ITypeSymbol Type, string Name)> memberTypes, DiagnosticInfo? diagnostic)
        {
            Members = members;
            MemberTypes = memberTypes;
            Diagnostic = diagnostic;
        }

        public EquatableArray<RequiredMemberInfo> Members { get; }

        // Not part of the emitted model - only used by TransitiveClosureWalker to enqueue each
        // required member's type for recursive discovery, the same way it does for constructor
        // parameters.
        public IReadOnlyList<(ITypeSymbol Type, string Name)> MemberTypes { get; }

        public DiagnosticInfo? Diagnostic { get; }

        public bool IsSuccess => Diagnostic is null;

        public static Result Success(EquatableArray<RequiredMemberInfo> members, IReadOnlyList<(ITypeSymbol, string)> memberTypes) =>
            new(members, memberTypes, null);

        public static Result Failure(DiagnosticInfo diagnostic) =>
            new(EquatableArray<RequiredMemberInfo>.Empty, [], diagnostic);
    }
}
