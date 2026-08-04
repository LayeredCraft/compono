#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ProviderAttempt](Compono.ProviderAttempt.md 'Compono\.ProviderAttempt')

## ProviderAttempt\(PipelineStage, Type, CompositionAttemptOutcome\) Constructor

One resolution\-pipeline stage tried for one composition request, and what it resulted in\.

```csharp
public ProviderAttempt(Compono.PipelineStage Stage, System.Type? Provider, Compono.CompositionAttemptOutcome Outcome);
```
#### Parameters

<a name='Compono.ProviderAttempt.ProviderAttempt(Compono.PipelineStage,System.Type,Compono.CompositionAttemptOutcome).Stage'></a>

`Stage` [PipelineStage](Compono.PipelineStage.md 'Compono\.PipelineStage')

Which pipeline stage this attempt was for\.

<a name='Compono.ProviderAttempt.ProviderAttempt(Compono.PipelineStage,System.Type,Compono.CompositionAttemptOutcome).Provider'></a>

`Provider` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The concrete [Compono\.ICompositionProvider](https://learn.microsoft.com/en-us/dotnet/api/compono.icompositionprovider 'Compono\.ICompositionProvider') type that made this attempt, or
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a context\-owned stage \(shared/scoped values, exact registrations,
collection\-plan/generated\-plan dispatch\) \- those aren't [Compono\.ICompositionProvider](https://learn.microsoft.com/en-us/dotnet/api/compono.icompositionprovider 'Compono\.ICompositionProvider')
instances at all, per `docs/architecture.md`'s Resolution Pipeline table\.

<a name='Compono.ProviderAttempt.ProviderAttempt(Compono.PipelineStage,System.Type,Compono.CompositionAttemptOutcome).Outcome'></a>

`Outcome` [CompositionAttemptOutcome](Compono.CompositionAttemptOutcome.md 'Compono\.CompositionAttemptOutcome')

What the stage resulted in\.

### Remarks
Deliberately compact \- per
`docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md`'s trace\-buffer
design: [Compono\.CompositionTraceBuffer](https://learn.microsoft.com/en-us/dotnet/api/compono.compositiontracebuffer 'Compono\.CompositionTraceBuffer') appends one of these per stage tried and rewinds on
success, so this type has to be cheap enough to append without threatening the allocation\-free
success path\. [Provider](Compono.ProviderAttempt.Provider.md 'Compono\.ProviderAttempt\.Provider') is a plain [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type') reference, not a runtime
reflection \*operation\* \- identical in kind to `PlanCache<T>`'s own closed\-generic\-\`Type\`
identity and the active\-construction\-frame stack's \`Type\`\-keyed lookup, both already established
elsewhere in this engine\. See
`docs/adr/0016-provider-identity-restored-in-provider-attempt.md` for why this field exists
at all \- [Compono\.Providers\.BuiltInProviders\.Default](https://learn.microsoft.com/en-us/dotnet/api/compono.providers.builtinproviders.default 'Compono\.Providers\.BuiltInProviders\.Default') already registers three
providers in stage 7 today, not a hypothetical future case, so the trace needs a way to tell
them apart \(supersedes
`docs/adr/0015-provider-identity-deferred-in-provider-attempt.md`'s deferral, which rested
on a premise that was already false when written\)\.