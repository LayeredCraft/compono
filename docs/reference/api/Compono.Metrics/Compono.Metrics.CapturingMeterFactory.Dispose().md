#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory')

## CapturingMeterFactory\.Dispose\(\) Method

Disposes every meter this factory created \(raising
`MeasurementsCompleted`\), then its [System\.Diagnostics\.Metrics\.MeterListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meterlistener 'System\.Diagnostics\.Metrics\.MeterListener')\. Idempotent and
thread\-safe\. Captured measurements stay readable afterwards; measurements recorded on the disposed
meters' instruments are no longer captured\.

```csharp
public void Dispose();
```

Implements [Dispose\(\)](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable.dispose 'System\.IDisposable\.Dispose')