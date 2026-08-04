#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[DuplicateRegistration](Compono.CompositionConfigurationError.DuplicateRegistration.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration')

## CompositionConfigurationError\.DuplicateRegistration\.Sources Property

Every call that registered this type, in call order \- always at least two\. A genuinely
immutable snapshot \([Compono\.ImmutableSnapshot](https://learn.microsoft.com/en-us/dotnet/api/compono.immutablesnapshot 'Compono\.ImmutableSnapshot')\), same guarantee as
[Sources](Compono.CompositionConfigurationError.DuplicateConfigurationOption.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption\.Sources')\.

```csharp
public System.Collections.Generic.IReadOnlyList<Compono.ConfigurationSource> Sources { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')