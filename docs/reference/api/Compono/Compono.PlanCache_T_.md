#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## PlanCache\<T\> Class

Holds the generated [ICompositionPlan&lt;T&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>') for [T](Compono.PlanCache_T_.md#Compono.PlanCache_T_.T 'Compono\.PlanCache\<T\>\.T'), per
`docs/adr/0004-composition-plan-discovery-and-dispatch.md`'s dispatch mechanism\.

```csharp
public static class PlanCache<T>
```
#### Type parameters

<a name='Compono.PlanCache_T_.T'></a>

`T`

The type the cached plan constructs\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → PlanCache\<T\>

### Remarks
A closed generic static field is one field per closed generic type in the CLR, so
[Instance](Compono.PlanCache_T_.Instance.md 'Compono\.PlanCache\<T\>\.Instance') is a direct field read on [Composer](Compono.Composer.md 'Compono\.Composer')'s hot path — not a
`typeof(T)`\-keyed dictionary lookup, and not runtime reflection\.

[Instance](Compono.PlanCache_T_.Instance.md 'Compono\.PlanCache\<T\>\.Instance') is populated exactly once, by a generated module initializer in the
            consuming assembly (never by `Compono` itself) — this is why the setter has to be
            [public](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/public 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/public') despite the "no static singletons" rule in
            `coding-standards.md`: cross-assembly generated code has no other way to reach it. This is a
            deliberate, ADR-recorded exception to that rule, not an oversight.

| Properties | |
| :--- | :--- |
| [Instance](Compono.PlanCache_T_.Instance.md 'Compono\.PlanCache\<T\>\.Instance') | The generated plan for [T](Compono.PlanCache_T_.md#Compono.PlanCache_T_.T 'Compono\.PlanCache\<T\>\.T'), or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if none has been registered — either because [T](Compono.PlanCache_T_.md#Compono.PlanCache_T_.T 'Compono\.PlanCache\<T\>\.T') was never discovered at a `Composer.Create<T>()` call site, or because the consuming assembly's module initializers haven't run yet\. |
