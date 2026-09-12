#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.WithBody\(Func\<byte\[\],bool\>\) Method

Adds a condition evaluating [predicate](Compono.Http.HttpResponseRegistrationBuilder.WithBody(System.Func_byte[],bool_).md#Compono.Http.HttpResponseRegistrationBuilder.WithBody(System.Func_byte[],bool_).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithBody\(System\.Func\<byte\[\],bool\>\)\.predicate') against the request body's raw
bytes \- the generic, media\-type\-agnostic foundation
[WithFormBody\(Func&lt;ILookup&lt;string,string&gt;,bool&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.WithFormBody(System.Func_System.Linq.ILookup_string,string_,bool_).md 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithFormBody\(System\.Func\<System\.Linq\.ILookup\<string,string\>,bool\>\)')/[WithJsonBody&lt;T&gt;\(Func&lt;T,bool&gt;, JsonTypeInfo&lt;T&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_) 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)') are
themselves built on \(ADR\-0062 D7/D7a\)\. A request with no content evaluates to no match;
[predicate](Compono.Http.HttpResponseRegistrationBuilder.WithBody(System.Func_byte[],bool_).md#Compono.Http.HttpResponseRegistrationBuilder.WithBody(System.Func_byte[],bool_).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithBody\(System\.Func\<byte\[\],bool\>\)\.predicate') is never invoked in that case\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder WithBody(System.Func<byte[],bool> predicate);
```
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithBody(System.Func_byte[],bool_).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte 'System\.Byte')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')