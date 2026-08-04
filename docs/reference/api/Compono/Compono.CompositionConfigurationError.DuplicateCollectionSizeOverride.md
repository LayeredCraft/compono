#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')

## CompositionConfigurationError\.DuplicateCollectionSizeOverride Class

The same member\-scoped `.For<T>().Member(x => x.Y).WithCollectionSize(...)` override
was set more than once across a single [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)')
callback, for the same \(declaring type, member name\) pair\. Reuses the identical keyed\-conflict
detection mechanism as [DuplicateRule](Compono.CompositionConfigurationError.DuplicateRule.md 'Compono\.CompositionConfigurationError\.DuplicateRule') \(same key shape, same "first wins, conflict
wins later" accumulation\) but is never compiled into a stage\-4 provider the way a member value
rule is \- a distinct case so the rendered message correctly names a duplicate size configuration
rather than reporting it as "a member rule" \(PR \#19 review\)\. See
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public sealed record CompositionConfigurationError.DuplicateCollectionSizeOverride : Compono.CompositionConfigurationError, System.IEquatable<Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError') → DuplicateCollectionSizeOverride

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DuplicateCollectionSizeOverride](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

| Constructors | |
| :--- | :--- |
| [DuplicateCollectionSizeOverride\(Type, string, IReadOnlyList&lt;ConfigurationSource&gt;\)](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.DuplicateCollectionSizeOverride(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride\.DuplicateCollectionSizeOverride\(System\.Type, string, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)') | Creates a [DuplicateCollectionSizeOverride](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride') error\. |

| Properties | |
| :--- | :--- |
| [DeclaringType](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.DeclaringType.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride\.DeclaringType') | The member's declaring type\. |
| [MemberName](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.MemberName.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride\.MemberName') | The member name\. |
| [Sources](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride\.Sources') | Every call that set this override, in call order \- always at least two\. A genuinely immutable snapshot \(`Compono.ImmutableSnapshot`\), same guarantee as [Sources](Compono.CompositionConfigurationError.DuplicateRegistration.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.Sources')\. |
