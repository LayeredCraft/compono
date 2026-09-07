#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CallVerifier](Compono.CallVerifier.md 'Compono\.CallVerifier')

## CallVerifier\.AtMost\(int\) Method

Asserts the member was called at most [times](Compono.CallVerifier.AtMost(int).md#Compono.CallVerifier.AtMost(int).times 'Compono\.CallVerifier\.AtMost\(int\)\.times') times\.

```csharp
public void AtMost(int times);
```
#### Parameters

<a name='Compono.CallVerifier.AtMost(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The member was called more than [times](Compono.CallVerifier.AtMost(int).md#Compono.CallVerifier.AtMost(int).times 'Compono\.CallVerifier\.AtMost\(int\)\.times') times\.