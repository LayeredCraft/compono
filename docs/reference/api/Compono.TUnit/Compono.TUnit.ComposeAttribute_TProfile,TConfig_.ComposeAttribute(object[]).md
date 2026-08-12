#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit').[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>')\.

```csharp
public ComposeAttribute(params object?[] configArguments);
```
#### Parameters

<a name='Compono.TUnit.ComposeAttribute_TProfile,TConfig_.ComposeAttribute(object[]).configArguments'></a>

`configArguments` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Profile configuration arguments, bound positionally to [TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')'s single
public constructor \- an entirely separate binding target from this attribute family's ordinary
inline values; every test method parameter is composed in full regardless of what's supplied
here\. See the type\-level remarks for why each argument should use the strongest attribute\-legal
type available rather than a bare string\.