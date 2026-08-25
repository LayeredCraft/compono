#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.RespondText\(string, string, Encoding\) Method

Responds with HTTP 200 OK and a fresh [System\.Net\.Http\.StringContent](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.stringcontent 'System\.Net\.Http\.StringContent') per invocation \- the
string itself is already immutable, so no serialize\-once optimization is needed here
\(contrast [RespondJson&lt;T&gt;\(T, JsonSerializerOptions\)](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions) 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)'), whose body IS
serialized once \- see ADR\-0051 "Serialize\-once\-to\-bytes model"\)\.

```csharp
public Compono.Http.HttpResponseRegistration RespondText(string content, string mediaType="text/plain", System.Text.Encoding? encoding=null);
```
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondText(string,string,System.Text.Encoding).content'></a>

`content` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondText(string,string,System.Text.Encoding).mediaType'></a>

`mediaType` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondText(string,string,System.Text.Encoding).encoding'></a>

`encoding` [System\.Text\.Encoding](https://learn.microsoft.com/en-us/dotnet/api/system.text.encoding 'System\.Text\.Encoding')

#### Returns
[HttpResponseRegistration](Compono.Http.HttpResponseRegistration.md 'Compono\.Http\.HttpResponseRegistration')