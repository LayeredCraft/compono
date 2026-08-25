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
    // `path` is null for the type requested directly at the call site, or a dotted trail like
    // "Customer.HomeAddress" when `type` was reached recursively while walking a parent type's
    // constructor parameters (TransitiveClosureWalker) - it names the traversal path in the
    // message so a nested failure doesn't just point at the top-level call site with no context.
    public static Result Select(INamedTypeSymbol type, Compilation compilation, LocationInfo? location, string? path = null)
    {
        // An abstract type can legally have a public constructor (called only from a derived
        // class's constructor) - `new AbstractType(...)` is never legal regardless, so this has
        // to be checked before constructor accessibility even matters.
        if (type.IsAbstract)
            return Result.Failure(new DiagnosticInfo(
                DiagnosticDescriptors.TypeNotConstructible,
                location,
                DisplayName(type, path),
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
                DisplayName(type, path),
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
                DisplayName(type, path)));

        // ADR-0052 (Part B, explicit constructor selection).
        // Consulted unconditionally, even when `type` has exactly one accessible constructor - a
        // stale UseConstructor<...>() left behind after an overload was removed must still be
        // diagnosed (CMP0033/CMP0034), not silently ignored just because the type happens to be
        // unambiguous today (code-review finding: the single-constructor fast path previously
        // returned before this scan ever ran). Compilation-wide scoping (ADR-0052's corrected
        // model): a conflicting or invalid selection is diagnosed regardless of which composition
        // path triggered this Analyze call, since there is exactly one generated plan for `type`
        // shared by every path that reaches it.
        var scan = ConstructorSelectionScanner.GetOrCreate(compilation);

        if (scan.TryGetConflict(type, out var conflictDiagnostic))
            return Result.Failure(conflictDiagnostic);

        if (scan.TryGetInvalid(type, out var invalidDiagnostic))
            return Result.Failure(invalidDiagnostic);

        if (scan.TryGetSelection(type, out var selectedConstructor))
            return ValidateParameterKinds(type, selectedConstructor, location, path);

        if (constructors.Length == 1)
            return ValidateParameterKinds(type, constructors[0], location, path);

        return Result.Failure(new DiagnosticInfo(
            DiagnosticDescriptors.AmbiguousConstructor,
            location,
            DisplayName(type, path),
            constructors.Length));
    }

    // The type-name argument passed to every CMP000x diagnostic - quoted, with an optional
    // "(reached via ...)" suffix for a type discovered while walking a parent's constructor
    // parameters rather than requested directly via Composer.Create<T>().
    private static string DisplayName(INamedTypeSymbol type, string? path) =>
        path is null
            ? $"'{type.ToDisplayString()}'"
            : $"'{type.ToDisplayString()}' (reached via {path})";

    // Every parameter shape here fails the same way: `context.Resolve<{{ type }}>()` either can't
    // be written (a `ref`/`out`/`ref readonly` parameter needs a modifier and a local, not an
    // ordinary argument expression) or can't compile once written (a ref struct or pointer type
    // can't be used as a generic type argument - CS0306/CS0611). Report a diagnostic instead of
    // emitting generated code that doesn't compile. `in` is deliberately allowed: callers may
    // legally pass a plain value to an `in` parameter with no modifier at all.
    private static Result ValidateParameterKinds(INamedTypeSymbol type, IMethodSymbol constructor, LocationInfo? location, string? path)
    {
        foreach (var parameter in constructor.Parameters)
        {
            if (parameter.RefKind is RefKind.Ref or RefKind.Out or RefKind.RefReadOnlyParameter)
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedParameterKind,
                    location,
                    DisplayName(type, path),
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
                    DisplayName(type, path),
                    parameter.Name,
                    "as a ref struct (ref-like type), which cannot be used as a generic type argument"));

            if (parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
                return Result.Failure(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedParameterKind,
                    location,
                    DisplayName(type, path),
                    parameter.Name,
                    "as a pointer type, which cannot be used as a generic type argument"));
        }

        return Result.Success(constructor);
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
