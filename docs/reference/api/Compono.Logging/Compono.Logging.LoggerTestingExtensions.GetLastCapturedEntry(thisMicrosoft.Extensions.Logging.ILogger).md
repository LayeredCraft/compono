#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LoggerTestingExtensions](Compono.Logging.LoggerTestingExtensions.md 'Compono\.Logging\.LoggerTestingExtensions')

## LoggerTestingExtensions\.GetLastCapturedEntry\(this ILogger\) Method

The most recently captured entry, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if nothing has been
            captured yet\.

```csharp
public static System.Nullable<Compono.Logging.CapturedLogEntry> GetLastCapturedEntry(this Microsoft.Extensions.Logging.ILogger logger);
```
#### Parameters

<a name='Compono.Logging.LoggerTestingExtensions.GetLastCapturedEntry(thisMicrosoft.Extensions.Logging.ILogger).logger'></a>

`logger` [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')

#### Returns
[System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[logger](Compono.Logging.LoggerTestingExtensions.GetLastCapturedEntry(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.GetLastCapturedEntry(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.GetLastCapturedEntry\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') is not a
            Compono\.Logging capturing logger\.