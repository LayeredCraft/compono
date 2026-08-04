#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## Composer Class

The public entry point for composing test data, per `docs/public-api.md`\.

```csharp
public sealed class Composer
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → Composer

### Remarks
`docs/architecture.md`'s builder configuration \(`Composer.Create(builder => ...)`\) is
            Milestone 3 scope; profiles, registrations, and configuration rules build on it as later Milestone 3
            phases land\. [Create&lt;T&gt;\(\)](Compono.Composer.Create.md#Compono.Composer.Create_T_() 'Compono\.Composer\.Create\<T\>\(\)') resolves through the real [Compono\.CompositionContext](https://learn.microsoft.com/en-us/dotnet/api/compono.compositioncontext 'Compono\.CompositionContext')/
            resolution pipeline \(Milestone 2\) rather than dispatching into a generated
            [ICompositionPlan&lt;T&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>') directly\.

| Methods | |
| :--- | :--- |
| [Create\(\)](Compono.Composer.Create.md#Compono.Composer.Create() 'Compono\.Composer\.Create\(\)') | Creates a new [Composer](Compono.Composer.md 'Compono\.Composer') with no explicit configuration \- equivalent to [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') with an empty callback\. |
| [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') | Creates a new [Composer](Compono.Composer.md 'Compono\.Composer') from an explicit configuration callback\. |
| [Create&lt;T&gt;\(\)](Compono.Composer.Create.md#Compono.Composer.Create_T_() 'Compono\.Composer\.Create\<T\>\(\)') | Composes an instance of [T](Compono.Composer.md#Compono.Composer.Create_T_().T 'Compono\.Composer\.Create\<T\>\(\)\.T') \- a new root composition operation, with its own scope and path, resolved through the same pipeline as any nested request\. |
| [CreateMany&lt;T&gt;\(int\)](Compono.Composer.CreateMany_T_(int).md 'Compono\.Composer\.CreateMany\<T\>\(int\)') | Composes [count](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).count 'Compono\.Composer\.CreateMany\<T\>\(int\)\.count') independent instances of [T](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).T 'Compono\.Composer\.CreateMany\<T\>\(int\)\.T') \- each its own root composition operation \(its own scope, path, and active\-construction\-frame stack\), per the Execution Flow section of `docs/plans/0002-milestone-2-core-composition-engine.md`\. Item `i`'s root seed forks from this composer's batch seed \(this composer's configured `WithSeed` value, or a freshly generated one if none was configured\) via `"CreateMany"` then `i` \(`docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md`\) \- no value is shared across items\. |
| [CreateRow\(Type\)](Compono.Composer.CreateRow(System.Type).md 'Compono\.Composer\.CreateRow\(System\.Type\)') | Creates a new [CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow') \- one composition scope for several sibling top\-level parameter requests \(e\.g\. one xUnit theory row's own method parameters\), sharing one seed, one shared\-value scope, and one pre\-rooted path\. See `docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md`\. |
