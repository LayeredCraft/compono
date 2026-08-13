#### [Compono\.TestDoubles](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionBuilderExtensions Class

Activates [GeneratedTestDoubleProvider](Compono.GeneratedTestDoubleProvider.md 'Compono\.GeneratedTestDoubleProvider') on a [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')\. See
ADR\-0043's "Runtime activation and precedence"\.

```csharp
public static class CompositionBuilderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionBuilderExtensions

| Methods | |
| :--- | :--- |
| [UseGeneratedTestDoubles\(this CompositionBuilder\)](Compono.CompositionBuilderExtensions.UseGeneratedTestDoubles(thisCompono.CompositionBuilder).md 'Compono\.CompositionBuilderExtensions\.UseGeneratedTestDoubles\(this Compono\.CompositionBuilder\)') | Registers a [GeneratedTestDoubleProvider](Compono.GeneratedTestDoubleProvider.md 'Compono\.GeneratedTestDoubleProvider')\. Call this \<b\>before\</b\>`UseNSubstitute()` \(or any other test\-double provider\) when both are installed \- stage 6 providers are tried in registration order, and a generated double should win over a generic substitute whenever both could satisfy the same interface\. See ADR\-0043's "Runtime activation and precedence"\. |
