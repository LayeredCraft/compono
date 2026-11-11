#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')

## CapturedLogEntry\.Category Property

The logger's category \- the exact string passed to
[Microsoft\.Extensions\.Logging\.ILoggerProvider\.CreateLogger\(System\.String\)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.iloggerprovider.createlogger#microsoft-extensions-logging-iloggerprovider-createlogger(system-string) 'Microsoft\.Extensions\.Logging\.ILoggerProvider\.CreateLogger\(System\.String\)') for a host\-captured entry, or the
`LoggerFactory.CreateLogger<T>()`\-equivalent name for a composed/directly
constructed [CapturingLogger&lt;T&gt;](Compono.Logging.CapturingLogger_T_.md 'Compono\.Logging\.CapturingLogger\<T\>')\. The non\-generic [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')
\(and a bare [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')\) uses the empty string\. This is part of the record's
value equality \(ADR\-0071's one intentional behavioral compatibility change\)\.

```csharp
public string Category { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')