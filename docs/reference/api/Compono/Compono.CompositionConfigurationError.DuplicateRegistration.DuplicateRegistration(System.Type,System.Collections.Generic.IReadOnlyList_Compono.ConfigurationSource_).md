#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[DuplicateRegistration](Compono.CompositionConfigurationError.DuplicateRegistration.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration')

## DuplicateRegistration\(Type, IReadOnlyList\<ConfigurationSource\>\) Constructor

Creates a [DuplicateRegistration](Compono.CompositionConfigurationError.DuplicateRegistration.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration') error\.

```csharp
public DuplicateRegistration(System.Type registeredType, System.Collections.Generic.IReadOnlyList<Compono.ConfigurationSource> sources);
```
#### Parameters

<a name='Compono.CompositionConfigurationError.DuplicateRegistration.DuplicateRegistration(System.Type,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).registeredType'></a>

`registeredType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The type registered more than once\.

<a name='Compono.CompositionConfigurationError.DuplicateRegistration.DuplicateRegistration(System.Type,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources'></a>

`sources` [System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

Every call that registered this type, in call order\.

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[sources](Compono.CompositionConfigurationError.DuplicateRegistration.DuplicateRegistration(System.Type,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md#Compono.CompositionConfigurationError.DuplicateRegistration.DuplicateRegistration(System.Type,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.DuplicateRegistration\(System\.Type, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)\.sources') has fewer than two entries\.