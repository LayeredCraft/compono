#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeterFactoryTestingExtensions](Compono.Metrics.MeterFactoryTestingExtensions.md 'Compono\.Metrics\.MeterFactoryTestingExtensions')

## MeterFactoryTestingExtensions\.ClearMeasurements\(this IMeterFactory\) Method

Discards every measurement captured by [factory](Compono.Metrics.MeterFactoryTestingExtensions.ClearMeasurements(thisSystem.Diagnostics.Metrics.IMeterFactory).md#Compono.Metrics.MeterFactoryTestingExtensions.ClearMeasurements(thisSystem.Diagnostics.Metrics.IMeterFactory).factory 'Compono\.Metrics\.MeterFactoryTestingExtensions\.ClearMeasurements\(this System\.Diagnostics\.Metrics\.IMeterFactory\)\.factory') so far\. Works before and
            after disposal\.

```csharp
public static void ClearMeasurements(this System.Diagnostics.Metrics.IMeterFactory factory);
```
#### Parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.ClearMeasurements(thisSystem.Diagnostics.Metrics.IMeterFactory).factory'></a>

`factory` [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory')

The capturing factory\.

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[factory](Compono.Metrics.MeterFactoryTestingExtensions.ClearMeasurements(thisSystem.Diagnostics.Metrics.IMeterFactory).md#Compono.Metrics.MeterFactoryTestingExtensions.ClearMeasurements(thisSystem.Diagnostics.Metrics.IMeterFactory).factory 'Compono\.Metrics\.MeterFactoryTestingExtensions\.ClearMeasurements\(this System\.Diagnostics\.Metrics\.IMeterFactory\)\.factory') is not a
            [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory')\.