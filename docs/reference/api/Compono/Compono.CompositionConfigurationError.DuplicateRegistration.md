#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')

## CompositionConfigurationError\.DuplicateRegistration Class

The same exact type was registered \(via `Register<T>`\) more than once across a
single [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') callback \- directly, or from a
profile, per `docs/adr/0019-registrations-and-service-provider-injection.md`'s deliberately
strict throw\-on\-duplicate decision\.

```csharp
public sealed record CompositionConfigurationError.DuplicateRegistration : Compono.CompositionConfigurationError, System.IEquatable<Compono.CompositionConfigurationError.DuplicateRegistration>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError') → DuplicateRegistration

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DuplicateRegistration](Compono.CompositionConfigurationError.DuplicateRegistration.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

| Constructors | |
| :--- | :--- |
| [DuplicateRegistration\(Type, IReadOnlyList&lt;ConfigurationSource&gt;\)](Compono.CompositionConfigurationError.DuplicateRegistration.DuplicateRegistration(System.Type,System.Collections.Generic.IReadOnlyList_Compono.ConfigurationSource_).md 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.DuplicateRegistration\(System\.Type, System\.Collections\.Generic\.IReadOnlyList\<Compono\.ConfigurationSource\>\)') | Creates a [DuplicateRegistration](Compono.CompositionConfigurationError.DuplicateRegistration.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration') error\. |

| Properties | |
| :--- | :--- |
| [RegisteredType](Compono.CompositionConfigurationError.DuplicateRegistration.RegisteredType.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.RegisteredType') | The type registered more than once\. |
| [Sources](Compono.CompositionConfigurationError.DuplicateRegistration.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration\.Sources') | Every call that registered this type, in call order \- always at least two\. A genuinely immutable snapshot \(`Compono.ImmutableSnapshot`\), same guarantee as [Sources](Compono.CompositionConfigurationError.DuplicateConfigurationOption.Sources.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption\.Sources')\. |
