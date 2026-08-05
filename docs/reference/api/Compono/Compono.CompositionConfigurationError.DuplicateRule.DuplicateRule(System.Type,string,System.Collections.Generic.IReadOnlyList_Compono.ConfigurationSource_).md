#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[DuplicateRule](Compono.CompositionConfigurationError.DuplicateRule.md 'Compono\.CompositionConfigurationError\.DuplicateRule')

## DuplicateRule\(Type, string, IReadOnlyList\<ConfigurationSource\>\) Constructor

Creates a [DuplicateRule](Compono.CompositionConfigurationError.DuplicateRule.md 'Compono\.CompositionConfigurationError\.DuplicateRule') error\.

```csharp
public DuplicateRule(System.Type ruleType, string? memberName, System.Collections.Generic.IReadOnlyList<Compono.ConfigurationSource> sources);
```
#### Parameters

<a name='Compono.CompositionConfigurationError.DuplicateRule.DuplicateRule(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).ruleType'></a>

`ruleType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

A type rule's own type, or a member rule's declaring type\.

<a name='Compono.CompositionConfigurationError.DuplicateRule.DuplicateRule(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).memberName'></a>

`memberName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The member name, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a type rule\.

<a name='Compono.CompositionConfigurationError.DuplicateRule.DuplicateRule(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources'></a>

`sources` [System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

Every call that set this rule, in call order\.

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[sources](Compono.CompositionConfigurationError.DuplicateRule.DuplicateRule(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md#Compono.CompositionConfigurationError.DuplicateRule.DuplicateRule(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).sources 'Compono\.CompositionConfigurationError\.DuplicateRule\.DuplicateRule\(System\.Type, string, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)\.sources') has fewer than two entries\.