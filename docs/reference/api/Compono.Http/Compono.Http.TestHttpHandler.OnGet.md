#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler')

## TestHttpHandler\.OnGet Method

| Overloads | |
| :--- | :--- |
| [OnGet\(Match&lt;string&gt;\)](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(Compono.Match_string_) 'Compono\.Http\.TestHttpHandler\.OnGet\(Compono\.Match\<string\>\)') | Matches an HTTP GET whose request URI's path/query satisfies [path](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(Compono.Match_string_).path 'Compono\.Http\.TestHttpHandler\.OnGet\(Compono\.Match\<string\>\)\.path') \([Any&lt;T&gt;\(\)](../Compono/Compono.Match.Any_T_().md 'Compono\.Match\.Any\`\`1')/[Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](../Compono/Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\`\`1\(System\.Func\{\`\`0,System\.Boolean\}\)')\)\. |
| [OnGet\(string\)](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string) 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)') | Matches an HTTP GET whose request URI's path/query equals [path](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string).path 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)\.path') exactly \- the normal, common\-case entry point\. Preserves [path](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string).path 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)\.path') verbatim in [Verify\(\)](Compono.Http.HttpResponseRegistration.Verify().md 'Compono\.Http\.HttpResponseRegistration\.Verify\(\)')'s diagnostics \(e\.g\. `GET /v1/customers/42`\), unlike the [Match&lt;T&gt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1') overload below, which can't \(ADR\-0051 Amendment 1\)\. |

<a name='Compono.Http.TestHttpHandler.OnGet(Compono.Match_string_)'></a>

## TestHttpHandler\.OnGet\(Match\<string\>\) Method

Matches an HTTP GET whose request URI's path/query satisfies [path](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(Compono.Match_string_).path 'Compono\.Http\.TestHttpHandler\.OnGet\(Compono\.Match\<string\>\)\.path') \([Any&lt;T&gt;\(\)](../Compono/Compono.Match.Any_T_().md 'Compono\.Match\.Any\`\`1')/[Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](../Compono/Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\`\`1\(System\.Func\{\`\`0,System\.Boolean\}\)')\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder OnGet(Compono.Match<string> path);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.OnGet(Compono.Match_string_).path'></a>

`path` [Compono\.Match&lt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

<a name='Compono.Http.TestHttpHandler.OnGet(string)'></a>

## TestHttpHandler\.OnGet\(string\) Method

Matches an HTTP GET whose request URI's path/query equals [path](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string).path 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)\.path') exactly \-
the normal, common\-case entry point\. Preserves [path](Compono.Http.TestHttpHandler.OnGet.md#Compono.Http.TestHttpHandler.OnGet(string).path 'Compono\.Http\.TestHttpHandler\.OnGet\(string\)\.path') verbatim in
[Verify\(\)](Compono.Http.HttpResponseRegistration.Verify().md 'Compono\.Http\.HttpResponseRegistration\.Verify\(\)')'s diagnostics \(e\.g\. `GET /v1/customers/42`\),
unlike the [Match&lt;T&gt;](../Compono/Compono.Match_T_.md 'Compono\.Match\`1') overload below, which can't \(ADR\-0051 Amendment 1\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder OnGet(string path);
```
#### Parameters

<a name='Compono.Http.TestHttpHandler.OnGet(string).path'></a>

`path` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')