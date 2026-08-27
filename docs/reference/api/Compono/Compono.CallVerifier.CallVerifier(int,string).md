#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CallVerifier](Compono.CallVerifier.md 'Compono\.CallVerifier')

## CallVerifier\(int, string\) Constructor

Performs terminal call\-count assertions after any generated member and argument filtering has
already been applied\. Deliberately minimal \- [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)')/[Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)')/
[Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') only, with no call\-order verification, per ADR\-0044 Requirement 3\.

```csharp
public CallVerifier(int observedCount, string memberDescription);
```
#### Parameters

<a name='Compono.CallVerifier.CallVerifier(int,string).observedCount'></a>

`observedCount` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

How many matching calls the generated verification surface observed\.

<a name='Compono.CallVerifier.CallVerifier(int,string).memberDescription'></a>

`memberDescription` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The declaring interface's display name plus member name, used to describe a verification failure\.