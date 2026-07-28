# Performance

Compono's source generator exists to avoid reflection-based construction
at runtime (`docs/manifesto.md`, `docs/adr/0001-source-generation-first.md`).
`benchmarks/Compono.Benchmarks` (a `BenchmarkDotNet` project) makes that a
measured claim rather than an assumption, per Milestone 1's explicit
"benchmark harness comparing generated construction with reflection
baselines" exit criteria (`docs/mvp.md`).

## What's measured, and what isn't yet

Milestone 1 only implements direct constructor invocation for a type whose
constructor takes no arguments, or whose argument types are themselves
composed the same way - `ICompositionContext.Resolve<TValue>()` is a
placeholder that throws `NotSupportedException` for any real value
resolution (`src/Compono/Composer.cs`). Concretely: a type with a
constructor parameter - even a parameter whose type has its own generated
plan - can't be composed end-to-end yet, because generated code resolves
every constructor argument through `context.Resolve<TParam>()`
(`src/Compono.Generators/Templates/CompositionPlan.scriban`), and
dispatching that call to the matching plan is Milestone 2's provider
resolution pipeline, not built yet.

So today's benchmark compares construction of a single flat, parameterless
type (`Leaf`) two ways:

- **Generated** - `composer.Create<Leaf>()`, dispatching to a source-generated
  `ICompositionPlan<Leaf>` via `PlanCache<Leaf>`.
- **Reflection** - `typeof(Leaf).GetConstructors().Single()` +
  `ConstructorInfo.Invoke([])`, the direct alternative Compono's generator
  replaces.

This will expand once Milestone 2 makes nested/primitive composition real -
the interesting comparison (a representative graph with several nested
composable properties) isn't measurable honestly until then.

## Baseline result

Recorded at Milestone 1 Phase 4's completion, Apple M3 Max, .NET 10.0.3
arm64 RyuJIT, Release configuration, `BenchmarkDotNet` `DefaultJob`:

| Method     | Mean      | Allocated |
|------------|----------:|----------:|
| Reflection | 20.070 ns |      56 B |
| Generated  |  3.292 ns |      24 B |

Generated construction ran **~6.1x faster** and allocated **~43%** as much
as the reflection baseline.

Numbers will shift as the composition engine grows past Milestone 1's
placeholder context - re-run and update this page rather than treating it
as a permanent result.

## Reproducing

```
dotnet run -c Release --project benchmarks/Compono.Benchmarks -f net10.0
```

`-c Release` is required - `BenchmarkDotNet` refuses to run a Debug build.
