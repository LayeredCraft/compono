# [ADR-0056] `CompositionBuilder.Share<T>()`: Graph-Wide Sharing as a Core Composition Concept

**Status:** Accepted

**Date:** 2026-08-28

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

Sharing today is expressed almost entirely through `[Shared]`, a
`Compono.XunitV3`/`Compono.TUnit`-only theory-parameter attribute (ADR-0021/
ADR-0022/ADR-0040) that rides on core mechanics (`CompositionScope`,
`CompositionContext`, `CompositionRow`) which have existed since Milestone 2
(ADR-0011) but were never exposed as a standalone, framework-independent
configuration surface. The real Compono.Logging dogfood
(`docs/adr/0055-compono-logging-testing-support-package.md`,
`PerformanceLoggingBehaviorTests.cs`) surfaced the concrete cost of that
gap: a test needing the exact `ILogger<T>` instance a SUT received had no
way to express "this type is shared" except by attaching `[Shared]` to a
theory parameter — even when the sharing intent was really a property of
the *composition configuration* (a profile reused across many tests), not
of any one test.

This is an **Accepted pre-1.0 product requirement**, not re-litigated
here: `CompositionBuilder.Share<T>()` (or a very closely equivalent
builder-level API) is intended to make sharing a first-class,
framework-independent composition concept, with `[Shared]` remaining as
convenient xUnit/TUnit syntax rather than the only practical way to
express shared identity. This ADR settles the mechanism's exact
semantics.

The full evidence trail — an audit of `[Shared]`'s exact current
mechanics, AutoFixture's `Freeze<T>()`/`[Frozen]` prior art, a real-usage
inventory across Compono's own tests and the `alexa-vox-craft` dogfood
consumer, four alternative retrieval designs investigated and three
rejected with concrete proof, and four empirical spikes run against a
real compiled prototype (all four passed, including their control cases)
— lives in
`docs/research/0014-shared-ergonomics-and-composition-builder-share-research.md`.
This ADR summarizes and settles the decision content from that research;
it does not re-derive the evidence.

## Decision Drivers

- The product owner's explicit requirement: `Share<T>()` must be a **true
  graph-wide resolution-semantics change for `T`**, not an opt-in a
  particular request can choose to make — every request for a
  `Share<T>()`-configured type within a graph must automatically
  participate, regardless of source, with `[Shared]` never required
  anywhere in that graph for that type.
- Production types must never need a Compono-specific annotation —
  `ServiceA(IMyInterface dependency)`/`ServiceB(IMyInterface dependency)`
  must receive identical shared identity with zero markers on either
  constructor.
- `docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md`'s
  already-`Accepted` scope lifetime (one `CompositionContext` per root
  operation) must not be silently widened — a composer-instance-wide
  singleton would be a new lifetime category this repo has never had, and
  would directly complicate the separately-tracked, not-yet-designed
  pre-1.0 disposal work (ADR-0022 Amendment 4's "ownership can't be
  determined safely" finding already constrains this).
- No service locator, no ambient/ `AsyncLocal` state, no hidden
  test-framework parameter injection — investigated explicitly and
  rejected with concrete proof (RESEARCH-0014 §6a/§6c), not merely
  disfavored by style.
- `[Shared]`'s existing, shipped, tested behavior must not regress for any
  current consumer.

## Considered Options

**For the sharing/retrieval mechanism as a whole:**
1. `Share<T>()` as pure graph-wide configuration, `[Shared]` unchanged as
   the only retrieval mechanism (RESEARCH-0014's first-pass conclusion).
2. `Share<T>()` as a graph-wide resolution-semantics change that also
   makes an *ordinary, undecorated* parameter/constructor request
   sufficient for retrieval (RESEARCH-0014's revised, evidence-tested
   conclusion).
3. A config-time strongly-typed handle (`Shared<T>` returned by
   `Share<T>()` itself, exposing `.Value` later).
4. Framework-level auto-injection with no declared parameter (a hidden
   extra theory parameter, or ambient/`AsyncLocal` retrieval read from
   inside the test body).

**For the lifetime boundary:**
1. One composition graph (one `Create<T>()` root, one `CreateMany` item,
   one `CompositionRow`) — identical to `[Shared]`'s existing boundary.
2. Composer-instance-wide (a shared value survives across multiple
   `Create<T>()`/`CreateMany<T>()` calls against the same `Composer`).

## Decision Outcome

**Chosen: Option 2 for the mechanism (graph-wide, ordinary-parameter
participation), Option 1 for the lifetime boundary (graph-scoped, never
composer-wide).** Both are now empirically confirmed, not just reasoned
through — four spikes against a real compiled prototype, including three
control cases proving the observed identity is actually caused by
`Share<T>()` and not some other latent behavior, all passed
(RESEARCH-0014 §11).

### Core semantic contract (normative)

`CompositionBuilder.Share<T>()` declares that `T` has **one shared
identity within a single composition graph**. Once configured:

- The first request for `T` in the graph resolves through the normal
  composition pipeline, exactly as it would without `Share<T>()`.
- That produced value becomes the graph's shared `T`.
- Every subsequent request for `T` in that same graph receives the exact
  same instance.
- **Request source does not matter.** The following all participate
  automatically, uniformly, with no special-casing between them:
  - generated constructor-plan requests (a real, compiled generated
    plan's own nested `context.Resolve<T>(descriptor)` call — proven
    empirically, spike 1, RESEARCH-0014 §11c);
  - nested/transitive dependencies, at any depth;
  - `ICompositionValueProvider` (stage 4-6) results;
  - exact `Register<T>(...)` results (spike 2);
  - the configured `IServiceProvider` fallback's results;
  - ordinary, **undecorated** `[Compose]` theory parameters, in either
    declaration order relative to their structural dependents (spike 3a).
- **No `[Shared]` attribute is required anywhere** for a
  `Share<T>()`-configured type. `[Shared]` remains valid to use (see
  below), but participation never depends on it.
- **Production types require no Compono-specific annotations of any
  kind.** Two ordinary, unattributed constructors —
  `ServiceA(IMyInterface dependency)`/`ServiceB(IMyInterface dependency)`
  — reached from the same composed root, with **no** test/theory
  parameter of `IMyInterface` anywhere, receive the identical shared
  instance purely because `IMyInterface` is graph-configured shared.
  Proven empirically, not just asserted: spike 3b, RESEARCH-0014 §11c,
  the strongest and least-precedented case in this ADR's evidence.

### Lifetime boundary

**Graph-scoped, using the existing `CompositionContext`/`CompositionScope`
boundary — never composer-instance-wide.**

- One `Composer.Create<T>()` call = one graph.
- One `CreateMany<T>()` item = one graph; **separate items never share**
  (empirically confirmed: the `CreateMany<T>(3)` boundary spike,
  RESEARCH-0014 §11c, showed sharing holds *within* each of three
  independent items while `ReferenceEquals` is `false` *across* any two
  items).
- One `CompositionRow` = one graph (the boundary `[Shared]` has always
  used, ADR-0011/ADR-0021, unchanged).
- Separate `Create<T>()` calls against the same `Composer` never share —
  `Share<T>()` introduces no composer-instance-wide singleton semantics.
  This is the literal, already-`Accepted` boundary `CompositionScope`'s
  own doc comment has stated since Milestone 2; `Share<T>()` does not
  widen it.

### Creation timing: lazy

`builder.Share<T>()` records configuration only — it runs during
`CompositionBuilder.Build()`, before any `CompositionContext` exists, so
there is nothing to construct at that point. Calling `Share<T>()` **must
not create `T`**. The first real request for `T` within the graph creates
it, through the normal pipeline, exactly as described above. If the graph
never requests `T`, `T` is never created. This is not a preference
layered on top of the mechanism — it is the only timing the mechanism
*can* have, given that `Share<T>()` has no `CompositionContext` to act
against until a real composition operation begins.

### Resolution source and precedence: unchanged

`Share<T>()` changes lifetime/identity semantics only — it never alters
which pipeline stage wins. Whatever would normally produce `T` (an exact
`Register<T>()`, the `IServiceProvider` fallback, a
`ICompositionValueProvider`, or ordinary generated-plan composition)
still produces it; `Share<T>()` only causes that result to be cached as
the graph's shared value afterward.

**`Register<T>()` and `Share<T>()` are orthogonal and order-independent.**
Empirically confirmed (spike 2, RESEARCH-0014 §11c): both

```csharp
builder.Register<T>(...).Share<T>();
```

and

```csharp
builder.Share<T>().Register<T>(...);
```

resolve `T` through the registration (not the generated plan) and
establish that registered instance as the graph's shared value, in both
orderings, with no observable difference. No precedence rule is
introduced between them — `Register<T>()`'s own existing, `Accepted`,
strict duplicate-registration contract (ADR-0019: registering the same
type twice is a build-time conflict, not last-write-wins) is untouched;
`Share<T>()` writes to an entirely separate configuration bucket (a set
of shared types) with no interaction logic needed between the two.

### Relationship with `[Shared]`

`[Shared]` remains fully supported with its existing, shipped, observable
semantics unchanged. Its conceptual role is now stated precisely:

- **Declarative, ad hoc, row-local sharing** for a type the
  composition/profile has **not** already configured shared via
  `Share<T>()` — the right tool when centralizing a `Share<T>()` call in
  a profile would be unwarranted ceremony for one test's local need.
- Because binding a parameter is how a `[Compose]` test retrieves any
  value at all, `[Shared]` naturally also gives the test body a handle to
  the value it establishes, in the same declaration.
- **`[Shared]` is never a required opt-in to consume or retrieve a type
  already configured through `Share<T>()`.** An ordinary, undecorated
  parameter is sufficient:

  ```csharp
  builder.Share<IMyInterface>();

  public void Test(
      IMyInterface dependency,
      MyService sut)
  {
      // dependency is guaranteed to be the exact instance injected into sut.
  }
  ```

  This ADR makes that guarantee normative, not incidental — `dependency`
  and `sut`'s own injected `IMyInterface` are the same instance by
  contract, empirically confirmed in both declaration orders (spike 3a).

### Duplicate `Share<T>()` calls

Calling `Share<T>()` more than once for the same `T` (directly, or once
from a profile and again from another) is **idempotent**, not a conflict.
Unlike `Register<T>()`'s strict duplicate-throw contract (ADR-0019), the
underlying store is a set of shared types; adding the same type twice is
naturally a no-op. `Register<T>()`'s duplicate-detection semantics are not
applied to `Share<T>()` — there is no coherent "which `Share<T>()` call
wins" question to answer, since every call asserts the identical fact
about the same type.

### Rejected alternatives

- **A config-time strongly-typed handle** (`Shared<T>` returned by
  `Share<T>()` itself, exposing `.Value` once a graph composes it) —
  rejected. One configuration-time handle cannot coherently represent
  independent values across `Composer` reuse (a second, independent
  `Create<T>()` call silently overwrites the first's value with no
  signal), `CreateMany<T>()` (proven, not asserted: `CreateMany(5)`
  produces five independent instances, and one handle cannot hold five
  values), or concurrent composition (a real data race under xUnit v3's
  default test parallelism, with no synchronization contract). It is also,
  in substance, a service locator with a generic type parameter attached —
  compile-time typing does not change the shape of the problem
  (RESEARCH-0014 §6a).
- **Framework/ambient retrieval** (a hidden extra theory parameter the
  test method never declared, or `AsyncLocal`-based retrieval read from
  inside the test body with no parameter) — rejected. The hidden-parameter
  shape has no real xUnit v3 extension point: a theory row's arity is
  fixed by the method's own declared parameter count, confirmed against
  the actual binding mechanics, not assumed. The ambient/`AsyncLocal`
  shape is a strictly *worse* service locator than the handle above (fully
  ambient, reachable from any code on that logical call context, no
  reference/type coupling to a specific graph at all) with unproven
  correctness under xUnit v3's real thread-pool/parallel-execution model,
  and no existing Compono precedent to build on (RESEARCH-0014 §6c).
- **Composer-instance-wide sharing** — rejected as the lifetime boundary.
  It would introduce a new lifetime category this repo has never had (a
  shared value outliving the graph that produced it, potentially
  referenced by an unbounded number of later, otherwise-unrelated
  `Create<T>()` calls against the same `Composer`), inconsistent with
  `CompositionScope`'s existing, `Accepted`, per-root-operation boundary,
  and would materially complicate the separately-tracked future disposal
  ADR's design space for no evidence-backed benefit (RESEARCH-0014 §8).

**Noted but explicitly not part of this ADR's accepted feature:** a
graph-scoped `CompositionRow` accessor (e.g. a `TryGetShared<T>()`-style
member, for hand-written `Composer.CreateRow` usage with no
`[Compose]`-attribute binding at all) is architecturally sound —
unlike the rejected handle, it would be requested from an object that
already represents exactly one graph, created at graph-creation time, with
no cross-call/cross-thread shared mutable state. It does not help the
`[Compose]`-attribute retrieval story this ADR settles (§ above), and is
deferred as a separate, smaller, optional future addition — not designed
further here (RESEARCH-0014 §6b).

## Disposal

**Not solved by this ADR.** `Share<T>()` retains the existing
graph-scoped lifetime boundary — it introduces no new lifetime category
beyond the disposal questions ADR-0022 Amendment 4 already left open for
ordinary `[Shared]` values (a `CompositionRow`/`Composer` has no
visibility into which pipeline stage produced a value, so it cannot
safely decide whether disposing it is even Compono's responsibility). The
separate, pre-1.0 disposal investigation remains solely responsible for
ownership/disposal semantics, for both `[Shared]` and `Share<T>()`
values alike; nothing in this ADR forecloses or constrains that future
work's options, which was itself an explicit evaluation criterion for the
lifetime-boundary decision above.

## Implementation constraint discovered by the spike (normative, not optional)

The spike prototype proved that **not every resolution stage reads the
same sharing flag by default.** The exact-registration stage and the
configured-`IServiceProvider`-fallback stage each have their own write
path into `CompositionScope`, and in the first prototype attempt, both
read a stale, independently-captured copy of the raw sharing flag rather
than the (correctly broadened) per-request flag every other stage already
used — a real bug, found and fixed during the spike, not a hypothetical
risk (RESEARCH-0014 §11b).

**The real implementation must therefore explicitly audit every pipeline
stage capable of storing a produced value into `CompositionScope`** —
generated-plan dispatch, every `ICompositionValueProvider` stage, exact
registrations, and the `IServiceProvider` fallback — and confirm each one
observes the graph-wide `Share<T>()` contract identically. This is not
phrased as "change one request field and every stage automatically
complies" — the spike disproved that framing directly. All resolution
sources must obey the same graph-wide `Share<T>()` contract; this is a
correctness requirement of the contract itself, not an implementation
suggestion.

## Compatibility and Architecture

- **Fully additive public API** — one new `CompositionBuilder` method, one
  new `CompositionConfiguration` field. No existing public member's
  signature or behavior changes.
- **No breaking change to `[Shared]`** — its existing, shipped behavior
  (declaration order among `[Shared]` parameters, duplicate-type
  rejection, row-local visibility) is entirely unchanged.
- **No generator-facing API required.** A generated plan's own nested
  `context.Resolve<T>(descriptor)` call needs no changes to participate —
  proven directly (spike 1), not merely argued from the shared
  `ICompositionContext` surface. `Share<T>()` is a purely runtime,
  `CompositionContext`-level concern, invisible to compile-time plan
  emission.
- **No runtime reflection introduced.** `Share<T>()` needs the same
  `typeof(T)` capture other generic builder methods (`Register<T>()`)
  already use — no `MakeGenericMethod`, no `Activator.CreateInstance`.
- **Native AOT/trimming goals preserved** — no new reflection pattern, no
  new generated-code surface. (A dedicated AOT smoke-test pass, matching
  this repo's standing practice for every other package/feature, is
  expected during real implementation, not asserted as already proven
  here — the spike prototype did not include an AOT publish check.)
- **No new service-locator state, no composer-wide mutable lifetime
  state** — both explicitly evaluated and rejected above.

## Documentation, Skills, and Evals — part of this feature's definition of done

Per this repo's own documentation/decision-recording standards, the
following are **required implementation-time deliverables**, not optional
follow-up cleanup:

- `docs/architecture/current/` (composition model, provider-pipeline
  pages) — a `Share<T>()` section, added once the real implementation
  ships, not before.
- `docs/public-api.md` — new public surface entry.
- `skills/compono/SKILL.md` — mechanism-choice guidance gains a
  `Share<T>()` row alongside `[Shared]`'s existing entry, with explicit
  guidance on when to reach for which.
- `skills/compono/references/registrations-profiles-and-scopes.md` — the
  primary home for `Share<T>()` usage guidance (already documents
  `Register<T>()`/`.For<T>()`/profiles); split into a dedicated sharing
  reference file if the combined material grows large enough to warrant
  it (an implementation-time call, not decided here).
- **Explicit profile blast-radius guidance** (a real finding from the
  research, not a hypothetical): adding `Share<T>()` to a profile several
  existing tests already reuse changes sharing semantics for *every* graph
  composed with that profile, silently, for any test that happens to
  structurally reach the type more than once. This is intentional
  (exactly the graph-wide contract this ADR defines) but must be
  documented clearly, since it is a materially larger blast radius than
  adding `[Shared]` to one test method's own row.
- `docs/packages/compono-logging.md`/`skills/compono/references/logging.md`
  — once `Share<T>()` ships, revise the `PerformanceLoggingBehavior`
  example to show `builder.UseLogging(...).Share<ILogger<
  PerformanceLoggingBehavior>>()` paired with an **ordinary, undecorated**
  `ILogger<PerformanceLoggingBehavior> logger` parameter — no `[Shared]`
  on it, matching this ADR's own contract and Spike 3a's empirical proof
  exactly (corrected in RESEARCH-0014 §10 before this ADR was drafted, to
  avoid shipping a documentation example that contradicts this ADR's own
  decision).
- `skills/compono-evals/evals.json` — at least one new eval distinguishing
  correct `Share<T>()` usage (graph-scoped, lazy, ordinary-parameter
  participation) from an incorrect assumption a model could plausibly
  make without skill guidance (composer-wide singleton semantics, or
  "solves cross-`Create<T>()`-call identity," which this ADR explicitly
  does not do).
- Worked examples demonstrating ordinary, undecorated parameter retrieval
  after `Share<T>()` configuration — both the theory-parameter shape and
  the "no retrieval parameter at all" production-shaped shape (§ above),
  since the evidence base for this ADR specifically found the second
  shape under-precedented and worth demonstrating explicitly rather than
  leaving implicit.

## Evidence

All decision content above is backed by empirical spike results against a
real, compiled prototype (RESEARCH-0014 §11), not reasoning alone:

- **Zero-`[Shared]` declaration-order independence** (spike 3a): an
  ordinary, undecorated `[Compose<TProfile>]` theory parameter observes
  the graph-shared instance identically regardless of whether it's
  declared before or after its structural dependent.
- **Nested generated-plan participation** (spike 1): a real, compiled,
  generator-emitted plan's own nested `context.Resolve<T>(descriptor)`
  call participates exactly like any other request source, with a control
  case proving the observed sharing is actually caused by `Share<T>()`.
- **Production-shaped nested-only sharing** (spike 3b): two ordinary,
  unattributed constructors reached only as nested dependencies — no
  `[Shared]`, no test parameter of the shared type, no Compono annotation
  anywhere — receive identical shared identity, with a control case
  confirming the same.
- **Registration/order independence** (spike 2): `Register<T>()` and
  `Share<T>()` in both call orders resolve through the registration and
  cache identically, found and fixed a real implementation bug along the
  way (§ above) rather than assuming the naive shape would just work.
- **`CreateMany<T>()` graph-boundary validation** (bonus spike): sharing
  holds within each independent item and never crosses between items.

This ADR distinguishes evidence-backed **behavior** (everything stated
under "Core semantic contract," "Lifetime boundary," "Creation timing,"
and "Resolution source and precedence" above, all directly proven) from
**implementation mechanics** (the specific `_sharedTypes`/`effectiveIsShared`
shape used in the spike prototype, which is illustrative of a workable
approach, not itself frozen by this ADR — the real implementation task
may refine the exact internal shape, provided it satisfies every
normative contract point above and the audit requirement in
"Implementation constraint discovered by the spike"). The existing spike
prototype (`src/Compono/CompositionBuilder.cs`,
`CompositionConfiguration.cs`, `CompositionContext.cs`, `Composer.cs`, and
`test/Compono.XunitV3.Tests/SPIKE_ShareSemanticsTests.cs`, all still
present in the working tree, uncommitted, spike-marked) is not promoted to
production code by this ADR — the real implementation task starts fresh
against this ADR's contract, using the spike as a proof of feasibility and
a source of regression-test shapes, not as a merge candidate.

## Links

- `docs/research/0014-shared-ergonomics-and-composition-builder-share-research.md`
  — the full evidence trail this ADR summarizes.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)
  — `CompositionScope`'s existing scope lifetime and sharing key, carried
  forward unchanged as this ADR's own lifetime boundary.
- [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md)
  — the frozen-configuration model `Share<T>()`'s own configuration
  accumulator follows, and the reasoning that rules out a config-time
  mutable handle.
- [ADR-0019](0019-registrations-and-service-provider-injection.md) —
  `Register<T>()`'s strict duplicate-registration contract, confirmed
  orthogonal to and untouched by `Share<T>()`.
- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)/
  [ADR-0022](0022-compono-xunit-package-design.md)/
  [ADR-0040](0040-compono-tunit-package-design.md) — `[Shared]`'s existing,
  unchanged semantics and the `CompositionRow` mechanism it rides on.
- [ADR-0055](0055-compono-logging-testing-support-package.md) — the real
  dogfood evidence (`PerformanceLoggingBehaviorTests.cs`) that motivated
  this feature.
