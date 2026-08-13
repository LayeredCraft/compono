#### [Compono\.TestDoubles](index.md 'index')
### [Compono](Compono.md 'Compono')

## GeneratedTestDoubleProvider Class

A stage\-6 test\-double provider that satisfies a request by looking up a factory already
registered into core [GeneratedTestDoubleRegistry](../Compono/Compono.GeneratedTestDoubleRegistry.md 'Compono\.GeneratedTestDoubleRegistry') \- populated by a
`Compono.Generators`\-emitted `[ModuleInitializer]` per discovered interface, never by
this type\. Registered via `CompositionBuilderExtensions.UseGeneratedTestDoubles()`\. See
ADR\-0043's "Runtime activation and precedence"\.

```csharp
public sealed class GeneratedTestDoubleProvider : Compono.ICompositionValueProvider
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → GeneratedTestDoubleProvider

Implements [ICompositionValueProvider](../Compono/Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')

| Methods | |
| :--- | :--- |
| [TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.GeneratedTestDoubleProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.GeneratedTestDoubleProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') | Attempts to produce a value for [request](Compono.GeneratedTestDoubleProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md#Compono.GeneratedTestDoubleProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).request 'Compono\.GeneratedTestDoubleProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)\.request')\. Returns [NotHandled](../Compono/Compono.CompositionProviderResult.NotHandled.md 'Compono\.CompositionProviderResult\.NotHandled') for any request this provider doesn't apply to, so a later provider or pipeline stage still gets a chance \- never throws for an expected non\-match\. |
