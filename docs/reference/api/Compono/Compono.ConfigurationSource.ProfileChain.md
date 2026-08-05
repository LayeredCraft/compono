#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource')

## ConfigurationSource\.ProfileChain Class

A builder call made from inside a profile's `Configure`, or nested inside another
profile's `Configure`\.

```csharp
public sealed record ConfigurationSource.ProfileChain : Compono.ConfigurationSource, System.IEquatable<Compono.ConfigurationSource.ProfileChain>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource') → ProfileChain

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[ProfileChain](Compono.ConfigurationSource.ProfileChain.md 'Compono\.ConfigurationSource\.ProfileChain')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

| Constructors | |
| :--- | :--- |
| [ProfileChain\(IReadOnlyList&lt;Type&gt;\)](Compono.ConfigurationSource.ProfileChain.ProfileChain(System.Collections.Generic.IReadOnlyList_System.Type_).md 'Compono\.ConfigurationSource\.ProfileChain\.ProfileChain\(System\.Collections\.Generic\.IReadOnlyList\<System\.Type\>\)') | Creates a [ProfileChain](Compono.ConfigurationSource.ProfileChain.md 'Compono\.ConfigurationSource\.ProfileChain') source\. |

| Properties | |
| :--- | :--- |
| [Profiles](Compono.ConfigurationSource.ProfileChain.Profiles.md 'Compono\.ConfigurationSource\.ProfileChain\.Profiles') | The applied profile types, outermost first\. A genuinely immutable snapshot \(`Compono.ImmutableSnapshot`\) taken at construction, never the caller\-supplied list itself \- the same mutation\-after\-construction concern [Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors') guards against\. |
