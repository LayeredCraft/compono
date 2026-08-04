#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionConfigurationException Class

Thrown when [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)')'s validation finds one or
more conflicts in the accumulated configuration\.

```csharp
public sealed class CompositionConfigurationException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → CompositionConfigurationException

### Remarks
Distinct from [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException'): this is a configuration\-time failure, thrown once
while building a [Composer](Compono.Composer.md 'Compono\.Composer'), never from a running [Create&lt;T&gt;\(\)](Compono.Composer.Create.md#Compono.Composer.Create_T_() 'Compono\.Composer\.Create\<T\>\(\)')/
[CreateMany&lt;T&gt;\(int\)](Compono.Composer.CreateMany_T_(int).md 'Compono\.Composer\.CreateMany\<T\>\(int\)') call\. See
`docs/adr/0017-immutable-composer-configuration-and-builder-model.md`'s Amendment\.

| Constructors | |
| :--- | :--- |
| [CompositionConfigurationException\(IReadOnlyList&lt;CompositionConfigurationError&gt;\)](Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).md 'Compono\.CompositionConfigurationException\.CompositionConfigurationException\(System\.Collections\.Generic\.IReadOnlyList\<Compono\.CompositionConfigurationError\>\)') | Creates a [CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException') from one or more structured errors\. Its [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message') is rendered from [errors](Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).md#Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).errors 'Compono\.CompositionConfigurationException\.CompositionConfigurationException\(System\.Collections\.Generic\.IReadOnlyList\<Compono\.CompositionConfigurationError\>\)\.errors'), not the other way around \- inspect [Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors') directly rather than parsing the message\. |

| Properties | |
| :--- | :--- |
| [Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors') | Every conflict found \- always at least one\. A genuinely immutable snapshot \(`Compono.ImmutableSnapshot`\) taken at construction, never the caller\-supplied list itself and never a plain array a caller could cast back to and mutate \- it can never drift from the already\-rendered [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message'), which is derived from this exact same snapshot\. |
