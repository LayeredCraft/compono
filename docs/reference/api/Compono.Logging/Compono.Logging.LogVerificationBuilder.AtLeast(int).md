#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.AtLeast\(int\) Method

Asserts the accumulated filters matched at least [times](Compono.Logging.LogVerificationBuilder.AtLeast(int).md#Compono.Logging.LogVerificationBuilder.AtLeast(int).times 'Compono\.Logging\.LogVerificationBuilder\.AtLeast\(int\)\.times') times\.

```csharp
public void AtLeast(int times);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.AtLeast(int).times'></a>

`times` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[TestDoubleVerificationException](../Compono/Compono.TestDoubleVerificationException.md 'Compono\.TestDoubleVerificationException')  
The filters matched fewer than
            [times](Compono.Logging.LogVerificationBuilder.AtLeast(int).md#Compono.Logging.LogVerificationBuilder.AtLeast(int).times 'Compono\.Logging\.LogVerificationBuilder\.AtLeast\(int\)\.times') times\.