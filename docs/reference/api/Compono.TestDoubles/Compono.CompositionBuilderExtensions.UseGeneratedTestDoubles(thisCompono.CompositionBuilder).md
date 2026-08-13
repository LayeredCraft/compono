#### [Compono\.TestDoubles](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilderExtensions](Compono.CompositionBuilderExtensions.md 'Compono\.CompositionBuilderExtensions')

## CompositionBuilderExtensions\.UseGeneratedTestDoubles\(this CompositionBuilder\) Method

Registers a [GeneratedTestDoubleProvider](Compono.GeneratedTestDoubleProvider.md 'Compono\.GeneratedTestDoubleProvider')\. Call this \<b\>before\</b\>`UseNSubstitute()` \(or any other test\-double provider\) when both are installed \- stage
6 providers are tried in registration order, and a generated double should win over a
generic substitute whenever both could satisfy the same interface\. See ADR\-0043's "Runtime
activation and precedence"\.

```csharp
public static Compono.CompositionBuilder UseGeneratedTestDoubles(this Compono.CompositionBuilder builder);
```
#### Parameters

<a name='Compono.CompositionBuilderExtensions.UseGeneratedTestDoubles(thisCompono.CompositionBuilder).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')