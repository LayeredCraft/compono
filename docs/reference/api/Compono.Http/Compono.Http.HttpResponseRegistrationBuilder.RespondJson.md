#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[HttpResponseRegistrationBuilder](Compono.Http.HttpResponseRegistrationBuilder.md 'Compono\.Http\.HttpResponseRegistrationBuilder')

## HttpResponseRegistrationBuilder\.RespondJson Method

| Overloads | |
| :--- | :--- |
| [RespondJson&lt;T&gt;\(T, JsonSerializerOptions\)](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions) 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)') | Responds with HTTP 200 OK and [value](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)\.value') serialized to JSON, using the ordinary runtime\-metadata [System\.Text\.Json\.JsonSerializer](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer 'System\.Text\.Json\.JsonSerializer') path\. [value](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)\.value') is serialized once, here, to an immutable byte buffer; every matched invocation constructs a fresh [System\.Net\.Http\.ByteArrayContent](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.bytearraycontent 'System\.Net\.Http\.ByteArrayContent') over that same buffer with its own explicit `Content-Type` header \(ADR\-0051 "Serialize\-once\-to\-bytes model"\)\. |
| [RespondJson&lt;T&gt;\(T, JsonTypeInfo&lt;T&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_) 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)') | Responds with HTTP 200 OK and [value](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.value') serialized to JSON via [jsonTypeInfo](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).jsonTypeInfo 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.jsonTypeInfo') \(e\.g\. a source\-generated `JsonSerializerContext`'s metadata\) \- the guaranteed\-AOT\-safe path, since it bypasses runtime resolver lookup entirely\. Same serialize\-once\-to\-bytes model as [RespondJson&lt;T&gt;\(T, JsonSerializerOptions\)](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions) 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)')\. |

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions)'></a>

## HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, JsonSerializerOptions\) Method

Responds with HTTP 200 OK and [value](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)\.value') serialized to JSON, using the
ordinary runtime\-metadata [System\.Text\.Json\.JsonSerializer](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer 'System\.Text\.Json\.JsonSerializer') path\. [value](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)\.value') is
serialized once, here, to an immutable byte buffer; every matched invocation constructs a
fresh [System\.Net\.Http\.ByteArrayContent](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.bytearraycontent 'System\.Net\.Http\.ByteArrayContent') over that same buffer with its own explicit
`Content-Type` header \(ADR\-0051 "Serialize\-once\-to\-bytes model"\)\.

```csharp
public Compono.Http.HttpResponseRegistration RespondJson<T>(T value, System.Text.Json.JsonSerializerOptions? options=null);
```
#### Type parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).T'></a>

`T`
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).value'></a>

`value` [T](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)\.T')

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).options'></a>

`options` [System\.Text\.Json\.JsonSerializerOptions](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions 'System\.Text\.Json\.JsonSerializerOptions')

#### Returns
[HttpResponseRegistration](Compono.Http.HttpResponseRegistration.md 'Compono\.Http\.HttpResponseRegistration')

### Remarks
Carries [System\.Diagnostics\.CodeAnalysis\.RequiresDynamicCodeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.requiresdynamiccodeattribute 'System\.Diagnostics\.CodeAnalysis\.RequiresDynamicCodeAttribute')/[System\.Diagnostics\.CodeAnalysis\.RequiresUnreferencedCodeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.requiresunreferencedcodeattribute 'System\.Diagnostics\.CodeAnalysis\.RequiresUnreferencedCodeAttribute')
because the underlying [System\.Text\.Json\.JsonSerializer\.Serialize&lt;&gt;\.Text\.Json\.JsonSerializerOptions\)](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.serialize--1#system-text-json-jsonserializer-serialize--1(--0-system-text-json-jsonserializeroptions) 'System\.Text\.Json\.JsonSerializer\.Serialize\`\`1\(\`\`0,System\.Text\.Json\.JsonSerializerOptions\)')
overload does \- `Compono.Http` itself introduces no reflection, but this overload's
runtime\-metadata resolution genuinely isn't Native\-AOT\-safe unless [options](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions).options 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)\.options')
supplies a source\-generated resolver\. Prefer
[RespondJson&lt;T&gt;\(T, JsonTypeInfo&lt;T&gt;\)](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_) 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)') in an AOT/trim\-sensitive project \- see
ADR\-0051 "JSON / AOT" for the verified attribute\-propagation rationale\.

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_)'></a>

## HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, JsonTypeInfo\<T\>\) Method

Responds with HTTP 200 OK and [value](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).value 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.value') serialized to JSON via
[jsonTypeInfo](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).jsonTypeInfo 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.jsonTypeInfo') \(e\.g\. a source\-generated `JsonSerializerContext`'s
metadata\) \- the guaranteed\-AOT\-safe path, since it bypasses runtime resolver lookup
entirely\. Same serialize\-once\-to\-bytes model as
[RespondJson&lt;T&gt;\(T, JsonSerializerOptions\)](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.JsonSerializerOptions) 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.JsonSerializerOptions\)')\.

```csharp
public Compono.Http.HttpResponseRegistration RespondJson<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo);
```
#### Type parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T'></a>

`T`
#### Parameters

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).value'></a>

`value` [T](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.T')

<a name='Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).jsonTypeInfo'></a>

`jsonTypeInfo` [System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1 'System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\`1')[T](Compono.Http.HttpResponseRegistrationBuilder.RespondJson.md#Compono.Http.HttpResponseRegistrationBuilder.RespondJson_T_(T,System.Text.Json.Serialization.Metadata.JsonTypeInfo_T_).T 'Compono\.Http\.HttpResponseRegistrationBuilder\.RespondJson\<T\>\(T, System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\<T\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1 'System\.Text\.Json\.Serialization\.Metadata\.JsonTypeInfo\`1')

#### Returns
[HttpResponseRegistration](Compono.Http.HttpResponseRegistration.md 'Compono\.Http\.HttpResponseRegistration')