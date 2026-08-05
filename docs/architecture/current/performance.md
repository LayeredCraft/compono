# Performance

Compono's source generator exists to avoid reflection-based construction
at runtime (see [Design Principles](../design-principles.md) and
[ADR-0001](../../adr/0001-source-generation-first.md)).
`benchmarks/Compono.Benchmarks` (a `BenchmarkDotNet` project) makes that a
measured claim rather than an assumption, per Milestone 1's explicit
"benchmark harness comparing generated construction with reflection
baselines" exit criteria (`docs/mvp.md`). Per
[ADR-0030 Amendment 2](../../adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items)'s
benchmark-claims policy, this page's methodology, environment disclosure,
and comparisons stay fully published and reproducible — the root
`README.md` and every package's description make claims only about
Compono itself, never a comparative headline like "faster than
AutoFixture."

## What's measured, and what isn't yet

Milestone 1 only implements direct constructor invocation end-to-end for a
type whose constructor takes no arguments -
`ICompositionContext.Resolve<TValue>()` is a placeholder that throws
`NotSupportedException` for any real value resolution
(`src/Compono/Composer.cs`). Concretely: a type with a
constructor parameter - even a parameter whose type has its own generated
plan - can't be composed end-to-end yet, because generated code resolves
every constructor argument through `context.Resolve<TParam>()`
(`src/Compono.Generators/Templates/CompositionPlan.scriban`), and
dispatching that call to the matching plan is Milestone 2's provider
resolution pipeline, not built yet.

So today's benchmark compares construction of a single flat, parameterless
type (`Leaf`) across two groups, kept deliberately separate so ecosystem
numbers can't be mistaken for the architecture's success criterion:

- **`ArchitectureBenchmarks`** - answers "does generated construction
  outperform a comparable reflection-based implementation?", the question
  Milestone 1's architecture actually needs to prove:
  - **Direct** - `new Leaf()`, the theoretical floor.
  - **Generated** - `composer.Create<Leaf>()`, dispatching to a
    source-generated `ICompositionPlan<Leaf>` via `PlanCache<Leaf>`.
  - **Reflection** - `typeof(Leaf).GetConstructors().Single()` +
    `ConstructorInfo.Invoke([])`, the direct alternative Compono's
    generator replaces.
- **`EcosystemBenchmarks`** - answers a different question: "how does
  Compono compare with AutoFixture, the established framework developers
  reach for today?" AutoFixture does substantially more runtime work and
  has different goals (randomized value generation, unexercised here since
  `Leaf` has no properties to fill), so this is a recognizable reference
  point users will ask about, not a target Compono is trying to "beat" -
  Compono is not an AutoFixture replacement.
  - **Generated** - same as above, repeated as this group's baseline.
  - **AutoFixture** - `new Fixture().Create<Leaf>()`.

This expanded once Milestone 2 made nested/primitive composition real -
see "Milestone 2 Phase 4: resolution-pipeline result" below for the
representative-graph comparison this section couldn't measure honestly
until then.

## Baseline result

Recorded at Milestone 1 Phase 4's completion, Apple M3 Max, .NET 10.0.3
arm64 RyuJIT, Release configuration, `BenchmarkDotNet` `DefaultJob`:

**Architecture benchmark**

| Method     | Mean      | Allocated |
|------------|----------:|----------:|
| Direct     |  2.309 ns |      24 B |
| Generated  |  3.309 ns |      24 B |
| Reflection | 19.769 ns |      56 B |

Generated construction ran **~6.0x faster** than the reflection baseline
and allocated the same as direct construction - within ~1 ns of the
theoretical floor.

**Ecosystem comparison**

| Method      | Mean         | Allocated |
|-------------|-------------:|----------:|
| Generated   |     2.474 ns |      24 B |
| AutoFixture | 1,523.054 ns |   4,440 B |

Generated construction ran **~616x faster** and allocated **~0.5%** as
much as AutoFixture. This gap is expected, not the point - AutoFixture is
doing real randomized-value-generation work that this flat, parameterless
type never exercises. Take it as a recognizable reference point, not
evidence the architecture benchmark above doesn't already establish on
its own.

Numbers will shift as the composition engine grows past Milestone 1's
placeholder context - re-run and update this page rather than treating it
as a permanent result.

## Milestone 2 Phase 4: resolution-pipeline result

`ArchitectureBenchmarks`/`EcosystemBenchmarks` above only ever measured
generated *construction* dispatch versus reflection for a flat,
parameterless type - nothing exercised the real resolution pipeline
(provider dispatch, deterministic random forking, collection generation,
the diagnostics trace buffer) until Milestone 2 made it real. Three new
benchmark classes close that gap with the `Customer`/`Address`
representative graph from `docs/plans/0002-milestone-2-core-composition-engine.md`'s
Execution Flow section (a nested composable type, every Phase 2 built-in
kind via `string`, and a `List<string>` collection member), run through
the real generator (`benchmarks/Compono.Benchmarks/ResolutionBenchmarkTypes.cs`),
mirroring `ArchitectureBenchmarks`/`EcosystemBenchmarks`' split so
ecosystem numbers stay separate from the architecture question:

- **`ResolutionArchitectureBenchmarks`** - `Direct`/`Generated`/`Reflection`,
  same shape as `ArchitectureBenchmarks` above. `Direct` stays the
  theoretical floor (no fields to fill, same as `Leaf`), but `Reflection`
  here does comparable real work to `Generated`:
  `ReflectionComposer.ComposeRecursive<T>()` fills every field with a
  genuinely random value (an 8-character alphanumeric string, a
  3-element collection - Compono's own defaults), not a fixed
  placeholder. The one remaining, deliberate asymmetry: `Reflection`'s
  randomness is ordinary `Random.Shared`, not Compono's deterministic,
  seed-forked `IRandomSource` - reproducibility is a Compono product
  feature (see [Deterministic Seeding](deterministic-seeding.md)), not a
  cost every random-value generator has to pay, so `Generated`'s cost
  below includes work `Reflection`'s doesn't.
- **`ResolutionEcosystemBenchmarks`** - `Generated`/`AutoFixture`, same
  shape as `EcosystemBenchmarks` above, except this time `Customer`
  actually has fields for AutoFixture's real randomized-value-generation
  work to fill (unlike `Leaf`).
- **`ResolutionBenchmarks`** - `Create`/`CreateMany` only, no external
  baseline; the comparison here is intrinsic (`CreateMany`'s cost at
  `count=10`/`count=100` against its own `count=1` baseline), the scaling
  question `docs/plans/0002-...`'s Phase 4 benchmark task asks. This is
  also the benchmark gate [ADR-0010](../../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)
  reserved for the diagnostics trace buffer: confirm it's actually
  allocation-free on the success path, and fall back to shallow
  diagnostics by default if it measurably harms the hot path.

Recorded at Milestone 2 Phase 4's completion (updated after a second PR #13
review round - see below), Apple M3 Max, .NET 10.0.3 arm64 RyuJIT, Release
configuration, `BenchmarkDotNet` `DefaultJob`:

**Resolution architecture benchmark**

| Method     | Mean      | Allocated |
|------------|----------:|----------:|
| Direct     |  14.17 ns |     160 B |
| Generated  | 837.50 ns |   2,776 B |
| Reflection | 403.08 ns |     832 B |

Generated resolution ran ~59.1x slower than `Direct`'s theoretical-floor
hardcoded construction and allocated ~17.3x as much - the real cost of
provider dispatch, random forking, collection generation, and diagnostics
tracing for this graph, not just constructor invocation. Against a
reflection baseline doing genuinely comparable work (real random values,
not placeholders), `Generated` ran ~2.1x slower and allocated ~3.3x as
much as `Reflection` - a real, honest gap. This is the number worth
discussing if the question is "why not just use reflection": Compono's
overhead over a comparable hand-rolled reflective composer is real, and
this table is where to look for it, not the `Direct` comparison above
(which was never a fair alternative to begin with - nobody ships
hardcoded test data).

**Resolution ecosystem comparison**

| Method      | Mean         | Allocated |
|-------------|-------------:|----------:|
| Generated   |    832.50 ns |   2.71 KB |
| AutoFixture | 76,314.30 ns |  99.21 KB |

Generated construction ran **~91.7x faster** and allocated **~2.7%** as
much as AutoFixture - this time with `Customer` actually giving
AutoFixture real randomized-value-generation work to do, unlike `Leaf`.

**Resolution pipeline (`Create`/`CreateMany`)**

| Method     | Count | Mean         | Allocated  | Alloc Ratio |
|------------|------:|-------------:|-----------:|------------:|
| Create     |     1 |    848.7 ns  |    2.71 KB |        1.00 |
| CreateMany |     1 |    883.0 ns  |    2.84 KB |        1.05 |
| Create     |    10 |    853.6 ns  |    2.71 KB |        1.00 |
| CreateMany |    10 |  8,898.3 ns  |   27.59 KB |       10.18 |
| Create     |   100 |    826.2 ns  |    2.71 KB |        1.00 |
| CreateMany |   100 | 87,801.8 ns  |  275.10 KB |      101.48 |

`Create<Customer>()` allocates ~2.71 KB per call regardless of
`CreateMany`'s batch size - a single root operation's cost, unaffected by
how many other independent items get composed around it in the same
process. `CreateMany<T>(count)` scales linearly with `count` (10.18×
allocation at `count=10`, 101.48× at `count=100`, against the `count=1`
baseline) - no super-linear growth from the checkpoint/rewind trace
buffer's bookkeeping, `IRandomSource`'s per-item seed forking, or scope
allocation.

### PR #13 review: what changed the numbers above

A round of review found (and fixed) real issues, not just documentation
gaps:

- **`CompositionRequest` converted from a class to a `readonly record
  struct`.** It's never stored beyond the synchronous `ResolveCore` call
  that builds it, so the heap allocation was pure waste. Measured directly
  (constructed and consumed the same way `ResolveCore` does, passed by
  `in`, not boxed): **40 B → 0 B**, **14.9 ns → 5.5 ns**. This is what
  moved every table above from their original numbers (`Generated` was
  2,792 B/873.0 ns in an earlier version of this page) to the ones shown
  now.
- **A generic root type rendered in raw CLR form in the diagnostic
  heading and failure message** (`Unable to compose List\`1.` instead of
  `List<Missing>`) - fixed by reusing `CompositionPath.FriendlyTypeName`
  at both call sites (`CompositionDiagnostic.ToString()`'s heading, and
  the stage-9 failure message).
- **An ancestor's in-flight generated-plan/collection-plan dispatch was
  silently absent from the trace** when a descendant failed - fixed by
  recording a new `CompositionAttemptOutcome.Pending` immediately before
  `Compose` runs, rewound away on success but surviving (correctly) when
  a descendant's failure means the ancestor's own `Success` never gets
  recorded either.
- **Stage 7's three built-in providers collapsed into one trace entry.**
  `BuiltInProviders.Default` genuinely has three providers registered
  (`PrimitiveValueProvider`, `EnumValueProvider`, `NullableValueProvider`)
  — now each provider's own decline is recorded as it's tried, so a
  failing request's trace shows the real number of attempts made, not a
  collapsed count of 1.
- **`FriendlyTypeName` didn't handle arrays of generic types.**
  `Type.IsGenericType` is `false` for an array type itself, so
  `List<Missing>[]` fell straight through to the raw CLR form
  (`List\`1[]`). Fixed by checking `IsArray` first and recursing into
  `GetElementType()`.

**Isolating the trace buffer's own share** of `Create<Customer>()`'s
2.71 KB - measured directly via `GC.GetAllocatedBytesForCurrentThread()`
around `new CompositionTraceBuffer()` in isolation, 1,000,000 iterations,
Release, .NET 10.0.3 arm64 (re-measured after bumping the buffer's initial
capacity from 16 to 32, and again after
[ADR-0016](../../adr/0016-provider-identity-restored-in-provider-attempt.md)
widened `ProviderAttempt` - see below):

| What                                                     | Bytes/instance |
|-----------------------------------------------------------|---------------:|
| `CompositionTraceBuffer` alone (its `ProviderAttempt[32]`) |          ~536 B |
| Bare `new CompositionContext()` (scope + active-frames + trace, no resolution) | ~780 B |

Still real, not literally zero (ADR-0010's "near-zero-allocation on
success, not zero-cost," not a stronger claim than that) - and **not
bounded at that number for every graph**: see below.

### Deep graph result

`ResolutionBenchmarks`' `Customer`/`Address` graph is only 2 levels deep -
never enough to exercise `CompositionTraceBuffer`'s growth path, since
each active ancestor frame dispatching through stage 8/a collection plan
retains ~6 entries (5 declined stages + a `Pending` marker) until its own
child returns. `DeepGraphBenchmarks` composes an 8-level-deep chain of
single-field composable types (`DeepLevel1` through `DeepLevel8`) instead
- deep enough (~48 entries at its deepest point) to exceed the buffer's
32-entry initial capacity and trigger a real `Array.Resize`:

| Method | Mean     | Allocated |
|--------|---------:|----------:|
| Create | 846.1 ns |   3.37 KB |

That's a genuinely deeper graph allocating *more* than the shallower,
wider `Customer` graph (2.71 KB) despite composing fewer total leaf values
(8 strings vs. `Customer`'s 7 strings + a 3-element list) - the resize is
a real, measurable contributor, not a theoretical one. The fix isn't to
pretend resizing can't happen (any growable array-backed collection
resizes eventually); it's to size the initial capacity for a typical
graph (bumped 16 → 32, covering ~5 levels of nesting without resizing -
see [The Provider Pipeline](provider-pipeline.md#diagnostics)'s own
4-level Diagnostics example) and be honest that deeper graphs still pay a
real, amortized `Array.Resize` cost, which this table now measures rather
than assumes away.

**No fallback to shallow diagnostics was needed** - the
allocation-free-on-success trace buffer design shipped as scoped, with
the growth-path caveat above now documented rather than glossed over.

### Provider identity restored: a real (accepted) allocation increase

[ADR-0016](../../adr/0016-provider-identity-restored-in-provider-attempt.md)
reverses [ADR-0015](../../adr/0015-provider-identity-deferred-in-provider-attempt.md)'s
deferral: `ProviderAttempt` gained a `Type? Provider` field so a failing
request's trace can tell stage 7's three real built-in providers apart,
not just count that three attempts happened. Widening the struct
(`PipelineStage` + `Type?` + `CompositionAttemptOutcome`) roughly doubles
each trace entry's size, which is exactly what moved
`CompositionTraceBuffer`'s own isolated cost from ~280 B to ~536 B per
instance, and `Create<Customer>()`'s total from 2.46 KB to 2.71 KB (+256 B,
~10%) - fully attributable to the wider struct, confirmed by the isolated
`CompositionTraceBuffer` measurement moving by almost exactly the same
256 B on its own. This is a real, accepted cost, not an oversight: three
indistinguishable `(BuiltInProvider, NotHandled)` trace entries were a
genuine diagnostic-quality gap, and paying ~10% more allocation on the
failure-adjacent bookkeeping path for a working "which provider" answer
is the tradeoff ADR-0016 accepts explicitly, not a regression to chase
back down.

## Reproducing

```
dotnet run -c Release --project benchmarks/Compono.Benchmarks -f net10.0
```

`-c Release` is required - `BenchmarkDotNet` refuses to run a Debug build.
Add `-- --filter "*Resolution*" "*DeepGraph*"` to run only the Milestone 2
resolution-pipeline benchmarks (the full suite, including
`ArchitectureBenchmarks`/`EcosystemBenchmarks`, takes several minutes).

## Open questions

**`CompositionTraceBuffer`'s own array allocation, per root operation** is
a genuine, unconditional allocation on every `Create<T>()`/`CreateMany<T>()`
item, not literally zero — consistent with ADR-0010's explicit
"near-zero-allocation on success, not zero-cost" framing, but real enough
that this page states the precise figure instead of an unqualified
"allocation-free" claim. A true zero-allocation design would still need
the buffer pooled/reused across root operations
(`CompositionContext` isn't currently pooled or reset-and-reused at all)
— a real architecture change, not a same-PR-sized fix. Deferred: revisit
if a future benchmark shows this mattering at a scale `docs/mvp.md`'s
scope actually exercises — no design has been chosen yet.
