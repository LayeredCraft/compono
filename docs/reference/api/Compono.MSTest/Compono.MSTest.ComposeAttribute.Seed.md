#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest').[ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute')

## ComposeAttribute\.Seed Property

An explicit root seed for this row \- the same underlying contract as
[WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values so a seed
reported in a failure message or [GetDisplayName\(MethodInfo, object\[\]\)](Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).md 'Compono\.MSTest\.ComposeAttribute\.GetDisplayName\(System\.Reflection\.MethodInfo, object\[\]\)')'s output is always pasteable
back into this property unchanged\. Unset: a fresh, non\-negative seed is generated on every
[GetData\(MethodInfo\)](Compono.MSTest.ComposeAttribute.GetData(System.Reflection.MethodInfo).md 'Compono\.MSTest\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo\)') call, matching `Compono.XunitV3`/`Compono.TUnit`'s own
`Seed` contract exactly\.

```csharp
public int Seed { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')