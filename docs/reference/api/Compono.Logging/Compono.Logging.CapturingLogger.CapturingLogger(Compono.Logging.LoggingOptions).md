#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')

## CapturingLogger\(LoggingOptions\) Constructor

Creates a standalone [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger'), usable with no Compono composition
involved at all \- the same "no factory needed for the common case" ergonomics
`Microsoft.Extensions.Diagnostics.Testing`'s `FakeLogger<T>` already
established as prior art \(RESEARCH\-0013 §3\)\.

```csharp
public CapturingLogger(Compono.Logging.LoggingOptions? options=null);
```
#### Parameters

<a name='Compono.Logging.CapturingLogger.CapturingLogger(Compono.Logging.LoggingOptions).options'></a>

`options` [LoggingOptions](Compono.Logging.LoggingOptions.md 'Compono\.Logging\.LoggingOptions')

Configuration, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for the default
            \([Microsoft\.Extensions\.Logging\.LogLevel\.Trace](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.trace 'Microsoft\.Extensions\.Logging\.LogLevel\.Trace') minimum level\)\.