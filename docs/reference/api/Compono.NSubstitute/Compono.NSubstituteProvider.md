#### [Compono\.NSubstitute](index.md 'index')
### [Compono](Compono.md 'Compono')

## NSubstituteProvider Class

A stage\-6 test\-double provider that composes an interface, delegate, or \(when
[SubstituteAbstractClasses](Compono.NSubstituteOptions.SubstituteAbstractClasses.md 'Compono\.NSubstituteOptions\.SubstituteAbstractClasses') allows it\) unsealed abstract\-class
request as a real NSubstitute substitute, via `NSubstitute.Substitute.For(System.Type[],System.Object[])`\.
Registered via `CompositionBuilderExtensions.UseNSubstitute()`\. See
`docs/adr/0025-compono-nsubstitute-package-design.md` \(Amendment 1 for the corrected shape
below\)\.

```csharp
public sealed class NSubstituteProvider : Compono.ICompositionValueProvider
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → NSubstituteProvider

Implements [ICompositionValueProvider](../Compono/Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')

| Constructors | |
| :--- | :--- |
| [NSubstituteProvider\(NSubstituteOptions\)](Compono.NSubstituteProvider.NSubstituteProvider(Compono.NSubstituteOptions).md 'Compono\.NSubstituteProvider\.NSubstituteProvider\(Compono\.NSubstituteOptions\)') | Creates an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider'), snapshotting [options](Compono.NSubstituteProvider.NSubstituteProvider(Compono.NSubstituteOptions).md#Compono.NSubstituteProvider.NSubstituteProvider(Compono.NSubstituteOptions).options 'Compono\.NSubstituteProvider\.NSubstituteProvider\(Compono\.NSubstituteOptions\)\.options')'s current values \- the provider never retains the caller\-owned [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions') instance itself, so a caller mutating it after construction can't change this already\-registered provider's behavior \(ADR\-0024's immutable\-provider\-registration guarantee\)\. |

| Methods | |
| :--- | :--- |
| [TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.NSubstituteProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.NSubstituteProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') | Attempts to produce a value for [request](Compono.NSubstituteProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md#Compono.NSubstituteProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request 'Compono\.NSubstituteProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)\.request')\. Returns [NotHandled](../Compono/Compono.CompositionProviderResult.NotHandled.md 'Compono\.CompositionProviderResult\.NotHandled') for any request this provider doesn't apply to, so a later provider or pipeline stage still gets a chance \- never throws for an expected non\-match\. |
