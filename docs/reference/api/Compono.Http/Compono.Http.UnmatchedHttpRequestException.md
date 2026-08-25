#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http')

## UnmatchedHttpRequestException Class

Thrown by [TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler') when a request reaches `SendAsync` and no
configured [HttpResponseRegistration](Compono.Http.HttpResponseRegistration.md 'Compono\.Http\.HttpResponseRegistration') matches it\. [TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler') is
strict by default \- see ADR\-0051 "Unmatched requests: strict by default" \- so this never
silently falls back to a fabricated response; a consumer that wants a fallback configures one
explicitly via `handler.When(_ => true)`\.

```csharp
public sealed class UnmatchedHttpRequestException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → UnmatchedHttpRequestException

| Constructors | |
| :--- | :--- |
| [UnmatchedHttpRequestException\(HttpMethod, Uri\)](Compono.Http.UnmatchedHttpRequestException.UnmatchedHttpRequestException(System.Net.Http.HttpMethod,System.Uri).md 'Compono\.Http\.UnmatchedHttpRequestException\.UnmatchedHttpRequestException\(System\.Net\.Http\.HttpMethod, System\.Uri\)') | Creates an exception describing the unmatched request's method and URI\. |
