#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.InCategoryPrefix\(string\) Method

Restricts matches to entries whose [Category](Compono.Logging.CapturedLogEntry.Category.md 'Compono\.Logging\.CapturedLogEntry\.Category') starts with
[prefix](Compono.Logging.LogVerificationBuilder.InCategoryPrefix(string).md#Compono.Logging.LogVerificationBuilder.InCategoryPrefix(string).prefix 'Compono\.Logging\.LogVerificationBuilder\.InCategoryPrefix\(string\)\.prefix') \(ordinal, case\-sensitive\)\. The prefix is literal \- no namespace
separator is implied \- so `InCategoryPrefix("MyApp.Jobs")` also matches
`MyApp.JobsLegacy`; pass a trailing dot \(`"MyApp.Jobs."`\) to match only categories
beneath that namespace\. An empty prefix matches every entry\.

```csharp
public Compono.Logging.LogVerificationBuilder InCategoryPrefix(string prefix);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.InCategoryPrefix(string).prefix'></a>

`prefix` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[prefix](Compono.Logging.LogVerificationBuilder.InCategoryPrefix(string).md#Compono.Logging.LogVerificationBuilder.InCategoryPrefix(string).prefix 'Compono\.Logging\.LogVerificationBuilder\.InCategoryPrefix\(string\)\.prefix') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.