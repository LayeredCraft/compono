#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit').[ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute')

## ComposeAttribute\.Seed Property

An explicit root seed for this row \- the same underlying contract as
[WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values \(enforced in
`Compono.TUnit.ComposeAttribute.GenerateDataSources(TUnit.Core.DataGeneratorMetadata)`\) so a seed reported via [OnTestDiscovered\(DiscoveredTestContext\)](Compono.TUnit.ComposeAttribute.OnTestDiscovered(TUnit.Core.DiscoveredTestContext).md 'Compono\.TUnit\.ComposeAttribute\.OnTestDiscovered\(TUnit\.Core\.DiscoveredTestContext\)') is
always pasteable back into this property unchanged\. Unset: a fresh, non\-negative seed is
generated for every row\.

```csharp
public int Seed { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')