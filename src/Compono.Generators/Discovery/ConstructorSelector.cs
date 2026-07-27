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
    public static Result Select(INamedTypeSymbol type)
    {
        var constructors = type.Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
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
