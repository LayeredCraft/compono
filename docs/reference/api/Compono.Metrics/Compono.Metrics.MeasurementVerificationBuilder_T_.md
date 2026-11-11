#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics')

## MeasurementVerificationBuilder\<T\> Class

A fluent filter chain over a resolved selection of captured measurements, ending in a thin, one\-line
forwarder to core [CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') \- [Once\(\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Once().md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Once\(\)')/[Never\(\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Never().md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Never\(\)')/[Exactly\(int\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Exactly(int).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Exactly\(int\)')
each build a [CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') from the filtered match count right here and call the corresponding
member\. [CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') itself is never part of this type's public surface, and no new
counting/`Times` abstraction is introduced\. See
docs/adr/0072\-compono\-metrics\-testing\-support\-package\.md §"Selection and matching"\.

```csharp
public sealed class MeasurementVerificationBuilder<T>
    where T : struct
```
#### Type parameters

<a name='Compono.Metrics.MeasurementVerificationBuilder_T_.T'></a>

`T`

The measurement value type \- one of the seven supported types\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → MeasurementVerificationBuilder\<T\>

| Methods | |
| :--- | :--- |
| [AtLeast\(int\)](Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtLeast\(int\)') | Asserts the accumulated filters matched at least [times](Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).md#Compono.Metrics.MeasurementVerificationBuilder_T_.AtLeast(int).times 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtLeast\(int\)\.times') times\. |
| [AtMost\(int\)](Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtMost\(int\)') | Asserts the accumulated filters matched at most [times](Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).md#Compono.Metrics.MeasurementVerificationBuilder_T_.AtMost(int).times 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.AtMost\(int\)\.times') times\. |
| [Exactly\(int\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Exactly(int).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Exactly\(int\)') | Asserts the accumulated filters matched exactly [times](Compono.Metrics.MeasurementVerificationBuilder_T_.Exactly(int).md#Compono.Metrics.MeasurementVerificationBuilder_T_.Exactly(int).times 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Exactly\(int\)\.times') times\. |
| [Matching\(Func&lt;CapturedMeasurement&lt;T&gt;,bool&gt;\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Matching\(System\.Func\<Compono\.Metrics\.CapturedMeasurement\<T\>,bool\>\)') | Restricts matches to measurements satisfying an arbitrary [predicate](Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).md#Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).predicate 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Matching\(System\.Func\<Compono\.Metrics\.CapturedMeasurement\<T\>,bool\>\)\.predicate') \- the             escape hatch for anything the named filters above don't cover \(an exact tag\-set match, a tolerance, and             so on\)\. |
| [Never\(\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Never().md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Never\(\)') | Asserts the accumulated filters never matched\. |
| [Once\(\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Once().md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Once\(\)') | Asserts the accumulated filters matched exactly once\. |
| [WithoutTag\(string\)](Compono.Metrics.MeasurementVerificationBuilder_T_.WithoutTag(string).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithoutTag\(string\)') | Restricts matches to measurements that have no tag with key [key](Compono.Metrics.MeasurementVerificationBuilder_T_.WithoutTag(string).md#Compono.Metrics.MeasurementVerificationBuilder_T_.WithoutTag(string).key 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithoutTag\(string\)\.key') \(ordinal\)\. |
| [WithTag\(string, object\)](Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithTag\(string, object\)') | Restricts matches to measurements that contain a tag with key [key](Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).md#Compono.Metrics.MeasurementVerificationBuilder_T_.WithTag(string,object).key 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithTag\(string, object\)\.key') \(ordinal\)             whose value satisfies [System\.Object\.Equals\(System\.Object,System\.Object\)](https://learn.microsoft.com/en-us/dotnet/api/system.object.equals#system-object-equals(system-object-system-object) 'System\.Object\.Equals\(System\.Object,System\.Object\)')\. Other tags are ignored, and there is             no numeric coercion \(`200`, an [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32'), doesn't match `200L`\)\. Chain calls for             several tags\. |
| [WithValue\(T\)](Compono.Metrics.MeasurementVerificationBuilder_T_.WithValue(T).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithValue\(T\)') | Restricts matches to measurements whose value equals [value](Compono.Metrics.MeasurementVerificationBuilder_T_.WithValue(T).md#Compono.Metrics.MeasurementVerificationBuilder_T_.WithValue(T).value 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.WithValue\(T\)\.value') exactly, using             [System\.Collections\.Generic\.EqualityComparer&lt;&gt;\.Default](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.equalitycomparer-1.default 'System\.Collections\.Generic\.EqualityComparer\`1\.Default')\. Floating\-point equality is exact \- use             [Matching\(Func&lt;CapturedMeasurement&lt;T&gt;,bool&gt;\)](Compono.Metrics.MeasurementVerificationBuilder_T_.Matching(System.Func_Compono.Metrics.CapturedMeasurement_T_,bool_).md 'Compono\.Metrics\.MeasurementVerificationBuilder\<T\>\.Matching\(System\.Func\<Compono\.Metrics\.CapturedMeasurement\<T\>,bool\>\)') for a tolerance\. |
