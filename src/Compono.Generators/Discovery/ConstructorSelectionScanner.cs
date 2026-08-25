using System.Runtime.CompilerServices;
using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Compono.Generators.Discovery;

/// <summary>
/// ADR-0052 (Part B, explicit constructor selection): finds every
/// <c>builder.For&lt;T&gt;().UseConstructor&lt;T1, ...&gt;()</c> call anywhere in the compilation and
/// resolves each to exactly one of <see cref="Result"/>'s three outcomes per type - a
/// selected constructor symbol, a conflict (two different selections for the same type), or an
/// invalid selection (no accessible constructor matches the requested parameter-type list).
/// <see cref="ConstructorSelector.Select"/> is this scanner's only consumer - it is intentionally
/// narrow to that one recognized call shape, not a general-purpose syntax scanner, and recognizes
/// nothing else (deliberately does not walk into a matched call's own arguments, nested
/// invocations, or any other member name - "one clear responsibility" per this ADR's own
/// constraint).
/// </summary>
/// <remarks>
/// <para>
/// <b>Compilation-wide, not per-profile</b> - a generated composition plan is one plan per type,
/// shared by every composition path that reaches it (confirmed directly against
/// <c>DiscoveredTypeInfo</c>/<c>ComposeMethodDiscovery</c> during this ADR's design dive - see
/// "Real incremental-generator pipeline spike" there). A selection is therefore resolved once per
/// type for the whole <see cref="Compilation"/>, not scoped to which profile/composition path
/// triggered discovery.
/// </para>
/// <para>
/// <b>Deterministic, order-independent</b> - <see cref="Compilation.SyntaxTrees"/> is walked in
/// whatever order Roslyn provides it (not guaranteed stable across runs), but the outcome per type
/// does not depend on that order: an idempotent (identical) repeated selection is order-independent
/// by construction (same symbol either way), and a genuine conflict is detected regardless of which
/// of the two conflicting selections is visited first - the diagnostic message names both, not just
/// "whichever came second."
/// </para>
/// <para>
/// <b>Malformed/incomplete source (mid-edit in an IDE)</b> - <c>SemanticModel.GetSymbolInfo</c>
/// returns no symbol for an invocation Roslyn can't yet bind (an incomplete generic argument list, a
/// typo'd member name, code the user is still typing) - such a call is simply skipped, exactly like
/// "not a <c>UseConstructor</c> call at all," never a generator crash or a spurious diagnostic on
/// unrelated code. Every real diagnostic this scanner attaches only fires once C# itself successfully
/// binds the offending call.
/// </para>
/// </remarks>
internal static class ConstructorSelectionScanner
{
    private static readonly ConditionalWeakTable<Compilation, Result> Cache = new();

    /// <summary>
    /// Returns this compilation's scan result, computing it once per <see cref="Compilation"/>
    /// instance and reusing it for every subsequent <see cref="ConstructorSelector.Select"/> call
    /// against the same compilation - the same caching shape
    /// <c>Compono.Generators.WellKnownTypes.WellKnownTypes.GetOrCreate</c> already uses.
    /// </summary>
    public static Result GetOrCreate(Compilation compilation) =>
        Cache.GetValue(compilation, static c => Scan(c));

    private static Result Scan(Compilation compilation)
    {
        var selections = new Dictionary<INamedTypeSymbol, (IMethodSymbol Constructor, Location Location)>(SymbolEqualityComparer.Default);
        var conflicts = new Dictionary<INamedTypeSymbol, DiagnosticInfo>(SymbolEqualityComparer.Default);
        var invalid = new Dictionary<INamedTypeSymbol, DiagnosticInfo>(SymbolEqualityComparer.Default);

        // Resolved once per compilation and compared by symbol identity below, not by matching the
        // containing type's simple name/arity - a consumer-defined type also named
        // `CompositionTypeRuleBuilder<T>` with its own generic `UseConstructor` method must never be
        // mistaken for Compono's real one (code-review finding). `null` means the real
        // `Compono.CompositionTypeRuleBuilder<T>` isn't even referenced by this compilation, so no
        // call anywhere in it can possibly be a real `UseConstructor` selection.
        var realBuilderType = GetRealCompositionTypeRuleBuilder(compilation);
        if (realBuilderType is null)
            return new Result(selections, conflicts, invalid);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                // Recognizes both the generic overloads (UseConstructor<T1, ...>()) and the
                // non-generic, arity-0 overload (UseConstructor(), selecting a parameterless
                // constructor - code-review finding: only the generic syntax was ever matched, so
                // a consumer had no way to select a parameterless constructor at all).
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    {
                        Name: GenericNameSyntax { Identifier.Text: "UseConstructor" }
                            or IdentifierNameSyntax { Identifier.Text: "UseConstructor" },
                    })
                {
                    continue;
                }

                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol
                    {
                        Name: "UseConstructor",
                        ContainingType: { TypeArguments: [var targetTypeArg] } containingType,
                    } method)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, realBuilderType))
                    continue;

                if (targetTypeArg is not INamedTypeSymbol targetType)
                    continue;

                var requestedParamTypes = method.TypeArguments;

                // Excludes ref/out/ref-readonly parameters from matching entirely, not just type -
                // ConstructorSelector.ValidateParameterKinds rejects those unconditionally after a
                // selection resolves a constructor, so a by-ref overload can never actually be the
                // one a consumer's UseConstructor<...>() call means to select. Leaving it in the
                // match candidates let pure parameter-type equality (e.g. Foo(ref int) vs. Foo(int))
                // pick whichever overload happened to come first in source declaration order -
                // matching by type alone can't tell them apart. `in` is deliberately still matched
                // (ValidateParameterKinds allows it too).
                var matched = targetType.Constructors.FirstOrDefault(c =>
                    !c.IsStatic &&
                    compilation.IsSymbolAccessibleWithin(c, compilation.Assembly) &&
                    c.Parameters.Length == requestedParamTypes.Length &&
                    c.Parameters.All(p => p.RefKind is RefKind.None or RefKind.In) &&
                    c.Parameters.Select(p => p.Type).SequenceEqual(requestedParamTypes, SymbolEqualityComparer.Default));

                if (matched is null)
                {
                    invalid[targetType] = new DiagnosticInfo(
                        DiagnosticDescriptors.InvalidConstructorSelection,
                        LocationInfo.From(invocation),
                        targetType.ToDisplayString(),
                        string.Join(", ", requestedParamTypes.Select(t => t.ToDisplayString())));
                    continue;
                }

                if (selections.TryGetValue(targetType, out var existing))
                {
                    // Idempotent repeat (same real constructor symbol) - accept silently, no conflict.
                    if (SymbolEqualityComparer.Default.Equals(existing.Constructor, matched))
                        continue;

                    conflicts[targetType] = new DiagnosticInfo(
                        DiagnosticDescriptors.ConflictingConstructorSelection,
                        LocationInfo.From(invocation),
                        targetType.ToDisplayString(),
                        existing.Constructor.ToDisplayString(),
                        matched.ToDisplayString());
                    continue;
                }

                selections[targetType] = (matched, invocation.GetLocation());
            }
        }

        return new Result(selections, conflicts, invalid);
    }

    // Same "filter to the well-known assembly" shape as
    // Compono.Generators.WellKnownTypes.WellKnownTypes.GetTypeByMetadataNameInTargetAssembly - not
    // reused directly since that cache's table doesn't carry generic-arity metadata names, and this
    // scanner only ever needs this one open-generic type, resolved once per compilation.
    private static INamedTypeSymbol? GetRealCompositionTypeRuleBuilder(Compilation compilation)
    {
        var candidates = compilation.GetTypesByMetadataName("Compono.CompositionTypeRuleBuilder`1");

        return candidates.Length switch
        {
            0 => null,
            1 => candidates[0],
            _ => candidates.FirstOrDefault(t =>
                t.ContainingAssembly.Identity.Name.Equals("Compono", StringComparison.Ordinal)),
        };
    }

    internal sealed class Result
    {
        private readonly Dictionary<INamedTypeSymbol, (IMethodSymbol Constructor, Location Location)> _selections;
        private readonly Dictionary<INamedTypeSymbol, DiagnosticInfo> _conflicts;
        private readonly Dictionary<INamedTypeSymbol, DiagnosticInfo> _invalid;

        public Result(
            Dictionary<INamedTypeSymbol, (IMethodSymbol Constructor, Location Location)> selections,
            Dictionary<INamedTypeSymbol, DiagnosticInfo> conflicts,
            Dictionary<INamedTypeSymbol, DiagnosticInfo> invalid)
        {
            _selections = selections;
            _conflicts = conflicts;
            _invalid = invalid;
        }

        public bool TryGetConflict(INamedTypeSymbol type, out DiagnosticInfo diagnostic) =>
            _conflicts.TryGetValue(type, out diagnostic!);

        public bool TryGetInvalid(INamedTypeSymbol type, out DiagnosticInfo diagnostic) =>
            _invalid.TryGetValue(type, out diagnostic!);

        public bool TryGetSelection(INamedTypeSymbol type, out IMethodSymbol constructor)
        {
            if (_selections.TryGetValue(type, out var entry))
            {
                constructor = entry.Constructor;
                return true;
            }

            constructor = null!;
            return false;
        }
    }
}
