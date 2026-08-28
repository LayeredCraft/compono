#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.Matching\(Func\<CapturedLogEntry,bool\>\) Method

Restricts matches to entries satisfying an arbitrary [predicate](Compono.Logging.LogVerificationBuilder.Matching(System.Func_Compono.Logging.CapturedLogEntry,bool_).md#Compono.Logging.LogVerificationBuilder.Matching(System.Func_Compono.Logging.CapturedLogEntry,bool_).predicate 'Compono\.Logging\.LogVerificationBuilder\.Matching\(System\.Func\<Compono\.Logging\.CapturedLogEntry,bool\>\)\.predicate') \-
            the escape hatch for anything the named filters above don't cover\.

```csharp
public Compono.Logging.LogVerificationBuilder Matching(System.Func<Compono.Logging.CapturedLogEntry,bool> predicate);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.Matching(System.Func_Compono.Logging.CapturedLogEntry,bool_).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

#### Returns
[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')