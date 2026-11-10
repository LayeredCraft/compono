#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## ComposeDataAttributeMetadata Struct

The attribute\-level metadata a `[ComposeData]` method contributes to the merge rule\. Skip
conditions are engine\-specific: the JIT package uses xUnit's [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String') condition names
and [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type') skip type, while AOT uses compiled [System\.Func&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-1 'System\.Func\`1') predicates\.

```csharp
public readonly struct ComposeDataAttributeMetadata
```

| Properties | |
| :--- | :--- |
| [DisableParallelization](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.DisableParallelization.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.DisableParallelization') | The attribute\-level disable\-parallelization flag, when set\. |
| [Explicit](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.Explicit.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.Explicit') | The attribute\-level explicit flag, when set\. |
| [Label](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.Label.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.Label') | The attribute\-level label, when set\. |
| [Skip](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.Skip.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.Skip') | The attribute\-level skip reason, when set\. |
| [SkipUnless](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.SkipUnless.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.SkipUnless') | The compiled attribute\-level skip\-unless predicate, when set\. |
| [SkipWhen](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.SkipWhen.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.SkipWhen') | The compiled attribute\-level skip\-when predicate, when set\. |
| [TestDisplayName](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.TestDisplayName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.TestDisplayName') | The attribute\-level test display name, when set\. |
| [TheorySkipCondition](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.TheorySkipCondition.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.TheorySkipCondition') | The theory\-level skip\-condition kind, as a code: `0` = none, `1` = `SkipUnless`, `2` = `SkipWhen` \(ADR\-0070 Amendment 5\)\. The generator emits a nonzero code only when the test method's own `[Fact]`/`[Theory]` declares exactly one of the two conditions; the runtime rewrites the composed row's skip group into that field\. |
| [Timeout](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.Timeout.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.Timeout') | The attribute\-level timeout, when set\. |
| [Traits](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.Traits.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata\.Traits') | The attribute\-level trait name/value pairs, when set\. |
