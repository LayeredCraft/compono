#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CallVerifier Struct

Performs terminal call\-count assertions after any generated member and argument filtering has
already been applied\. Deliberately minimal \- [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)')/[Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)')/
[Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') only, with no call\-order verification, per ADR\-0044 Requirement 3\.

```csharp
public readonly struct CallVerifier
```

| Constructors | |
| :--- | :--- |
| [CallVerifier\(int, string\)](Compono.CallVerifier.CallVerifier(int,string).md 'Compono\.CallVerifier\.CallVerifier\(int, string\)') | Performs terminal call\-count assertions after any generated member and argument filtering has already been applied\. Deliberately minimal \- [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)')/[Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)')/ [Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') only, with no call\-order verification, per ADR\-0044 Requirement 3\. |

| Methods | |
| :--- | :--- |
| [Exactly\(int\)](Compono.CallVerifier.Exactly(int).md 'Compono\.CallVerifier\.Exactly\(int\)') | Asserts the member was called exactly [times](Compono.CallVerifier.Exactly(int).md#Compono.CallVerifier.Exactly(int).times 'Compono\.CallVerifier\.Exactly\(int\)\.times') times\. |
| [Never\(\)](Compono.CallVerifier.Never().md 'Compono\.CallVerifier\.Never\(\)') | Asserts the member was never called\. |
| [Once\(\)](Compono.CallVerifier.Once().md 'Compono\.CallVerifier\.Once\(\)') | Asserts the member was called exactly once\. |
