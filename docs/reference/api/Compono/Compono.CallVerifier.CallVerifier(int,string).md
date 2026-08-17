#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CallVerifier](Compono.CallVerifier.md 'Compono\.CallVerifier')

## CallVerifier\(int, string\) Constructor

Asserts how many times a generated test double's member was called, backed by
[ConfiguredCallCount](Compono.ReturnConfig_T_.ConfiguredCallCount.md 'Compono\.ReturnConfig\<T\>\.ConfiguredCallCount')\. Deliberately minimal \- [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)')/
[Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)')/[Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') only, no argument matchers, no call\-order verification,
per ADR\-0044 Requirement 3\.

```csharp
public CallVerifier(int observedCount, string memberDescription);
```
#### Parameters

<a name='Compono.CallVerifier.CallVerifier(int,string).observedCount'></a>

`observedCount` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

How many times the member's dispatch body actually ran\.

<a name='Compono.CallVerifier.CallVerifier(int,string).memberDescription'></a>

`memberDescription` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The declaring interface's display name plus member name, used to describe a verification failure\.