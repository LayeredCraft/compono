#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CallVerifier](Compono.CallVerifier.md 'Compono\.CallVerifier')

## CallVerifier\.Exactly\(int\) Method

Asserts the member was called exactly [times](Compono.CallVerifier.Exactly(int).md#Compono.CallVerifier.Exactly(int).times 'Compono\.CallVerifier\.Exactly\(int\)\.times') times\.

```csharp
public void Exactly(int times);
```
#### Parameters

<a name='Compono.CallVerifier.Exactly(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The member was not called exactly [times](Compono.CallVerifier.Exactly(int).md#Compono.CallVerifier.Exactly(int).times 'Compono\.CallVerifier\.Exactly\(int\)\.times') times\.