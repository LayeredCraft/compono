#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeasurementVerificationBuilder&lt;T&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

## MeasurementVerificationBuilder\<T\>\.WithValue\(T\) Method

Restricts matches to measurements whose value equals [value](Compono.Metrics.MeasurementVerificationBuilder_T_.WithValue(T).md#Compono.Metrics.MeasurementVerificationBuilder_T_.WithValue(T).value 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithValue\(T\)\.value') exactly, using
            [System\.Collections\.Generic\.EqualityComparer&lt;&gt;\.Default](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.equalitycomparer-1.default 'System\.Collections\.Generic\.EqualityComparer\`1\.Default')\. Floating\-point equality is exact \- use
            [Matching\(Func&lt;CapturedMeasurement&lt;T&gt;,bool&gt;\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Matching\(System\.Func\<Compono\.Metrics\.CapturedMeasurement\<T\>,bool\>\)') for a tolerance\.

```csharp
public Compono.Metrics.MeasurementVerificationBuilder<T> WithValue(T value);
```
#### Parameters

<a name='Compono.Metrics.MeasurementVerificationBuilder_T_.WithValue(T).value'></a>

`value` [T](Compono.Metrics.MeasurementVerificationBuilder_T_.md#Compono.Metrics.MeasurementVerificationBuilder_T_.T 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.T')

#### Returns
[Compono\.Metrics\.MeasurementVerificationBuilder&lt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')[T](Compono.Metrics.MeasurementVerificationBuilder_T_.md#Compono.Metrics.MeasurementVerificationBuilder_T_.T 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.T')[&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')