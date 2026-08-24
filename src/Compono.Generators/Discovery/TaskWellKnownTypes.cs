using System.Linq;
using Compono.Generators.WellKnownTypes;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Resolves the real, externally-referenced BCL <c>System.Threading.Tasks.Task&lt;T&gt;</c>/
/// <c>System.Threading.Tasks.ValueTask&lt;T&gt;</c> type symbols by identity, not by
/// namespace/simple-name/nesting alone - kept distinct from <see cref="WellKnownTypes.WellKnownTypes"/>
/// for the same generic-arity reason <see cref="CollectionWellKnownTypes"/> already documents (that
/// type's debug self-check assumes a non-generic metadata name). A consumer's own type sharing the
/// "System.Threading.Tasks" namespace and the simple name "Task"/"ValueTask" - nested (round 4) or
/// top-level (round 5) - is never conflated with the genuine BCL type once compared through here.
/// </summary>
/// <remarks>
/// Deliberately uses <see cref="Compilation.GetTypesByMetadataName"/> (plural, every candidate across
/// every assembly), not the simpler singular <see cref="Compilation.GetTypeByMetadataName"/> - proven
/// wrong for this purpose by a real compile spike (Codex review, PR #107 round 5): when a consumer's
/// own compilation declares a type of the identical fully-qualified name as one in a referenced
/// assembly, C# resolves that specific name to the *source*-declared type everywhere in that
/// compilation (<c>CS0436</c>, "using the type defined in 'source.cs'"), and
/// <c>GetTypeByMetadataName</c> follows that exact same source-wins rule rather than returning
/// <see langword="null"/> for the ambiguity - so it silently returned the consumer's own shadow type,
/// not the real BCL one, and the fix appeared to do nothing (confirmed with a real
/// <c>CS0029</c> compile failure in generated code before this was corrected).
///
/// The candidate set is then filtered down to whichever one shares its <c>ContainingAssembly</c> with
/// the compilation's own resolved <see cref="SpecialType.System_Object"/> - not merely "any assembly
/// other than the consumer's own" (round 5's original, narrower filter). Every real .NET compilation
/// has exactly one true core/runtime assembly, and <c>System.Object</c> can only ever resolve to a
/// type declared in it - the same assembly the genuine <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c>
/// live in for every target this repo builds against. Round 5's filter alone still let a *third-party*
/// referenced assembly (neither the consumer's own compilation nor the real core assembly) that
/// happens to also declare a type named identically win via arbitrary candidate-list ordering (Codex
/// review, PR #107 round 12) - anchoring to <c>System.Object</c>'s own assembly instead picks the one
/// unambiguously correct candidate regardless of how many impostors are also on the reference list.
/// </remarks>
internal sealed class TaskWellKnownTypes
{
    private static readonly BoundedCacheWithFactory<Compilation, TaskWellKnownTypes> Cache = new();

    private readonly INamedTypeSymbol? _task;
    private readonly INamedTypeSymbol? _taskOfT;
    private readonly INamedTypeSymbol? _valueTaskOfT;

    private TaskWellKnownTypes(Compilation compilation)
    {
        _task = ResolveExternal(compilation, "System.Threading.Tasks.Task");
        _taskOfT = ResolveExternal(compilation, "System.Threading.Tasks.Task`1");
        _valueTaskOfT = ResolveExternal(compilation, "System.Threading.Tasks.ValueTask`1");
    }

    internal static TaskWellKnownTypes GetOrCreate(Compilation compilation) =>
        Cache.GetOrCreateValue(compilation, static c => new TaskWellKnownTypes(c));

    /// <summary>Whether <paramref name="named"/> is a closed instantiation of the real, externally-referenced BCL <c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c>.</summary>
    internal bool IsTaskOfTOrValueTaskOfT(INamedTypeSymbol named) =>
        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, _taskOfT) ||
        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, _valueTaskOfT);

    /// <summary>
    /// Whether <paramref name="named"/> is the real, externally-referenced BCL non-generic
    /// <c>Task</c> - relevant because <see cref="TestDoubleDefaults.TryGetDefaultExpression"/>'s
    /// zero-type-argument branch emits a fully-qualified reference to <c>Task.CompletedTask</c>, which
    /// (like the generic identity checks above) would otherwise silently return the real BCL type for
    /// a member whose declared return type is an unrelated consumer type merely sharing the name
    /// (Codex review, PR #107 round 7). The non-generic <c>ValueTask</c> case needs no equivalent
    /// check - its own zero-argument default expression is the bare "default" literal, which
    /// references no type by name at all and target-types correctly against whatever the explicit
    /// implementation's own declared return type is, real or shadowed alike.
    /// </summary>
    internal bool IsTask(INamedTypeSymbol named) =>
        SymbolEqualityComparer.Default.Equals(named, _task);

    /// <summary>Whether <paramref name="named"/> is a closed instantiation of the real, externally-referenced BCL <c>Task&lt;T&gt;</c> specifically (not <c>ValueTask&lt;T&gt;</c>).</summary>
    internal bool IsTaskOfT(INamedTypeSymbol named) =>
        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, _taskOfT);

    /// <summary>Whether <paramref name="named"/> is a closed instantiation of the real, externally-referenced BCL <c>ValueTask&lt;T&gt;</c> specifically (not <c>Task&lt;T&gt;</c>).</summary>
    internal bool IsValueTaskOfT(INamedTypeSymbol named) =>
        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, _valueTaskOfT);

    private static INamedTypeSymbol? ResolveExternal(Compilation compilation, string metadataName)
    {
        var coreAssembly = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;

        return compilation.GetTypesByMetadataName(metadataName)
            .FirstOrDefault(t => SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, coreAssembly));
    }
}
