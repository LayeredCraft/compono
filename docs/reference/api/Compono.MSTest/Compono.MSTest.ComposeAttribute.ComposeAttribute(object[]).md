#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest').[ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute')\.

```csharp
public ComposeAttribute(params object?[] inlineValues);
```
#### Parameters

<a name='Compono.MSTest.ComposeAttribute.ComposeAttribute(object[]).inlineValues'></a>

`inlineValues` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Values supplied positionally, left\-to\-right from the test method's first parameter \- every
parameter at an index beyond this array's length is composed instead\. An explicit
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') entry is a supplied value, not "not supplied": presence is determined
by array length alone\. Matches `Compono.XunitV3.ComposeAttribute`'s own
`params object?[]` single\-null/single\-array binding\-ambiguity handling exactly \(see
`Compono.MSTest.ComposeAttribute.NormalizeParamsArguments(System.Object[])`\)\.