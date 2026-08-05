#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CollectionPlanCache\<T\> Class

Holds the generated collection [ICompositionPlan&lt;T&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>') for the closed collection type
[T](Compono.CollectionPlanCache_T_.md#Compono.CollectionPlanCache_T_.T 'Compono\.CollectionPlanCache\<T\>\.T') \(e\.g\. `List<Address>`\), per
`docs/adr/0014-generator-emitted-collection-plans.md`\.

```csharp
public static class CollectionPlanCache<T>
```
#### Type parameters

<a name='Compono.CollectionPlanCache_T_.T'></a>

`T`

The closed collection type the cached plan constructs\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CollectionPlanCache\<T\>

### Remarks
Mirrors [PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>') exactly \- a closed generic static field is a direct field read
on `Compono.CompositionContext`'s hot path, not a `typeof(T)`\-keyed dictionary lookup and
not runtime reflection\. Kept as a distinct cache from [PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>') rather than reused
for both: dispatch through this cache happens at stage 7 \(built\-in value providers\), after
registrations/profile/semantic/test\-double providers have already had first refusal, whereas
[PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>') dispatch is stage 8\.

[Instance](Compono.CollectionPlanCache_T_.Instance.md 'Compono\.CollectionPlanCache\<T\>\.Instance') is populated exactly once, by a generated module initializer in the
            consuming assembly (never by `Compono` itself) - the same cross-assembly reason
            [PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>')'s setter is [public](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/public 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/public') despite `coding-standards.md`'s
            "no static singletons" rule.

When every type composing the closed [T](Compono.CollectionPlanCache_T_.md#Compono.CollectionPlanCache_T_.T 'Compono\.CollectionPlanCache\<T\>\.T') is a BCL type (e.g.
`List<int>`), this closed instantiation's home context is the non-collectible default
context, even though the plan instance stored in it is defined in the consuming assembly - if that
assembly is loaded into a collectible `AssemblyLoadContext`, this field permanently roots it.
Deferred - see `docs/architecture.md`'s Open Architectural Decisions.

| Properties | |
| :--- | :--- |
| [Instance](Compono.CollectionPlanCache_T_.Instance.md 'Compono\.CollectionPlanCache\<T\>\.Instance') | The generated collection plan for [T](Compono.CollectionPlanCache_T_.md#Compono.CollectionPlanCache_T_.T 'Compono\.CollectionPlanCache\<T\>\.T'), or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if none has been registered \- either because [T](Compono.CollectionPlanCache_T_.md#Compono.CollectionPlanCache_T_.T 'Compono\.CollectionPlanCache\<T\>\.T') was never discovered in the transitive composition graph reachable from a `Composer.Create<T>()` call site, or because the consuming assembly's module initializers haven't run yet\. |
