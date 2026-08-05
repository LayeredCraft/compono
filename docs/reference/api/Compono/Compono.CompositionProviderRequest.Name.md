#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionProviderRequest](Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')

## CompositionProviderRequest\.Name Property

The declaring constructor parameter/required member/test\-method\-parameter's own name, for
diagnostic display and name\-based provider matching \(e\.g\. a future `Compono.Bogus`
member\-name convention\) \- [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a request with no name of its own \(a
collection element, dictionary key/value, or manual resolve\)\.

```csharp
public string? Name { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')