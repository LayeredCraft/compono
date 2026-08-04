#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ConfigurationSource Class

Where one accumulated [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder') entry \(a registration, a rule, or a scalar
configuration option\) came from \- a direct builder call, or the chain of profiles whose
`Configure` it ran inside of\.

```csharp
public abstract record ConfigurationSource : System.IEquatable<Compono.ConfigurationSource>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ConfigurationSource

Derived  
↳ [ProfileChain](Compono.ConfigurationSource.ProfileChain.md 'Compono\.ConfigurationSource\.ProfileChain')

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

### Remarks
A discriminated union, matching this codebase's existing `Compono.PathSegment`/
`Compono.CompositionResult` shape\. Used only for diagnostics \- naming every contributing source
of a [CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError') \- never for resolution behavior\. `public`
because [CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError'), which exposes it, is `public`\. See
`docs/adr/0018-composition-profiles.md`'s provenance decision\.

| Fields | |
| :--- | :--- |
| [Direct](Compono.ConfigurationSource.Direct.md 'Compono\.ConfigurationSource\.Direct') | The single, shared instance representing a builder call made outside any profile\. |
