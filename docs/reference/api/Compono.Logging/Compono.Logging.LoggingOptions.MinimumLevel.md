#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LoggingOptions](Compono.Logging.LoggingOptions.md 'Compono\.Logging\.LoggingOptions')

## LoggingOptions\.MinimumLevel Property

The minimum [Microsoft\.Extensions\.Logging\.LogLevel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel 'Microsoft\.Extensions\.Logging\.LogLevel') a captor records\. Real filtering, not merely an
[Microsoft\.Extensions\.Logging\.ILogger\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger.isenabled#microsoft-extensions-logging-ilogger-isenabled(microsoft-extensions-logging-loglevel) 'Microsoft\.Extensions\.Logging\.ILogger\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)') opinion layered on top of an otherwise\-complete capture
stream: an entry below this level is never captured\. [Microsoft\.Extensions\.Logging\.LogLevel\.None](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.none 'Microsoft\.Extensions\.Logging\.LogLevel\.None') disables
all logging entirely, and is itself never an enabled/capturable level regardless of this
value \- see the "MinimumLevel semantics" section of ADR\-0055's amendment for the exact rule\.
Defaults to [Microsoft\.Extensions\.Logging\.LogLevel\.Trace](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.trace 'Microsoft\.Extensions\.Logging\.LogLevel\.Trace') \(every ordinary level captured\)\.

```csharp
public Microsoft.Extensions.Logging.LogLevel MinimumLevel { get; set; }
```

#### Property Value
[Microsoft\.Extensions\.Logging\.LogLevel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel 'Microsoft\.Extensions\.Logging\.LogLevel')