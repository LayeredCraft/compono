#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## LoggerTestingExtensions Class

Direct inspection and fluent verification over any [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger') \- no assertion
framework required\. Every method here requires the [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger') it's called on to
actually be a [Compono\.Logging](Compono.Logging.md 'Compono\.Logging') capturing logger \(produced by
[UseLogging\(this CompositionBuilder, Action&lt;LoggingOptions&gt;\)](Compono.Logging.CompositionBuilderExtensions.UseLogging(thisCompono.CompositionBuilder,System.Action_Compono.Logging.LoggingOptions_).md 'Compono\.Logging\.CompositionBuilderExtensions\.UseLogging\(this Compono\.CompositionBuilder, System\.Action\<Compono\.Logging\.LoggingOptions\>\)'), or directly via
[CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')/[CapturingLogger&lt;T&gt;](Compono.Logging.CapturingLogger_T_.md 'Compono\.Logging\.CapturingLogger\<T\>')'s public constructors\) \- calling
any of these against [Microsoft\.Extensions\.Logging\.Abstractions\.NullLogger&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.abstractions.nulllogger-1 'Microsoft\.Extensions\.Logging\.Abstractions\.NullLogger\`1'), an
NSubstitute substitute, a `Compono.TestDoubles`\-generated double, or any other
non\-Compono\.Logging [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger') throws immediately, diagnostically, rather than
returning an empty/default result\. See docs/adr/0055\-compono\-logging\-testing\-support\-package\.md's
"Failure semantics for a non\-Compono\.Logging ILogger" section\.

```csharp
public static class LoggerTestingExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → LoggerTestingExtensions

| Methods | |
| :--- | :--- |
| [ClearCapturedEntries\(this ILogger\)](Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md 'Compono\.Logging\.LoggerTestingExtensions\.ClearCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)') | Discards every entry captured by [logger](Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.ClearCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.ClearCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') so far\. |
| [GetCapturedEntries\(this ILogger\)](Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md 'Compono\.Logging\.LoggerTestingExtensions\.GetCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)') | Every entry captured by [logger](Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.GetCapturedEntries(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.GetCapturedEntries\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') so far, oldest first\. |
| [GetLastCapturedEntry\(this ILogger\)](Compono.Logging.LoggerTestingExtensions.GetLastCapturedEntry(thisMicrosoft.Extensions.Logging.ILogger).md 'Compono\.Logging\.LoggerTestingExtensions\.GetLastCapturedEntry\(this Microsoft\.Extensions\.Logging\.ILogger\)') | The most recently captured entry, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if nothing has been             captured yet\. |
| [Verify\(this ILogger\)](Compono.Logging.LoggerTestingExtensions.Verify(thisMicrosoft.Extensions.Logging.ILogger).md 'Compono\.Logging\.LoggerTestingExtensions\.Verify\(this Microsoft\.Extensions\.Logging\.ILogger\)') | The entry point for fluent verification, e\.g\. `logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once()` \- matching the same single\-verb vocabulary `Compono.TestDoubles`/`Compono.Http` already established \(`repository.Verify().Save().Once()`, `registration.Verify().Once()`\), not a two\-verb `VerifyLog()...Verify()` shape\. |
