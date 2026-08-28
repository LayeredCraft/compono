#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## LogVerificationBuilder Class

A fluent filter chain over a logger's captured entries, ending in a thin, one\-line forwarder to
core [CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') \- [Once\(\)](Compono.Logging.LogVerificationBuilder.Once().md 'Compono\.Logging\.LogVerificationBuilder\.Once\(\)')/[Never\(\)](Compono.Logging.LogVerificationBuilder.Never().md 'Compono\.Logging\.LogVerificationBuilder\.Never\(\)')/[Exactly\(int\)](Compono.Logging.LogVerificationBuilder.Exactly(int).md 'Compono\.Logging\.LogVerificationBuilder\.Exactly\(int\)') each
build a [CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') from the filtered match count right here and call the
corresponding member\. [CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') itself is never part of this type's public
surface, and no new counting/`Times` abstraction is introduced\. See
docs/adr/0055\-compono\-logging\-testing\-support\-package\.md §7/§11/§12\.

```csharp
public sealed class LogVerificationBuilder
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → LogVerificationBuilder

| Methods | |
| :--- | :--- |
| [AtLevel\(LogLevel\)](Compono.Logging.LogVerificationBuilder.AtLevel(Microsoft.Extensions.Logging.LogLevel).md 'Compono\.Logging\.LogVerificationBuilder\.AtLevel\(Microsoft\.Extensions\.Logging\.LogLevel\)') | Restricts matches to entries logged at exactly [level](Compono.Logging.LogVerificationBuilder.AtLevel(Microsoft.Extensions.Logging.LogLevel).md#Compono.Logging.LogVerificationBuilder.AtLevel(Microsoft.Extensions.Logging.LogLevel).level 'Compono\.Logging\.LogVerificationBuilder\.AtLevel\(Microsoft\.Extensions\.Logging\.LogLevel\)\.level')\. |
| [Exactly\(int\)](Compono.Logging.LogVerificationBuilder.Exactly(int).md 'Compono\.Logging\.LogVerificationBuilder\.Exactly\(int\)') | Asserts the accumulated filters matched exactly [times](Compono.Logging.LogVerificationBuilder.Exactly(int).md#Compono.Logging.LogVerificationBuilder.Exactly(int).times 'Compono\.Logging\.LogVerificationBuilder\.Exactly\(int\)\.times') times\. |
| [Matching\(Func&lt;CapturedLogEntry,bool&gt;\)](Compono.Logging.LogVerificationBuilder.Matching(System.Func_Compono.Logging.CapturedLogEntry,bool_).md 'Compono\.Logging\.LogVerificationBuilder\.Matching\(System\.Func\<Compono\.Logging\.CapturedLogEntry,bool\>\)') | Restricts matches to entries satisfying an arbitrary [predicate](Compono.Logging.LogVerificationBuilder.Matching(System.Func_Compono.Logging.CapturedLogEntry,bool_).md#Compono.Logging.LogVerificationBuilder.Matching(System.Func_Compono.Logging.CapturedLogEntry,bool_).predicate 'Compono\.Logging\.LogVerificationBuilder\.Matching\(System\.Func\<Compono\.Logging\.CapturedLogEntry,bool\>\)\.predicate') \-             the escape hatch for anything the named filters above don't cover\. |
| [Never\(\)](Compono.Logging.LogVerificationBuilder.Never().md 'Compono\.Logging\.LogVerificationBuilder\.Never\(\)') | Asserts the accumulated filters never matched\. |
| [Once\(\)](Compono.Logging.LogVerificationBuilder.Once().md 'Compono\.Logging\.LogVerificationBuilder\.Once\(\)') | Asserts the accumulated filters matched exactly once\. |
| [WithEventId\(EventId\)](Compono.Logging.LogVerificationBuilder.WithEventId(Microsoft.Extensions.Logging.EventId).md 'Compono\.Logging\.LogVerificationBuilder\.WithEventId\(Microsoft\.Extensions\.Logging\.EventId\)') | Restricts matches to entries logged with exactly [eventId](Compono.Logging.LogVerificationBuilder.WithEventId(Microsoft.Extensions.Logging.EventId).md#Compono.Logging.LogVerificationBuilder.WithEventId(Microsoft.Extensions.Logging.EventId).eventId 'Compono\.Logging\.LogVerificationBuilder\.WithEventId\(Microsoft\.Extensions\.Logging\.EventId\)\.eventId')\. |
| [WithException&lt;TException&gt;\(\)](Compono.Logging.LogVerificationBuilder.WithException_TException_().md 'Compono\.Logging\.LogVerificationBuilder\.WithException\<TException\>\(\)') | Restricts matches to entries whose exception is a [TException](Compono.Logging.LogVerificationBuilder.WithException_TException_().md#Compono.Logging.LogVerificationBuilder.WithException_TException_().TException 'Compono\.Logging\.LogVerificationBuilder\.WithException\<TException\>\(\)\.TException')\. |
| [WithMessageContaining\(string\)](Compono.Logging.LogVerificationBuilder.WithMessageContaining(string).md 'Compono\.Logging\.LogVerificationBuilder\.WithMessageContaining\(string\)') | Restricts matches to entries whose formatted message contains [text](Compono.Logging.LogVerificationBuilder.WithMessageContaining(string).md#Compono.Logging.LogVerificationBuilder.WithMessageContaining(string).text 'Compono\.Logging\.LogVerificationBuilder\.WithMessageContaining\(string\)\.text')             \(ordinal comparison\)\. |
