#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[DuplicateCollectionSizeOverride](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride')

## DuplicateCollectionSizeOverride\(Type, string, IReadOnlyList\<ConfigurationSource\>\) Constructor

Creates a [DuplicateCollectionSizeOverride](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride') error\.

```csharp
public DuplicateCollectionSizeOverride(System.Type declaringType, string memberName, System.Collections.Generic.IReadOnlyList<Compono.ConfigurationSource> sources);
```
#### Parameters

<a name='Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.DuplicateCollectionSizeOverride(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).declaringType'></a>

`declaringType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The member's declaring type\.

<a name='Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.DuplicateCollectionSizeOverride(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).memberName'></a>

`memberName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The member name\.

<a name='Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.DuplicateCollectionSizeOverride(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources'></a>

`sources` [System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

Every call that set this override, in call order\.

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[sources](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.DuplicateCollectionSizeOverride(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md#Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.DuplicateCollectionSizeOverride(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride\.DuplicateCollectionSizeOverride\(System\.Type, string, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)\.sources') has fewer than two entries\.