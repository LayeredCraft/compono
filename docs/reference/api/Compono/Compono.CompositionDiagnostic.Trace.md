#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic')

## CompositionDiagnostic\.Trace Property

The stage/outcome attempts tried for the failing request and its ancestors \- never a sibling
request that already succeeded elsewhere in this operation
\(`docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md`'s
checkpoint/rewind trace buffer\)\.

```csharp
public System.Collections.Generic.IReadOnlyList<Compono.ProviderAttempt> Trace { get; init; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[ProviderAttempt](Compono.ProviderAttempt.md 'Compono\.ProviderAttempt')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')