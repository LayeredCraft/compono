#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionProviderResult Struct

What an [ICompositionValueProvider](Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider') reports for one [CompositionProviderRequest](Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')\.

```csharp
public readonly struct CompositionProviderResult
```

### Remarks
Deliberately only two cases, mirroring the engine's own internal provider result contract: a
public provider can report that it doesn't apply, or that it produced a value \- never a
stronger "failure\." An unhandled exception a provider's own
[TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') implementation throws propagates uncaught,
exactly like an internal pipeline\-stage provider's exception does today\. See
`docs/adr/0024-public-provider-extensibility-model.md`\.

| Properties | |
| :--- | :--- |
| [NotHandled](Compono.CompositionProviderResult.NotHandled.md 'Compono\.CompositionProviderResult\.NotHandled') | The provider does not handle this request\. |

| Methods | |
| :--- | :--- |
| [Handled\(object\)](Compono.CompositionProviderResult.Handled(object).md 'Compono\.CompositionProviderResult\.Handled\(object\)') | The provider produced [value](Compono.CompositionProviderResult.Handled(object).md#Compono.CompositionProviderResult.Handled(object).value 'Compono\.CompositionProviderResult\.Handled\(object\)\.value') for this request\. |
