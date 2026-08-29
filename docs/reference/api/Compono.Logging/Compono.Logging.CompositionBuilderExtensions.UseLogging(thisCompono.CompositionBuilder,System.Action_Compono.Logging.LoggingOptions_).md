#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CompositionBuilderExtensions](Compono.Logging.CompositionBuilderExtensions.md 'Compono\.Logging\.CompositionBuilderExtensions')

## CompositionBuilderExtensions\.UseLogging\(this CompositionBuilder, Action\<LoggingOptions\>\) Method

Registers a stage\-6 test\-double provider \(`Compono.Logging.LoggingProvider`\) so a bare
[Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger') or any closed
[Microsoft\.Extensions\.Logging\.ILogger&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1') composes as a
[CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')/[CapturingLogger&lt;T&gt;](Compono.Logging.CapturingLogger_T_.md 'Compono\.Logging\.CapturingLogger\<T\>')\.

```csharp
public static Compono.CompositionBuilder UseLogging(this Compono.CompositionBuilder builder, System.Action<Compono.Logging.LoggingOptions>? configure=null);
```
#### Parameters

<a name='Compono.Logging.CompositionBuilderExtensions.UseLogging(thisCompono.CompositionBuilder,System.Action_Compono.Logging.LoggingOptions_).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

The builder to register into\.

<a name='Compono.Logging.CompositionBuilderExtensions.UseLogging(thisCompono.CompositionBuilder,System.Action_Compono.Logging.LoggingOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[LoggingOptions](Compono.Logging.LoggingOptions.md 'Compono\.Logging\.LoggingOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Optional configuration for the resulting captors' behavior\.

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

### Remarks
Register this \<b\>before\</b\>`UseNSubstitute()` if
[Microsoft\.Extensions\.Logging\.ILogger&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1') should resolve through
Compono\.Logging rather than an NSubstitute substitute \- Compono's stage\-6 providers resolve
in registration order \(first\-registered\-wins\), an existing, documented, `Accepted`
pattern \(ADR\-0024\) this package follows rather than replacing\. Registration order against
`UseGeneratedTestDoubles()` is \<b\>not\</b\> relevant here: when `ComponoGeneratedLogging`
is enabled \(the package default\), `ILogger`/`ILogger{TCategoryName}` are
Logging\-owned abstractions excluded from `Compono.TestDoubles` generation entirely, so
`UseGeneratedTestDoubles()` has no generated double to offer for these types regardless
of order \(ADR\-0055 Amendment 4\)\. See
docs/adr/0055\-compono\-logging\-testing\-support\-package\.md's "Runtime activation and
precedence" section\.