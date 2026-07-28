using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
    public static Result Collect(INamedTypeSymbol type, IMethodSymbol constructor, Compilation compilation, LocationInfo? location, string? path)
    {
        if (constructor.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
            return Result.Success(EquatableArray<RequiredMemberInfo>.Empty, []);

        // Base-to-derived, so a required member's ordinal - the stable identity
        // CompositionRequestDescriptor carries for path/random-fork identity, per
        // docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md's amendment
        // 2 - reflects its position in the inheritance hierarchy, not which type happens to
        // override it. An override doesn't move its member to a new structural position: it only
        // refines the member the base type already declared, so the base declaration's position in
        // this walk is what determines the ordinal, while the most-derived override's symbol
        // (correct accessibility/type info) is what actually gets emitted. Declaration order is
        // preserved within each type's own GetMembers() call.
        var typeChain = EnumerateTypeAndBases(type).Reverse().ToArray();

        var orderedNames = new List<string>();
        var latestByName = new Dictionary<string, ISymbol>();

        foreach (var t in typeChain)
        {
            foreach (var member in t.GetMembers())
            {
                if (member is not (IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }))
                    continue;

                if (!latestByName.ContainsKey(member.Name))
                    orderedNames.Add(member.Name);

                latestByName[member.Name] = member;
            }
        }

        var requiredMembers = orderedNames.Select(name => latestByName[name]).ToArray();

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

            // The C# compiler only lets a *C#-authored* required member declare a shape the
            // generated object initializer can actually assign (an accessible init/set accessor,
            // a non-readonly field) - but `IsRequired` is ultimately just metadata Roslyn reads off
            // any assembly, C#-authored or not. A required property with no accessible setter, or
            // a required field that's readonly or itself inaccessible, is a shape the C# compiler
            // would never let a C# type declare but a non-C# (or hand-crafted IL) assembly can
            // still expose - emitting an assignment to either produces CS0272 (no accessible
            // setter) or CS0191 (assigning a readonly field outside its declaring constructor) in
            // the generated file. Same rationale as ConstructorSelector's own
            // compilation.IsSymbolAccessibleWithin check on constructors: verify against the real
            // accessibility-domain rules, not just an accessibility-enum check, since a member
            // that's merely `internal` in a referenced assembly is only actually assignable from
            // generated code (which lives in the consuming assembly) with an InternalsVisibleTo
            // grant.
            if (!IsAssignableFromGeneratedCode(member, compilation))
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedRequiredMemberKind,
                    location,
                    DisplayName(type, path),
                    member.Name,
                    member is IFieldSymbol
                        ? "as a readonly or inaccessible field, which Compono cannot assign in a generated object initializer"
                        : "with no accessible init/set accessor, which Compono cannot assign in a generated object initializer"));

            infos.Add(new RequiredMemberInfo(
                EscapeIdentifier(member.Name),
                memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                memberType.NullableAnnotation == NullableAnnotation.Annotated,
                member.Name));
            memberTypes.Add((memberType, member.Name));
        }

        return Result.Success(infos.ToEquatableArray(), memberTypes);
    }

    private static string DisplayName(INamedTypeSymbol type, string? path) =>
        path is null
            ? $"'{type.ToDisplayString()}'"
            : $"'{type.ToDisplayString()}' (reached via {path})";

    private static bool IsAssignableFromGeneratedCode(ISymbol member, Compilation compilation) => member switch
    {
        IPropertySymbol { SetMethod: { } setMethod } => compilation.IsSymbolAccessibleWithin(setMethod, compilation.Assembly),
        IPropertySymbol => false,
        IFieldSymbol field => !field.IsReadOnly && compilation.IsSymbolAccessibleWithin(field, compilation.Assembly),
        _ => throw new InvalidOperationException("Unreachable: filtered to properties/fields above."),
    };

    // A required member can legally be named with an escaped reserved keyword
    // (`public required string @class { get; init; }`) - member.Name reports it as the bare
    // keyword text ("class"), which the template emits directly into an object-initializer target
    // (`class = ...`). That's a reserved word, not a valid identifier there, so it has to be
    // re-escaped with `@` before emission - contextual keywords (`var`, `async`, ...) are legal
    // identifiers unescaped and must NOT get an `@` prefix, so this checks the real reserved-keyword
    // set via SyntaxFacts rather than a hand-maintained list.
    private static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

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
