#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler')

## TestHttpHandler\.CreateClient\(Uri\) Method

Creates an [System\.Net\.Http\.HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient 'System\.Net\.Http\.HttpClient') over this handler with `disposeHandler: false` \-
the caller owns and disposes the returned client; this handler's own lifetime is
independent \(ADR\-0051 "Lifecycle, disposal, concurrency"\)\. May be called more than once to
produce several independent clients sharing this handler and its request log\.

```csharp
public System.Net.Http.HttpClient CreateClient(System.Uri? baseAddress=null);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.CreateClient(System.Uri).baseAddress'></a>

`baseAddress` [System\.Uri](https://learn.microsoft.com/en-us/dotnet/api/system.uri 'System\.Uri')

#### Returns
[System\.Net\.Http\.HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient 'System\.Net\.Http\.HttpClient')