#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ICompositionPlan&lt;T&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>')

## ICompositionPlan\<T\>\.Compose\(ICompositionContext\) Method

Constructs an instance of [T](Compono.ICompositionPlan_T_.md#Compono.ICompositionPlan_T_.T 'Compono\.ICompositionPlan\<T\>\.T'), resolving any constructor arguments through
[context](Compono.ICompositionPlan_T_.Compose(Compono.ICompositionContext).md#Compono.ICompositionPlan_T_.Compose(Compono.ICompositionContext).context 'Compono\.ICompositionPlan\<T\>\.Compose\(Compono\.ICompositionContext\)\.context')\.

```csharp
T Compose(Compono.ICompositionContext context);
```
#### Parameters

<a name='Compono.ICompositionPlan_T_.Compose(Compono.ICompositionContext).context'></a>

`context` [ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')

The active composition context\.

#### Returns
[T](Compono.ICompositionPlan_T_.md#Compono.ICompositionPlan_T_.T 'Compono\.ICompositionPlan\<T\>\.T')