#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataAttribute](Compono.XunitV3.Aot.ComposeDataAttribute.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute')

## ComposeDataAttribute\.Seed Property

An explicit root seed pinned to every row this attribute produces \- the same contract as
`Compono.XunitV3.ComposeDataAttribute.Seed`\. Never read at runtime; the generator resolves
it from this attribute's use site at compile time\.

```csharp
public int Seed { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')