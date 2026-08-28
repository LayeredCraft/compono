#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## LoggingOptions Class

Configuration for [UseLogging\(this CompositionBuilder, Action&lt;LoggingOptions&gt;\)](Compono.Logging.CompositionBuilderExtensions.UseLogging(thisCompono.CompositionBuilder,System.Action_Compono.Logging.LoggingOptions_).md 'Compono\.Logging\.CompositionBuilderExtensions\.UseLogging\(this Compono\.CompositionBuilder, System\.Action\<Compono\.Logging\.LoggingOptions\>\)') and for directly
constructing a [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')/[CapturingLogger&lt;T&gt;](Compono.Logging.CapturingLogger_T_.md 'Compono\.Logging\.CapturingLogger\<T\>')\. Fixed at
construction time \- there is no runtime\-mutable equivalent, unlike
`LayeredCraft.StructuredLogging`'s `TestLogger.MinimumLogLevel` setter\. See
docs/adr/0055\-compono\-logging\-testing\-support\-package\.md\.

```csharp
public sealed class LoggingOptions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → LoggingOptions

| Properties | |
| :--- | :--- |
| [MinimumLevel](Compono.Logging.LoggingOptions.MinimumLevel.md 'Compono\.Logging\.LoggingOptions\.MinimumLevel') | The minimum [Microsoft\.Extensions\.Logging\.LogLevel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel 'Microsoft\.Extensions\.Logging\.LogLevel') a captor records\. Real filtering, not merely an [Microsoft\.Extensions\.Logging\.ILogger\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger.isenabled#microsoft-extensions-logging-ilogger-isenabled(microsoft-extensions-logging-loglevel) 'Microsoft\.Extensions\.Logging\.ILogger\.IsEnabled\(Microsoft\.Extensions\.Logging\.LogLevel\)') opinion layered on top of an otherwise\-complete capture stream: an entry below this level is never captured\. [Microsoft\.Extensions\.Logging\.LogLevel\.None](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.none 'Microsoft\.Extensions\.Logging\.LogLevel\.None') disables all logging entirely, and is itself never an enabled/capturable level regardless of this value \- see the "MinimumLevel semantics" section of ADR\-0055's amendment for the exact rule\. Defaults to [Microsoft\.Extensions\.Logging\.LogLevel\.Trace](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.trace 'Microsoft\.Extensions\.Logging\.LogLevel\.Trace') \(every ordinary level captured\)\. |
