# [ADR-0034] Benchmark Suite Strategy and Redesign

**Status:** Accepted

**Date:** 2026-08-05

**Decision Makers:** Nick Cipollina (solo, confirmed through direct discussion)

## Context

`benchmarks/Compono.Benchmarks` grew organically across Milestones 1–2:
`ArchitectureBenchmarks`/`EcosystemBenchmarks` exist to answer one
Milestone 1 exit criterion (does generated construction beat a
reflection baseline, for a single flat `Leaf` type);
`ResolutionArchitectureBenchmarks`/`ResolutionEcosystemBenchmarks`/
`ResolutionBenchmarks` repeat that shape once Milestone 2 made nested
composition real, against a `Customer`/`Address` graph invented for that
milestone's own Execution Flow example; `DeepGraphBenchmarks` exists
solely to trigger `CompositionTraceBuffer`'s `Array.Resize` path, a
one-off PR #13 review artifact. Each class answers a real question that
mattered *at the time it was written*, but the result is eight files with
no shared model set, no consistent categorization, no source-generator
build-time coverage, no consumer-scenario coverage (a real `[Compose]`
row, a profile with providers active), and no CI job running any of it —
a milestone-by-milestone accretion, not a designed suite. This became
visible while reviewing Milestone 8 Phase 5's plan to simply *document*
these existing benchmarks under `architecture/current/performance.md`:
documenting an undesigned suite would publish that lack of design as if
it were intentional.

This ADR replaces that suite's questions and structure from first
principles, discussed and confirmed directly rather than designed in
isolation: build/source-generator benchmarks stay in the same
BenchmarkDotNet project (in-process `GeneratorDriver` invocations, not a
separate toolchain); the suite is structured to make a future CI
regression gate straightforward, but no such gate is stood up in this
phase — that is deliberately deferred, separate scope.

## Decision Drivers

- The suite should answer engineering questions first, and marketing/
  documentation questions second — a benchmark that only exists to
  produce a favorable number for `README.md` is a benchmark that
  shouldn't exist.
- Every benchmark must have a stated purpose; a benchmark that no longer
  answers a meaningful question should be deleted, not kept out of
  inertia.
- AutoFixture comparisons are one data point among several audiences,
  not the suite's organizing principle — this repo already carries an
  explicit non-goal against comparative marketing claims
  ([ADR-0030 Amendment 2](0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items)'s
  benchmark-claims policy), and the suite's *shape* should reflect that,
  not just the prose around it.
- A result unfavorable to Compono (cached reflection wins a scenario,
  AutoFixture wins a scenario) must be published and explained, not
  dropped or reframed — honesty is a harder bar than reproducibility
  alone.
- The suite must remain a permanent engineering asset — reused models,
  stable benchmark names, and a structure a future CI regression gate
  can adopt without a second redesign.
- Reflection, as a baseline, must represent what a competent hand-rolled
  alternative would actually do (caching reflection metadata), not a
  naive strawman that makes Compono look better than a fair comparison
  would — the same "baseline parity" lesson PR #13 review already
  learned the hard way for randomness cost, generalized into a
  permanent rule rather than a one-off fix.

## Considered Options

1. **Incrementally patch the existing 8 files** — add a few new
   benchmark classes (build-time, consumer scenario) alongside what
   already exists, leaving `ArchitectureBenchmarks`/`EcosystemBenchmarks`/
   `ResolutionArchitectureBenchmarks`/etc. in place.
2. **A flat, larger benchmark list** — design new categories of
   questions (as below) but keep every benchmark as an independent,
   uncategorized class in one folder, matching the project's current
   physical layout.
3. **A fully redesigned suite**, organized into explicit categories by
   audience and question, built on one reused model set, with explicit
   fair-comparison rules, replacing every existing benchmark class
   rather than layering on top of them.

## Decision Outcome

Chosen option: **3 — a fully redesigned suite**, replacing all 8 existing
benchmark files. Patching (Option 1) would leave the actual defect (no
coherent question set, milestone-specific models) untouched — the new
categories would sit next to old ones answering superseded milestone
questions, which is exactly the accretion problem this ADR exists to
fix. A flat list of new categories (Option 2) fixes the *question* gap
but not the *model* gap — without one reused representative model set,
each new category would reinvent its own types the same way `Leaf` and
`Customer`/`Address` were invented, and the suite would still lack a
structure a future contributor could extend predictably.

### Suite philosophy

The suite exists to answer engineering questions first, and marketing/
documentation questions second. Every benchmark class states the
question it answers (in its own summary XML doc, matching this repo's
existing documentation convention) — a benchmark that stops answering a
meaningful question gets deleted in the PR that makes it meaningless, not
left to accumulate. A result unfavorable to Compono is published exactly
like a favorable one: isolated, explained, and left for a maintainer to
judge whether it's worth optimizing — never dropped, hidden, or reframed.

### What this suite is (and isn't)

`BenchmarkDotNet` benchmarks answer narrow, **comparative engineering
questions** — "does approach A cost more than approach B, for this one
operation, isolated from everything else" — under an artificially clean
environment (JIT-warmed, GC-isolated, single-operation iteration).
They are **not**:

- A substitute for full-application performance testing (an app's actual
  hot path includes far more than one composition call).
- A scalability or load test (no concurrency, no sustained throughput,
  no resource contention — see Scalability below for what this suite
  *can* say about growth, which is still single-threaded and isolated).
- A guarantee about any specific consumer's real-world numbers — every
  published result is a data point from one environment (disclosed in
  full, per Reporting Rules below), not a promise.

This section exists so a reader of the published results (maintainer or
consumer) calibrates expectations correctly, rather than reading a
microbenchmark's nanosecond figure as "how fast my test suite will run."

### Representative models

`Models/` holds the canonical representative types every category draws
from, instead of each category inventing its own (the mistake this ADR
corrects) — the common language the rest of this ADR's categories and
rules are defined in terms of: `SimplePoco` (flat, no dependencies —
replaces `Leaf`), `MediumAggregate` (one nested composable dependency +
every built-in kind + a collection member — replaces `Customer`/
`Address`), `DeepGraph` (an N-level chain — replaces `DeepLevel1`-
`DeepLevel8`), `LargeCollection` (a model with a large collection
member, for Scalability), `SharedValueGraph` (a sibling-parameter shape
for Consumer Scenarios' shared-value case), and `ProviderBackedModel`
(an interface-typed member NSubstitute can satisfy, plus a
convention-matching `string` member Bogus can satisfy). A category adds
a new model only when none of these already represents its question —
not as a matter of course.

### Benchmark categories

Six benchmark categories (folders), each answering a distinct question
for a distinct audience, drawing on the representative models above,
plus cross-cutting Fair Comparison and Reporting rules that apply to all
of them:

1. **Implementation strategies** (`ImplementationStrategies/`) —
   maintainer-facing: how close does generated composition come to
   handwritten construction? What does runtime reflection actually cost,
   cached and uncached? Did an implementation change regress
   performance? These compare **implementation techniques for the same
   job**, not competing frameworks, ordered against a single theoretical
   upper bound:

   ```text
   Handwritten construction   (theoretical ceiling — hand-authored, no
                                abstraction cost at all)
           ↓
   Generated composition      (Compono's actual mechanism — the number
                                that matters: how close to the ceiling?)
           ↓
   Cached reflection          (a competent hand-rolled alternative that
                                memoizes constructor/member metadata —
                                the fair "should Compono use reflection
                                instead" comparison)
           ↓
   Uncached reflection        (the naive case — shows what caching alone
                                buys, kept as its own baseline rather
                                than folded into "reflection")
   ```

   Handwritten construction is the **theoretical upper bound**, not just
   another baseline — generated composition is always evaluated against
   how close it gets to that ceiling, not against reflection as if
   reflection were the target. Reflection (cached and uncached) is a
   second, independent comparison this category also answers: is
   Compono's own generated approach actually justified over a realistic
   reflection-based alternative? Expression-tree-based construction is a
   future candidate for this same ordering if ever explored, not built
   now.
2. **Consumer scenarios** (`ConsumerScenarios/`) — "what performance
   should a user expect in realistic applications?" A simple POCO, a
   medium aggregate, a deep object graph, large collections, shared
   values, a `Compono.Bogus`-enabled profile, a `Compono.NSubstitute`-
   enabled profile — realistic usage, not isolated mechanism cost. This
   is the category most likely to surface in public documentation.
3. **External comparison** (`ExternalComparison/`) — AutoFixture belongs
   here, as one comparison point answering "what should a developer
   expect when migrating," not the suite's center. Equivalent object
   graphs, equivalent work, both directions published honestly: if
   AutoFixture wins a scenario, that result ships too.
4. **Feature overhead** (`FeatureOverhead/`) — isolates the incremental
   cost of one Compono feature at a time via additive layering: generated
   composition alone → + shared values → + member rules → + type rules →
   + providers → + `UseBogus()` → + `UseNSubstitute()`. Answers "how
   expensive is this one feature, on its own?"
5. **Scalability** (`Scalability/`) — performance as complexity grows:
   `CreateMany` at 1/10/100/1000, shallow vs. deep graphs, growing
   collection sizes. Exists to catch **algorithmic** regressions
   (super-linear growth), not just constant-factor ones — this is where
   `DeepGraphBenchmarks`' original question (does a deep enough graph
   trigger `CompositionTraceBuffer`'s resize path) actually lives now,
   generalized into a real shallow-vs-deep comparison instead of a
   one-off artifact. Still single-threaded and isolated per "What this
   suite is (and isn't)" above — not a substitute for a real load test.
6. **Source generation** (`SourceGeneration/`) — a separate concern from
   runtime performance: clean vs. incremental generation cost, across a
   matrix of composable-type counts, measured in-process via Roslyn's
   `GeneratorDriver`/`CSharpGeneratorDriver` — same BenchmarkDotNet
   project, not a separate timing harness (confirmed directly: staying
   in one project keeps one report format and one reproduction story,
   and BenchmarkDotNet can benchmark arbitrary in-process code, including
   a generator driver run). Primarily serves maintainers.

### Fair comparison rules

1. **Baseline parity.** Any non-Compono baseline (handwritten, reflection,
   AutoFixture) does equivalent real work to what Compono actually does
   for that model — same output shape, same randomness cost where
   randomness is part of the comparison — never a placeholder or
   simplified alternative. (This is the existing `ReflectionComposer`
   lesson from PR #13 review, generalized from a one-off fix into a
   permanent rule every new baseline is held to.)
2. **Reflection means two baselines, not one.** "Reflection" is always
   reported as **cached** (a realistic hand-rolled composer that
   memoizes `ConstructorInfo`/member metadata per type) and **uncached**
   (naive, re-reflecting every call) — never conflated into a single
   "reflection" number. This is one of the strongest design decisions in
   this suite: comparing against a *competent* reflection implementation,
   not a strawman, is what makes a result honest. If cached reflection
   legitimately wins a scenario, that result is published exactly like
   any other — it's valuable information about whether Compono's
   generated-code overhead is actually worth paying in that shape.
3. **Handwritten construction is the ceiling, not a baseline among
   equals.** Every Implementation Strategies comparison reports Generated
   against Handwritten first (how close to the theoretical floor?), then
   against Cached/Uncached Reflection second (is Compono's approach
   justified over the realistic alternative?) — never presented as if
   beating reflection were the goal on its own.
4. **Comparisons stay inside their category.** A number from one
   category (e.g. Implementation Strategies' isolated construction-
   dispatch cost) is never directly compared against a number from a
   different category (e.g. a Consumer Scenario's end-to-end cost) —
   only benchmarks on the same model, in the same category, from the
   same run are compared to each other.
5. **Honest publication.** A result unfavorable to Compono is published
   exactly like a favorable one — isolated, explained, left for a
   maintainer to judge whether it's worth optimizing.
6. **Every benchmark states its question**, in its own class-level XML
   doc summary — a project convention (checked in review), not just
   aspirational text.

### Reporting rules

Every published benchmark reports the same fixed set of columns — no
future documentation gets to cherry-pick a single favorable metric out
of a richer result set:

- **Mean**
- **Error** and **StdDev** (BenchmarkDotNet's own noise-characterization
  columns — a Mean without them is not a trustworthy number)
- **Ratio** against the category's designated baseline, where the
  category has one (Implementation Strategies' Handwritten/Generated/
  Cached/Uncached Reflection rows; External Comparison's AutoFixture
  rows) — omitted only where no baseline is meaningful (e.g. Scalability's
  intrinsic `CreateMany` batch-size comparison, which is already a ratio
  against its own `count=1` case)
- **Allocated bytes**, and **Gen0**/**Gen1**/**Gen2** collection counts
  where BenchmarkDotNet reports them as nonzero — `[MemoryDiagnoser]` is
  mandatory on every benchmark class in every category, full stop, not a
  per-class judgment call. Memory behavior matters as much as throughput
  for test infrastructure that runs constantly.
- **Full environment disclosure** on every published result — .NET
  version, architecture, OS, Release configuration, BenchmarkDotNet job —
  already existing practice, generalized here into a permanent rule
  every category follows, not just the ones written so far.

A page that reports Mean alone (or Mean and Allocated alone) is not
meeting this bar, even if every other rule in this ADR is followed.

### Regression-detection readiness, not automation

Per direct discussion: this phase structures the suite so a future CI
regression gate is straightforward — stable, permanent benchmark class/
method names (renaming breaks historical comparability), and full
BenchmarkDotNet result artifacts (`*-report-github.md`/`.csv`/`.html`)
produced per category — but does **not** stand up that gate now. A real
CI job comparing against a stored baseline needs a runner with
consistent hardware, baseline storage/versioning, and a noise-tolerance
threshold policy this repo's CI doesn't have yet — that's separate,
future scope (see `docs/roadmap/future-packages.md`'s sibling page,
`docs/roadmap/post-mvp.md`, for where a concrete future candidate like
this gets tracked once there's real evidence it's needed).

### Positive Consequences

- One coherent question set per audience (maintainers, consumers,
  migrators), instead of milestone-specific artifacts a new reader has
  to reverse-engineer the history of to understand.
- A reused model set makes adding a new benchmark cheap and consistent,
  rather than inventing a new bespoke type each time.
- The cached-vs-uncached reflection split, evaluated against handwritten
  construction as the explicit ceiling, answers the implementation-
  strategy question ("should Compono reach for reflection anywhere, and
  how close does generated code get to the theoretical best case")
  more honestly than the old suite's single naive baseline with no
  stated ceiling at all.
- Public performance documentation can be capability-oriented (what does
  a consumer actually experience) without either fabricating numbers or
  drowning the reader in maintainer-facing internals.

### Negative Consequences

- A full rewrite discards the old suite's git history for each
  individual benchmark number (mitigated: the historical figures already
  published in ADRs/plans stay exactly as recorded — this ADR doesn't
  retroactively invalidate past `Accepted` decisions that cited them,
  only replaces the suite going forward).
- More benchmark classes overall (six categories plus baselines/models)
  means more to keep green and update when the engine's shape changes —
  accepted, since the alternative (the old accreted suite) already had
  this cost without the benefit of a coherent structure.
- No automated regression gate yet — a real regression could still land
  undetected until someone manually reruns the suite. Accepted per the
  direct discussion above: standing up that gate is separate, future
  scope, not blocked on but also not solved by this ADR.

## Pros and Cons of the Options

### Option 1: Incrementally patch the existing 8 files

- Good, because it's the smallest diff.
- Bad, because it doesn't remove the actual defect (milestone-specific
  models, no categorization) — new, well-designed benchmarks would sit
  next to old ones answering superseded questions.

### Option 2: Flat list of new categories, no reused model set

- Good, because it fixes the question-coverage gap (build-time, consumer
  scenarios) without a full rewrite.
- Bad, because each new category would still invent its own one-off
  types, reproducing the exact problem (`Leaf`, `Customer`/`Address`,
  `DeepLevel1`-`8`) this ADR exists to fix.

### Option 3: Fully redesigned suite (chosen)

- Good, because it fixes both the question gap and the model gap at
  once, and leaves a structure a future contributor can extend
  predictably.
- Bad, because it's the largest diff and discards the old suite's
  benchmark-by-benchmark continuity (mitigated above).

## Links

- [PLAN-0008](../plans/0008-milestone-8-public-preview.md) Phase 5 — the
  plan this ADR's implementation is tracked under.
- [ADR-0030 Amendment 2](0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items) —
  the benchmark-claims policy this ADR's public-documentation direction
  and "AutoFixture is one comparison point, not the center" framing
  implement.
- `benchmarks/Compono.Benchmarks/` — the existing suite this ADR
  replaces.
