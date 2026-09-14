#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>')\.

```csharp
public ComposeAttribute(params object?[] configArguments);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.ComposeAttribute(object[]).configArguments'></a>

`configArguments` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Profile configuration arguments, bound positionally to [TConfig](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')'s single
public constructor at compile time by `Compono.Generators` \- never read by this attribute
itself at runtime \(it carries no `GetData` override; xUnit's AOT pipeline never invokes
it\)\.