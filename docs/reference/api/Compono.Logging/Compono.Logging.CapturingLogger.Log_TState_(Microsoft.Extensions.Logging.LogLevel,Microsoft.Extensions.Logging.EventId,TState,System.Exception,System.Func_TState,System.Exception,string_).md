#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')

## CapturingLogger\.Log\<TState\>\(LogLevel, EventId, TState, Exception, Func\<TState,Exception,string\>\) Method

Writes a log entry\.

```csharp
public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, System.Exception? exception, System.Func<TState,System.Exception?,string> formatter);
```
#### Type parameters

<a name='Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).TState'></a>

`TState`

The type of the object to be written\.
#### Parameters

<a name='Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).logLevel'></a>

`logLevel` [Microsoft\.Extensions\.Logging\.LogLevel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel 'Microsoft\.Extensions\.Logging\.LogLevel')

Entry will be written on this level\.

<a name='Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).eventId'></a>

`eventId` [Microsoft\.Extensions\.Logging\.EventId](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.eventid 'Microsoft\.Extensions\.Logging\.EventId')

Id of the event\.

<a name='Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).state'></a>

`state` [TState](Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).md#Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).TState 'Compono\.Logging\.CapturingLogger\.Log\<TState\>\(Microsoft\.Extensions\.Logging\.LogLevel, Microsoft\.Extensions\.Logging\.EventId, TState, System\.Exception, System\.Func\<TState,System\.Exception,string\>\)\.TState')

The entry to be written\. Can be also an object\.

<a name='Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).exception'></a>

`exception` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')

The exception related to this entry\.

<a name='Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).formatter'></a>

`formatter` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[TState](Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).md#Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).TState 'Compono\.Logging\.CapturingLogger\.Log\<TState\>\(Microsoft\.Extensions\.Logging\.LogLevel, Microsoft\.Extensions\.Logging\.EventId, TState, System\.Exception, System\.Func\<TState,System\.Exception,string\>\)\.TState')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')

Function to create a [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String') message of the [state](Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).md#Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).state 'Compono\.Logging\.CapturingLogger\.Log\<TState\>\(Microsoft\.Extensions\.Logging\.LogLevel, Microsoft\.Extensions\.Logging\.EventId, TState, System\.Exception, System\.Func\<TState,System\.Exception,string\>\)\.state') and [exception](Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).md#Compono.Logging.CapturingLogger.Log_TState_(Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,TState,System.Exception,System.Func_TState,System.Exception,string_).exception 'Compono\.Logging\.CapturingLogger\.Log\<TState\>\(Microsoft\.Extensions\.Logging\.LogLevel, Microsoft\.Extensions\.Logging\.EventId, TState, System\.Exception, System\.Func\<TState,System\.Exception,string\>\)\.exception')\.

Implements [Log&lt;TState&gt;\(LogLevel, EventId, TState, Exception, Func&lt;TState,Exception,string&gt;\)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger.log--1#microsoft-extensions-logging-ilogger-log--1(microsoft-extensions-logging-loglevel-microsoft-extensions-logging-eventid---0-system-exception-system-func{--0-system-exception-system-string}) 'Microsoft\.Extensions\.Logging\.ILogger\.Log\`\`1\(Microsoft\.Extensions\.Logging\.LogLevel,Microsoft\.Extensions\.Logging\.EventId,\`\`0,System\.Exception,System\.Func\{\`\`0,System\.Exception,System\.String\}\)')