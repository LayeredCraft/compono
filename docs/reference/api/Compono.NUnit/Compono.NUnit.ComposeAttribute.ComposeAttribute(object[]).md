#### [Compono\.NUnit](index.md 'index')
### [Compono\.NUnit](Compono.NUnit.md 'Compono\.NUnit').[ComposeAttribute](Compono.NUnit.ComposeAttribute.md 'Compono\.NUnit\.ComposeAttribute')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute](Compono.NUnit.ComposeAttribute.md 'Compono\.NUnit\.ComposeAttribute')\.

```csharp
public ComposeAttribute(params object?[] inlineValues);
```
#### Parameters

<a name='Compono.NUnit.ComposeAttribute.ComposeAttribute(object[]).inlineValues'></a>

`inlineValues` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Values supplied positionally, left\-to\-right from the test method's first parameter \- every
parameter at an index beyond this array's length is composed instead\. An explicit
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') entry is a supplied value, not "not supplied": presence is determined
by array length alone\. Matches `Compono.XunitV3.ComposeAttribute`'s own
`params object?[]` single\-null/single\-array binding\-ambiguity handling exactly \(see
`Compono.NUnit.ComposeAttribute.NormalizeParamsArguments(System.Object[])`\)\.