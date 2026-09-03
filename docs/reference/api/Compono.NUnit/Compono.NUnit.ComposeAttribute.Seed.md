#### [Compono\.NUnit](index.md 'index')
### [Compono\.NUnit](Compono.NUnit.md 'Compono\.NUnit').[ComposeAttribute](Compono.NUnit.ComposeAttribute.md 'Compono\.NUnit\.ComposeAttribute')

## ComposeAttribute\.Seed Property

An explicit root seed for this row \- the same underlying contract as
[WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values so a seed
reported in a failure message or the constructed test's display name is always pasteable back
into this property unchanged\. Unset: a fresh, non\-negative seed is generated on every
[BuildFrom\(IMethodInfo, Test\)](Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).md 'Compono\.NUnit\.ComposeAttribute\.BuildFrom\(NUnit\.Framework\.Interfaces\.IMethodInfo, NUnit\.Framework\.Internal\.Test\)') call, matching `Compono.XunitV3`/`Compono.TUnit`/
`Compono.MSTest`'s own `Seed` contract exactly\.

```csharp
public int Seed { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')