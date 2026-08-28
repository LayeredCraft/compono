#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## CapturingLogger\<T\> Class

The generic counterpart to [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger') \- implemented once, works for every
closed [Microsoft\.Extensions\.Logging\.ILogger&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1'), no per\-[T](Compono.Logging.CapturingLogger_T_.md#Compono.Logging.CapturingLogger_T_.T 'Compono\.Logging\.CapturingLogger\<T\>\.T') generation needed
for its own behavior\. Composes an internal `Compono.Logging.LogEntryCollector` directly rather than
containing or delegating to a [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger') instance \(composition over
inheritance, deliberately deviating from `LayeredCraft.StructuredLogging`'s
`TestLogger<T> : TestLogger` shape\)\. Directly, publicly constructible \- composing
through [UseLogging\(this CompositionBuilder, Action&lt;LoggingOptions&gt;\)](Compono.Logging.CompositionBuilderExtensions.UseLogging(thisCompono.CompositionBuilder,System.Action_Compono.Logging.LoggingOptions_).md 'Compono\.Logging\.CompositionBuilderExtensions\.UseLogging\(this Compono\.CompositionBuilder, System\.Action\<Compono\.Logging\.LoggingOptions\>\)') is not required, and
[LoggingFactoryRegistry](Compono.Logging.LoggingFactoryRegistry.md 'Compono\.Logging\.LoggingFactoryRegistry')'s generated activators call this exact same
public constructor rather than a separate internal\-only path\. See
docs/adr/0055\-compono\-logging\-testing\-support\-package\.md\.

```csharp
public sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>, Microsoft.Extensions.Logging.ILogger
```
#### Type parameters

<a name='Compono.Logging.CapturingLogger_T_.T'></a>

`T`

The logger's category type\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CapturingLogger\<T\>

Implements [Microsoft\.Extensions\.Logging\.ILogger&lt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1')[T](Compono.Logging.CapturingLogger_T_.md#Compono.Logging.CapturingLogger_T_.T 'Compono\.Logging\.CapturingLogger\<T\>\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1'), [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')

| Constructors | |
| :--- | :--- |
| [CapturingLogger\(LoggingOptions\)](Compono.Logging.CapturingLogger_T_.CapturingLogger(Compono.Logging.LoggingOptions).md 'Compono\.Logging\.CapturingLogger\<T\>\.CapturingLogger\(Compono\.Logging\.LoggingOptions\)') | |

| Methods | |
| :--- | :--- |
| [BeginScope&lt;TState&gt;\(TState\)](Compono.Logging.CapturingLogger_T_.BeginScope_TState_(TState).md 'Compono\.Logging\.CapturingLogger\<T\>\.BeginScope\<TState\>\(TState\)') | Begins a logical operation scope\. |
| [IsEnabled\(LogLevel\)](Compono.Logging.CapturingLogger_T_.IsEnabled(Microsoft.Extensions.Logging.LogLevel).md 'Compono\.Logging\.CapturingLogger\<T\>\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)') | Checks if the given [logLevel](Compono.Logging.CapturingLogger_T_.IsEnabled(Microsoft.Extensions.Logging.LogLevel).md#Compono.Logging.CapturingLogger_T_.IsEnabled(Microsoft.Extensions.Logging.LogLevel).logLevel 'Compono\.Logging\.CapturingLogger\<T\>\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)\.logLevel') is enabled\. |
| [Log&lt;TState&gt;\(LogLevel, EventId, TState, Exception, Func&lt;TState,Exception,string&gt;\)](Compono.Logging.CapturingLogger_T_.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).md 'Compono\.Logging\.CapturingLogger\<T\>\.Log\<TState\>\(Microsoft\.Extensions\.Logging\.LogLevel, Microsoft\.Extensions\.Logging\.EventId, TState, System\.Exception, System\.Func\<TState,System\.Exception,string\>\)') | Writes a log entry\. |
