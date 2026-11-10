#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute')

## ComposeDataAttribute\.Seed Property

An explicit root seed pinned to every row this attribute produces \- the same underlying contract
as [Seed](Compono.XunitV3.ComposeAttribute.Seed.md 'Compono\.XunitV3\.ComposeAttribute\.Seed'), but restricted to non\-negative values \(enforced by the
execution algorithm, not here\)\. A plain [int](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int'), not [int?](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int? 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int?') \- an
attribute named argument cannot target a [System\.Nullable&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1') property \(CS0655\); see
`Compono.XunitV3.ComposeDataAttribute.SeedAsNullable` for the property the execution algorithm actually reads\.

```csharp
public int Seed { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')