#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## LogCollector Class

A test\-owned sink for host\-wide logging output\. Register one with
[AddComponoCapture\(this ILoggingBuilder, LogCollector\)](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)') on a host's
[Microsoft\.Extensions\.Logging\.ILoggingBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.iloggingbuilder 'Microsoft\.Extensions\.Logging\.ILoggingBuilder'), then inspect it with
[GetCapturedEntries\(\)](Compono.Logging.LogCollector.GetCapturedEntries().md 'Compono\.Logging\.LogCollector\.GetCapturedEntries\(\)')/[GetLastCapturedEntry\(\)](Compono.Logging.LogCollector.GetLastCapturedEntry().md 'Compono\.Logging\.LogCollector\.GetLastCapturedEntry\(\)') or verify it with
[Verify\(\)](Compono.Logging.LogCollector.Verify().md 'Compono\.Logging\.LogCollector\.Verify\(\)') \- the same verification surface [LoggerTestingExtensions](Compono.Logging.LoggerTestingExtensions.md 'Compono\.Logging\.LoggerTestingExtensions') offers
for a composed logger\. The collector is thread\-safe, is never disposed or reset by the host, and
stays readable after the host is disposed\. See
docs/adr/0071\-compono\-logging\-host\-wide\-log\-capture\.md\.

```csharp
public sealed class LogCollector
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → LogCollector

| Constructors | |
| :--- | :--- |
| [LogCollector\(\)](Compono.Logging.LogCollector.LogCollector().md 'Compono\.Logging\.LogCollector\.LogCollector\(\)') | Creates an empty, test\-owned collector\. |

| Methods | |
| :--- | :--- |
| [ClearCapturedEntries\(\)](Compono.Logging.LogCollector.ClearCapturedEntries().md 'Compono\.Logging\.LogCollector\.ClearCapturedEntries\(\)') | Discards every entry captured so far\. The collector stays fully usable afterward \-             clearing never detaches it from a host that is still capturing into it\. |
| [GetCapturedEntries\(\)](Compono.Logging.LogCollector.GetCapturedEntries().md 'Compono\.Logging\.LogCollector\.GetCapturedEntries\(\)') | Every entry captured so far, oldest first \- a snapshot, safe to read while logging             continues on other threads\. |
| [GetLastCapturedEntry\(\)](Compono.Logging.LogCollector.GetLastCapturedEntry().md 'Compono\.Logging\.LogCollector\.GetLastCapturedEntry\(\)') | The most recently captured entry, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if nothing has been             captured yet\. |
| [Verify\(\)](Compono.Logging.LogCollector.Verify().md 'Compono\.Logging\.LogCollector\.Verify\(\)') | The entry point for fluent verification over this collector's entries, e\.g\. `logs.Verify().AtLevelOrAbove(LogLevel.Error).Never()`\. |
