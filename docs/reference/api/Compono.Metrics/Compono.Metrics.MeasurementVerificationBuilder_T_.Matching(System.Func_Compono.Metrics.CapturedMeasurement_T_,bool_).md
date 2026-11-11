#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeasurementVerificationBuilder&lt;T&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

## MeasurementVerificationBuilder\<T\>\.Matching\(Func\<CapturedMeasurement\<T\>,bool\>\) Method

Restricts matches to measurements satisfying an arbitrary [predicate](Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).md#Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).predicate 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Matching\(System\.Func\<Compono\.Metrics\.CapturedMeasurement\<T\>,bool\>\)\.predicate') \- the
            escape hatch for anything the named filters above don't cover \(an exact tag\-set match, a tolerance, and
            so on\)\.

```csharp
public Compono.Metrics.MeasurementVerificationBuilder<T> Matching(System.Func<Compono.Metrics.CapturedMeasurement<T>,bool> predicate);
```
#### Parameters

<a name='Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[Compono\.Metrics\.CapturedMeasurement&lt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')[T](Compono.Metrics.MeasurementVerificationBuilder_T_.md#Compono.Metrics.MeasurementVerificationBuilder_T_.T 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.T')[&gt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

#### Returns
[Compono\.Metrics\.MeasurementVerificationBuilder&lt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')[T](Compono.Metrics.MeasurementVerificationBuilder_T_.md#Compono.Metrics.MeasurementVerificationBuilder_T_.T 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.T')[&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[predicate](Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).md#Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).predicate 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Matching\(System\.Func\<Compono\.Metrics\.CapturedMeasurement\<T\>,bool\>\)\.predicate') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.