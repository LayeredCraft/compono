#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

## CompositionRow\.Seed Property

This row's root deterministic seed \- matches [WithSeed\(int\)](Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(int\)')'s
[int](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int') contract exactly, so a value read here is always pasteable directly into
a seed\-configuration API that reports it \(e\.g\. a test\-framework integration's own attribute\)\.

```csharp
public int Seed { get; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')