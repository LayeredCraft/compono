#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeasurementVerificationBuilder&lt;T&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

## MeasurementVerificationBuilder\<T\>\.AtLeast\(int\) Method

Asserts the accumulated filters matched at least [times](Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).md#Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).times 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtLeast\(int\)\.times') times\.

```csharp
public void AtLeast(int times);
```
#### Parameters

<a name='Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](../Compono/Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The filters matched fewer than
            [times](Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).md#Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).times 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtLeast\(int\)\.times') times\.