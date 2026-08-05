#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')

## ComposeAttribute\(object\[\]\) Constructor

Creates a [ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')\.

```csharp
public ComposeAttribute(params object?[] inlineValues);
```
#### Parameters

<a name='Compono.XunitV3.ComposeAttribute.ComposeAttribute(object[]).inlineValues'></a>

`inlineValues` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Values supplied positionally, left\-to\-right from the test method's first parameter \- every
parameter at an index beyond this array's length is composed instead\. An explicit
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') entry is a supplied value, not "not supplied": presence is determined by
array length alone\. A single [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') argument \(e\.g\. `[Compose(null)]`\) binds
in the C\# compiler's non\-expanded `params` form \- the whole array, not a one\-element array
containing [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \- so [inlineValues](Compono.XunitV3.ComposeAttribute.ComposeAttribute(object[]).md#Compono.XunitV3.ComposeAttribute.ComposeAttribute(object[]).inlineValues 'Compono\.XunitV3\.ComposeAttribute\.ComposeAttribute\(object\[\]\)\.inlineValues') itself arrives
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') in exactly that case \(PR \#23 review\); treated as a one\-element array
containing that [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') value, matching what the author's single\-argument syntax
actually means rather than surfacing the compiler's array\-vs\-element ambiguity as a thrown
exception\. The same non\-expanded binding form applies to a single reference\-array\-typed
argument covariantly convertible to [object](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/object 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/object')?\[\] \- e\.g\.
`[Compose(new string[] { "a", "b" })]` \- which arrives here as that exact
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')\[\] instance \(runtime type `string[]`, not `object[]`\) rather than
a 2\-element [object](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/object 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/object')?\[\] \(PR \#24 review\); wrapped as a one\-element array
containing that whole array value, so it binds to a single array\-typed parameter rather than
being misread as two separate inline values\.