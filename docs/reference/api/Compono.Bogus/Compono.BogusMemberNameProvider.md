#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono')

## BogusMemberNameProvider Class

A stage\-5 semantic value provider that matches an exact, conservative allowlist of
`string`\-typed member names \(`FirstName`, `Email`, etc\.\) against a real,
deterministically\-seeded [Bogus\.Faker](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker 'Bogus\.Faker') value\. Registered via
`CompositionBuilderExtensions.UseBogus()`\. See
`docs/adr/0027-compono-bogus-package-design.md`\.

```csharp
public sealed class BogusMemberNameProvider : Compono.ICompositionValueProvider
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → BogusMemberNameProvider

Implements [ICompositionValueProvider](../Compono/Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')

| Constructors | |
| :--- | :--- |
| [BogusMemberNameProvider\(string\)](Compono.BogusMemberNameProvider.BogusMemberNameProvider(string).md 'Compono\.BogusMemberNameProvider\.BogusMemberNameProvider\(string\)') | Creates a [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') using [locale](Compono.BogusMemberNameProvider.BogusMemberNameProvider(string).md#Compono.BogusMemberNameProvider.BogusMemberNameProvider(string).locale 'Compono\.BogusMemberNameProvider\.BogusMemberNameProvider\(string\)\.locale')\. |

| Methods | |
| :--- | :--- |
| [TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.BogusMemberNameProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.BogusMemberNameProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') | Attempts to produce a value for [request](Compono.BogusMemberNameProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md#Compono.BogusMemberNameProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request 'Compono\.BogusMemberNameProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)\.request')\. Returns [NotHandled](../Compono/Compono.CompositionProviderResult.NotHandled.md 'Compono\.CompositionProviderResult\.NotHandled') for any request this provider doesn't apply to, so a later provider or pipeline stage still gets a chance \- never throws for an expected non\-match\. |
