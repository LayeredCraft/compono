#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[CapturedMeasurement&lt;T&gt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')

## CapturedMeasurement\<T\>\.Instrument Property

The actual [Instrument](Compono.Metrics.CapturedMeasurement_T_.Instrument.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Instrument') instance that recorded this measurement \(the one the SUT
created\)\. Through [System\.Diagnostics\.Metrics\.Instrument\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.instrument.meter 'System\.Diagnostics\.Metrics\.Instrument\.Meter') it references the
actual [System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter'), and through [System\.Diagnostics\.Metrics\.Meter\.Scope](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter.scope 'System\.Diagnostics\.Metrics\.Meter\.Scope') the factory\. Descriptive members
\(for example [System\.Diagnostics\.Metrics\.Instrument\.Name](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.instrument.name 'System\.Diagnostics\.Metrics\.Instrument\.Name'),
[System\.Diagnostics\.Metrics\.Instrument\.Enabled](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.instrument.enabled 'System\.Diagnostics\.Metrics\.Instrument\.Enabled'), and the meter's name/version/tags\)
stay readable after the factory is disposed\.

```csharp
public System.Diagnostics.Metrics.Instrument Instrument { get; }
```

#### Property Value
[System\.Diagnostics\.Metrics\.Instrument](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.instrument 'System\.Diagnostics\.Metrics\.Instrument')