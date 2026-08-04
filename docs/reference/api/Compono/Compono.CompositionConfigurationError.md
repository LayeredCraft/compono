#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionConfigurationError Class

One conflict found while validating a [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')'s accumulated configuration \-
the structured detail behind a [CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException')\.

```csharp
public abstract record CompositionConfigurationError : System.IEquatable<Compono.CompositionConfigurationError>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionConfigurationError

Derived  
↳ [DuplicateCollectionSizeOverride](Compono.CompositionConfigurationError.DuplicateCollectionSizeOverride.md 'Compono\.CompositionConfigurationError\.DuplicateCollectionSizeOverride')  
↳ [DuplicateConfigurationOption](Compono.CompositionConfigurationError.DuplicateConfigurationOption.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption')  
↳ [DuplicateRegistration](Compono.CompositionConfigurationError.DuplicateRegistration.md 'Compono\.CompositionConfigurationError\.DuplicateRegistration')  
↳ [DuplicateRule](Compono.CompositionConfigurationError.DuplicateRule.md 'Compono\.CompositionConfigurationError\.DuplicateRule')  
↳ [ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle')

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[CompositionConfigurationError](Compono.CompositionConfigurationError.md 'Compono\.CompositionConfigurationError')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')

### Remarks
A discriminated union \- the same shape this codebase already uses for `Compono.PathSegment` and
`Compono.CompositionResult` \- so each conflict kind carries only the fields relevant to it,
rather than one flat type with a `Kind` enum and fields only some kinds use\. New cases are
added by whichever Milestone 3 phase introduces the conflict they describe; only
[DuplicateConfigurationOption](Compono.CompositionConfigurationError.DuplicateConfigurationOption.md 'Compono\.CompositionConfigurationError\.DuplicateConfigurationOption') exists so far \(Phase 0\)\. See
`docs/adr/0017-immutable-composer-configuration-and-builder-model.md`'s Amendment\.