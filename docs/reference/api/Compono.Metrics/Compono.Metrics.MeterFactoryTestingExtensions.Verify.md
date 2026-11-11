#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeterFactoryTestingExtensions](Compono.Metrics.MeterFactoryTestingExtensions.md 'Compono\.Metrics\.MeterFactoryTestingExtensions')

## MeterFactoryTestingExtensions\.Verify Method

| Overloads | |
| :--- | :--- |
| [Verify&lt;T&gt;\(this IMeterFactory, string\)](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string) 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)') | The entry point for fluent verification, for example `meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").Once()`\. The selection is resolved here, when the builder is created, so a meter ambiguity, type conflict, or type mismatch throws before any filter is added\. |
| [Verify&lt;T&gt;\(this IMeterFactory, string, string\)](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string) 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)') | The entry point for fluent verification scoped to one meter name\. The selection is resolved here, when the builder is created\. |

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string)'></a>

## MeterFactoryTestingExtensions\.Verify\<T\>\(this IMeterFactory, string\) Method

The entry point for fluent verification, for example
`meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").Once()`\. The selection is
resolved here, when the builder is created, so a meter ambiguity, type conflict, or type mismatch
throws before any filter is added\.

```csharp
public static Compono.Metrics.MeasurementVerificationBuilder<T> Verify<T>(this System.Diagnostics.Metrics.IMeterFactory factory, string instrumentName)
    where T : struct;
```
#### Type parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).T'></a>

`T`

The measurement value type \- one of the seven supported types\.
#### Parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).factory'></a>

`factory` [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory')

The capturing factory\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).instrumentName'></a>

`instrumentName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The instrument name to match \(ordinal, case\-sensitive\)\.

#### Returns
[Compono\.Metrics\.MeasurementVerificationBuilder&lt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')[T](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.T')[&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[factory](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).factory 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.factory') is not a
            [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory'); the name matches instruments in more than one meter; the name
            matches instruments that record more than one value type; or the matching instruments record a type
            other than [T](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.T')\.

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.instrumentName') is null or empty; or
            [T](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string\)\.T') is not one of the seven supported measurement value types\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string)'></a>

## MeterFactoryTestingExtensions\.Verify\<T\>\(this IMeterFactory, string, string\) Method

The entry point for fluent verification scoped to one meter name\. The selection is resolved here, when
the builder is created\.

```csharp
public static Compono.Metrics.MeasurementVerificationBuilder<T> Verify<T>(this System.Diagnostics.Metrics.IMeterFactory factory, string meterName, string instrumentName)
    where T : struct;
```
#### Type parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).T'></a>

`T`

The measurement value type \- one of the seven supported types\.
#### Parameters

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).factory'></a>

`factory` [System\.Diagnostics\.Metrics\.IMeterFactory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.imeterfactory 'System\.Diagnostics\.Metrics\.IMeterFactory')

The capturing factory\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).meterName'></a>

`meterName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The meter name to match \(ordinal, case\-sensitive\)\.

<a name='Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).instrumentName'></a>

`instrumentName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The instrument name to match \(ordinal, case\-sensitive\)\.

#### Returns
[Compono\.Metrics\.MeasurementVerificationBuilder&lt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')[T](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.T')[&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[factory](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).factory 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.factory') is not a
            [CapturingMeterFactory](Compono.Metrics.CapturingMeterFactory.md 'Compono\.Metrics\.CapturingMeterFactory'); the name matches instruments that record more than one value type;
            or the matching instruments record a type other than [T](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.T')\.

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[meterName](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).meterName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.meterName') or [instrumentName](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).instrumentName 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.instrumentName')
            is null or empty; or [T](Compono.Metrics.MeterFactoryTestingExtensions.Verify.md#Compono.Metrics.MeterFactoryTestingExtensions.Verify_T_(thisSystem.Diagnostics.Metrics.IMeterFactory,string,string).T 'Compono\.Metrics\.MeterFactoryTestingExtensions\.Verify\<T\>\(this System\.Diagnostics\.Metrics\.IMeterFactory, string, string\)\.T') is not one of the seven supported measurement value
            types\.