#### [Compono\.XunitV3](index.md 'index')

## Compono\.XunitV3 Namespace

| Classes | |
| :--- | :--- |
| [ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute') | Composes an xUnit v3 theory row's parameters through Compono \- the default \(no explicit profile\) entry point\. Every parameter not supplied inline is composed; a parameter targeted by a supplied inline value takes that value instead, taking precedence over composition\. See `docs/adr/0022-compono-xunit-package-design.md` for the full binding algorithm, seed policy, and diagnostics\. |
| [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>') | Composes an xUnit v3 theory row's parameters through Compono, with [TProfile](Compono.XunitV3.ComposeAttribute_TProfile_.md#Compono.XunitV3.ComposeAttribute_TProfile_.TProfile 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>\.TProfile') applied to the underlying [Composer](../Compono/Compono.Composer.md 'Compono\.Composer') \- equivalent to `Composer.Create(builder => builder.AddProfile<TProfile>())`\. See [ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute') for the full binding algorithm\. |
| [SharedAttribute](Compono.XunitV3.SharedAttribute.md 'Compono\.XunitV3\.SharedAttribute') | Marks a `[Compose]`\-attributed test method parameter as shared: its composed \(or inline\-supplied\) value is stored in the row's [CompositionRow](../Compono/Compono.CompositionRow.md 'Compono\.CompositionRow') scope, so any other composed parameter or nested generated dependency that structurally requests the same type in the same row reuses this exact value instead of composing its own independent one\. |
