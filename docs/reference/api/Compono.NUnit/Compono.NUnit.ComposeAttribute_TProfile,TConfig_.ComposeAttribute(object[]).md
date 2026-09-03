#### [Compono\.NUnit](index.md 'index')
### [Compono\.NUnit](Compono.NUnit.md 'Compono\.NUnit').[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.NUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile,TConfig\>')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.NUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile,TConfig\>')\.

```csharp
public ComposeAttribute(params object?[] configArguments);
```
#### Parameters

<a name='Compono.NUnit.ComposeAttribute_TProfile,TConfig_.ComposeAttribute(object[]).configArguments'></a>

`configArguments` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Profile configuration arguments, bound positionally to [TConfig](Compono.NUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.NUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.NUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')'s
single public constructor \- an entirely separate binding target from this attribute
family's ordinary inline values; every test method parameter is composed in full
regardless of what's supplied here\.