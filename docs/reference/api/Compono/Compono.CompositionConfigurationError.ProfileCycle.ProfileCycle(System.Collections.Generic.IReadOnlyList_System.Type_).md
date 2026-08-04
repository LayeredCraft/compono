#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle')

## ProfileCycle\(IReadOnlyList\<Type\>\) Constructor

Creates a [ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle') error\.

```csharp
public ProfileCycle(System.Collections.Generic.IReadOnlyList<System.Type> chain);
```
#### Parameters

<a name='Compono.CompositionConfigurationError.ProfileCycle.ProfileCycle(System.Collections.Generic.IReadOnlyList_System.Type_).chain'></a>

`chain` [System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

The full cycle, in application order, with the repeated profile type at both ends\.

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[chain](Compono.CompositionConfigurationError.ProfileCycle.ProfileCycle(System.Collections.Generic.IReadOnlyList_System.Type_).md#Compono.CompositionConfigurationError.ProfileCycle.ProfileCycle(System.Collections.Generic.IReadOnlyList_System.Type_).chain 'Compono\.CompositionConfigurationError\.ProfileCycle\.ProfileCycle\(System\.Collections\.Generic\.IReadOnlyList\<System\.Type\>\)\.chain') has fewer than two entries, contains a [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') entry,
            or its first and last entries differ\.