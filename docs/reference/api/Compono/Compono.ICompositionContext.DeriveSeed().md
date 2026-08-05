#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')

## ICompositionContext\.DeriveSeed\(\) Method

Derives a deterministic [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32') seed from this context's root seed and the request
currently being resolved \- usable to seed a caller\-owned PRNG \(e\.g\. a `Bogus.Randomizer`\)
without exposing the engine's own internal random source or path representation\. The same root
seed and the same request path always derive the same value; a different path \(a different
member, a different constructor parameter, a different element of a collection\) always derives
independently\. Calling this method repeatedly for the same active request returns the same
value every time \- it does not advance any stream, and does not perturb any other value's own
derivation\. Valid in the same scope as [Resolve&lt;TValue&gt;\(\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_() 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)'): from inside a
registration or configuration\-rule factory, or a public
[TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') invocation\. See
`docs/adr/0026-deterministic-seed-derivation-for-providers.md`\.

```csharp
int DeriveSeed();
```

#### Returns
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
No registration/configuration\-rule factory or public provider invocation is currently in
progress\.