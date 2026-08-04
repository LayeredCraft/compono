#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[DuplicateConfigurationOption](Compono.CompositionConfigurationError.DuplicateConfigurationOption.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption')

## CompositionConfigurationError\.DuplicateConfigurationOption\.Sources Property

Every call that set this option, in call order \- always at least two\. A genuinely
immutable snapshot \([Compono\.ImmutableSnapshot](https://learn.microsoft.com/en-us/dotnet/api/compono.immutablesnapshot 'Compono\.ImmutableSnapshot')\) taken at construction, never the
caller\-supplied list itself and never a plain array a caller could cast back to and
mutate \- the same mutation\-after\-construction concern
[Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors') guards against, one level deeper\.

```csharp
public System.Collections.Generic.IReadOnlyList<Compono.ConfigurationSource> Sources { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')