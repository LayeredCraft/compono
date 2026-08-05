#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ICompositionValueProvider](Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')

## ICompositionValueProvider\.TryProvide\(CompositionProviderRequest, ICompositionContext\) Method

Attempts to produce a value for [request](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md#Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)\.request')\. Returns
[NotHandled](Compono.CompositionProviderResult.NotHandled.md 'Compono\.CompositionProviderResult\.NotHandled') for any request this provider doesn't
apply to, so a later provider or pipeline stage still gets a chance \- never throws for an
expected non\-match\.

```csharp
Compono.CompositionProviderResult TryProvide(in Compono.CompositionProviderRequest request, Compono.ICompositionContext context);
```
#### Parameters

<a name='Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request'></a>

`request` [CompositionProviderRequest](Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')

The request to attempt\.

<a name='Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).context'></a>

`context` [ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')

The active composition context \- a provider may call `context.Resolve<T>()` to
compose part of its value from a nested request, exactly as an internal provider already may
\(`docs/architecture.md`'s Providers section\)\. Asking this same provider to resolve the
exact same requested type again \(a genuine cycle, not an ordinary nested request for a
different type\) is detected and reported as a diagnosed [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')
rather than recursing indefinitely \- see `docs/adr/0024-public-provider-extensibility-model.md`'s
Amendment 1\.

#### Returns
[CompositionProviderResult](Compono.CompositionProviderResult.md 'Compono\.CompositionProviderResult')