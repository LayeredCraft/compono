#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.Throws\(Exception\) Method

Configures this registration to throw [exception](Compono.Http.HttpResponseRegistrationBuilder.Throws(System.Exception).md#Compono.Http.HttpResponseRegistrationBuilder.Throws(System.Exception).exception 'Compono\.Http\.HttpResponseRegistrationBuilder\.Throws\(System\.Exception\)\.exception') instead of returning a
response \- the exact same instance is rethrown on every matched invocation \(verified: an
[System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') carries no disposal semantics comparable to
[System\.Net\.Http\.HttpContent](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcontent 'System\.Net\.Http\.HttpContent')'s, so there is no freshness requirement analogous to
`Respond*`'s \- see ADR\-0051 "Response state: factory, not instance"\)\. No exception
factory/callback overload \- reusing the same instance is the entire v1 behavior\.

```csharp
public Compono.Http.HttpResponseRegistration Throws(System.Exception exception);
```
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.Throws(System.Exception).exception'></a>

`exception` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')

#### Returns
[HttpResponseRegistration](Compono.Http.HttpResponseRegistration.md 'Compono\.Http\.HttpResponseRegistration')