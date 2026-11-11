#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics')

## CapturingMeterFactory Class

A hand\-written, reflection\-free [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory') that captures the measurements of every
instrument its meters publish into an inspectable, thread\-safe, Compono\-native model
\([CapturedMeasurement&lt;T&gt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')\)\. Directly, publicly constructible \- composing through
[UseMetrics\(this CompositionBuilder\)](Compono.Metrics.CompositionBuilderExtensions.UseMetrics(thisCompono.CompositionBuilder).md 'Compono\.Metrics\.CompositionBuilderExtensions\.UseMetrics\(this Compono\.CompositionBuilder\)') is not required\. See
docs/adr/0072\-compono\-metrics\-testing\-support\-package\.md\.

```csharp
public sealed class CapturingMeterFactory : System.Diagnostics.Metrics.IMeterFactory, System.IDisposable
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CapturingMeterFactory

Implements [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory'), [System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')

### Remarks

Factory semantics match `DefaultMeterFactory`: [Create\(MeterOptions\)](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)')
rejects a [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')[System\.Diagnostics\.Metrics\.MeterOptions](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meteroptions 'System\.Diagnostics\.Metrics\.MeterOptions') and a foreign [System\.Diagnostics\.Metrics\.MeterOptions\.Scope](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meteroptions.scope 'System\.Diagnostics\.Metrics\.MeterOptions\.Scope'),
throws [System\.ObjectDisposedException](https://learn.microsoft.com/en-us/dotnet/api/system.objectdisposedexception 'System\.ObjectDisposedException') after disposal, never mutates the caller's options, and caches
meters by name, version, and order-insensitive tag set (duplicate tag keys are significant - see
[Create\(MeterOptions\)](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)')). Disposing a [System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter') a consumer obtained from [Create\(MeterOptions\)](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)') is a
no-op: the factory owns the meter's lifetime and releases it on [Dispose\(\)](Compono.Metrics.CapturingMeterFactory.Dispose().md 'Compono\.Metrics\.CapturingMeterFactory\.Dispose\(\)').

Each factory owns one lazily created [System\.Diagnostics\.Metrics\.MeterListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meterlistener 'System\.Diagnostics\.Metrics\.MeterListener'), created on the first [Create\(MeterOptions\)](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)')
and disposed with the factory. It enables only instruments whose [System\.Diagnostics\.Metrics\.Meter\.Scope](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter.scope 'System\.Diagnostics\.Metrics\.Meter\.Scope') is this factory,
so two factories never see each other's measurements even for identical meter and instrument names. There
is no static or shared capture state.

Disposal is the test's responsibility ([System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory') already extends
[System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')); composition never disposes the factory. An undisposed factory that created
meters stays referenced by the BCL's static meter list for the process lifetime - the same cost as an
undisposed `DefaultMeterFactory`.

| Constructors | |
| :--- | :--- |
| [CapturingMeterFactory\(\)](Compono.Metrics.CapturingMeterFactory.CapturingMeterFactory().md 'Compono\.Metrics\.CapturingMeterFactory\.CapturingMeterFactory\(\)') | Creates a standalone [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory'), usable with no Compono composition involved at all\. |

| Methods | |
| :--- | :--- |
| [Create\(MeterOptions\)](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)') | Creates a [System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter') bound to this factory\. The same name, version, and tag set \(order\-insensitive, [System\.Object\.Equals\(System\.Object,System\.Object\)](https://learn.microsoft.com/en-us/dotnet/api/system.object.equals#system-object-equals(system-object-system-object) 'System\.Object\.Equals\(System\.Object,System\.Object\)') on values\) returns the same [System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter'); anything else creates a new one\. Duplicate tag keys are significant: `[k=1, k=1]` is a different set from `[k=1, j=2]`\. |
| [Dispose\(\)](Compono.Metrics.CapturingMeterFactory.Dispose().md 'Compono\.Metrics\.CapturingMeterFactory\.Dispose\(\)') | Disposes every meter this factory created \(raising `MeasurementsCompleted`\), then its [System\.Diagnostics\.Metrics\.MeterListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meterlistener 'System\.Diagnostics\.Metrics\.MeterListener')\. Idempotent and thread\-safe\. Captured measurements stay readable afterwards; measurements recorded on the disposed meters' instruments are no longer captured\. Disposing a [System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter') a consumer obtained from [Create\(MeterOptions\)](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)') is a no\-op \- the factory owns the meter's lifetime, matching `DefaultMeterFactory`\. |
