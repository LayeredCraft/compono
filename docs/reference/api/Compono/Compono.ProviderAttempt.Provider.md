#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ProviderAttempt](Compono.ProviderAttempt.md 'Compono\.ProviderAttempt')

## ProviderAttempt\.Provider Property

The concrete `Compono.ICompositionProvider` type that made this attempt, or
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a context\-owned stage \(shared/scoped values, exact registrations,
collection\-plan/generated\-plan dispatch\) \- those aren't `Compono.ICompositionProvider`
instances at all, per `docs/architecture.md`'s Resolution Pipeline table\.

```csharp
public System.Type? Provider { get; init; }
```

#### Property Value
[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')