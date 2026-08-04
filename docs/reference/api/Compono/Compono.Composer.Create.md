#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[Composer](Compono.Composer.md 'Compono\.Composer')

## Composer\.Create Method

| Overloads | |
| :--- | :--- |
| [Create\(\)](Compono.Composer.Create.md#Compono.Composer.Create() 'Compono\.Composer\.Create\(\)') | Creates a new [Composer](Compono.Composer.md 'Compono\.Composer') with no explicit configuration \- equivalent to [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') with an empty callback\. |
| [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') | Creates a new [Composer](Compono.Composer.md 'Compono\.Composer') from an explicit configuration callback\. |
| [Create&lt;T&gt;\(\)](Compono.Composer.Create.md#Compono.Composer.Create_T_() 'Compono\.Composer\.Create\<T\>\(\)') | Composes an instance of [T](Compono.Composer.md#Compono.Composer.Create_T_().T 'Compono\.Composer\.Create\<T\>\(\)\.T') \- a new root composition operation, with its own scope and path, resolved through the same pipeline as any nested request\. |

<a name='Compono.Composer.Create()'></a>

## Composer\.Create\(\) Method

Creates a new [Composer](Compono.Composer.md 'Compono\.Composer') with no explicit configuration \- equivalent to
[Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') with an empty callback\.

```csharp
public static Compono.Composer Create();
```

#### Returns
[Composer](Compono.Composer.md 'Compono\.Composer')

<a name='Compono.Composer.Create(System.Action_Compono.CompositionBuilder_)'></a>

## Composer\.Create\(Action\<CompositionBuilder\>\) Method

Creates a new [Composer](Compono.Composer.md 'Compono\.Composer') from an explicit configuration callback\.

```csharp
public static Compono.Composer Create(System.Action<Compono.CompositionBuilder> configure);
```
#### Parameters

<a name='Compono.Composer.Create(System.Action_Compono.CompositionBuilder_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Configures the new composer's [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')\. Runs synchronously, exactly
once, before this method returns\.

#### Returns
[Composer](Compono.Composer.md 'Compono\.Composer')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[configure](Compono.Composer.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_).configure 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)\.configure') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

[CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException')  
The accumulated configuration has one or more conflicts \(e\.g\. `WithSeed` called more than
once\)\.

<a name='Compono.Composer.Create_T_()'></a>

## Composer\.Create\<T\>\(\) Method

Composes an instance of [T](Compono.Composer.md#Compono.Composer.Create_T_().T 'Compono\.Composer\.Create\<T\>\(\)\.T') \- a new root composition operation, with
its own scope and path, resolved through the same pipeline as any nested request\.

```csharp
public T Create<T>();
```
#### Type parameters

<a name='Compono.Composer.Create_T_().T'></a>

`T`

The type to compose\.

#### Returns
[T](Compono.Composer.md#Compono.Composer.Create_T_().T 'Compono\.Composer\.Create\<T\>\(\)\.T')

#### Exceptions

[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
No explicit value, shared value, registration, provider, or generated plan could satisfy
[T](Compono.Composer.md#Compono.Composer.Create_T_().T 'Compono\.Composer\.Create\<T\>\(\)\.T')\.