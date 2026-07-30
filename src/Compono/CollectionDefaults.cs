namespace Compono;

/// <summary>
/// The built-in collection size a generated collection plan falls back to when no
/// <c>WithCollectionSize</c> configuration applies - the constant <c>docs/adr/0013-collection-generation-semantics.md</c>
/// fixed and <c>docs/adr/0020-composition-configuration-rules.md</c> parameterizes without reopening.
/// </summary>
internal static class CollectionDefaults
{
    /// <summary>The built-in default collection size.</summary>
    internal const int Size = 3;
}
