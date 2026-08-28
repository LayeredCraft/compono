#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## CapturingLogger Class

A hand\-written, reflection\-free [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger') that captures every logged entry into an
inspectable, thread\-safe, Compono\-native model \([CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')\) \- real scope
tracking via [Microsoft\.Extensions\.Logging\.LoggerExternalScopeProvider](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loggerexternalscopeprovider 'Microsoft\.Extensions\.Logging\.LoggerExternalScopeProvider'), structured\-property extraction, and
genuine [MinimumLevel](Compono.Logging.LoggingOptions.MinimumLevel.md 'Compono\.Logging\.LoggingOptions\.MinimumLevel') filtering\. Directly, publicly constructible \-
composing through [UseLogging\(this CompositionBuilder, Action&lt;LoggingOptions&gt;\)](Compono.Logging.CompositionBuilderExtensions.UseLogging(thisCompono.CompositionBuilder,System.Action_Compono.Logging.LoggingOptions_).md 'Compono\.Logging\.CompositionBuilderExtensions\.UseLogging\(this Compono\.CompositionBuilder, System\.Action\<Compono\.Logging\.LoggingOptions\>\)') is not required\. See
docs/adr/0055\-compono\-logging\-testing\-support\-package\.md\.

```csharp
public sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CapturingLogger

Implements [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')

| Constructors | |
| :--- | :--- |
| [CapturingLogger\(LoggingOptions\)](Compono.Logging.CapturingLogger.CapturingLogger(Compono.Logging.LoggingOptions).md 'Compono\.Logging\.CapturingLogger\.CapturingLogger\(Compono\.Logging\.LoggingOptions\)') | Creates a standalone [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger'), usable with no Compono composition involved at all \- the same "no factory needed for the common case" ergonomics `Microsoft.Extensions.Diagnostics.Testing`'s `FakeLogger<T>` already established as prior art \(RESEARCH\-0013 §3\)\. |

| Methods | |
| :--- | :--- |
| [BeginScope&lt;TState&gt;\(TState\)](Compono.Logging.CapturingLogger.BeginScope_TState_(TState).md 'Compono\.Logging\.CapturingLogger\.BeginScope\<TState\>\(TState\)') | Begins a logical operation scope\. |
| [IsEnabled\(LogLevel\)](Compono.Logging.CapturingLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel).md 'Compono\.Logging\.CapturingLogger\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)') | Checks if the given [logLevel](Compono.Logging.CapturingLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel).md#Compono.Logging.CapturingLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel).logLevel 'Compono\.Logging\.CapturingLogger\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)\.logLevel') is enabled\. |
| [Log&lt;TState&gt;\(LogLevel, EventId, TState, Exception, Func&lt;TState,Exception,string&gt;\)](Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).md 'Compono\.Logging\.CapturingLogger\.Log\<TState\>\(Microsoft\.Extensions\.Logging\.LogLevel, Microsoft\.Extensions\.Logging\.EventId, TState, System\.Exception, System\.Func\<TState,System\.Exception,string\>\)') | Writes a log entry\. |
