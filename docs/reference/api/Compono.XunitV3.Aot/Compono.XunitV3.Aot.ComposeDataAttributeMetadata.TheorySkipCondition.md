#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataAttributeMetadata](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata')

## ComposeDataAttributeMetadata\.TheorySkipCondition Property

The theory\-level skip\-condition kind, as a code: `0` = none, `1` =
`SkipUnless`, `2` = `SkipWhen` \(ADR\-0070 Amendment 5\)\. The generator emits a
nonzero code only when the test method's own `[Fact]`/`[Theory]` declares exactly one
of the two conditions; the runtime rewrites the composed row's skip group into that field\.

```csharp
public int TheorySkipCondition { get; init; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')