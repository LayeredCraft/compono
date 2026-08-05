#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## PipelineStage Enum

Which resolution\-pipeline stage \(`docs/architecture.md`\) a [ProviderAttempt](Compono.ProviderAttempt.md 'Compono\.ProviderAttempt') was
tried at\.

```csharp
public enum PipelineStage
```
### Fields

<a name='Compono.PipelineStage.SharedOrScopedValue'></a>

`SharedOrScopedValue` 0

Stage 2: shared or scoped values\.

<a name='Compono.PipelineStage.ExactRegistration'></a>

`ExactRegistration` 1

Stage 3: exact registrations\.

<a name='Compono.PipelineStage.ConfigurationRule'></a>

`ConfigurationRule` 2

Stage 4: configuration rules \(type/member value rules, per ADR\-0020\)\.

<a name='Compono.PipelineStage.SemanticProvider'></a>

`SemanticProvider` 3

Stage 5: semantic value providers\.

<a name='Compono.PipelineStage.TestDoubleProvider'></a>

`TestDoubleProvider` 4

Stage 6: test\-double providers\.

<a name='Compono.PipelineStage.BuiltInProvider'></a>

`BuiltInProvider` 5

Stage 7: built\-in value providers, including collection dispatch\.

<a name='Compono.PipelineStage.GeneratedPlan'></a>

`GeneratedPlan` 6

Stage 8: generated composition plans\.

### Remarks
No case for stage 1 \(explicit values, no mechanism until Milestone 3\) or stage 9 \(diagnostic
failure, the terminal absence of any attempt succeeding, not an attempt itself\) \- only stages
that can actually record a [ProviderAttempt](Compono.ProviderAttempt.md 'Compono\.ProviderAttempt') today have a case\.