#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## CompositionBuilderExtensions Class

Registers [Compono\.Logging](Compono.Logging.md 'Compono\.Logging') support into a [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')\.

```csharp
public static class CompositionBuilderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionBuilderExtensions

| Methods | |
| :--- | :--- |
| [UseLogging\(this CompositionBuilder, Action&lt;LoggingOptions&gt;\)](Compono.Logging.CompositionBuilderExtensions.UseLogging(thisCompono.CompositionBuilder,System.Action_Compono.Logging.LoggingOptions_).md 'Compono\.Logging\.CompositionBuilderExtensions\.UseLogging\(this Compono\.CompositionBuilder, System\.Action\<Compono\.Logging\.LoggingOptions\>\)') | Registers a stage\-6 test\-double provider \(`Compono.Logging.LoggingProvider`\) so a bare [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger') or any closed [Microsoft\.Extensions\.Logging\.ILogger&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1') composes as a [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')/[CapturingLogger&lt;T&gt;](Compono.Logging.CapturingLogger_T_.md 'Compono\.Logging\.CapturingLogger\<T\>')\. |
