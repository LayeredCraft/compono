using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Implements <c>docs/adr/0002-constructor-selection-algorithm.md</c>: exactly one accessible
/// constructor is selected deterministically, or ambiguity/absence becomes a diagnostic - never a
/// heuristic guess.
/// </summary>
internal static class ConstructorSelector
{
    // `location` is the `Composer.Create<T>()` call site's type-argument location (threaded down
    // from CreateInvocationDiscovery), not T's own declaration - a failing composition is
    // diagnosed where the caller asked for it, not wherever T happens to be declared (which may be
    // a different file entirely, or not even in this compilation for a referenced-assembly type).
    public static Result Select(INamedTypeSymbol type, Compilation compilation, LocationInfo? location)
    {
        // An abstract type can legally have a public constructor (called only from a derived
        // class's constructor) - `new AbstractType(...)` is never legal regardless, so this has
        // to be checked before constructor accessibility even matters.
        if (type.IsAbstract)
            return Result.Failure(new DiagnosticInfo(
                DiagnosticDescriptors.TypeNotConstructible,
                location,
                type.ToDisplayString(),
                "abstract"));

        // A delegate type isn't abstract, and Roslyn exposes a synthetic (object, IntPtr)
        // constructor on it that would otherwise sail through the checks below - but
        // `new SomeDelegate(arg1, arg2)` isn't legal delegate-construction syntax in C# (delegates
        // are constructed from a method group or lambda, not by invoking that synthetic
        // constructor directly), so emitting it would produce a generated plan that fails to
        // compile.
        if (type.TypeKind == TypeKind.Delegate)
            return Result.Failure(new DiagnosticInfo(
                DiagnosticDescriptors.TypeNotConstructible,
                location,
                type.ToDisplayString(),
                "a delegate type"));

        // Checked via the real C# accessibility-domain rules (compilation.IsSymbolAccessibleWithin),
        // not a plain Public-or-Internal filter - a `type` from a referenced assembly with an
        // `internal` constructor is only actually callable from the generated code (which lives in
        // the consuming assembly, compilation.Assembly) if that assembly has an InternalsVisibleTo
        // grant. A plain accessibility-enum check would let this through and emit a generated plan
        // that fails to compile in the consumer's project instead of reporting CMP0002.
        var constructors = type.Constructors
            .Where(c => !c.IsStatic && compilation.IsSymbolAccessibleWithin(c, compilation.Assembly))
            .ToArray();

        if (constructors.Length == 0)
            return Result.Failure(new DiagnosticInfo(
                DiagnosticDescriptors.NoAccessibleConstructor,
                location,
                type.ToDisplayString()));

        if (constructors.Length == 1)
        {
            var constructor = constructors[0];

            // `required` member assignment (via an object initializer) is explicitly deferred to
            // a later milestone - Phase 0 only ever emits bare `new T(...)`. If T has a required
            // member and its constructor isn't annotated [SetsRequiredMembers] (the one way a
            // constructor can satisfy "required" on its own), that bare call is CS9035 in the
            // generated file. Report it instead of emitting code that doesn't compile.
            if (HasUnassignedRequiredMembers(type, constructor))
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnassignedRequiredMembers,
                    location,
                    type.ToDisplayString()));

            return ValidateParameterKinds(type, constructor, location);
        }

        return Result.Failure(new DiagnosticInfo(
            DiagnosticDescriptors.AmbiguousConstructor,
            location,
            type.ToDisplayString(),
            constructors.Length));
    }

    // Every parameter shape here fails the same way: `context.Resolve<{{ type }}>()` either can't
    // be written (a `ref`/`out`/`ref readonly` parameter needs a modifier and a local, not an
    // ordinary argument expression) or can't compile once written (a ref struct or pointer type
    // can't be used as a generic type argument - CS0306/CS0611). Report a diagnostic instead of
    // emitting generated code that doesn't compile. `in` is deliberately allowed: callers may
    // legally pass a plain value to an `in` parameter with no modifier at all.
    private static Result ValidateParameterKinds(INamedTypeSymbol type, IMethodSymbol constructor, LocationInfo? location)
    {
        foreach (var parameter in constructor.Parameters)
        {
            if (parameter.RefKind is RefKind.Ref or RefKind.Out or RefKind.RefReadOnlyParameter)
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedParameterKind,
                    location,
                    type.ToDisplayString(),
                    parameter.Name,
                    parameter.RefKind switch
                    {
                        RefKind.Ref => "by ref",
                        RefKind.Out => "by out",
                        _ => "by ref readonly",
                    }));

            if (parameter.Type.IsRefLikeType)
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedParameterKind,
                    location,
                    type.ToDisplayString(),
                    parameter.Name,
                    "as a ref struct (ref-like type), which cannot be used as a generic type argument"));

            if (parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedParameterKind,
                    location,
                    type.ToDisplayString(),
                    parameter.Name,
                    "as a pointer type, which cannot be used as a generic type argument"));
        }

        return Result.Success(constructor);
    }

    private static bool HasUnassignedRequiredMembers(INamedTypeSymbol type, IMethodSymbol constructor)
    {
        var hasRequiredMembers = EnumerateTypeAndBases(type)
            .SelectMany(t => t.GetMembers())
            .Any(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true });

        if (!hasRequiredMembers)
            return false;

        return !constructor.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypeAndBases(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }

    internal readonly struct Result
    {
        private Result(IMethodSymbol? constructor, DiagnosticInfo? diagnostic)
        {
            Constructor = constructor;
            Diagnostic = diagnostic;
        }

        public IMethodSymbol? Constructor { get; }

        public DiagnosticInfo? Diagnostic { get; }

        public bool IsSuccess => Constructor is not null;

        public static Result Success(IMethodSymbol constructor) => new(constructor, null);

        public static Result Failure(DiagnosticInfo diagnostic) => new(null, diagnostic);
    }
}
