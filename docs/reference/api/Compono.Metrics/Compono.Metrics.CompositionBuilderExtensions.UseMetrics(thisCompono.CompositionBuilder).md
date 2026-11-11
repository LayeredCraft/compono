#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[CompositionBuilderExtensions](Compono.Metrics.CompositionBuilderExtensions.md 'Compono\.Metrics\.CompositionBuilderExtensions')

## CompositionBuilderExtensions\.UseMetrics\(this CompositionBuilder\) Method

Registers a stage\-6 test\-double provider \(`Compono.Metrics.MetricsProvider`\) so a bare
[System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory') composes as a new
[CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory') per request\.

```csharp
public static Compono.CompositionBuilder UseMetrics(this Compono.CompositionBuilder builder);
```
#### Parameters

<a name='Compono.Metrics.CompositionBuilderExtensions.UseMetrics(thisCompono.CompositionBuilder).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

The builder to register into\.

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')  
The same [builder](Compono.Metrics.CompositionBuilderExtensions.UseMetrics(thisCompono.CompositionBuilder).md#Compono.Metrics.CompositionBuilderExtensions.UseMetrics(thisCompono.CompositionBuilder).builder 'Compono\.Metrics\.CompositionBuilderExtensions\.UseMetrics\(this Compono\.CompositionBuilder\)\.builder'), for chaining\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[builder](Compono.Metrics.CompositionBuilderExtensions.UseMetrics(thisCompono.CompositionBuilder).md#Compono.Metrics.CompositionBuilderExtensions.UseMetrics(thisCompono.CompositionBuilder).builder 'Compono\.Metrics\.CompositionBuilderExtensions\.UseMetrics\(this Compono\.CompositionBuilder\)\.builder') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

### Remarks
Register this \<b\>before\</b\>`UseNSubstitute()` if
[System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory') should resolve through Compono\.Metrics rather
than an NSubstitute substitute \- Compono's stage\-6 providers resolve in registration order
\(first\-registered\-wins\), an existing, documented, `Accepted` pattern \(ADR\-0024\) this package
follows rather than replacing\. An exact `Register<IMeterFactory>(...)` always wins over
`UseMetrics()` regardless of order\.