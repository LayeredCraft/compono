#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler')

## TestHttpHandler\.When\(Func\<HttpRequestMessage,bool\>\) Method

Matches any request satisfying [predicate](Compono.Http.TestHttpHandler.When(System.Func_System.Net.Http.HttpRequestMessage,bool_).md#Compono.Http.TestHttpHandler.When(System.Func_System.Net.Http.HttpRequestMessage,bool_).predicate 'Compono\.Http\.TestHttpHandler\.When\(System\.Func\<System\.Net\.Http\.HttpRequestMessage,bool\>\)\.predicate') \- the whole\-request escape
hatch for conditions spanning method/URI/headers/content type at once\. A plain
[System\.Func&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2'), not [Match&lt;T&gt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1') \- see ADR\-0051 "Should this reuse
core Match\<T\>, or stay HTTP\-native?" for why\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder When(System.Func<System.Net.Http.HttpRequestMessage,bool> predicate);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.When(System.Func_System.Net.Http.HttpRequestMessage,bool_).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Net\.Http\.HttpRequestMessage](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestmessage 'System\.Net\.Http\.HttpRequestMessage')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')