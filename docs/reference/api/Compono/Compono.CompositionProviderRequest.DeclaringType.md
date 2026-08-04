#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionProviderRequest](Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')

## CompositionProviderRequest\.DeclaringType Property

The type whose constructor/required member declares this request, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')
for a request with no member identity \(a collection element, a manual resolve, or the
composition root itself\) \- the same field/same semantics
[DeclaringType](Compono.CompositionRequestDescriptor.DeclaringType.md 'Compono\.CompositionRequestDescriptor\.DeclaringType') already carries, per
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public System.Type? DeclaringType { get; }
```

#### Property Value
[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')