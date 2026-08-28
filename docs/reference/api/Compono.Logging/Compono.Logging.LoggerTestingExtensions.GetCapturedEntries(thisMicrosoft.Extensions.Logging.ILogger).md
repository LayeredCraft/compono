#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LoggerTestingExtensions](Compono.Logging.LoggerTestingExtensions.md 'Compono\.Logging\.LoggerTestingExtensions')

## LoggerTestingExtensions\.GetCapturedEntries\(this ILogger\) Method

Every entry captured by [logger](Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.GetCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') so far, oldest first\.

```csharp
public static System.Collections.Generic.IReadOnlyList<Compono.Logging.CapturedLogEntry> GetCapturedEntries(this Microsoft.Extensions.Logging.ILogger logger);
```
#### Parameters

<a name='Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger'></a>

`logger` [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')

#### Returns
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[logger](Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.GetCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') is not a
            Compono\.Logging capturing logger\.