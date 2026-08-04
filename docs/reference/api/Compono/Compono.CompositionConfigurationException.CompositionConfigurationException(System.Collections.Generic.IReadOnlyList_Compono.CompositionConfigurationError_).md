#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException')

## CompositionConfigurationException\(IReadOnlyList\<CompositionConfigurationError\>\) Constructor

Creates a [CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException') from one or more structured errors\.
Its [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message') is rendered from [errors](Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).md#Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).errors 'Compono\.CompositionConfigurationException\.CompositionConfigurationException\(System\.Collections\.Generic\.IReadOnlyList\<Compono\.CompositionConfigurationError\>\)\.errors'), not the other
way around \- inspect [Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors') directly rather than parsing the message\.

```csharp
public CompositionConfigurationException(System.Collections.Generic.IReadOnlyList<Compono.CompositionConfigurationError> errors);
```
#### Parameters

<a name='Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).errors'></a>

`errors` [System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

Every conflict found\. Copied into an immutable snapshot \- mutating a list passed here after
this constructor returns has no effect on [Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors')\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[errors](Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).md#Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).errors 'Compono\.CompositionConfigurationException\.CompositionConfigurationException\(System\.Collections\.Generic\.IReadOnlyList\<Compono\.CompositionConfigurationError\>\)\.errors') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[errors](Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).md#Compono.CompositionConfigurationException.CompositionConfigurationException(System.Collections.Generic.IReadOnlyList_Compono.CompositionConfigurationError_).errors 'Compono\.CompositionConfigurationException\.CompositionConfigurationException\(System\.Collections\.Generic\.IReadOnlyList\<Compono\.CompositionConfigurationError\>\)\.errors') is empty\.