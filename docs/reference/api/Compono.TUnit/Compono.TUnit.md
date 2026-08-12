#### [Compono\.TUnit](index.md 'index')

## Compono\.TUnit Namespace

| Classes | |
| :--- | :--- |
| [ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute') | Composes a TUnit test method's parameters through Compono \- the default \(no explicit profile\) entry point\. Every parameter not supplied inline is composed; a parameter targeted by a supplied inline value takes that value instead, taking precedence over composition\. See `docs/adr/0040-compono-tunit-package-design.md` for the full binding algorithm, seed policy, and diagnostics \- adapted from `Compono.XunitV3.ComposeAttribute`, not a byte\-for\-byte port \(TUnit hands a data source `TUnit.Core.DataGeneratorMetadata`, not a `MethodInfo`\)\. |
| [SharedAttribute](Compono.TUnit.SharedAttribute.md 'Compono\.TUnit\.SharedAttribute') | Marks a `[Compose]`\-attributed test method parameter as shared: its composed \(or inline\-supplied\) value is stored in the row's [CompositionRow](../Compono/Compono.CompositionRow.md 'Compono\.CompositionRow') scope, so any other composed parameter or nested generated dependency that structurally requests the same type in the same row reuses this exact value instead of composing its own independent one\. |
