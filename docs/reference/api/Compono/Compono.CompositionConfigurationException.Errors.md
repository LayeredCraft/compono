#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException')

## CompositionConfigurationException\.Errors Property

Every conflict found \- always at least one\. A genuinely immutable snapshot
\([Compono\.ImmutableSnapshot](https://learn.microsoft.com/en-us/dotnet/api/compono.immutablesnapshot 'Compono\.ImmutableSnapshot')\) taken at construction, never the caller\-supplied list itself
and never a plain array a caller could cast back to and mutate \- it can never drift from the
already\-rendered [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message'), which is derived from this exact same
snapshot\.

```csharp
public System.Collections.Generic.IReadOnlyList<Compono.CompositionConfigurationError> Errors { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')