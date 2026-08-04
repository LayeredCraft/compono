#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')

## CompositionConfigurationError\.DuplicateRule Class

The same configuration \<em\>value\</em\> rule was set more than once across a single
[Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') callback \- a type rule for the same
type twice, or a member rule for the same \(declaring type, member name\) pair twice\. A member
rule and a type rule that could both match the same request are never a conflict with each
other, even though both may apply \- they're different specificity, not the same key\. A
duplicate member\-scoped `WithCollectionSize` override is a distinct case
\([DuplicateCollectionSizeOverride](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride')\), not this one \- a size override never compiles
into a stage\-4 rule, so reporting it as "a rule" would be misleading \(PR \#19 review\)\. See
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public sealed record CompositionConfigurationError.DuplicateRule : Compono.CompositionConfigurationError, System.IEquatable<Compono.CompositionConfigurationError.DuplicateRule>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError') → DuplicateRule

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DuplicateRule](Compono.CompositionConfigurationError.DuplicateRule.md 'Compono\.CompositionConfigurationError\.DuplicateRule')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

| Constructors | |
| :--- | :--- |
| [DuplicateRule\(Type, string, IReadOnlyList&lt;ConfigurationSource&gt;\)](Compono.CompositionConfigurationError.DuplicateRule.DuplicateRule(System.Type,string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md 'Compono\.CompositionConfigurationError\.DuplicateRule\.DuplicateRule\(System\.Type, string, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)') | Creates a [DuplicateRule](Compono.CompositionConfigurationError.DuplicateRule.md 'Compono\.CompositionConfigurationError\.DuplicateRule') error\. |

| Properties | |
| :--- | :--- |
| [MemberName](Compono.CompositionConfigurationError.DuplicateRule.MemberName.md 'Compono\.CompositionConfigurationError\.DuplicateRule\.MemberName') | The member name, for a member rule \- [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a type rule\. |
| [RuleType](Compono.CompositionConfigurationError.DuplicateRule.RuleType.md 'Compono\.CompositionConfigurationError\.DuplicateRule\.RuleType') | A type rule's own type, or a member rule's declaring type\. |
| [Sources](Compono.CompositionConfigurationError.DuplicateRule.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateRule\.Sources') | Every call that set this rule, in call order \- always at least two\. A genuinely immutable snapshot \([Compono\.ImmutableSnapshot](https://learn.microsoft.com/en-us/dotnet/api/compono.immutablesnapshot 'Compono\.ImmutableSnapshot')\), same guarantee as [Sources](Compono.CompositionConfigurationError.DuplicateRegistration.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.Sources')\. |
