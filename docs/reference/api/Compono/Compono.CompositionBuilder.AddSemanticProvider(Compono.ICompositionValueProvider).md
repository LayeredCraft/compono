#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.AddSemanticProvider\(ICompositionValueProvider\) Method

Registers [provider](Compono.CompositionBuilder.AddSemanticProvider(Compono.ICompositionValueProvider).md#Compono.CompositionBuilder.AddSemanticProvider(Compono.ICompositionValueProvider).provider 'Compono\.CompositionBuilder\.AddSemanticProvider\(Compono\.ICompositionValueProvider\)\.provider') into pipeline stage 5 \(semantic value providers\) \- the
open\-ended, pattern\-matching extension point an integration package \(e\.g\. a future
`Compono.Bogus`\) uses instead of a closed\-set [For&lt;T&gt;\(\)](Compono.CompositionBuilder.For_T_().md 'Compono\.CompositionBuilder\.For\<T\>\(\)') rule\. See
`docs/adr/0024-public-provider-extensibility-model.md`\.

```csharp
public Compono.CompositionBuilder AddSemanticProvider(Compono.ICompositionValueProvider provider);
```
#### Parameters

<a name='Compono.CompositionBuilder.AddSemanticProvider(Compono.ICompositionValueProvider).provider'></a>

`provider` [ICompositionValueProvider](Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')

The provider to register\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

### Remarks
Multiple providers may be registered \- tried in registration order, same as every other
extensible pipeline stage; the first to report a value wins\.