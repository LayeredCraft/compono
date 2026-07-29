namespace Compono;

/// <summary>
/// What a <see cref="CompositionRequestDescriptor"/> is requesting a value for.
/// </summary>
public enum CompositionRequestKind
{
    /// <summary>The request is for a selected constructor's parameter.</summary>
    ConstructorParameter,

    /// <summary>The request is for a required init-only member.</summary>
    RequiredMember,

    /// <summary>
    /// The request is for one element of a sequence-shaped collection (array, <c>List&lt;T&gt;</c>,
    /// <c>HashSet&lt;T&gt;</c>) at a given index - emitted only by a generated collection plan, per
    /// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s third amendment.
    /// </summary>
    CollectionElement,

    /// <summary>The request is for a <c>Dictionary&lt;TKey, TValue&gt;</c> entry's key at a given position.</summary>
    DictionaryKey,

    /// <summary>The request is for a <c>Dictionary&lt;TKey, TValue&gt;</c> entry's value at a given position.</summary>
    DictionaryValue,
}
