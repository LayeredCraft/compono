#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')

## ComposeAttribute\.SupportsDiscoveryEnumeration\(\) Method

Returns [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if the data attribute supports enumeration during
discovery; [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') otherwise\. Data attributes with expensive computational
costs and/or randomized data sets should return [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\.

```csharp
public override bool SupportsDiscoveryEnumeration();
```

Implements `SupportsDiscoveryEnumeration()`

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')