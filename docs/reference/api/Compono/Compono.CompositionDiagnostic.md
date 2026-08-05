#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionDiagnostic Class

The structured detail behind a thrown [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') \- what couldn't be
composed, where in the graph, what was tried, and with which seed\. `docs/public-api.md`'s
Diagnostics API \(`exception.Diagnostic`\)\.

```csharp
public sealed class CompositionDiagnostic
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionDiagnostic

### Remarks
[ToString\(\)](Compono.CompositionDiagnostic.ToString().md 'Compono\.CompositionDiagnostic\.ToString\(\)') renders `docs/architecture.md`'s Diagnostics example format \- the
            shape a consumer gets from `Console.WriteLine(exception.Diagnostic)`\.

| Properties | |
| :--- | :--- |
| [FailedType](Compono.CompositionDiagnostic.FailedType.md 'Compono\.CompositionDiagnostic\.FailedType') | The type that could not be composed\. |
| [Message](Compono.CompositionDiagnostic.Message.md 'Compono\.CompositionDiagnostic\.Message') | A human\-readable, remediation\-oriented explanation of what went wrong\. |
| [Path](Compono.CompositionDiagnostic.Path.md 'Compono\.CompositionDiagnostic\.Path') | The request path from the root to [FailedType](Compono.CompositionDiagnostic.FailedType.md 'Compono\.CompositionDiagnostic\.FailedType'), rendered as a tree\. |
| [RootType](Compono.CompositionDiagnostic.RootType.md 'Compono\.CompositionDiagnostic\.RootType') | The type requested at the root of this composition operation\. |
| [Seed](Compono.CompositionDiagnostic.Seed.md 'Compono\.CompositionDiagnostic\.Seed') | This operation's root deterministic seed\. |
| [Trace](Compono.CompositionDiagnostic.Trace.md 'Compono\.CompositionDiagnostic\.Trace') | The stage/outcome attempts tried for the failing request and its ancestors \- never a sibling request that already succeeded elsewhere in this operation \(`docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md`'s checkpoint/rewind trace buffer\)\. |

| Methods | |
| :--- | :--- |
| [ToString\(\)](Compono.CompositionDiagnostic.ToString().md 'Compono\.CompositionDiagnostic\.ToString\(\)') | Renders this diagnostic in `docs/architecture.md`'s Diagnostics example format\. |
