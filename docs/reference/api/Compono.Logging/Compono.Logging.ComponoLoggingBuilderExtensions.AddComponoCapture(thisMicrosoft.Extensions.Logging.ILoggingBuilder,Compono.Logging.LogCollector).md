#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[ComponoLoggingBuilderExtensions](Compono.Logging.ComponoLoggingBuilderExtensions.md 'Compono\.Logging\.ComponoLoggingBuilderExtensions')

## ComponoLoggingBuilderExtensions\.AddComponoCapture\(this ILoggingBuilder, LogCollector\) Method

Registers [collector](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).collector 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.collector') as a Compono logging provider on
[builder](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).builder 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.builder')\. Every host logger's entries are then appended to the collector
\(subject to the host's own filter rules\) and inspectable via
[GetCapturedEntries\(\)](Compono.Logging.LogCollector.GetCapturedEntries().md 'Compono\.Logging\.LogCollector\.GetCapturedEntries\(\)')/[Verify\(\)](Compono.Logging.LogCollector.Verify().md 'Compono\.Logging\.LogCollector\.Verify\(\)')\. This never
changes filter rules\. Registering the same collector twice is idempotent \(one provider, no
duplicate entries\); registering distinct collectors adds one provider per collector, and each
receives every entry\. Returns [builder](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).builder 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.builder') for chaining\.

```csharp
public static Microsoft.Extensions.Logging.ILoggingBuilder AddComponoCapture(this Microsoft.Extensions.Logging.ILoggingBuilder builder, Compono.Logging.LogCollector collector);
```
#### Parameters

<a name='Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).builder'></a>

`builder` [Microsoft\.Extensions\.Logging\.ILoggingBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.iloggingbuilder 'Microsoft\.Extensions\.Logging\.ILoggingBuilder')

<a name='Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).collector'></a>

`collector` [LogCollector](Compono.Logging.LogCollector.md 'Compono\.Logging\.LogCollector')

#### Returns
[Microsoft\.Extensions\.Logging\.ILoggingBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.iloggingbuilder 'Microsoft\.Extensions\.Logging\.ILoggingBuilder')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[builder](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).builder 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.builder') or
            [collector](Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).md#Compono.Logging.ComponoLoggingBuilderExtensions.AddComponoCapture(thisMicrosoft.Extensions.Logging.ILoggingBuilder,Compono.Logging.LogCollector).collector 'Compono\.Logging\.ComponoLoggingBuilderExtensions\.AddComponoCapture\(this Microsoft\.Extensions\.Logging\.ILoggingBuilder, Compono\.Logging\.LogCollector\)\.collector') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.