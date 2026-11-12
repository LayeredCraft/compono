#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.NotInCategoryPrefix\(string\) Method

Restricts matches to entries whose [Category](Compono.Logging.CapturedLogEntry.Category.md 'Compono\.Logging\.CapturedLogEntry\.Category') does \<b\>not\</b\> start
with [prefix](Compono.Logging.LogVerificationBuilder.NotInCategoryPrefix(string).md#Compono.Logging.LogVerificationBuilder.NotInCategoryPrefix(string).prefix 'Compono\.Logging\.LogVerificationBuilder\.NotInCategoryPrefix\(string\)\.prefix') \(ordinal, case\-sensitive\) \- for host\-wide guards such as "nothing
at [Microsoft\.Extensions\.Logging\.LogLevel\.Error](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel.error 'Microsoft\.Extensions\.Logging\.LogLevel\.Error') outside `MyApp.Jobs.`"\. The prefix is literal, exactly as for
[InCategoryPrefix\(string\)](Compono.Logging.LogVerificationBuilder.InCategoryPrefix(string).md 'Compono\.Logging\.LogVerificationBuilder\.InCategoryPrefix\(string\)'); an empty prefix matches no entry\.

```csharp
public Compono.Logging.LogVerificationBuilder NotInCategoryPrefix(string prefix);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.NotInCategoryPrefix(string).prefix'></a>

`prefix` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[prefix](Compono.Logging.LogVerificationBuilder.NotInCategoryPrefix(string).md#Compono.Logging.LogVerificationBuilder.NotInCategoryPrefix(string).prefix 'Compono\.Logging\.LogVerificationBuilder\.NotInCategoryPrefix\(string\)\.prefix') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.