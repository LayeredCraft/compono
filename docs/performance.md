# Performance

Compono's source generator exists to avoid reflection-based construction
at runtime (`docs/manifesto.md`, `docs/adr/0001-source-generation-first.md`).
`benchmarks/Compono.Benchmarks` (a `BenchmarkDotNet` project) makes that a
measured claim rather than an assumption, per Milestone 1's explicit
"benchmark harness comparing generated construction with reflection
baselines" exit criteria (`docs/mvp.md`).

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
  placeholder. An earlier version of this baseline used fixed
  placeholders, which made `Reflection` faster than `Generated` for
  doing categorically less work rather than because reflective dispatch
  actually beats source-generated dispatch - a misleading comparison
  caught in PR #13 review and fixed by rewriting the baseline to do real
  value generation. The one remaining, deliberate asymmetry:
  `Reflection`'s randomness is ordinary `Random.Shared`, not Compono's
  deterministic, seed-forked `IRandomSource` - reproducibility is a
  Compono product feature (`README.md`'s "Deterministic by design"), not
  a cost every random-value generator has to pay, so `Generated`'s cost
  below includes work `Reflection`'s doesn't.
- **`ResolutionEcosystemBenchmarks`** - `Generated`/`AutoFixture`, same
  shape as `EcosystemBenchmarks` above, except this time `Customer`
  actually has fields for AutoFixture's real randomized-value-generation
  work to fill (unlike `Leaf`).
- **`ResolutionBenchmarks`** - `Create`/`CreateMany` only, no external
  baseline; the comparison here is intrinsic (`CreateMany`'s cost at
  `count=10`/`count=100` against its own `count=1` baseline), the scaling
  question `docs/plans/0002-...`'s Phase 4 benchmark task asks. This is
  also the benchmark gate [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)
  reserved for the diagnostics trace buffer: confirm it's actually
  allocation-free on the success path, and fall back to shallow
  diagnostics by default if it measurably harms the hot path.

Recorded at Milestone 2 Phase 4's completion, Apple M3 Max, .NET 10.0.3
arm64 RyuJIT, Release configuration, `BenchmarkDotNet` `DefaultJob`:

**Resolution architecture benchmark**

| Method     | Mean      | Allocated |
|------------|----------:|----------:|
| Direct     |  13.84 ns |     160 B |
| Generated  | 873.03 ns |   2,792 B |
| Reflection | 383.80 ns |     832 B |

Generated resolution ran ~63.1x slower than `Direct`'s theoretical-floor
hardcoded construction and allocated ~17.5x as much - the real cost of
provider dispatch, random forking, collection generation, and diagnostics
tracing for this graph, not just constructor invocation. Against a
reflection baseline doing genuinely comparable work (real random values,
not placeholders), `Generated` ran ~2.3x slower and allocated ~3.4x as
much as `Reflection` - a real, honest gap, not the ~3.9x-faster inversion
the placeholder-value baseline previously (and misleadingly) showed. This
is the number worth discussing if the question is "why not just use
reflection": Compono's overhead over a comparable hand-rolled reflective
composer is real, and this table is where to look for it, not the
`Direct` comparison above (which was never a fair alternative to begin
with - nobody ships hardcoded test data).

**Resolution ecosystem comparison**

| Method      | Mean         | Allocated |
|-------------|-------------:|----------:|
| Generated   |    859.48 ns |   2.73 KB |
| AutoFixture | 78,095.80 ns |  99.21 KB |

Generated construction ran **~90.9x faster** and allocated **~2.75%** as
much as AutoFixture - this time with `Customer` actually giving
AutoFixture real randomized-value-generation work to do, unlike `Leaf`.

**Resolution pipeline (`Create`/`CreateMany`)**

| Method     | Count | Mean         | Allocated  | Alloc Ratio |
|------------|------:|-------------:|-----------:|------------:|
| Create     |     1 |    878.4 ns  |    2.73 KB |        1.00 |
| CreateMany |     1 |    927.7 ns  |    2.86 KB |        1.05 |
| Create     |    10 |    896.2 ns  |    2.73 KB |        1.00 |
| CreateMany |    10 |  9,298.4 ns  |   27.75 KB |       10.18 |
| Create     |   100 |    930.0 ns  |    2.73 KB |        1.00 |
| CreateMany |   100 | 97,435.5 ns  |  276.66 KB |      101.47 |

`Create<Customer>()` allocates ~2.73 KB per call regardless of
`CreateMany`'s batch size - a single root operation's cost, unaffected by
how many other independent items get composed around it in the same
process. `CreateMany<T>(count)` scales linearly with `count` (10.18×
allocation at `count=10`, 101.47× at `count=100`, against the `count=1`
baseline) - no super-linear growth from the checkpoint/rewind trace
buffer's bookkeeping, `IRandomSource`'s per-item seed forking, or scope
allocation.

**Isolating the trace buffer's own share of that 2.73 KB** (a PR #13
review point: the table above alone can't tell you how much of it is
diagnostics-tracing overhead versus real value generation) - measured
directly via `GC.GetAllocatedBytesForCurrentThread()` around
`new CompositionTraceBuffer()` in isolation, 1,000,000 iterations,
Release, .NET 10.0.3 arm64:

| What                                                     | Bytes/instance |
|-----------------------------------------------------------|---------------:|
| `CompositionTraceBuffer` alone (its `ProviderAttempt[16]`) |          184 B |
| Bare `new CompositionContext()` (scope + active-frames + trace, no resolution) | 424 B |

The trace buffer is ~43% of an empty context's own setup cost, and ~6.6%
of a real `Customer` composition's full 2.73 KB - real, not literally
zero (`ADR-0010`'s "near-zero-allocation on success, not zero-cost," not
a stronger claim than that), but a small and bounded fraction dominated
by the actual random-value-generation work happening around it. A true
zero-allocation trace buffer would need pooling/reuse across root
operations - `docs/architecture.md`'s Open Architectural Decisions tracks
this as a deferred item, not a same-PR fix.

**No fallback to shallow diagnostics was needed** - the
allocation-free-on-success trace buffer design shipped as scoped.

## Reproducing

```
dotnet run -c Release --project benchmarks/Compono.Benchmarks -f net10.0
```

`-c Release` is required - `BenchmarkDotNet` refuses to run a Debug build.
Add `-- --filter "*Resolution*"` to run only the Milestone 2
resolution-pipeline benchmarks (the full suite, including
`ArchitectureBenchmarks`/`EcosystemBenchmarks`, takes several minutes).
