#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionException Class

Thrown when a composition request reaches the resolution pipeline's terminal stage \- no
explicit value, shared value, registration, provider, or generated plan could satisfy it\.

```csharp
public sealed class CompositionException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → CompositionException

### Remarks
This is the thrown\-exception boundary `docs/public-api.md`'s examples catch \-
[Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)') must return a plain `TValue`, so a
terminal non\-success pipeline outcome has no return\-value channel to report through and
converts to this exception instead, per
`docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md`\. The pipeline's own
internal stages still communicate via `Compono.CompositionResult`, not exceptions \- only
this outward\-facing boundary throws\.

| Constructors | |
| :--- | :--- |
| [CompositionException\(CompositionDiagnostic\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic) 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic\)') | Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') from a structured [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic') \- the shape every pipeline\-thrown instance uses, per `docs/public-api.md`'s Diagnostics API\. |
| [CompositionException\(CompositionDiagnostic, Exception\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception) 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic, System\.Exception\)') | Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') from a structured [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic'), preserving [innerException](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception).innerException 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic, System\.Exception\)\.innerException') \- the shape a configured `IServiceProvider` throwing during stage 3's fallback sub\-step uses, per `docs/adr/0019-registrations-and-service-provider-injection.md` \("never `throw ex;`, the original exception is always preserved"\)\. |
| [CompositionException\(string\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string) 'Compono\.CompositionException\.CompositionException\(string\)') | Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') with no structured [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic')\. |

| Properties | |
| :--- | :--- |
| [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') | The structured detail behind this failure, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if this instance was constructed from a plain message\. |

| Methods | |
| :--- | :--- |
| [WithSeedInMessage\(CompositionException, int\)](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)') | Creates a copy of [original](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md#Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)\.original') whose [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message') has a `"Seed: <value>"` line appended, so a consumer building custom composition\-failure tooling \(e\.g\. a test\-framework integration reporting a reproducible seed\) can surface it without needing [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') to be present \- [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') already renders its own `"Seed:"` line via [ToString\(\)](Compono.CompositionDiagnostic.ToString().md 'Compono\.CompositionDiagnostic\.ToString\(\)') when it's there, but not every [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') has one \(a plain [CompositionException\(string\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string) 'Compono\.CompositionException\.CompositionException\(string\)'), e\.g\. a generated collection plan's unique\-value\-exhaustion failure, has none\)\. |
