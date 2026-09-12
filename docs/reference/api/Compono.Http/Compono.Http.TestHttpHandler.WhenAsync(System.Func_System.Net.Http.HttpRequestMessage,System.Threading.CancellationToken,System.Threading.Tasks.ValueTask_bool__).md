#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler')

## TestHttpHandler\.WhenAsync\(Func\<HttpRequestMessage,CancellationToken,ValueTask\<bool\>\>\) Method

Matches any request satisfying [predicate](Compono.Http.TestHttpHandler.WhenAsync(System.Func_System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask_bool__).md#Compono.Http.TestHttpHandler.WhenAsync(System.Func_System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask_bool__).predicate 'Compono\.Http\.TestHttpHandler\.WhenAsync\(System\.Func\<System\.Net\.Http\.HttpRequestMessage,System\.Threading\.CancellationToken,System\.Threading\.Tasks\.ValueTask\<bool\>\>\)\.predicate') \- the async peer of
[When\(Func&lt;HttpRequestMessage,bool&gt;\)](Compono.Http.TestHttpHandler.When(System.Func_System.Net.Http.HttpRequestMessage,bool_).md 'Compono\.Http\.TestHttpHandler\.When\(System\.Func\<System\.Net\.Http\.HttpRequestMessage,bool\>\)'), for a condition that needs to read the request body/await something to
decide \(ADR\-0062 D4\)\. Also the primitive every named matcher \([WithHeader\(string, string\)](Compono.Http.HttpResponseRegistrationBuilder.WithHeader(string,string).md 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithHeader\(string, string\)')/
[WithBody\(Func&lt;byte\[\],bool&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.WithBody(System.Func_byte[],bool_).md 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithBody\(System\.Func\<byte\[\],bool\>\)')/[WithFormBody\(Func&lt;ILookup&lt;string,string&gt;,bool&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.WithFormBody(System.Func_System.Linq.ILookup_string,string_,bool_).md 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithFormBody\(System\.Func\<System\.Linq\.ILookup\<string,string\>,bool\>\)')/
`WithJsonBody`\) compiles to internally \- exposed publicly so a condition none of them
covers still has a first\-class escape hatch, the same role [When\(Func&lt;HttpRequestMessage,bool&gt;\)](Compono.Http.TestHttpHandler.When(System.Func_System.Net.Http.HttpRequestMessage,bool_).md 'Compono\.Http\.TestHttpHandler\.When\(System\.Func\<System\.Net\.Http\.HttpRequestMessage,bool\>\)') already plays
for the synchronous case\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder WhenAsync(System.Func<System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask<bool>> predicate);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.WhenAsync(System.Func_System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask_bool__).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Net\.Http\.HttpRequestMessage](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestmessage 'System\.Net\.Http\.HttpRequestMessage')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.Tasks\.ValueTask&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')