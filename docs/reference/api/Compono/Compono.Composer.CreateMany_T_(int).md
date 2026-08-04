#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[Composer](Compono.Composer.md 'Compono\.Composer')

## Composer\.CreateMany\<T\>\(int\) Method

Composes [count](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).count 'Compono\.Composer\.CreateMany\<T\>\(int\)\.count') independent instances of [T](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).T 'Compono\.Composer\.CreateMany\<T\>\(int\)\.T') \- each its
own root composition operation \(its own scope, path, and active\-construction\-frame stack\), per
the Execution Flow section of `docs/plans/0002-milestone-2-core-composition-engine.md`\.
Item `i`'s root seed forks from this composer's batch seed \(this composer's configured
`WithSeed` value, or a freshly generated one if none was configured\) via
`"CreateMany"` then `i`
\(`docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md`\) \- no
value is shared across items\.

```csharp
public System.Collections.Generic.IReadOnlyList<T> CreateMany<T>(int count);
```
#### Type parameters

<a name='Compono.Composer.CreateMany_T_(int).T'></a>

`T`

The type to compose\.
#### Parameters

<a name='Compono.Composer.CreateMany_T_(int).count'></a>

`count` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

How many instances to compose\.

#### Returns
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[T](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).T 'Compono\.Composer\.CreateMany\<T\>\(int\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')  
A fully, eagerly materialized list of exactly [count](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).count 'Compono\.Composer\.CreateMany\<T\>\(int\)\.count') instances \- empty but
never [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') when [count](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).count 'Compono\.Composer\.CreateMany\<T\>\(int\)\.count') is `0`\.

#### Exceptions

[System\.ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception 'System\.ArgumentOutOfRangeException')  
[count](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).count 'Compono\.Composer\.CreateMany\<T\>\(int\)\.count') is negative\.

[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
No explicit value, shared value, registration, provider, or generated plan could satisfy
[T](Compono.Composer.CreateMany_T_(int).md#Compono.Composer.CreateMany_T_(int).T 'Compono\.Composer\.CreateMany\<T\>\(int\)\.T') for one of the requested instances\.