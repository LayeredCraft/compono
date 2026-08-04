#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionAttemptOutcome Enum

What a [ProviderAttempt](Compono.ProviderAttempt.md 'Compono\.ProviderAttempt') resulted in\.

```csharp
public enum CompositionAttemptOutcome
```
### Fields

<a name='Compono.CompositionAttemptOutcome.NotHandled'></a>

`NotHandled` 0

Nothing at this stage applied to the request\.

<a name='Compono.CompositionAttemptOutcome.Success'></a>

`Success` 1

This stage composed the requested value\.

<a name='Compono.CompositionAttemptOutcome.Failure'></a>

`Failure` 2

This stage established authoritative ownership of the request but couldn't complete it \(an
invalid shared/registered value, or a detected construction cycle\)\.

<a name='Compono.CompositionAttemptOutcome.Pending'></a>

`Pending` 3

This stage took ownership of the request and began composing it, but hasn't concluded \-
recorded for a generated\-plan or collection\-plan dispatch immediately before invoking
`Compose`, so an ancestor still in flight when a descendant fails isn't silently absent
from the materialized trace\. Never survives a successful resolution \- the eventual
[Success](Compono.CompositionAttemptOutcome.md#Compono.CompositionAttemptOutcome.Success 'Compono\.CompositionAttemptOutcome\.Success') entry recorded alongside it is what gets rewound away
\(`Compono.CompositionTraceBuffer`'s remarks\); only on failure does this entry \(with no
following [Success](Compono.CompositionAttemptOutcome.md#Compono.CompositionAttemptOutcome.Success 'Compono\.CompositionAttemptOutcome\.Success')/[Failure](Compono.CompositionAttemptOutcome.md#Compono.CompositionAttemptOutcome.Failure 'Compono\.CompositionAttemptOutcome\.Failure') at the same position\) stay in the trace\.