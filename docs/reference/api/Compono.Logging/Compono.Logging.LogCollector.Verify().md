#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogCollector](Compono.Logging.LogCollector.md 'Compono\.Logging\.LogCollector')

## LogCollector\.Verify\(\) Method

The entry point for fluent verification over this collector's entries, e\.g\.
`logs.Verify().AtLevelOrAbove(LogLevel.Error).Never()`\.

```csharp
public Compono.Logging.LogVerificationBuilder Verify();
```

#### Returns
[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')