#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.RespondBytes\(byte\[\], string\) Method

Responds with HTTP 200 OK and [content](Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).md#Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).content 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondBytes\(byte\[\], string\)\.content') verbatim, as
[mediaType](Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).md#Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).mediaType 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondBytes\(byte\[\], string\)\.mediaType') \- the raw\-bytes counterpart to [RespondText\(string, string, Encoding\)](Compono.Http.HttpResponseRegistrationBuilder.RespondText(string,string,System.Text.Encoding).md 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondText\(string, string, System\.Text\.Encoding\)') for
binary payloads \(e\.g\. a fetched certificate's DER bytes\) that would otherwise need a lossy
or awkward text encoding to round\-trip through [RespondText\(string, string, Encoding\)](Compono.Http.HttpResponseRegistrationBuilder.RespondText(string,string,System.Text.Encoding).md 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondText\(string, string, System\.Text\.Encoding\)')\. Same
serialize\-once\-to\-bytes model as [RespondJson&lt;T&gt;\(T, JsonSerializerOptions\)](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions) 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)'):
[content](Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).md#Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).content 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondBytes\(byte\[\], string\)\.content') is defensively copied once at registration time \- not retained
by reference \- so a caller mutating or reusing its own array afterward can never change an
already\-registered response; every matched invocation constructs a fresh
[System\.Net\.Http\.ByteArrayContent](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.bytearraycontent 'System\.Net\.Http\.ByteArrayContent') over that private copy \(ADR\-0051 Amendment 2\)\.

```csharp
public Compono.Http.HttpResponseRegistration RespondBytes(byte[] content, string mediaType="application/octet-stream");
```
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).content'></a>

`content` [System\.Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte 'System\.Byte')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondBytes(byte[],string).mediaType'></a>

`mediaType` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[HttpResponseRegistration](Compono.Http.HttpResponseRegistration.md 'Compono\.Http\.HttpResponseRegistration')