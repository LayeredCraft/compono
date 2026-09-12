#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.WithJsonBody Method

| Overloads | |
| :--- | :--- |
| [WithJsonBody&lt;T&gt;\(Func&lt;T,bool&gt;, JsonSerializerOptions\)](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions) 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.JsonSerializerOptions\)') | Adds a condition requiring a JSON `Content-Type` and evaluating [predicate](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.JsonSerializerOptions\)\.predicate') against the body deserialized via [System\.Text\.Json\.JsonSerializer\.Deserialize&lt;&gt;\.String,System\.Text\.Json\.JsonSerializerOptions\)](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize--1#system-text-json-jsonserializer-deserialize--1(system-string-system-text-json-jsonserializeroptions) 'System\.Text\.Json\.JsonSerializer\.Deserialize\`\`1\(System\.String,System\.Text\.Json\.JsonSerializerOptions\)')'s ordinary runtime\-metadata path \(ADR\-0062 D7a/D8\)\. |
| [WithJsonBody&lt;T&gt;\(Func&lt;T,bool&gt;, JsonTypeInfo&lt;T&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_) 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)') | Adds a condition requiring a JSON `Content-Type` \(`application/json`, or any media type whose subtype ends in `+json`, case\-insensitive\) and evaluating [predicate](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.predicate') against the body deserialized via [jsonTypeInfo](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).jsonTypeInfo 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.jsonTypeInfo') \(e\.g\. a source\-generated `JsonSerializerContext`'s metadata\) \- the guaranteed\-AOT\-safe overload, since it bypasses runtime resolver lookup entirely \(ADR\-0062 D7a/D8\)\. A missing or non\-JSON `Content-Type` evaluates to no match without reading or deserializing the body\. A body that cannot be deserialized as [T](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.T') propagates the underlying [System\.Text\.Json\.JsonException](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonexception 'System\.Text\.Json\.JsonException') directly \- it is not treated as "no match" \(ADR\-0062 D10\)\. |

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions)'></a>

## HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(Func\<T,bool\>, JsonSerializerOptions\) Method

Adds a condition requiring a JSON `Content-Type` and evaluating
[predicate](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.JsonSerializerOptions\)\.predicate') against the body deserialized via
[System\.Text\.Json\.JsonSerializer\.Deserialize&lt;&gt;\.String,System\.Text\.Json\.JsonSerializerOptions\)](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize--1#system-text-json-jsonserializer-deserialize--1(system-string-system-text-json-jsonserializeroptions) 'System\.Text\.Json\.JsonSerializer\.Deserialize\`\`1\(System\.String,System\.Text\.Json\.JsonSerializerOptions\)')'s ordinary
runtime\-metadata path \(ADR\-0062 D7a/D8\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder WithJsonBody<T>(System.Func<T?,bool> predicate, System.Text.Json.JsonSerializerOptions? options=null);
```
#### Type parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions).T'></a>

`T`
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[T](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.JsonSerializerOptions\)\.T')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions).options'></a>

`options` [System\.Text\.Json\.JsonSerializerOptions](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions 'System\.Text\.Json\.JsonSerializerOptions')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

### Remarks
Carries [System\.Diagnostics\.CodeAnalysis\.RequiresDynamicCodeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.requiresdynamiccodeattribute 'System\.Diagnostics\.CodeAnalysis\.RequiresDynamicCodeAttribute')/[System\.Diagnostics\.CodeAnalysis\.RequiresUnreferencedCodeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.requiresunreferencedcodeattribute 'System\.Diagnostics\.CodeAnalysis\.RequiresUnreferencedCodeAttribute')
because the underlying deserialize overload does \- `Compono.Http` itself introduces no
reflection, but this overload's runtime\-metadata resolution genuinely isn't Native\-AOT\-safe
unless [options](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.JsonSerializerOptions).options 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.JsonSerializerOptions\)\.options') supplies a source\-generated resolver\. Prefer
[WithJsonBody&lt;T&gt;\(Func&lt;T,bool&gt;, JsonTypeInfo&lt;T&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_) 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)') in an AOT/trim\-sensitive
project\.

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_)'></a>

## HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(Func\<T,bool\>, JsonTypeInfo\<T\>\) Method

Adds a condition requiring a JSON `Content-Type` \(`application/json`, or any
media type whose subtype ends in `+json`, case\-insensitive\) and evaluating
[predicate](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).predicate 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.predicate') against the body deserialized via [jsonTypeInfo](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).jsonTypeInfo 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.jsonTypeInfo')
\(e\.g\. a source\-generated `JsonSerializerContext`'s metadata\) \- the guaranteed\-AOT\-safe
overload, since it bypasses runtime resolver lookup entirely \(ADR\-0062 D7a/D8\)\. A missing or
non\-JSON `Content-Type` evaluates to no match without reading or deserializing the
body\. A body that cannot be deserialized as [T](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.T') propagates the
underlying [System\.Text\.Json\.JsonException](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonexception 'System\.Text\.Json\.JsonException') directly \- it is not treated as "no match"
\(ADR\-0062 D10\)\.

```csharp
public Compono.Http.HttpResponseRegistrationBuilder WithJsonBody<T>(System.Func<T?,bool> predicate, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo);
```
#### Type parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T'></a>

`T`
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).predicate'></a>

`predicate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[T](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.T')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

<a name='Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).jsonTypeInfo'></a>

`jsonTypeInfo` [System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1 'System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\`1')[T](Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody.md#Compono.Http.HttpResponseRegistrationBuilder.WithJsonBody_T_(System.Func_T,bool_,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.WithJsonBody\<T\>\(System\.Func\<T,bool\>, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1 'System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\`1')

#### Returns
[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')