#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource').[ProfileChain](Compono.ConfigurationSource.ProfileChain.md 'Compono\.ConfigurationSource\.ProfileChain')

## ConfigurationSource\.ProfileChain\.Profiles Property

The applied profile types, outermost first\. A genuinely immutable snapshot
\(`Compono.ImmutableSnapshot`\) taken at construction, never the caller\-supplied list
itself \- the same mutation\-after\-construction concern
[Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors') guards against\.

```csharp
public System.Collections.Generic.IReadOnlyList<System.Type> Profiles { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')