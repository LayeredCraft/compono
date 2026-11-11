#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogCollector](Compono.Logging.LogCollector.md 'Compono\.Logging\.LogCollector')

## LogCollector\.GetCapturedEntries\(\) Method

Every entry captured so far, oldest first \- a snapshot, safe to read while logging
            continues on other threads\.

```csharp
public System.Collections.Generic.IReadOnlyList<Compono.Logging.CapturedLogEntry> GetCapturedEntries();
```

#### Returns
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')