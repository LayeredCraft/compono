#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## SharedAttribute Class

Marks a `[ComposeData]`\-attributed test method parameter as shared under xUnit v3's Native
AOT pipeline \- the AOT counterpart to `Compono.XunitV3.SharedAttribute`\. Its generated value
is stored in the row's composition scope, so any other composed parameter or nested generated
dependency that structurally requests the same type in the same row reuses this exact value
instead of composing its own independent one\.

```csharp
public sealed class SharedAttribute : System.Attribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → SharedAttribute

### Remarks
Like the other `Compono.XunitV3.Aot` marker attributes, this is a thin marker only \- it is
discovered by `Compono.Generators` at compile time \(matched on this type's own fully
qualified metadata name, never on assignability/inheritance\) and carries no runtime composition
logic of its own\. AOT consumers cannot reference `Compono.XunitV3.SharedAttribute` because
that package depends on the reflection\-mode xUnit extensibility package this package exists to
avoid \(ADR\-0066\), so this marker is defined in the AOT namespace instead \(ADR\-0070 Amendment 3\)\.

Type-based only, matching the engine's existing shared-value scope key
(`docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md`) - no
name/qualifier-based sharing. See ADR-0022's "[Shared] semantics" section for the full binding
rules.

Referencing this marker alone is not enough to make sharing work: the actual generated
`ShareExplicit<T>`/`ResolveShared<T>` behavior for AOT `[ComposeData]`
rows is produced by `Compono.Generators`.