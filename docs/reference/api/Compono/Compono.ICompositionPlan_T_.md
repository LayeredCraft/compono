#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ICompositionPlan\<T\> Interface

A source\-generated construction plan for [T](Compono.ICompositionPlan_T_.md#Compono.ICompositionPlan_T_.T 'Compono\.ICompositionPlan\<T\>\.T') — invokes [T](Compono.ICompositionPlan_T_.md#Compono.ICompositionPlan_T_.T 'Compono\.ICompositionPlan\<T\>\.T')'s
selected constructor directly, per `docs/adr/0002-constructor-selection-algorithm.md`, with no
runtime reflection\.

```csharp
public interface ICompositionPlan<out T>
```
#### Type parameters

<a name='Compono.ICompositionPlan_T_.T'></a>

`T`

The type this plan constructs\.

| Methods | |
| :--- | :--- |
| [Compose\(ICompositionContext\)](Compono.ICompositionPlan_T_.Compose(Compono.ICompositionContext).md 'Compono\.ICompositionPlan\<T\>\.Compose\(Compono\.ICompositionContext\)') | Constructs an instance of [T](Compono.ICompositionPlan_T_.md#Compono.ICompositionPlan_T_.T 'Compono\.ICompositionPlan\<T\>\.T'), resolving any constructor arguments through [context](Compono.ICompositionPlan_T_.Compose(Compono.ICompositionContext).md#Compono.ICompositionPlan_T_.Compose(Compono.ICompositionContext).context 'Compono\.ICompositionPlan\<T\>\.Compose\(Compono\.ICompositionContext\)\.context')\. |
