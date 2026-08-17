#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CallVerifier Struct

Asserts how many times a generated test double's member was called, backed by
[ConfiguredCallCount](Compono.ReturnConfig_T_.ConfiguredCallCount.md 'Compono\.ReturnConfig\<T\>\.ConfiguredCallCount')\. Deliberately minimal \- [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)')/
[Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)')/[Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') only, no argument matchers, no call\-order verification,
per ADR\-0044 Requirement 3\.

```csharp
public readonly struct CallVerifier
```

| Constructors | |
| :--- | :--- |
| [CallVerifier\(int, string\)](Compono.CallVerifier.CallVerifier(int,string).md 'Compono\.CallVerifier\.CallVerifier\(int, string\)') | Asserts how many times a generated test double's member was called, backed by [ConfiguredCallCount](Compono.ReturnConfig_T_.ConfiguredCallCount.md 'Compono\.ReturnConfig\<T\>\.ConfiguredCallCount')\. Deliberately minimal \- [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)')/ [Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)')/[Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') only, no argument matchers, no call\-order verification, per ADR\-0044 Requirement 3\. |

| Methods | |
| :--- | :--- |
| [Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') | Asserts the member was called exactly [times](Compono.CallVerifier.Exactly(int).md#Compono.CallVerifier.Exactly(int).times 'Compono\.CallVerifier\.Exactly\(int\)\.times') times\. |
| [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)') | Asserts the member was never called\. |
| [Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)') | Asserts the member was called exactly once\. |
