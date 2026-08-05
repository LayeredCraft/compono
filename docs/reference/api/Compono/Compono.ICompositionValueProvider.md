#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ICompositionValueProvider Interface

A public extension point for pipeline stage 5 \(semantic value providers\) or stage 6
\(test\-double providers\) \- open\-ended, pattern\-matching composition logic a closed\-set
`.For<T>()` rule can't express \("any interface type," "any member named
`Email`"\)\. Registered via [AddSemanticProvider\(ICompositionValueProvider\)](Compono.CompositionBuilder.AddSemanticProvider(Compono.ICompositionValueProvider).md 'Compono\.CompositionBuilder\.AddSemanticProvider\(Compono\.ICompositionValueProvider\)') or
[AddTestDoubleProvider\(ICompositionValueProvider\)](Compono.CompositionBuilder.AddTestDoubleProvider(Compono.ICompositionValueProvider).md 'Compono\.CompositionBuilder\.AddTestDoubleProvider\(Compono\.ICompositionValueProvider\)') \- which method an integration's own
`UseX()` extension calls decides which stage a given instance participates in; the
interface itself is not stage\-specific\. See
`docs/adr/0024-public-provider-extensibility-model.md`\.

```csharp
public interface ICompositionValueProvider
```

### Remarks
An implementation must be safe to invoke repeatedly, including concurrently, once constructed \-
a [Composer](Compono.Composer.md 'Compono\.Composer')'s configuration \(and every provider registered into it\) is immutable
and reused across every composition call it ever serves, exactly like every other
builder\-compiled piece of configuration \(a `.For<T>()` rule, a registration factory\)\.

| Methods | |
| :--- | :--- |
| [TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') | Attempts to produce a value for [request](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md#Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)\.request')\. Returns [NotHandled](Compono.CompositionProviderResult.NotHandled.md 'Compono\.CompositionProviderResult\.NotHandled') for any request this provider doesn't apply to, so a later provider or pipeline stage still gets a chance \- never throws for an expected non\-match\. |
