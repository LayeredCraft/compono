#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>')

## PlanCache\<T\>\.Instance Property

The generated plan for [T](Compono.PlanCache_T_.md#Compono.PlanCache_T_.T 'Compono\.PlanCache\<T\>\.T'), or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if none has been
registered — either because [T](Compono.PlanCache_T_.md#Compono.PlanCache_T_.T 'Compono\.PlanCache\<T\>\.T') was never discovered at a
`Composer.Create<T>()` call site, or because the consuming assembly's module
initializers haven't run yet\.

```csharp
public static Compono.ICompositionPlan<T>? Instance { get; set; }
```

#### Property Value
[Compono\.ICompositionPlan&lt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>')[T](Compono.PlanCache_T_.md#Compono.PlanCache_T_.T 'Compono\.PlanCache\<T\>\.T')[&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>')