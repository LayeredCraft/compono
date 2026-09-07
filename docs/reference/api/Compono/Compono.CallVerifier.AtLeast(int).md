#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CallVerifier](Compono.CallVerifier.md 'Compono\.CallVerifier')

## CallVerifier\.AtLeast\(int\) Method

Asserts the member was called at least [times](Compono.CallVerifier.AtLeast(int).md#Compono.CallVerifier.AtLeast(int).times 'Compono\.CallVerifier\.AtLeast\(int\)\.times') times\.

```csharp
public void AtLeast(int times);
```
#### Parameters

<a name='Compono.CallVerifier.AtLeast(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The member was called fewer than [times](Compono.CallVerifier.AtLeast(int).md#Compono.CallVerifier.AtLeast(int).times 'Compono\.CallVerifier\.AtLeast\(int\)\.times') times\.