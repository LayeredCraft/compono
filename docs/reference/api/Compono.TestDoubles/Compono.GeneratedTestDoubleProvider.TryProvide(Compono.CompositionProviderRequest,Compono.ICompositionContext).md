#### [Compono\.TestDoubles](index.md 'index')
### [Compono](Compono.md 'Compono').[GeneratedTestDoubleProvider](Compono.GeneratedTestDoubleProvider.md 'Compono\.GeneratedTestDoubleProvider')

## GeneratedTestDoubleProvider\.TryProvide\(CompositionProviderRequest, ICompositionContext\) Method

Attempts to produce a value for [request](Compono.GeneratedTestDoubleProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md#Compono.GeneratedTestDoubleProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request 'Compono\.GeneratedTestDoubleProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)\.request')\. Returns
[NotHandled](../Compono/Compono.CompositionProviderResult.NotHandled.md 'Compono\.CompositionProviderResult\.NotHandled') for any request this provider doesn't
apply to, so a later provider or pipeline stage still gets a chance \- never throws for an
expected non\-match\.

```csharp
public Compono.CompositionProviderResult TryProvide(in Compono.CompositionProviderRequest request, Compono.ICompositionContext context);
```
#### Parameters

<a name='Compono.GeneratedTestDoubleProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request'></a>

`request` [CompositionProviderRequest](../Compono/Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')

The request to attempt\.

<a name='Compono.GeneratedTestDoubleProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).context'></a>

`context` [ICompositionContext](../Compono/Compono.ICompositionContext.md 'Compono\.ICompositionContext')

The active composition context \- a provider may call `context.Resolve<T>()` to
compose part of its value from a nested request, exactly as an internal provider already may
\(`docs/architecture.md`'s Providers section\)\. Asking this same provider to resolve the
exact same requested type again \(a genuine cycle, not an ordinary nested request for a
different type\) is detected and reported as a diagnosed [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')
rather than recursing indefinitely \- see `docs/adr/0024-public-provider-extensibility-model.md`'s
Amendment 1\.

Implements [TryProvide\(CompositionProviderRequest, ICompositionContext\)](../Compono/Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest@,Compono\.ICompositionContext\)')

#### Returns
[CompositionProviderResult](../Compono/Compono.CompositionProviderResult.md 'Compono\.CompositionProviderResult')