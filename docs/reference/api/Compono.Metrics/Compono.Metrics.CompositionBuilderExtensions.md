#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics')

## CompositionBuilderExtensions Class

Registers [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics') support into a [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')\.

```csharp
public static class CompositionBuilderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionBuilderExtensions

| Methods | |
| :--- | :--- |
| [UseMetrics\(this CompositionBuilder\)](Compono.Metrics.CompositionBuilderExtensions.UseMetrics(thisCompono.CompositionBuilder).md 'Compono\.Metrics\.CompositionBuilderExtensions\.UseMetrics\(this Compono\.CompositionBuilder\)') | Registers a stage\-6 test\-double provider \(`Compono.Metrics.MetricsProvider`\) so a bare [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory') composes as a new [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory') per request\. |
