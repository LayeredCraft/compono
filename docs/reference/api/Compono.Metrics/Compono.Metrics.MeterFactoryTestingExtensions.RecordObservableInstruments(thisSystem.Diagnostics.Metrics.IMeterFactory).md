#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeterFactoryTestingExtensions](Compono.Metrics.MeterFactoryTestingExtensions.md 'Compono\.Metrics\.MeterFactoryTestingExtensions')

## MeterFactoryTestingExtensions\.RecordObservableInstruments\(this IMeterFactory\) Method

Polls this factory's observable instruments: calls this factory's listener's
[System\.Diagnostics\.Metrics\.MeterListener\.RecordObservableInstruments](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meterlistener.recordobservableinstruments 'System\.Diagnostics\.Metrics\.MeterListener\.RecordObservableInstruments')\. Callbacks run synchronously on the calling
thread and only for this factory's observable instruments; each observation appends measurements, so
calling it twice records twice\. Reads never poll\. A no\-op before the first
[Create\(MeterOptions\)](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)') or after disposal\.

```csharp
public static void RecordObservableInstruments(this System.Diagnostics.Metrics.IMeterFactory factory);
```
#### Parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.RecordObservableInstruments(thisSystem.Diagnostics.Metrics.IMeterFactory).factory'></a>

`factory` [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory')

The capturing factory\.

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[factory](Compono.Metrics.MeterFactoryTestingExtensions.RecordObservableInstruments(thisSystem.Diagnostics.Metrics.IMeterFactory).md#Compono.Metrics.MeterFactoryTestingExtensions.RecordObservableInstruments(thisSystem.Diagnostics.Metrics.IMeterFactory).factory 'Compono\.Metrics\.MeterFactoryTestingExtensions\.RecordObservableInstruments\(this System\.Diagnostics\.Metrics\.IMeterFactory\)\.factory') is not a
            [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory')\.