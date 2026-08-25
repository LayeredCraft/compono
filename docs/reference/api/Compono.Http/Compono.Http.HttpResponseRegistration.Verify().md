#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistration](Compono.Http.HttpResponseRegistration.md 'Compono\.Http\.HttpResponseRegistration')

## HttpResponseRegistration\.Verify\(\) Method

Asserts how many times this registration matched a request, reusing the core
[CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier') type unchanged \- `Never()`/`Once()`/
`Exactly(n)`\. Kept deliberately separate from [Requests](Compono.Http.TestHttpHandler.Requests.md 'Compono\.Http\.TestHttpHandler\.Requests'),
which answers a different question \("what was actually sent"\) \- see ADR\-0051 "Kept separate:
global request\-log inspection"\.

```csharp
public Compono.CallVerifier Verify();
```

#### Returns
[CallVerifier](../Compono/Compono.CallVerifier.md 'Compono\.CallVerifier')