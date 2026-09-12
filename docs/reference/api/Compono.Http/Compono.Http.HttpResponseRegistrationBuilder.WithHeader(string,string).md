#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.WithHeader\(string, string\) Method

Adds a condition requiring header [name](Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).md#Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).name 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithHeader\(string, string\)\.name') to carry [value](Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).md#Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithHeader\(string, string\)\.value') \-
checked against both [System\.Net\.Http\.HttpRequestMessage\.Headers](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestmessage.headers 'System\.Net\.Http\.HttpRequestMessage\.Headers') and
[System\.Net\.Http\.HttpRequestMessage\.Content](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestmessage.content 'System\.Net\.Http\.HttpRequestMessage\.Content')'s own [System\.Net\.Http\.HttpContent\.Headers](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcontent.headers 'System\.Net\.Http\.HttpContent\.Headers'), with no
precedence between the two collections \(ADR\-0062 D5\): matches if [value](Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).md#Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithHeader\(string, string\)\.value')
appears in either\. Header\-name comparison is case\-insensitive \(matching
[System\.Net\.Http\.Headers\.HttpHeaders](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpheaders 'System\.Net\.Http\.Headers\.HttpHeaders')'s own contract\); value comparison is
ordinal\. A header with multiple values matches if any one of them equals
[value](Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).md#Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithHeader\(string, string\)\.value')\. A missing header evaluates to no match, never an exception\. The
value for `Authorization`/`Proxy-Authorization`/`Cookie`/`Set-Cookie`
\(case\-insensitive\) is redacted in this registration's diagnostic description only \- matching
always compares the real value \(ADR\-0062 D6\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder WithHeader(string name, string value);
```
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).name'></a>

`name` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).value'></a>

`value` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')