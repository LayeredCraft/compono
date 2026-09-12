#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.WithFormBody\(Func\<ILookup\<string,string\>,bool\>\) Method

Adds a condition requiring `Content-Type: application/x-www-form-urlencoded`
\(case\-insensitive\) and evaluating [predicate](Compono.Http.HttpResponseRegistrationBuilder.WithFormBody(System.Func_System.Linq.ILookup_string,string_,bool_).md#Compono.Http.HttpResponseRegistrationBuilder.WithFormBody(System.Func_System.Linq.ILookup_string,string_,bool_).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithFormBody\(System\.Func\<System\.Linq\.ILookup\<string,string\>,bool\>\)\.predicate') against the parsed form
fields \(ADR\-0062 D7/D7a\)\. A missing or different `Content-Type` evaluates to no match
without reading the body at all\. Percent\-escapes are decoded, and a raw `+` is decoded
to a space, per `application/x-www-form-urlencoded` semantics \(ADR\-0062 D7's corrected
mechanism\)\. [predicate](Compono.Http.HttpResponseRegistrationBuilder.WithFormBody(System.Func_System.Linq.ILookup_string,string_,bool_).md#Compono.Http.HttpResponseRegistrationBuilder.WithFormBody(System.Func_System.Linq.ILookup_string,string_,bool_).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithFormBody\(System\.Func\<System\.Linq\.ILookup\<string,string\>,bool\>\)\.predicate') receives an [System\.Linq\.ILookup&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.linq.ilookup-2 'System\.Linq\.ILookup\`2'),
not a dictionary, because a real form body can legitimately repeat a key \(e\.g\. a checkbox
group\) \- the lookup's indexer returns every value for a key, in wire order, or an empty
sequence \(never a throw\) for an absent key\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder WithFormBody(System.Func<System.Linq.ILookup<string,string>,bool> predicate);
```
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithFormBody(System.Func_System.Linq.ILookup_string,string_,bool_).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Linq\.ILookup&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.linq.ilookup-2 'System\.Linq\.ILookup\`2')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[,](https://learn.microsoft.com/en-us/dotnet/api/system.linq.ilookup-2 'System\.Linq\.ILookup\`2')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.linq.ilookup-2 'System\.Linq\.ILookup\`2')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')