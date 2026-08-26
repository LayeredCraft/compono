#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler')

## TestHttpHandler\.OnPut Method

| Overloads | |
| :--- | :--- |
| [OnPut\(Match&lt;string&gt;\)](Compono.Http.TestHttpHandler.OnPut.md#Compono.Http.TestHttpHandler.OnPut(Compono.Match_string_) 'Compono\.Http\.TestHttpHandler\.OnPut\(Compono\.Match\<string\>\)') | Matches an HTTP PUT whose request URI's path/query satisfies [path](Compono.Http.TestHttpHandler.OnPut.md#Compono.Http.TestHttpHandler.OnPut(Compono.Match_string_).path 'Compono\.Http\.TestHttpHandler\.OnPut\(Compono\.Match\<string\>\)\.path') \([Any&lt;T&gt;\(\)](../Compono/Compono.Match.Any_T_().md 'Compono\.Match\.Any\`\`1')/[Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](../Compono/Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\`\`1\(System\.Func\{\`\`0,System\.Boolean\}\)')\)\. |
| [OnPut\(string\)](Compono.Http.TestHttpHandler.OnPut.md#Compono.Http.TestHttpHandler.OnPut(string) 'Compono\.Http\.TestHttpHandler\.OnPut\(string\)') | Matches an HTTP PUT whose request URI's path/query equals [path](Compono.Http.TestHttpHandler.OnPut.md#Compono.Http.TestHttpHandler.OnPut(string).path 'Compono\.Http\.TestHttpHandler\.OnPut\(string\)\.path') exactly \- see [OnGet\(string\)](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string) 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)')'s remarks \(same rationale, every `OnX` method\)\. |

<a name='Compono.Http.TestHttpHandler.OnPut(Compono.Match_string_)'></a>

## TestHttpHandler\.OnPut\(Match\<string\>\) Method

Matches an HTTP PUT whose request URI's path/query satisfies [path](Compono.Http.TestHttpHandler.OnPut.md#Compono.Http.TestHttpHandler.OnPut(Compono.Match_string_).path 'Compono\.Http\.TestHttpHandler\.OnPut\(Compono\.Match\<string\>\)\.path') \([Any&lt;T&gt;\(\)](../Compono/Compono.Match.Any_T_().md 'Compono\.Match\.Any\`\`1')/[Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](../Compono/Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\`\`1\(System\.Func\{\`\`0,System\.Boolean\}\)')\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder OnPut(Compono.Match<string> path);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.OnPut(Compono.Match_string_).path'></a>

`path` [Compono\.Match&lt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

<a name='Compono.Http.TestHttpHandler.OnPut(string)'></a>

## TestHttpHandler\.OnPut\(string\) Method

Matches an HTTP PUT whose request URI's path/query equals [path](Compono.Http.TestHttpHandler.OnPut.md#Compono.Http.TestHttpHandler.OnPut(string).path 'Compono\.Http\.TestHttpHandler\.OnPut\(string\)\.path') exactly \-
see [OnGet\(string\)](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string) 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)')'s remarks \(same rationale, every `OnX` method\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder OnPut(string path);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.OnPut(string).path'></a>

`path` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')