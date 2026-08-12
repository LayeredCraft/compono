#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit').[ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute')\.

```csharp
public ComposeAttribute(params object?[] inlineValues);
```
#### Parameters

<a name='Compono.TUnit.ComposeAttribute.ComposeAttribute(object[]).inlineValues'></a>

`inlineValues` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Values supplied positionally, left\-to\-right from the test method's first parameter \- every
parameter at an index beyond this array's length is composed instead\. Matches
`Compono.XunitV3.ComposeAttribute`'s own single\-null/single\-array constructor\-binding
edge cases exactly \- see `Compono.TUnit.ComposeAttribute.NormalizeParamsArguments(System.Object[])`\.