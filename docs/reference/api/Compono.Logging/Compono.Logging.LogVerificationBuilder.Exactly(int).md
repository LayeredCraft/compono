#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.Exactly\(int\) Method

Asserts the accumulated filters matched exactly [times](Compono.Logging.LogVerificationBuilder.Exactly(int).md#Compono.Logging.LogVerificationBuilder.Exactly(int).times 'Compono\.Logging\.LogVerificationBuilder\.Exactly\(int\)\.times') times\.

```csharp
public void Exactly(int times);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.Exactly(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](../Compono/Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The filters did not match exactly
            [times](Compono.Logging.LogVerificationBuilder.Exactly(int).md#Compono.Logging.LogVerificationBuilder.Exactly(int).times 'Compono\.Logging\.LogVerificationBuilder\.Exactly\(int\)\.times') times\.