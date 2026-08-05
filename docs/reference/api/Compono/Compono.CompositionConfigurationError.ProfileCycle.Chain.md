#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError').[ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle')

## CompositionConfigurationError\.ProfileCycle\.Chain Property

The full cycle, in application order, with the repeated profile type at both ends \(e\.g\.
`[ProfileA, ProfileB, ProfileA]`\) \- always at least two entries\. A genuinely immutable
snapshot \(`Compono.ImmutableSnapshot`\), same guarantee as
[Sources](Compono.CompositionConfigurationError.DuplicateRegistration.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.Sources')\.

```csharp
public System.Collections.Generic.IReadOnlyList<System.Type> Chain { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')