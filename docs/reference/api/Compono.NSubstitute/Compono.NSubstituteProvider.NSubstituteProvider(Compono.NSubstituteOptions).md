#### [Compono\.NSubstitute](index.md 'index')
### [Compono](Compono.md 'Compono').[NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider')

## NSubstituteProvider\(NSubstituteOptions\) Constructor

Creates an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider'), snapshotting [options](Compono.NSubstituteProvider.NSubstituteProvider(Compono.NSubstituteOptions).md#Compono.NSubstituteProvider.NSubstituteProvider(Compono.NSubstituteOptions).options 'Compono\.NSubstituteProvider\.NSubstituteProvider\(Compono\.NSubstituteOptions\)\.options')'s
current values \- the provider never retains the caller\-owned [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions')
instance itself, so a caller mutating it after construction can't change this
already\-registered provider's behavior \(ADR\-0024's immutable\-provider\-registration guarantee\)\.

```csharp
public NSubstituteProvider(Compono.NSubstituteOptions options);
```
#### Parameters

<a name='Compono.NSubstituteProvider.NSubstituteProvider(Compono.NSubstituteOptions).options'></a>

`options` [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions')

The options to snapshot\.