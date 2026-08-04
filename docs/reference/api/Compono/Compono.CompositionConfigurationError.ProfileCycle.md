#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')

## CompositionConfigurationError\.ProfileCycle Class

A profile, directly or transitively \(via `AddProfile` called from inside another profile's
`Configure`\), applied itself again while it was already applying \- detected and thrown
immediately from `AddProfile`, never aggregated into a [CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException')
alongside any other conflict\.

```csharp
public sealed record CompositionConfigurationError.ProfileCycle : Compono.CompositionConfigurationError, System.IEquatable<Compono.CompositionConfigurationError.ProfileCycle>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError') → ProfileCycle

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

### Remarks
Identity for cycle purposes is a profile's declared CLR type \(`profile.GetType()`\),
regardless of whether it reached the builder via `AddProfile<T>()` or
`AddProfile(instance)` \- see `docs/adr/0018-composition-profiles.md`\.

| Constructors | |
| :--- | :--- |
| [ProfileCycle\(IReadOnlyList&lt;Type&gt;\)](Compono.CompositionConfigurationError.ProfileCycle.ProfileCycle(System.Collections.Generic.IReadOnlyList_System.Type_).md 'Compono\.CompositionConfigurationError\.ProfileCycle\.ProfileCycle\(System\.Collections\.Generic\.IReadOnlyList\<System\.Type\>\)') | Creates a [ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle') error\. |

| Properties | |
| :--- | :--- |
| [Chain](Compono.CompositionConfigurationError.ProfileCycle.Chain.md 'Compono\.CompositionConfigurationError\.ProfileCycle\.Chain') | The full cycle, in application order, with the repeated profile type at both ends \(e\.g\. `[ProfileA, ProfileB, ProfileA]`\) \- always at least two entries\. A genuinely immutable snapshot \(`Compono.ImmutableSnapshot`\), same guarantee as [Sources](Compono.CompositionConfigurationError.DuplicateRegistration.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.Sources')\. |
