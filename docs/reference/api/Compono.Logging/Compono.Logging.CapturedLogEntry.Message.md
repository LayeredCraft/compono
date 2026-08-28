#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')

## CapturedLogEntry\.Message Property

The pre\-formatted message, produced via the caller's own `formatter(state, exception)`
delegate \- never re\-derived by [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger'), so it can't diverge from what a
real logging provider would have produced\.

```csharp
public string Message { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')