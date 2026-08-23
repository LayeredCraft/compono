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
/// <c>CS0029</c> compile failure in generated code before this was corrected). Filtering
/// <see cref="Compilation.GetTypesByMetadataName"/>'s full candidate set down to whichever one is
/// <b>not</b> declared in the compilation's own assembly finds the genuinely external BCL type even
/// when a shadow exists, and the interface's own declared return type (which itself resolves to the
/// shadow, per the same source-wins rule) then correctly fails the identity comparison against it.
/// </remarks>
internal sealed class TaskWellKnownTypes
{
    private static readonly BoundedCacheWithFactory<Compilation, TaskWellKnownTypes> Cache = new();

    private readonly INamedTypeSymbol? _taskOfT;
    private readonly INamedTypeSymbol? _valueTaskOfT;

    private TaskWellKnownTypes(Compilation compilation)
    {
        _taskOfT = ResolveExternal(compilation, "System.Threading.Tasks.Task`1");
        _valueTaskOfT = ResolveExternal(compilation, "System.Threading.Tasks.ValueTask`1");
    }

    internal static TaskWellKnownTypes GetOrCreate(Compilation compilation) =>
        Cache.GetOrCreateValue(compilation, static c => new TaskWellKnownTypes(c));

    /// <summary>Whether <paramref name="named"/> is a closed instantiation of the real, externally-referenced BCL <c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c>.</summary>
    internal bool IsTaskOfTOrValueTaskOfT(INamedTypeSymbol named) =>
        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, _taskOfT) ||
        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, _valueTaskOfT);

    private static INamedTypeSymbol? ResolveExternal(Compilation compilation, string metadataName) =>
        compilation.GetTypesByMetadataName(metadataName)
            .FirstOrDefault(t => !SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, compilation.Assembly));
}
