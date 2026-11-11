#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## ComponoLoggingBuilderExtensions Class

Host integration entry point \- registers a test\-owned [LogCollector](Compono.Logging.LogCollector.md 'Compono\.Logging\.LogCollector') as a logging
provider so every host logger's output lands in it\. Named to avoid any ambiguity with
`Microsoft.Extensions.Logging.LoggingBuilderExtensions`\. See
docs/adr/0071\-compono\-logging\-host\-wide\-log\-capture\.md\.

```csharp
public static class ComponoLoggingBuilderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ComponoLoggingBuilderExtensions

| Methods | |
| :--- | :--- |
| [AddComponoCapture\(this ILoggingBuilder, LogCollector\)](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)') | Registers [collector](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).collector 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.collector') as a Compono logging provider on [builder](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).builder 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.builder')\. Every host logger's entries are then appended to the collector \(subject to the host's own filter rules\) and inspectable via [GetCapturedEntries\(\)](Compono.Logging.LogCollector.GetCapturedEntries().md 'Compono\.Logging\.LogCollector\.GetCapturedEntries\(\)')/[Verify\(\)](Compono.Logging.LogCollector.Verify().md 'Compono\.Logging\.LogCollector\.Verify\(\)')\. This never changes filter rules\. Registering the same collector twice is idempotent \(one provider, no duplicate entries\); registering distinct collectors adds one provider per collector, and each receives every entry\. Returns [builder](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).builder 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.builder') for chaining\. |
