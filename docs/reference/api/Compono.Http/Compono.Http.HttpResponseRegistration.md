#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http')

## HttpResponseRegistration Class

One `OnGet`/`OnPost`/\.\.\./`When` configuration on a [TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler') \-
the handle returned from configuring a response, and the verification identity for it\. Owns its
matcher, its response\-factory behavior, and its own matched\-call count \- see ADR\-0051
"Registration handle, not a re\-declared matcher" for why verification is a method on this type
rather than a second predicate declared inside `Verify(...)`\.

```csharp
public sealed class HttpResponseRegistration
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → HttpResponseRegistration

| Methods | |
| :--- | :--- |
| [Verify\(\)](Compono.Http.HttpResponseRegistration.Verify().md 'Compono\.Http\.HttpResponseRegistration\.Verify\(\)') | Asserts how many times this registration matched a request, reusing the core [CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') type unchanged \- `Never()`/`Once()`/ `Exactly(n)`\. Kept deliberately separate from [Requests](Compono.Http.TestHttpHandler.Requests.md 'Compono\.Http\.TestHttpHandler\.Requests'), which answers a different question \("what was actually sent"\) \- see ADR\-0051 "Kept separate: global request\-log inspection"\. |
