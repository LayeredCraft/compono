#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CollectionPlanCache&lt;T&gt;](Compono.CollectionPlanCache_T_.md 'Compono\.CollectionPlanCache\<T\>')

## CollectionPlanCache\<T\>\.Instance Property

The generated collection plan for [T](Compono.CollectionPlanCache_T_.md#Compono.CollectionPlanCache_T_.T 'Compono\.CollectionPlanCache\<T\>\.T'), or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if none
has been registered \- either because [T](Compono.CollectionPlanCache_T_.md#Compono.CollectionPlanCache_T_.T 'Compono\.CollectionPlanCache\<T\>\.T') was never discovered in the
transitive composition graph reachable from a `Composer.Create<T>()` call site, or
because the consuming assembly's module initializers haven't run yet\.

```csharp
public static Compono.ICompositionPlan<T>? Instance { get; set; }
```

#### Property Value
[Compono\.ICompositionPlan&lt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>')[T](Compono.CollectionPlanCache_T_.md#Compono.CollectionPlanCache_T_.T 'Compono\.CollectionPlanCache\<T\>\.T')[&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>')