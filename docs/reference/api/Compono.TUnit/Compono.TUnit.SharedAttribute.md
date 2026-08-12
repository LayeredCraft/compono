#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit')

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
name/qualifier\-based sharing\. See `docs/adr/0040-compono-tunit-package-design.md`'s
"Package shape" section \- mirrors `Compono.XunitV3.SharedAttribute`'s binding rules
exactly \(declaration order among `[Shared]` parameters, duplicate\-type rejection,
visibility scoped to the current row only\), duplicated rather than shared per that ADR's
binding\-logic decision\.