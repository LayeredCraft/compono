#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest')

## SharedAttribute Class

Marks a `[Compose]`\-attributed test method parameter as shared: its composed \(or
inline\-supplied\) value is stored in the row's [CompositionRow](../Compono/Compono.CompositionRow.md 'Compono\.CompositionRow') scope, so any other
composed parameter or nested generated dependency that structurally requests the same type in
the same row reuses this exact value instead of composing its own independent one\.

```csharp
public sealed class SharedAttribute : System.Attribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → SharedAttribute

### Remarks
Type\-based only, matching the engine's existing shared\-value scope key
\(`docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md`\) \- no
name/qualifier\-based sharing\. See ADR\-0057's binding sections for the full binding rules
\(declaration order among `[Shared]` parameters, duplicate\-type rejection, visibility
scoped to the current row \- i\.e\. one `GetData` call \- only\)\.