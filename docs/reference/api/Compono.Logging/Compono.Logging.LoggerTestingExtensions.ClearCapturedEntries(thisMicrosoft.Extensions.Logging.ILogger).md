#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LoggerTestingExtensions](Compono.Logging.LoggerTestingExtensions.md 'Compono\.Logging\.LoggerTestingExtensions')

## LoggerTestingExtensions\.ClearCapturedEntries\(this ILogger\) Method

Discards every entry captured by [logger](Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.ClearCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') so far\.

```csharp
public static void ClearCapturedEntries(this Microsoft.Extensions.Logging.ILogger logger);
```
#### Parameters

<a name='Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger'></a>

`logger` [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[logger](Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.ClearCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') is not a
            Compono\.Logging capturing logger\.