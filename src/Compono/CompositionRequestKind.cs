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
    /// <c>docs/adr/0014-generator-emitted-collection-plans.md</c>.
    /// </summary>
    CollectionElement,

    /// <summary>The request is for a <c>Dictionary&lt;TKey, TValue&gt;</c> entry's key at a given position.</summary>
    DictionaryKey,

    /// <summary>The request is for a <c>Dictionary&lt;TKey, TValue&gt;</c> entry's value at a given position.</summary>
    DictionaryValue,

    /// <summary>
    /// The request is one descriptor-less <see cref="ICompositionContext.Resolve{TValue}()"/> call
    /// made inside a registration or configuration-rule factory, or a public
    /// <see cref="ICompositionValueProvider.TryProvide"/> invocation - never emitted by generated
    /// code, per <c>docs/adr/0019-registrations-and-service-provider-injection.md</c> and
    /// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
    /// </summary>
    ManualResolve,

    /// <summary>
    /// The request is for one of a test method's own parameters, as opposed to a constructor
    /// parameter or required member a generated plan is filling in - emitted only by a test-framework
    /// integration composing a <see cref="CompositionRow"/> row, never by generated code. See
    /// <c>docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md</c>.
    /// </summary>
    TestParameter,
}
