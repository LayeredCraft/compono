#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')

## CompositionConfigurationError\.DuplicateConfigurationOption Class

The same scalar \(singleton\-valued\) configuration option \- `WithSeed`,
`WithCollectionSize`'s global default, or `UseServiceProvider` \- was set more than
once across a single [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') callback\.

```csharp
public sealed record CompositionConfigurationError.DuplicateConfigurationOption : Compono.CompositionConfigurationError, System.IEquatable<Compono.CompositionConfigurationError.DuplicateConfigurationOption>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError') → DuplicateConfigurationOption

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DuplicateConfigurationOption](Compono.CompositionConfigurationError.DuplicateConfigurationOption.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

### Remarks
Deliberately fail\-fast rather than last\-wins, unlike a typical "options builder" convention \-
see `docs/adr/0017-immutable-composer-configuration-and-builder-model.md`'s Amendment for
why a contradictory scalar configuration \(e\.g\. two different seeds\) has no coherent effective
value to fall back to\.

| Constructors | |
| :--- | :--- |
| [DuplicateConfigurationOption\(string, IReadOnlyList&lt;ConfigurationSource&gt;\)](Compono.CompositionConfigurationError.DuplicateConfigurationOption.DuplicateConfigurationOption(string,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption\.DuplicateConfigurationOption\(string, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)') | Creates a [DuplicateConfigurationOption](Compono.CompositionConfigurationError.DuplicateConfigurationOption.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption') error\. |

| Properties | |
| :--- | :--- |
| [OptionName](Compono.CompositionConfigurationError.DuplicateConfigurationOption.OptionName.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption\.OptionName') | The builder verb's name, e\.g\. `"WithSeed"`\. |
| [Sources](Compono.CompositionConfigurationError.DuplicateConfigurationOption.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption\.Sources') | Every call that set this option, in call order \- always at least two\. A genuinely immutable snapshot \([Compono\.ImmutableSnapshot](https://learn.microsoft.com/en-us/dotnet/api/compono.immutablesnapshot 'Compono\.ImmutableSnapshot')\) taken at construction, never the caller\-supplied list itself and never a plain array a caller could cast back to and mutate \- the same mutation\-after\-construction concern [Errors](Compono.CompositionConfigurationException.Errors.md 'Compono\.CompositionConfigurationException\.Errors') guards against, one level deeper\. |
