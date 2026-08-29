# The Provider Pipeline

Resolved by [ADR-0010](../../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md).
[Concepts: Providers](../../concepts/providers.md) covers what a provider
is conceptually; this page answers "what order do providers execute,"
at full depth.

## Resolution order

The default resolution order is fixed — not configurable, by users or by
providers reordering themselves:

| # | Stage | Kind |
|---|---|---|
| 1 | Explicit values | Context-owned deterministic check |
| 2 | Shared or scoped values | Context-owned deterministic check against the scope. Any request (`[Shared]` or not) sees an already-shared value for its type on read; a request only populates scope on write when its `IsShared` flag is set — either directly (`[Shared]`, `ResolveShared`) or because its type was declared via [`CompositionBuilder.Share<T>()`](../../adr/0056-composition-builder-share-graph-wide-sharing.md), which broadens `IsShared` to every request for that type, from any resolution stage, for the whole graph. |
| 3 | Exact registrations | **Hybrid**: a context-owned deterministic lookup against the exact-registration table, then — only on a miss, if `UseServiceProvider(...)` was configured — a fallback `IServiceProvider.GetService(typeof(T))` call. |
| 4 | Configuration rules | Ordered `ICompositionProvider` collection populated by type/member value rules compiled from `builder.For<T>()...`, whether reached directly or via a profile. |
| 5 | Semantic value providers | Ordered `ICompositionProvider` collection. Public registration surface: `builder.AddSemanticProvider(ICompositionValueProvider)`. `Compono.Bogus`'s `BogusMemberNameProvider` is this stage's first real registrant. |
| 6 | Test-double providers | Ordered `ICompositionProvider` collection. Public registration surface: `builder.AddTestDoubleProvider(ICompositionValueProvider)`. `Compono.NSubstitute`'s `NSubstituteProvider`, `Compono.TestDoubles`'s `GeneratedTestDoubleProvider`, and `Compono.Logging`'s `LoggingProvider` are real registrants — registration order between them decides which one resolves an interface request first when more than one is installed, same as any other stage. For `ILogger`/`ILogger<T>` specifically, [ADR-0055](../../adr/0055-compono-logging-testing-support-package.md) Amendment 4 makes `GeneratedTestDoubleProvider` unable to handle those types at all once `ComponoGeneratedLogging` is enabled (they're Logging-owned, excluded from `Compono.TestDoubles` generation) — so order against `UseGeneratedTestDoubles()` isn't observable for them. Register `UseLogging()` before `UseNSubstitute()` when `ILogger`/`ILogger<T>` should resolve to a `CapturingLogger`/`CapturingLogger<T>` rather than a substitute — `NSubstituteProvider` is unaffected by Amendment 4 and still resolves them if registered first. |
| 7 | Built-in value providers | **Hybrid**: an ordered provider collection (primitives, enums, nullable value types) tried first, followed by a context-owned deterministic dispatch through `CollectionPlanCache<T>` for the five built-in collection shapes. |
| 8 | Generated composition plans | Context-owned deterministic dispatch via `PlanCache<T>` — **not** an `ICompositionProvider`; see [Generated Plans and Discovery](generated-plans-and-discovery.md). |
| 9 | Diagnostic failure | Context-owned terminal stage |

Only stage 7 has anything registered *unconditionally*
(`BuiltInProviders.Default`); every other stage is opt-in, populated only
when a consumer actually calls `.For<T>()` (stage 4), `UseNSubstitute()`/
`UseGeneratedTestDoubles()` (stage 6), `UseBogus()` (stage 5), or registers
a hand-written provider directly. Provider order *within* an extensible stage is registration
order — stage 7 alone holds three real providers
(`PrimitiveValueProvider`, `EnumValueProvider`, `NullableValueProvider`),
so "no stage has more than one provider" isn't true today. No *richer*
ordering rule (priority, specificity) exists yet because these three
providers claim disjoint type sets — a richer rule becomes a real
question only once two providers could plausibly both claim the same
type differently.

## Providers

Providers satisfy composition requests within one of the extensible
pipeline stages above (4/5/6/7) — the context-owned stages (1/2/3/8/9)
are not providers. A provider reports whether it did not apply
(`NotHandled`) or successfully composed a value (`Success`); ordinary
providers **cannot** report `Failure` — that's reserved for the
context-owned authoritative stages (an exact registration whose factory
throws, or generated-plan dispatch when a plan exists but fails or a
recursion cycle is detected). The rule: `Failure` means "authoritative
ownership was established, but resolution could not complete," never a
stronger form of `NotHandled` — this is what stops a provider that merely
can't produce *this* particular request from accidentally blocking a
later stage that could have.

**Public providers (stages 5/6).** Stages 4/7 are implemented entirely
inside `Compono` and never exposed for an outside package to author its
own. Stages 5/6 exist specifically for an integration package to
contribute open-ended, pattern-matching logic ("any interface type"),
resolved by
[ADR-0024](../../adr/0024-public-provider-extensibility-model.md):

```csharp
public interface ICompositionValueProvider
{
    CompositionProviderResult TryProvide(
        in CompositionProviderRequest request,
        ICompositionContext context);
}
```

`CompositionProviderRequest`/`CompositionProviderResult` are decoupled
from the internal `CompositionRequest`/`CompositionResult` pair — no
path, no shared-scope flag, no pipeline plumbing a provider author has no
legitimate use for. Internally, each public provider is wrapped in a
`PublicProviderAdapter : ICompositionProvider`, so the rest of the
pipeline treats it exactly like an internal one, with diagnostics naming
the real wrapped provider's type. A thrown exception from `TryProvide`
propagates uncaught — same "exceptions signal a bug" principle as
everywhere else in this pipeline.

## Recursion detection

A repeated *type* appearing twice in a graph (two sibling properties of
the same type, or the same type reachable via two different paths) is
ordinary graph shape, not a cycle. A genuine cycle is a type whose
*construction* is still actively in progress when it's requested again.
Resolved by
[ADR-0011](../../adr/0011-composition-scope-shared-values-and-recursion-detection.md):
`CompositionPath` records every request edge for diagnostics and random
forking, while a distinct internal active-construction-frame stack is
pushed only around structural construction (generated-plan dispatch,
stage 8) and checked only there — after explicit values, shared/scoped
values, and exact registrations have already had a chance to terminate
the graph. A self-referencing type resolved by a registered or shared
instance never touches the recursion mechanism at all; only an actual
in-progress construction cycle does, and the resulting diagnostic reports
the chain of active frames that formed the cycle, not just a list of
repeated types.

## Diagnostics

Diagnostics track the root request, current request path, provider
decisions, selected plan, constructor selection, scope reuse,
registration matches, seed, failure reason, and suggested remediation:

```text
Unable to compose CreateOrderHandler.

CreateOrderHandler
└── IOrderProcessor processor
    └── OrderValidator validator
        └── IRuleProvider rules

No registration, semantic provider, test-double provider,
built-in provider, or generated plan could satisfy IRuleProvider.

Seed: 8492173
```

This is designed to cost as little as possible on the normal successful
path — "near-zero-allocation on success, not zero-cost": a context-owned,
reusable, array-backed trace buffer (`CompositionTraceBuffer`) records a
compact struct (`ProviderAttempt`: stage, provider type, outcome — no
strings, no per-append allocation) per stage attempt, and rewinds on
success instead of retaining anything. Only a failing request
materializes its slice of that buffer into the durable
`CompositionDiagnostic` (`exception.Diagnostic`) before the buffer
unwinds further. `ProviderAttempt.Provider` is the concrete
`ICompositionProvider` type that made the attempt (`null` for a
context-owned stage, which isn't a provider instance at all) — see
[Performance](performance.md) for the measured allocation cost of this
mechanism.

## Open questions

**Public versus internal use of `Type`** in provider-facing contracts
remains an open design question, not yet resolved by an ADR.
