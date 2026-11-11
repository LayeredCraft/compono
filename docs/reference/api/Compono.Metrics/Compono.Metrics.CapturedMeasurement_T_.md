#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics')

## CapturedMeasurement\<T\> Struct

A single captured measurement: the recorded value, its tags, and the [Instrument](Compono.Metrics.CapturedMeasurement_T_.Instrument.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Instrument') that
recorded it\. A plain [readonly](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/readonly 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/readonly')[struct](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/struct 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/struct') \- deliberately not a
[record](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/record 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/record')[struct](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/struct 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/struct'), so there is no synthesized value equality that
would suggest `==` compares [Tags](Compono.Metrics.CapturedMeasurement_T_.Tags.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Tags') by value; use [Value](Compono.Metrics.CapturedMeasurement_T_.Value.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Value') directly or
the [Verify&lt;T&gt;\(this IMeterFactory, string\)](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string) 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)') filter chain\.
See docs/adr/0072\-compono\-metrics\-testing\-support\-package\.md\.

```csharp
public readonly struct CapturedMeasurement<T>
    where T : struct
```
#### Type parameters

<a name='Compono.Metrics.CapturedMeasurement_T_.T'></a>

`T`

The measurement's value type \- one of the seven BCL measurement value types
            \([System\.Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte 'System\.Byte'), [System\.Int16](https://learn.microsoft.com/en-us/dotnet/api/system.int16 'System\.Int16'), [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32'), [System\.Int64](https://learn.microsoft.com/en-us/dotnet/api/system.int64 'System\.Int64'), [System\.Single](https://learn.microsoft.com/en-us/dotnet/api/system.single 'System\.Single'),
            [System\.Double](https://learn.microsoft.com/en-us/dotnet/api/system.double 'System\.Double'), [System\.Decimal](https://learn.microsoft.com/en-us/dotnet/api/system.decimal 'System\.Decimal')\)\.

| Properties | |
| :--- | :--- |
| [Instrument](Compono.Metrics.CapturedMeasurement_T_.Instrument.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Instrument') | The actual [Instrument](Compono.Metrics.CapturedMeasurement_T_.Instrument.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Instrument') instance that recorded this measurement \(the one the SUT created\)\. Through [System\.Diagnostics\.Metrics\.Instrument\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.instrument.meter 'System\.Diagnostics\.Metrics\.Instrument\.Meter') it references the actual [System\.Diagnostics\.Metrics\.Meter](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter 'System\.Diagnostics\.Metrics\.Meter'), and through [System\.Diagnostics\.Metrics\.Meter\.Scope](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter.scope 'System\.Diagnostics\.Metrics\.Meter\.Scope') the factory\. Descriptive members \(for example [System\.Diagnostics\.Metrics\.Instrument\.Name](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.instrument.name 'System\.Diagnostics\.Metrics\.Instrument\.Name'), [System\.Diagnostics\.Metrics\.Instrument\.Enabled](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.instrument.enabled 'System\.Diagnostics\.Metrics\.Instrument\.Enabled'), and the meter's name/version/tags\) stay readable after the factory is disposed\. |
| [Tags](Compono.Metrics.CapturedMeasurement_T_.Tags.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Tags') | This measurement's tags, copied out of the BCL's callback span in callback order\. A per\-measurement copy that never aliases the BCL's span or any other measurement's tags; empty \(never [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\) when the measurement had no tags\. Meter\-level and instrument\-level tags are not merged in \- they're reachable through [Instrument](Compono.Metrics.CapturedMeasurement_T_.Instrument.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Instrument')\. |
| [Value](Compono.Metrics.CapturedMeasurement_T_.Value.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Value') | The recorded value, exactly as the instrument received it \(no normalization, no             aggregation\)\. |
