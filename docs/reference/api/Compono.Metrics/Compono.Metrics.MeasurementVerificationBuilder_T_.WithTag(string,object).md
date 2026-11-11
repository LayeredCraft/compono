#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[MeasurementVerificationBuilder&lt;T&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

## MeasurementVerificationBuilder\<T\>\.WithTag\(string, object\) Method

Restricts matches to measurements that contain a tag with key [key](Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).md#Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).key 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithTag\(string, object\)\.key') \(ordinal\)
            whose value satisfies [System\.Object\.Equals\(System\.Object,System\.Object\)](https://learn.microsoft.com/en-us/dotnet/api/system.object.equals#system-object-equals(system-object-system-object) 'System\.Object\.Equals\(System\.Object,System\.Object\)')\. Other tags are ignored, and there is
            no numeric coercion \(`200`, an [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32'), doesn't match `200L`\)\. Chain calls for
            several tags\.

```csharp
public Compono.Metrics.MeasurementVerificationBuilder<T> WithTag(string key, object? value);
```
#### Parameters

<a name='Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).key'></a>

`key` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a name='Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).value'></a>

`value` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

#### Returns
[Compono\.Metrics\.MeasurementVerificationBuilder&lt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')[T](Compono.Metrics.MeasurementVerificationBuilder_T_.md#Compono.Metrics.MeasurementVerificationBuilder_T_.T 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.T')[&gt;](Compono.Metrics.MeasurementVerificationBuilder_T_.md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[key](Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).md#Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).key 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithTag\(string, object\)\.key') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.