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
    public static Result Select(INamedTypeSymbol type, Compilation compilation)
    {
        // An abstract type can legally have a public constructor (called only from a derived
        // class's constructor) - `new AbstractType(...)` is never legal regardless, so this has
        // to be checked before constructor accessibility even matters.
        if (type.IsAbstract)
            return Result.Failure(new DiagnosticInfo(
                DiagnosticDescriptors.TypeNotConstructible,
                LocationInfo.From(type),
                type.ToDisplayString()));

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
                LocationInfo.From(type),
                type.ToDisplayString()));

        if (constructors.Length == 1)
            return Result.Success(constructors[0]);

        return Result.Failure(new DiagnosticInfo(
            DiagnosticDescriptors.AmbiguousConstructor,
            LocationInfo.From(type),
            type.ToDisplayString(),
            constructors.Length));
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
