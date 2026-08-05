#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.AddTestDoubleProvider\(ICompositionValueProvider\) Method

Registers [provider](Compono.CompositionBuilder.AddTestDoubleProvider(Compono.ICompositionValueProvider).md#Compono.CompositionBuilder.AddTestDoubleProvider(Compono.ICompositionValueProvider).provider 'Compono\.CompositionBuilder\.AddTestDoubleProvider\(Compono\.ICompositionValueProvider\)\.provider') into pipeline stage 6 \(test\-double providers\) \- the
open\-ended, pattern\-matching extension point an integration package \(e\.g\.
`Compono.NSubstitute`\) uses instead of a closed\-set [For&lt;T&gt;\(\)](Compono.CompositionBuilder.For_T_().md 'Compono\.CompositionBuilder\.For\<T\>\(\)') rule\. See
`docs/adr/0024-public-provider-extensibility-model.md`\.

```csharp
public Compono.CompositionBuilder AddTestDoubleProvider(Compono.ICompositionValueProvider provider);
```
#### Parameters

<a name='Compono.CompositionBuilder.AddTestDoubleProvider(Compono.ICompositionValueProvider).provider'></a>

`provider` [ICompositionValueProvider](Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')

The provider to register\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

### Remarks
Multiple providers may be registered \- tried in registration order, same as every other
extensible pipeline stage; the first to report a value wins\.