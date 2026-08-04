#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionRequestKind Enum

What a [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor') is requesting a value for\.

```csharp
public enum CompositionRequestKind
```
### Fields

<a name='Compono.CompositionRequestKind.ConstructorParameter'></a>

`ConstructorParameter` 0

The request is for a selected constructor's parameter\.

<a name='Compono.CompositionRequestKind.RequiredMember'></a>

`RequiredMember` 1

The request is for a required init\-only member\.

<a name='Compono.CompositionRequestKind.CollectionElement'></a>

`CollectionElement` 2

The request is for one element of a sequence\-shaped collection \(array, `List<T>`,
`HashSet<T>`\) at a given index \- emitted only by a generated collection plan, per
`docs/adr/0014-generator-emitted-collection-plans.md`\.

<a name='Compono.CompositionRequestKind.DictionaryKey'></a>

`DictionaryKey` 3

The request is for a `Dictionary<TKey, TValue>` entry's key at a given position\.

<a name='Compono.CompositionRequestKind.DictionaryValue'></a>

`DictionaryValue` 4

The request is for a `Dictionary<TKey, TValue>` entry's value at a given position\.

<a name='Compono.CompositionRequestKind.ManualResolve'></a>

`ManualResolve` 5

The request is one descriptor\-less [Resolve&lt;TValue&gt;\(\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_() 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)') call
made inside a registration or configuration\-rule factory, or a public
[TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') invocation \- never emitted by generated
code, per `docs/adr/0019-registrations-and-service-provider-injection.md` and
`docs/adr/0024-public-provider-extensibility-model.md`\.

<a name='Compono.CompositionRequestKind.TestParameter'></a>

`TestParameter` 6

The request is for one of a test method's own parameters, as opposed to a constructor
parameter or required member a generated plan is filling in \- emitted only by a test\-framework
integration composing a [CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow') row, never by generated code\. See
`docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md`\.