#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeasurementVerificationBuilder&lt;T&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

## MeasurementVerificationBuilder\<T\>\.AtMost\(int\) Method

Asserts the accumulated filters matched at most [times](Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).md#Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).times 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtMost\(int\)\.times') times\.

```csharp
public void AtMost(int times);
```
#### Parameters

<a name='Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](../Compono/Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The filters matched more than
            [times](Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).md#Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).times 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtMost\(int\)\.times') times\.