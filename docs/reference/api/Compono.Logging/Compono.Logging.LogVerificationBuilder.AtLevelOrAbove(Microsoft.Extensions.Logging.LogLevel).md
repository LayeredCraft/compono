#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.AtLevelOrAbove\(LogLevel\) Method

Restricts matches to entries logged at [level](Compono.Logging.LogVerificationBuilder.AtLevelOrAbove(Microsoft.Extensions.Logging.LogLevel).md#Compono.Logging.LogVerificationBuilder.AtLevelOrAbove(Microsoft.Extensions.Logging.LogLevel).level 'Compono\.Logging\.LogVerificationBuilder\.AtLevelOrAbove\(Microsoft\.Extensions\.Logging\.LogLevel\)\.level') or any more severe level
\(e\.g\. `AtLevelOrAbove(LogLevel.Error)` matches Error and Critical\)\. A captured entry is
never [Microsoft\.Extensions\.Logging\.LogLevel\.None](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.none 'Microsoft\.Extensions\.Logging\.LogLevel\.None'), and neither is [Microsoft\.Extensions\.Logging\.LogLevel\.None](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.none 'Microsoft\.Extensions\.Logging\.LogLevel\.None') ever treated as
"above" anything, so `AtLevelOrAbove(LogLevel.None)` matches nothing\.

```csharp
public Compono.Logging.LogVerificationBuilder AtLevelOrAbove(Microsoft.Extensions.Logging.LogLevel level);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.AtLevelOrAbove(Microsoft.Extensions.Logging.LogLevel).level'></a>

`level` [Microsoft\.Extensions\.Logging\.LogLevel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel 'Microsoft\.Extensions\.Logging\.LogLevel')

#### Returns
[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')