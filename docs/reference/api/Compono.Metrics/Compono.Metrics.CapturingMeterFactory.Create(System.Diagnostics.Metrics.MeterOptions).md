#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory')

## CapturingMeterFactory\.Create\(MeterOptions\) Method

Creates a [System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter') bound to this factory\. The same name, version, and tag set
\(order\-insensitive, [System\.Object\.Equals\(System\.Object,System\.Object\)](https://learn.microsoft.com/en-us/dotnet/api/system.object.equals#system-object-equals(system-object-system-object) 'System\.Object\.Equals\(System\.Object,System\.Object\)') on values\) returns the same
[System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter'); anything else creates a new one\.

```csharp
public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options);
```
#### Parameters

<a name='Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).options'></a>

`options` [System\.Diagnostics\.Metrics\.MeterOptions](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meteroptions 'System\.Diagnostics\.Metrics\.MeterOptions')

The meter options\.

Implements [Create\(MeterOptions\)](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory.create#system-diagnostics-metrics-imeterfactory-create(system-diagnostics-metrics-meteroptions) 'System\.Diagnostics\.Metrics\.IMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)')

#### Returns
[System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[options](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md#Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).options 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)\.options') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[options](Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).md#Compono.Metrics.CapturingMeterFactory.Create(System.Diagnostics.Metrics.MeterOptions).options 'Compono\.Metrics\.CapturingMeterFactory\.Create\(System\.Diagnostics\.Metrics\.MeterOptions\)\.options')'s [System\.Diagnostics\.Metrics\.MeterOptions\.Scope](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meteroptions.scope 'System\.Diagnostics\.Metrics\.MeterOptions\.Scope')
            is non\-[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') and isn't this factory\.

[System\.ObjectDisposedException](https://learn.microsoft.com/en-us/dotnet/api/system.objectdisposedexception 'System\.ObjectDisposedException')  
This factory has been disposed\.