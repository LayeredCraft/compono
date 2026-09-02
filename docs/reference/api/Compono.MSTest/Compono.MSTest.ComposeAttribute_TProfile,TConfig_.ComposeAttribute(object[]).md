#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest').[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>')\.

```csharp
public ComposeAttribute(params object?[] configArguments);
```
#### Parameters

<a name='Compono.MSTest.ComposeAttribute_TProfile,TConfig_.ComposeAttribute(object[]).configArguments'></a>

`configArguments` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Profile configuration arguments, bound positionally to [TConfig](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md#Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')'s
single public constructor \- an entirely separate binding target from this attribute
family's ordinary inline values; every test method parameter is composed in full
regardless of what's supplied here\.