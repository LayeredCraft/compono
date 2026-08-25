#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler')

## TestHttpHandler\.OnPost Method

| Overloads | |
| :--- | :--- |
| [OnPost\(Match&lt;string&gt;\)](Compono.Http.TestHttpHandler.OnPost.md#Compono.Http.TestHttpHandler.OnPost(Compono.Match_string_) 'Compono\.Http\.TestHttpHandler\.OnPost\(Compono\.Match\<string\>\)') | Matches an HTTP POST whose request URI's path/query satisfies [path](Compono.Http.TestHttpHandler.OnPost.md#Compono.Http.TestHttpHandler.OnPost(Compono.Match_string_).path 'Compono\.Http\.TestHttpHandler\.OnPost\(Compono\.Match\<string\>\)\.path') \([Any&lt;T&gt;\(\)](../Compono/Compono.Match.Any_T_().md 'Compono\.Match\.Any\`\`1')/[Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](../Compono/Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\`\`1\(System\.Func\{\`\`0,System\.Boolean\}\)')\)\. |
| [OnPost\(string\)](Compono.Http.TestHttpHandler.OnPost.md#Compono.Http.TestHttpHandler.OnPost(string) 'Compono\.Http\.TestHttpHandler\.OnPost\(string\)') | Matches an HTTP POST whose request URI's path/query equals [path](Compono.Http.TestHttpHandler.OnPost.md#Compono.Http.TestHttpHandler.OnPost(string).path 'Compono\.Http\.TestHttpHandler\.OnPost\(string\)\.path') exactly \- see [OnGet\(string\)](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string) 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)')'s remarks \(same rationale, every `OnX` method\)\. |

<a name='Compono.Http.TestHttpHandler.OnPost(Compono.Match_string_)'></a>

## TestHttpHandler\.OnPost\(Match\<string\>\) Method

Matches an HTTP POST whose request URI's path/query satisfies [path](Compono.Http.TestHttpHandler.OnPost.md#Compono.Http.TestHttpHandler.OnPost(Compono.Match_string_).path 'Compono\.Http\.TestHttpHandler\.OnPost\(Compono\.Match\<string\>\)\.path') \([Any&lt;T&gt;\(\)](../Compono/Compono.Match.Any_T_().md 'Compono\.Match\.Any\`\`1')/[Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](../Compono/Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\`\`1\(System\.Func\{\`\`0,System\.Boolean\}\)')\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder OnPost(Compono.Match<string> path);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.OnPost(Compono.Match_string_).path'></a>

`path` [Compono\.Match&lt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

<a name='Compono.Http.TestHttpHandler.OnPost(string)'></a>

## TestHttpHandler\.OnPost\(string\) Method

Matches an HTTP POST whose request URI's path/query equals [path](Compono.Http.TestHttpHandler.OnPost.md#Compono.Http.TestHttpHandler.OnPost(string).path 'Compono\.Http\.TestHttpHandler\.OnPost\(string\)\.path') exactly \-
see [OnGet\(string\)](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string) 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)')'s remarks \(same rationale, every `OnX` method\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder OnPost(string path);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.OnPost(string).path'></a>

`path` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')