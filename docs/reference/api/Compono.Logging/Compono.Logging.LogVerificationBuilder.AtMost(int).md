#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.AtMost\(int\) Method

Asserts the accumulated filters matched at most [times](Compono.Logging.LogVerificationBuilder.AtMost(int).md#Compono.Logging.LogVerificationBuilder.AtMost(int).times 'Compono\.Logging\.LogVerificationBuilder\.AtMost\(int\)\.times') times\.

```csharp
public void AtMost(int times);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.AtMost(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](../Compono/Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The filters matched more than
            [times](Compono.Logging.LogVerificationBuilder.AtMost(int).md#Compono.Logging.LogVerificationBuilder.AtMost(int).times 'Compono\.Logging\.LogVerificationBuilder\.AtMost\(int\)\.times') times\.