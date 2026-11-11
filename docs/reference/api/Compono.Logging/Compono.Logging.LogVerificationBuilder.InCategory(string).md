#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

## LogVerificationBuilder\.InCategory\(string\) Method

Restricts matches to entries whose [Category](Compono.Logging.CapturedLogEntry.Category.md 'Compono\.Logging\.CapturedLogEntry\.Category') is exactly
[category](Compono.Logging.LogVerificationBuilder.InCategory(string).md#Compono.Logging.LogVerificationBuilder.InCategory(string).category 'Compono\.Logging\.LogVerificationBuilder\.InCategory\(string\)\.category') \(ordinal, case\-sensitive \- use [Matching\(Func&lt;CapturedLogEntry,bool&gt;\)](Compono.Logging.LogVerificationBuilder.Matching(System.Func_Compono.Logging.CapturedLogEntry,bool_).md 'Compono\.Logging\.LogVerificationBuilder\.Matching\(System\.Func\<Compono\.Logging\.CapturedLogEntry,bool\>\)') for a prefix
or wildcard match\)\.

```csharp
public Compono.Logging.LogVerificationBuilder InCategory(string category);
```
#### Parameters

<a name='Compono.Logging.LogVerificationBuilder.InCategory(string).category'></a>

`category` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[category](Compono.Logging.LogVerificationBuilder.InCategory(string).md#Compono.Logging.LogVerificationBuilder.InCategory(string).category 'Compono\.Logging\.LogVerificationBuilder\.InCategory\(string\)\.category') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.