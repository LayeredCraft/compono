#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[DuplicateConfigurationOption](Compono.CompositionConfigurationError.DuplicateConfigurationOption.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption')

## DuplicateConfigurationOption\(string, IReadOnlyList\<ConfigurationSource\>\) Constructor

Creates a [DuplicateConfigurationOption](Compono.CompositionConfigurationError.DuplicateConfigurationOption.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption') error\.

```csharp
public DuplicateConfigurationOption(string optionName, System.Collections.Generic.IReadOnlyList<Compono.ConfigurationSource> sources);
```
#### Parameters

<a name='Compono.CompositionConfigurationError.DuplicateConfigurationOption.DuplicateConfigurationOption(string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).optionName'></a>

`optionName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The builder verb's name, e\.g\. `"WithSeed"`\.

<a name='Compono.CompositionConfigurationError.DuplicateConfigurationOption.DuplicateConfigurationOption(string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources'></a>

`sources` [System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

Every call that set this option, in call order\. Copied into an immutable snapshot \-
mutating a list passed here after this constructor returns has no effect on
[Sources](Compono.CompositionConfigurationError.DuplicateConfigurationOption.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption\.Sources')\.

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[sources](Compono.CompositionConfigurationError.DuplicateConfigurationOption.DuplicateConfigurationOption(string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md#Compono.CompositionConfigurationError.DuplicateConfigurationOption.DuplicateConfigurationOption(string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption\.DuplicateConfigurationOption\(string, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)\.sources') has fewer than two entries\.