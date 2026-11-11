#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeterFactoryTestingExtensions](Compono.Metrics.MeterFactoryTestingExtensions.md 'Compono\.Metrics\.MeterFactoryTestingExtensions')

## MeterFactoryTestingExtensions\.GetMeasurements Method

| Overloads | |
| :--- | :--- |
| [GetMeasurements&lt;T&gt;\(this IMeterFactory, string\)](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string) 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)') | Every measurement captured for instruments named [instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.instrumentName'), across every meter, merged in capture order \(ADR\-0072 selection rule 7\)\. A new list is returned on each call; later captures, [ClearMeasurements\(this IMeterFactory\)](Compono.Metrics.MeterFactoryTestingExtensions.ClearMeasurements(thisSystem.Diagnostics.Metrics.IMeterFactory).md 'Compono\.Metrics\.MeterFactoryTestingExtensions\.ClearMeasurements\(this System\.Diagnostics\.Metrics\.IMeterFactory\)'), and [Dispose\(\)](Compono.Metrics.CapturingMeterFactory.Dispose().md 'Compono\.Metrics\.CapturingMeterFactory\.Dispose\(\)') never change a list already returned\. |
| [GetMeasurements&lt;T&gt;\(this IMeterFactory, string, string\)](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string) 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)') | Every measurement captured for instruments named [instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.instrumentName') in the meter named [meterName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).meterName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.meterName'), merged in capture order\. A new list is returned on each call\. |

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string)'></a>

## MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this IMeterFactory, string\) Method

Every measurement captured for instruments named [instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.instrumentName'), across every
meter, merged in capture order \(ADR\-0072 selection rule 7\)\. A new list is returned on each call;
later captures, [ClearMeasurements\(this IMeterFactory\)](Compono.Metrics.MeterFactoryTestingExtensions.ClearMeasurements(thisSystem.Diagnostics.Metrics.IMeterFactory).md 'Compono\.Metrics\.MeterFactoryTestingExtensions\.ClearMeasurements\(this System\.Diagnostics\.Metrics\.IMeterFactory\)'), and [Dispose\(\)](Compono.Metrics.CapturingMeterFactory.Dispose().md 'Compono\.Metrics\.CapturingMeterFactory\.Dispose\(\)') never
change a list already returned\.

```csharp
public static System.Collections.Generic.IReadOnlyList<Compono.Metrics.CapturedMeasurement<T>> GetMeasurements<T>(this System.Diagnostics.Metrics.IMeterFactory factory, string instrumentName)
    where T : struct;
```
#### Type parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).T'></a>

`T`

The measurement value type \- one of the seven supported types\.
#### Parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).factory'></a>

`factory` [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory')

The capturing factory\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).instrumentName'></a>

`instrumentName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The instrument name to match \(ordinal, case\-sensitive\)\.

#### Returns
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[Compono\.Metrics\.CapturedMeasurement&lt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')[T](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.T')[&gt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[factory](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).factory 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.factory') is not a
            [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory')\.

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.instrumentName') is null or empty; or
            [T](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.T') is not one of the seven supported measurement value types\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string)'></a>

## MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this IMeterFactory, string, string\) Method

Every measurement captured for instruments named [instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.instrumentName') in the meter named
[meterName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).meterName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.meterName'), merged in capture order\. A new list is returned on each call\.

```csharp
public static System.Collections.Generic.IReadOnlyList<Compono.Metrics.CapturedMeasurement<T>> GetMeasurements<T>(this System.Diagnostics.Metrics.IMeterFactory factory, string meterName, string instrumentName)
    where T : struct;
```
#### Type parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).T'></a>

`T`

The measurement value type \- one of the seven supported types\.
#### Parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).factory'></a>

`factory` [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory')

The capturing factory\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).meterName'></a>

`meterName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The meter name to match \(ordinal, case\-sensitive\)\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).instrumentName'></a>

`instrumentName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The instrument name to match \(ordinal, case\-sensitive\)\.

#### Returns
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[Compono\.Metrics\.CapturedMeasurement&lt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')[T](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.T')[&gt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[factory](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).factory 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.factory') is not a
            [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory')\.

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[meterName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).meterName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.meterName') or [instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.instrumentName')
            is null or empty; or [T](Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements.md#Compono.Metrics.MeterFactoryTestingExtensions.GetMeasurements_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.GetMeasurements\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.T') is not one of the seven supported measurement value
            types\.