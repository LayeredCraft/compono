# [RESEARCH-0014] Shared Ergonomics: `CompositionBuilder.Share<T>()` Design Research

**Status:** Research complete, revised after a second design pass on the
retrieval question (§6/§6a-§6d below), then empirically validated by four
passing spikes against a real compiled prototype (§11). This research's
findings and spike evidence are the basis for
[ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md)
(`Accepted`), which now holds the normative decision content.

**Revision note (this pass):** the first pass concluded `Share<T>()` does
not solve retrieval and `[Shared]` should remain the only retrieval
mechanism, full stop. That conclusion was **too quick** — it answered "does
`Share<T>()` alone solve retrieval" (no) without asking "does `Share<T>()`
change what `[Shared]` needs to do, or let a plain, undecorated parameter
do `[Shared]`'s job." §6a-§6d below investigate that directly, evaluate a
strongly-typed handle concept and reject it with concrete proof (not
assertion), and produce a real, complete before/after for the actual
`PerformanceLoggingBehaviorTests.cs` file rather than the single abstract
example the first pass used. The revised conclusion (§6d) is genuinely
different from the first pass's: `Share<T>()` **can** remove the `[Shared]`
attribute (not the parameter) from the dominant retrieval-for-verification
case, for the specific and common shape where multiple test methods share
one composition profile.

**Product requirement (given, not re-litigated by this research):** a
`CompositionBuilder.Share<T>()`-style core API is Accepted pre-1.0 direction.
This document investigates *how* it should work, not *whether* it should
exist.

## 1. Exact current `[Shared]` semantics

`[Shared]` is **not a core-Compono concept at all today** — there is no
`SharedAttribute` in `src/Compono`. It's declared independently in
`src/Compono.XunitV3/SharedAttribute.cs` and `src/Compono.TUnit/SharedAttribute.cs`
(byte-identical semantics, deliberately duplicated per ADR-0040's own
"binding-logic decision" rather than shared as a library, since each
package's binding plan is otherwise fully separate). What core Compono
*does* already have, and what both attributes are thin declarative sugar
over, is:

- **`CompositionScope`** (`src/Compono/CompositionScope.cs`) — "stores at
  most one shared value per requested type, for the lifetime of one root
  composition operation." A plain `Dictionary<Type, object?>`. Internal,
  type-keyed only (ADR-0011: name/qualifier-based sharing is deferred past
  Milestone 4, "no concrete consumer has motivated it yet" — still true
  today).
- **`CompositionContext`** owns exactly one `CompositionScope` instance
  (`private readonly CompositionScope _scope = new();`,
  `src/Compono/CompositionContext.cs:28`). One `CompositionContext` = one
  scope, no exceptions.
- **`CompositionRow`** (`src/Compono/CompositionRow.cs`) — the actual
  sharing mechanism `[Shared]` rides on. It wraps **one**
  `CompositionContext` and exposes `Resolve<T>()` (ordinary, non-sharing),
  `ResolveShared<T>()` (compose through the full pipeline **and** store the
  result into scope), and `ShareExplicit<T>()` (store an already-known
  value into scope, no composition). It is public, core, and
  framework-independent — `Compono.XunitV3`/`Compono.TUnit` obtain one via
  `Composer.CreateRow(declaringType)` and call these three members through
  cached, reflection-free delegates built once per parameter at bind-plan
  construction time (ADR-0022, "Runtime-typed `CompositionRow` invocation").

### Precise answers to each sub-question

- **Lifetime boundary:** one `CompositionContext`. For plain
  `Composer.Create<T>()`/`CreateMany<T>()`, that's **one root operation**
  (one `Create<T>()` call, or one `CreateMany` item — `Composer.cs:61-72`,
  `95-103`: each call/item builds a fresh `CompositionContext`, nothing
  survives between them). For a test-framework row, `Composer.CreateRow`
  (`Composer.cs:116-131`) builds **one** `CompositionContext` reused across
  every parameter in that row — this is what lets `[Shared] Foo foo,
  ServiceA a, ServiceB b` all observe the same `Foo`. There is **no**
  composer-instance-wide scope anywhere today: a `CompositionBuilder`'s
  accumulated config (`CompositionConfiguration`) is frozen, stateless data
  (ADR-0017) — it holds no `CompositionScope` of its own, and nothing about
  it survives into a second `Create<T>()` call except the frozen
  configuration itself.
- **Creation timing (today):** effectively eager relative to the row/root —
  a `[Shared]` parameter is unconditionally composed as part of binding
  algorithm step 6 (ADR-0022), before any non-shared parameter (step 7),
  regardless of whether anything else in the row actually depends on it.
  There is no laziness concept in the current mechanism because there's no
  "declare sharing, defer construction" step to begin with — a `[Shared]`
  parameter *is* a composition request, made directly.
- **First request vs. later requests:** stage 2 of the pipeline
  (`CompositionContext.cs:501-509`) checks `_scope.TryGet(requestedType,
  out sharedValue)` **unconditionally, for every request, regardless of its
  own `IsShared` flag**. This is the mechanism that lets an *unmarked*
  nested constructor parameter (e.g. `PerformanceLoggingBehavior`'s private
  `ILogger<PerformanceLoggingBehavior>` field) transparently receive the
  already-shared instance with no `[Shared]` marker of its own — only the
  *write* side (storing into scope) is gated on `IsShared`. First request
  for a shared type: scope miss → falls through the normal pipeline
  (registration/provider/generated plan, whichever legitimately satisfies
  it) → result stored into scope only because `IsShared` was `true` for
  that specific request. Every subsequent request for the same type in the
  same context: scope hit, pipeline never re-invoked, no second
  registration/provider/generated-plan cost paid.
- **Does `[Shared]` both configure sharing and retrieve the value?** Yes,
  today, inseparably — there is no way to say "share `Foo`" without also
  being the parameter that receives `Foo`. This conflation is exactly the
  gap `Share<T>()` is meant to close (§9 below).
- **Multiple `[Shared]` parameters of the same type:** rejected at
  signature-validation time, before a row is even created (ADR-0022,
  "Duplicate `[Shared]` types fail clearly," naming both parameters) —
  belt-and-suspenders re-checked at the pipeline level too
  (`CompositionContext.cs:515-519`: a second `IsShared` request for an
  already-populated type throws, in case something bypasses the
  framework's own validation).
- **Multiple composition roots in the same row:** this *is* the row
  mechanism's entire purpose — every parameter in one `[Compose]` theory
  method is one row, one shared `CompositionContext`. `[Shared]` values
  compose first (in their own declaration order among themselves), then
  every other parameter composes and can transparently observe them via
  the unconditional scope-read above.
- **`Composer.Create<T>()` outside a test framework:** no sharing
  mechanism exists at all today for hand-written code that isn't going
  through `CreateRow`. Two independent `composer.Create<Foo>()` calls
  produce two independent instances, full stop — this is the literal gap
  the product intent's own example (`builder.Share<Foo>()` used directly
  with `Composer.Create`) is asking to close.
- **`CreateMany<T>()`:** `count` fully independent root operations
  (`Composer.cs:156-181`), each its own `CompositionContext`/scope/seed
  fork — confirmed unchanged since ADR-0011. No cross-item sharing exists
  or is implied by anything in this research; `Share<T>()` sharing a value
  *within* one `CreateMany` item's own graph (if that item's `T` has
  internal fan-out reaching the shared type twice) is a natural
  consequence of the graph-scoped boundary (§4), not a new per-batch
  concept.
- **Nested dependencies:** participate automatically and transparently, as
  described above — this is proven, shipped behavior (ADR-0021's own
  stated purpose), not a hypothesis.

## 2. AutoFixture prior art

**`Fixture.Freeze<T>()`** (`AutoFixture` core, framework-independent) —
eagerly calls `fixture.Create<T>()` once, right there, synchronously, then
installs a customization (a fixed-value builder) so every subsequent
`fixture.Create<T>()`/nested specimen request for `T` on that `Fixture`
instance returns the same cached instance. **Eager**, not lazy — but this
reads less like a considered creation-timing decision and more like a
consequence of `Fixture`'s own object model: a `Fixture` is a long-lived,
mutable, single specimen-graph builder, and `Freeze<T>()` is conventionally
called immediately before the SUT is requested in the same test method, so
eager-vs-lazy is nearly moot in practice — the value is about to be
consumed either way.

**`[Frozen]`** (`AutoFixture.Xunit2`, xUnit-specific) — a parameter
attribute on an `[AutoData]`-driven parameter. Order-sensitive in a way
that closely parallels Compono's own rule: frozen parameters are
established before the fixture composes the rest of the row, matching
`[Shared]`'s "shared parameters compose first" rule almost exactly. Two
things `[Frozen]` supports that Compono's `[Shared]` deliberately does
not, both worth naming explicitly as prior art *not* being adopted absent
new evidence:

- `[Frozen(As = typeof(IFoo))]` — freeze a concrete `Foo` but register the
  frozen value under a *different* requested type (`IFoo`), so later
  `IFoo` requests elsewhere in the graph also receive it. Compono's
  sharing key is the parameter's own declared type only, no such
  redirection.
- AutoFixture does not reject multiple `[Frozen]` parameters of the same
  type at bind time the way Compono's `[Shared]` does (ADR-0022,
  "Duplicate `[Shared]` types fail clearly") — Compono is deliberately
  stricter here, and nothing in this research suggests changing that.

**Conclusion for this research:** AutoFixture's ordering discipline
(freeze/frozen values established before the rest of the graph composes)
already matches Compono's own established rule and is not new evidence for
anything. AutoFixture's *eager* creation timing is the one place prior art
actively points a different direction from what this research recommends
(§5) — noted as a deliberate, justified departure, not an oversight.

## 3. Real `[Shared]` usage inventory

Across Compono's own `test/`, `alexa-vox-craft` (real dogfood consumer,
read-only grep, no repo modification), and `structured-logging` (zero
`[Shared]` usage — it doesn't drive Compono theories this way):

| Pattern | Example | Frequency |
|---|---|---|
| **Retrieve a generated test double for `Configure()`/`Verify()`** while it's also injected into the SUT | `test/Compono.TestDoubles.SampleTests/ClosedInstantiationTests.cs`, `alexa-vox-craft`'s `[Shared] IAttributesManager attributesManager, AttributesManager manager`/`[Shared] IPersistenceAdapter persistenceAdapter, AttributesManager manager` | **Dominant** — the large majority of every real usage found |
| **Retrieve a `CapturingLogger`/`ILogger<T>` for `Verify()`** | `PerformanceLoggingBehaviorTests.cs` (the Compono.Logging dogfood case) | Same pattern as above, logging-specific |
| **Retrieve a fake delegate for both `Returns(...)` config and `CallCount`/verification** | `alexa-vox-craft`'s `[Shared] FakeRequestHandlerDelegate next` — used in nearly every `Pipeline/*BehaviorTests.cs` file | Very common, same underlying pattern (configure+verify the graph-owned instance) |
| **Retrieve an HTTP test harness for request assertions** | `alexa-vox-craft`'s `[Shared] HttpTestHarness handler` (`Smapi.Tests`, `InSkillPurchasing.Tests`) — over 30 occurrences | Very common |
| **Share a value object for identity across multiple composed dependents, no verification intent** | `alexa-vox-craft`'s `AttributesManagerTests.cs`: `[Shared] SkillRequest skillRequest` passed alongside `SkillRequestFactory factory` so both observe the same request instance | Present, meaningfully less common than the retrieval-for-verification patterns above |
| **Share a real, stateful DI container across multiple roots** | `alexa-vox-craft`'s `[Shared] IServiceCollection services` (`Wrappers/*Tests.cs`) | Present, niche |
| **Compono's own binding-mechanics tests** (scalar `string`/`int`, disposable-ordering fixtures) | `test/Compono.XunitV3.Tests/Fixtures/SampleTestMethods.cs` | Test-infrastructure only, not representative of real usage |

**Finding:** the overwhelming dominant real-world pattern (a large majority
of every genuine usage found, across Compono's own sample tests and a real
production consumer) is **"declare sharing so the test can retrieve the
exact graph-owned instance for post-hoc inspection/verification,"** not
"share a value for construction-time identity only." This directly shapes
§9 — a pure `Share<T>()` configuration API without *any* retrieval story
would leave the dominant use case exactly as awkward as it is today.

## 4. Recommended `CompositionBuilder.Share<T>()` semantics

### Lifetime boundary

**One composition graph — the same boundary `CompositionContext`/
`CompositionScope` already use today.** For `Composer.Create<T>()`, that's
one root operation; for `CreateMany<T>(count)`, one boundary per item; for
a `CompositionRow`, the whole row (matching `[Shared]`'s existing
behavior exactly). **Not** composer-instance-wide. This is not a new
design choice invented for this research — it is the literal, load-bearing
lifetime `CompositionScope`'s own doc comment states today ("for the
lifetime of one root composition operation") and the only boundary
`[Shared]` has ever had. Choosing anything wider would require inventing
an entirely new state-holding lifetime for `Composer`/`CompositionBuilder`
that does not exist anywhere in the engine today (a `CompositionBuilder`'s
frozen `CompositionConfiguration` is stateless — ADR-0017) and would
directly work against the disposal-safety reasoning already recorded in
ADR-0022 Amendment 4 (§11).

### Creation timing

**Lazy, on first request within the graph.** `builder.Share<Foo>()`
records "`Foo` is a shared type" as pure configuration data in
`CompositionConfiguration` — it runs during `CompositionBuilder.Build()`,
before any `CompositionContext` even exists, so there is nothing to
construct yet. The first real request for `Foo` within a given graph
(triggered by whatever actually asks for it — a constructor parameter, a
`[Compose]` row parameter, a manual `Resolve<Foo>()`) flows through the
existing pipeline exactly as it does today, and the *existing* write-gate
(`CompositionContext.cs`'s `StoreSharedAndReturn` path, today keyed only on
a request's own `IsShared` flag) additionally fires whenever the requested
type is builder-configured as shared. If nothing in the graph ever
requests `Foo`, nothing is ever composed — this is not a policy decision
requiring new machinery, it is what happens for free by not composing
anything you weren't asked for, matching the product intent's own stated
expectation exactly ("configuring sharing should not construct an unused
dependency"). This is also the option requiring the **least** new code:
eager creation would require inventing a new "compose immediately at
`Build()`/`Create<T>()` time, before any real request exists" code path
that has no current equivalent anywhere in the engine.

**Mechanically, the minimal implementation shape:** extend
`CompositionConfiguration` with a `IReadOnlySet<Type> SharedTypes` (or
equivalent), populated by a new `_sharedTypes` accumulator on
`CompositionBuilder`. Inside `CompositionContext`'s per-request resolution
path, the effective `isShared` flag for a request becomes `isShared ||
_configuration.SharedTypes.Contains(requestedType)` — a single-line
extension point, not a restructuring. The unconditional scope-read at
stage 2 needs no change at all; it already applies to every request
regardless of source.

### Resolution source

**Confirmed compatible with the existing pipeline, no new work required.**
`[Shared]`/`ResolveShared<T>` already resolve through the *entire* pipeline
before storing — an exact `Register<T>` registration wins over generated
composition for a shared request exactly as it would for an unshared one
(ADR-0022: "'shared' only changes what happens *after* a value is
produced ... never which stage produces it"). This is proven, shipped
behavior, not a hypothesis to test — `Share<T>()` inherits it unchanged
because it uses the identical write-gate. This holds uniformly for
`Register<T>()`/`Register<T>(factory)`, built-in generation, and every
`ICompositionValueProvider` (`GeneratedTestDoubleProvider`,
`Compono.Logging`'s `LoggingProvider`, `Compono.NSubstitute`'s
`NSubstituteProvider`, and any future public provider) — none of them are
aware of scope/sharing at all; the gating happens entirely in
`CompositionContext`, one layer above every provider.

### Precedence with `Register<T>()`

**No precedence rule needed — the two are orthogonal, order-independent
configuration.** `Register<T>()` is strict, not last-write-wins:
registering the same `T` twice is a **build-time conflict** (throws,
ADR-0019 — a real correction to an initial assumption made mid-research
that this repo used a "last registration wins" convention; that convention
exists only for a generated test double's own *member-level*
`Configure()`/`Verify()` calls, an entirely separate mechanism, not for
`Register<T>()` itself). Because `Share<T>()` and `Register<T>()` write
to two independent configuration buckets (`_registrationFactories` and a
new `_sharedTypes` set respectively) with no shared key collision logic
between them, `builder.Register<Foo>(...); builder.Share<Foo>();` and
`builder.Share<Foo>(); builder.Register<Foo>(...);` are **provably
identical** — `Register<T>`'s own existing duplicate-detection is
untouched, and `Share<T>()` merely marks the *already-determined* winning
value for caching. Recommend: no precedence contract needs to be
documented beyond "these are independent configuration calls, order never
matters" — anything more would be manufacturing a rule this design doesn't
actually need.

## 5. Relationship to `[Shared]`

**Yes — `[Shared]` should become sugar for "request `Share<T>()` semantics
for this parameter's type, scoped to this row," not a separate mechanism.**
Concretely: a `[Shared]` parameter's binding continues to call
`ResolveShared<T>`/`ShareExplicit<T>` exactly as it does today (nothing
about the parameter's own behavior needs to change) — but `CompositionRow`
gains the ability to also honor builder-configured `Share<T>()` types
transparently for *every* parameter in the row, shared xUnit/TUnit code
included, because both packages' binding plans already route through the
identical core `CompositionRow`/`CompositionContext` (confirmed: TUnit's
`SharedAttribute` is a byte-for-byte semantic duplicate of XunitV3's,
ADR-0040). This means **no per-framework work is needed at all** to make
`Share<T>()` available uniformly to xUnit and TUnit — it is a pure core
Compono change; both integration packages inherit it automatically the
moment `CompositionContext` understands builder-configured shared types.

- **Does `[Shared]` simply request `Share<T>()` semantics?** Conceptually
  yes, for the write-and-cache half. It additionally does something
  `Share<T>()` alone cannot: bind a parameter slot to retrieve the value
  (§6).
- **Can multiple `[Shared]` parameters of the same type safely map to the
  same mechanism?** No — and this should not change. `[Shared]`'s own
  duplicate-type rejection is a parameter-binding-level rule (you can't
  have two theory parameters both claiming to be *the* retrieval point for
  the same type), entirely orthogonal to whether the underlying type is
  also `Share<T>()`-configured at the builder level. A `[Shared] Foo foo`
  parameter on a row whose builder already called `builder.Share<Foo>()`
  is not a conflict — it's simply the parameter used to retrieve the value
  `Share<T>()` already made shared.
- **Can xUnit and TUnit use the same underlying core semantics?** Already
  true today for `[Shared]` itself (§1), and `Share<T>()` extends this
  with zero additional per-framework surface — one sharing model
  (`CompositionScope`/`CompositionContext`), two syntactic entry points now
  (`[Shared]`, `Share<T>()`), soon three effectively (both, combined, per
  §6's recommendation).

## 6. Observation / retrieving the shared instance

**`Share<T>()` by itself does not return anything usable — it has no
`.Value`, no handle, nothing a test can call `.Verify()` on. A value must
still reach the test body through some channel.** That much of the first
pass's conclusion stands. But the first pass then jumped from "`Share<T>()`
returns nothing" to "therefore `[Shared]` must remain unchanged as the
retrieval mechanism" without checking whether `Share<T>()` changes *what
kind of parameter* is enough to retrieve a value — it does, and that
changes the answer. §6a-§6d below investigate three genuinely different
retrieval shapes, reject two of them with concrete evidence rather than
assertion, and land on a real, working ergonomic improvement for the
dominant case identified in §3.

### 6a. Candidate: a config-time strongly-typed handle — investigated and rejected

The concept: `builder.Share<T>()` (or a distinctly-named variant) returns
some `Shared<T>` handle at builder-configuration time, later exposing
`.Value` once a graph has actually composed `T`.

```csharp
// Illustrative only - not a proposed API name/shape.
Shared<ILogger<PerformanceLoggingBehavior>> logger = builder.Share<ILogger<PerformanceLoggingBehavior>>();
var composer = Composer.Create(b => { logger = b.Share<...>(); });
var behavior = composer.Create<PerformanceLoggingBehavior>();
logger.Value.Verify()...
```

This is incoherent as soon as it's checked against the exact constraints
raised for investigation, not just in the abstract:

- **One `Composer` used for multiple `Create<T>()` calls.** The handle is
  created once, during the single builder-configure callback
  (`Composer.Create(Action<CompositionBuilder> configure)` runs `configure`
  exactly once, ADR-0017). If the resulting `Composer` is then used for two
  independent `Create<PerformanceLoggingBehavior>()` calls (a completely
  ordinary, supported thing to do with a `Composer` today — nothing about
  `Composer` prevents reuse), each call composes its own independent
  `ILogger<PerformanceLoggingBehavior>` (§1: independent
  `CompositionContext`/scope per root operation). One handle object,
  `.Value` set by whichever call happens to run — the **second** call's
  write silently overwrites the first. Any code still holding a reference
  to the first composed `behavior`, expecting `logger.Value` to be *its*
  logger, silently observes the wrong graph's value instead. No exception,
  no warning — just a wrong answer.
- **`CreateMany<T>()` is the sharpest, cleanest proof this is broken, not
  just awkward.** `composer.CreateMany<PerformanceLoggingBehavior>(5)`
  produces five independent instances from five independent graphs (§1,
  unchanged since ADR-0011) — five different logger instances. One
  `Shared<T>` handle, created once at configuration time, cannot coherently
  expose "the" value: there isn't one. This alone disqualifies a
  config-time-scoped handle regardless of how the concurrency/reuse
  question above is resolved.
- **Concurrency.** Nothing in this codebase prevents two threads calling
  `composer.Create<T>()` on the same `Composer` concurrently (xUnit v3
  itself parallelizes test execution by default). A shared mutable
  `.Value` slot, written by whichever composition finishes first/last with
  no synchronization contract, is a textbook data race — not a
  hypothetical edge case, a predictable consequence of how this repo's own
  test runners already execute.
- **Hidden mutable state on `CompositionBuilder`/configuration.**
  `CompositionConfiguration` is deliberately immutable once frozen
  (ADR-0017 — "nothing about a later `Create<T>()`/`CreateMany<T>()` call
  can observe a mutation made after that point"). A handle whose `.Value`
  is written *by* a later `Create<T>()` call is exactly the kind of
  post-freeze mutation ADR-0017 exists to rule out, even if the mutable
  field lives on the handle object rather than literally on
  `CompositionConfiguration` itself — the freeze guarantee is meaningless
  if a builder-returned object can still be mutated by later composition
  activity.
- **Disposal.** An already-hard question (§8) gets strictly harder: now
  there are potentially *multiple* writes to the same slot over the
  `Composer`'s lifetime, each potentially producing an `IDisposable` value,
  with no defined moment at which "the" value's lifetime ends.
- **Service locator, precisely.** Strip the generic type parameter away and
  this is: a mutable slot, written by code that has no reference to the
  reader, read by code that has no reference to the writer, with no
  temporal or identity coupling between the two beyond "whichever
  `Create<T>()` call happened to run." That is the definition of a service
  locator. Compile-time typing (`Shared<T>` vs. `object`) does not change
  the shape of the problem, it only makes the anti-pattern type-safe.

**Rejected outright.** Not "rejected because it's stylistically
disfavored" — rejected because `CreateMany<T>()` alone proves there is no
coherent single value for it to hold, and the concurrency/reuse case
proves that even restricting to "one `Create<T>()` call only" would still
require documenting an unenforceable usage constraint ("don't reuse this
`Composer`, don't call this concurrently") that nothing in the type system
or runtime enforces — exactly the shape of subtly-broken design the
product owner asked to guard against.

### 6b. Candidate: a graph-scoped accessor on the object that already represents one graph

The reason 6a fails is temporal: it tries to hand back a value-shaped
answer *before* a graph exists. But Compono already has a real object that
comes into existence *at* graph-creation time and stays alive exactly as
long as the caller holds a reference to it: **`CompositionRow`**
(`Composer.CreateRow`, §1). Unlike the rejected handle, a
`CompositionRow` is never reused across multiple graphs (`CreateRow` is
called once per row/graph, by construction) and is never returned before
a graph exists (`CreateRow` *is* the graph-creation call).

`CompositionRow` already exposes `Resolve<TValue>(in
CompositionRequestDescriptor)` — but the no-descriptor overload
(`ICompositionContext.Resolve<TValue>()`) that would make this ergonomic
for hand-written code is **not usable outside a registration/rule/provider
callback today** — confirmed directly in source
(`CompositionContext.cs:254-267`): it throws
`InvalidOperationException` unless called from inside an active
"manual resolve frame," which only exists while a factory/provider is
running. A hand-written caller wanting to retrieve an already-shared value
after composing would have to construct a `CompositionRequestDescriptor`
by hand (`CompositionRequestDescriptor.cs` — a public struct with a public
constructor, so not *impossible*, but its `Ordinal` feeds real path/
random-fork bookkeeping (`ResolveCore`, `CompositionContext.cs:480-481`
runs before the scope check) — a value genuinely meant for
compile-time-generated call sites, not hand-typed ones, per its own doc
comment ("the compact, compile-time-constructible value a generated
`ICompositionPlan<T>` passes").

**This means a coherent graph-scoped retrieval mechanism is architecturally
sound, but does not exist as an ergonomic surface today for hand-written
`Composer.CreateRow`/`Create<T>()` usage.** A small, new,
narrowly-scoped member (illustrative name only — `CompositionRow
.TryGetShared<T>(out T value)` or similar, not usable to *compose*
anything, only to look up whatever the graph's own `CompositionScope`
already holds) would be coherent because:

- It's requested from an object the caller already holds a direct
  reference to, tied to exactly one graph — never ambient, never
  reachable from unrelated code.
- Each `CompositionRow`'s `CompositionContext`/`CompositionScope` is a
  private field on that one instance (§1) — no cross-call, cross-thread
  shared mutable slot exists; concurrency is a non-issue by construction.
- It answers "was this type ever established as shared in *this specific*
  already-alive graph," never "give me a value for `T` from wherever" —
  the opposite of a locator's defining trait (global/ambient reachability
  with no caller-held identity).

**This is out of scope to build in this research pass** (no
production code is being written here), and it does not, by itself, help
the `[Compose]`-theory-attribute dogfood syntax at all — a `[Theory,
Compose]` test method never sees the `CompositionRow` object; the binding
plan constructs and discards it internally (§1, §6c). It's recorded here
as a real, sound, *possible* small addition for plain
`Composer.CreateRow`/hand-written usage specifically — worth naming as a
candidate for the eventual ADR (or an immediate, separately-scoped
fast-follow), not something this research resolves further, since nothing
in the motivating Logging dogfood case actually goes through hand-written
`CreateRow` usage (it's `[Theory, Compose]` end to end).

### 6c. Candidate: framework-level auto-injection without a declared parameter — investigated and rejected

Could `Compono.XunitV3`/`Compono.TUnit` hand a test method a value it never
declared a parameter for — via a hidden extra parameter, or ambient
(`AsyncLocal`) state read from inside the test body?

- **Hidden extra parameter:** not a real extension point, confirmed
  against the actual binding mechanics (ADR-0022, §1): the binding plan
  assembles `object?[]` in **method declaration order** and returns it as
  the theory row's data — its arity is fixed by
  `MethodInfo.GetParameters()`'s own count. There is no channel to hand
  the test method more values than it declared parameters for; xUnit v3
  itself invokes the method reflectively with exactly that array. This
  isn't a missing convenience Compono chose not to build — the shape
  doesn't exist to build it into.
- **Ambient/`AsyncLocal` retrieval from inside the test body, no
  parameter:** technically constructible (store a reference to the row's
  scope in an `AsyncLocal<T>` before invoking the test method, expose a
  static accessor the test body calls directly) — and rejected for two
  independent reasons. First, it **is** a service locator in a more
  dangerous form than 6a: fully ambient, reachable from *any* code running
  on that logical call context, with no reference/type coupling to a
  specific graph at all — strictly worse than 6a's already-rejected
  config-time handle, not a lesser version of it. Second, its correctness
  under real execution is genuinely unproven: xUnit v3 parallelizes tests
  across threads by default, and `AsyncLocal` flow across a test runner's
  own thread-pool scheduling and an async test method's own `await`
  boundaries is exactly the kind of behavior this repo's own standing
  practice requires proving with a real spike before trusting, not
  assuming from reasoning — and no existing Compono mechanism uses
  ambient/`AsyncLocal` value flow anywhere today to build on. Proposing it
  here would be inventing a materially larger, riskier design surface than
  the ergonomics problem it would solve.

**Rejected.** Not pursued further — no real xUnit v3/TUnit extension point
supports the first shape at all, and the second is a strictly worse
service locator than 6a with unproven concurrency correctness.

### 6d. The actual improvement: `Share<T>()` changes what an *ordinary* parameter can do

The retrieval channel for `[Theory, Compose]`-driven tests is, and remains,
**a declared parameter** — 6c rules out anything else for that syntax.
But `Share<T>()` changes something real about what that parameter needs to
look like. Recall the exact write-gate proposed in §4: the effective
`IsShared` flag for a request becomes `isShared || SharedTypes
.Contains(requestedType)` — this fires for **any** request of a
builder-configured shared type, not only ones coming from a `[Shared]`-
attributed theory parameter. That means once `Share<ILogger<
PerformanceLoggingBehavior>>()` is configured (once, on a reusable
profile), an **ordinary, undecorated** `ILogger<PerformanceLoggingBehavior>
logger` theory parameter — no `[Shared]` attribute at all — participates
in the exact same caching `[Shared]` provides today, because the write
side no longer depends on the parameter carrying its own attribute; it
depends on the builder configuration, which the profile already
centralizes.

Two closed-graph resolution orders both produce the identical shared
instance, with no ordering rule to remember (an improvement over
`[Shared]`'s own declaration-order requirement, §1):

- If the ordinary `logger` parameter composes first (binding step 7,
  declaration order), its own top-level request establishes the cache;
  `behavior`'s later nested constructor request for the same type hits it.
- If `behavior` composes first, *its own nested* constructor request for
  `ILogger<PerformanceLoggingBehavior>` is the one that establishes the
  cache (the write-gate is type-based, not "only for top-level test
  parameters" — it fires identically for a nested nested request); the
  later top-level `logger` parameter request then hits it.

This is real, and it is exactly the case §3's usage inventory found
dominant. It requires **no new API beyond the `Share<T>()` builder method
this research already recommends** — `Compono.XunitV3`/`Compono.TUnit`'s
existing "ordinary (non-`[Shared]`) parameter → `Resolve<T>` invoker" path
needs no change at all; the only change is in core `CompositionContext`'s
write-gate, which was already the recommended implementation shape in §4.

**What it does not remove: the parameter itself.** A `[Compose]`-driven
test method has no channel to a composed value except a declared
parameter (6c) — `logger` must still be declared. What `Share<T>()` +
this ordinary-parameter pattern removes is the **`[Shared]` attribute**,
and — more importantly for the real file this evidence comes from — it
moves the *reason* sharing happens from "repeated per-test annotation" to
"a single, self-documenting statement in the shared profile." See §6e for
the complete before/after against the real dogfood file, which is where
this actually matters: `PerformanceLoggingBehaviorTests.cs` has **eight**
methods each independently declaring `[Shared] ILogger<
PerformanceLoggingBehavior> logger` — the identical attribute, repeated
eight times, purely because each test method has to re-request sharing
for itself. Centralizing it in the profile removes that repetition
entirely, in exchange for zero new API surface.

### 6e. Complete before/after: `PerformanceLoggingBehaviorTests.cs`

**Today (current, shipped state — one of eight identical-shaped methods):**

```csharp
// MediatRTestProfile.Configure(CompositionBuilder builder):
builder.UseLogging(options => options.MinimumLevel = LogLevel.Debug)
    .UseGeneratedTestDoubles()
    // ...

[Theory]
[Compose<MediatRTestProfile>]
public async Task Handle_WithSuccessfulRequest_LogsDebugMessages(
    [Shared] ILogger<PerformanceLoggingBehavior> logger,
    PerformanceLoggingBehavior behavior,
    IHandlerInput handlerInput,
    [Shared] FakeRequestHandlerDelegate next,
    SkillResponse expectedResponse,
    SkillRequest skillRequest)
{
    handlerInput.Configure().RequestEnvelope().Returns(skillRequest);
    next.Returns(Task.FromResult(expectedResponse));

    var result = await behavior.Handle(handlerInput, CancellationToken, next);

    result.Should().Be(expectedResponse);
    logger.GetCapturedEntries().Count(e => e.LogLevel == LogLevel.Debug).Should().Be(2);
    logger.Verify().AtLevel(LogLevel.Debug).WithMessageContaining("Processing Alexa skill request").Once();
    logger.Verify().AtLevel(LogLevel.Debug).WithMessageContaining("Successfully processed Alexa skill request").Once();
}
```

`[Shared] ILogger<PerformanceLoggingBehavior> logger` is repeated,
unchanged, in all eight test methods in this file.

**Candidate A — `Share<T>()` + existing `[Shared]` retrieval (first pass's
original conclusion):**

```csharp
// MediatRTestProfile.Configure:
builder.UseLogging(options => options.MinimumLevel = LogLevel.Debug)
    .Share<ILogger<PerformanceLoggingBehavior>>()   // new, but changes nothing observable here
    .UseGeneratedTestDoubles();
```

Test method: **byte-for-byte unchanged** — still `[Shared] ILogger<
PerformanceLoggingBehavior> logger`, repeated eight times. **This
candidate provides zero visible improvement to the actual motivating
file.** Stated plainly because the first pass's conclusion, applied
concretely, does not move this real example at all — worth being honest
about rather than leaving abstract.

**Candidate B — `Share<T>()` + ordinary (undecorated) parameter (§6d):**

```csharp
// MediatRTestProfile.Configure:
builder.UseLogging(options => options.MinimumLevel = LogLevel.Debug)
    .Share<ILogger<PerformanceLoggingBehavior>>()
    .UseGeneratedTestDoubles();

[Theory]
[Compose<MediatRTestProfile>]
public async Task Handle_WithSuccessfulRequest_LogsDebugMessages(
    ILogger<PerformanceLoggingBehavior> logger,   // [Shared] removed - profile already shares it
    PerformanceLoggingBehavior behavior,
    IHandlerInput handlerInput,
    [Shared] FakeRequestHandlerDelegate next,
    SkillResponse expectedResponse,
    SkillRequest skillRequest)
{
    // body identical to today
}
```

**Real, measurable improvement:** the `[Shared]` attribute is removed from
`logger` in all eight methods (`next` still needs `[Shared]` — nothing
about *it* is centrally configurable the same way, since it's a
per-test-supplied fake, not a profile-level cross-cutting concern — so
this candidate doesn't touch it, correctly). The parameter still exists
(6c: it must), but it no longer reads as a per-test workaround — it reads
exactly like `IHandlerInput handlerInput` above it: an ordinary composed
dependency. The "why is this shared" question moves from eight repeated
attributes to one line in the profile.

**Candidate C — config-time handle (§6a):** rejected; would not compile
into a coherent example at all given `CreateMany`/reuse/concurrency
proof — no before/after to show, because there is no safe implementation.

**Candidate D — framework auto-injection/ambient retrieval (§6c):**
rejected; no real extension point for the hidden-parameter shape, and the
ambient/`AsyncLocal` shape is a strictly worse service locator with
unproven concurrency correctness — no before/after to show, because it
should not be built.

### 6f. Does `Share<T>()` alone satisfy the original product-owner ergonomics goal?

**Not by itself (Candidate A), but yes in combination with the
ordinary-parameter pattern it enables (Candidate B), for the dominant real
case identified in §3.** The original complaint was specifically about
`[Shared]` being used as "a test-framework parameter purely as an
ergonomic workaround" for a core composition-lifetime concern. Candidate B
directly answers that: the parameter remains (unavoidable under `[Compose]`,
§6c), but the *sharing configuration* — the actual "core composition-
lifetime concern" the complaint named — moves out of the test-framework
attribute entirely and into core `CompositionBuilder` configuration, exactly
matching the product intent's own framing ("`[Shared]` may remain as
convenient xUnit/TUnit syntax, but it should no longer be the only
practical way to express that Foo should have shared identity"). §6g below
states the precise semantic contract this implies, since it's easy to
under-state it as "an ordinary parameter *can* participate if it happens
to" rather than the stronger, correct claim: it *always* does, for every
request of that type, with no opt-in of any kind.

### 6g. The semantic contract, stated precisely and without ambiguity

`Share<T>()` is a **graph-wide resolution-semantics change for `T`**, not
an opt-in a particular request can choose to make. Once
`builder.Share<TFoo>()` is configured:

- The **first** request for `TFoo` anywhere in that graph — a top-level
  `[Compose]` parameter, a generated constructor-plan request, a
  provider-driven request, a nested/transitive dependency several levels
  deep, a hand-written `Resolve<TFoo>()` call — resolves through the
  normal pipeline exactly as it would if `Share<T>()` had never been
  called, and its result becomes the graph's one shared `TFoo` instance.
- **Every subsequent request** for `TFoo` in that same graph, from any
  source, automatically observes that same instance. This is not
  conditional on the request being test-framework-driven, not conditional
  on a `[Shared]` attribute being present anywhere, and not conditional on
  a test method declaring any parameter of that type at all.
- **No production type ever needs to know this is happening.** Two
  ordinary, unannotated constructors —
  `ServiceA(TFoo dependency)`/`ServiceB(TFoo dependency)` — reached from
  the same composed root both receive the identical `TFoo` instance purely
  because `TFoo` is graph-configured shared, with no Compono-specific
  attribute, marker interface, or any other production-code change
  required (§7 restates this against the engine's own request-dispatch
  code, not just as a design intention).
- **`[Shared]` is never required to *participate* in a type `Share<T>()`
  has already configured shared.** An ordinary, undecorated parameter of
  that type gets full sharing semantics automatically (§6d). Attaching
  `[Shared]` to such a parameter would be redundant, not incorrect (it
  asks for exactly the semantics the type already has) — but it is never
  *needed*.

This settles the wording issue in the two mechanisms' remaining roles:

1. **`Share<T>()`** — core composition configuration. Graph-wide. Lazy on
   first request. Every request for `T` in the graph participates
   automatically, uniformly, regardless of source. Requires no `[Shared]`
   anywhere in that graph for `T`.
2. **`[Shared]`** — test-framework declarative convenience, unchanged from
   today's shipped behavior (§1). Establishes **ad hoc, row-local**
   sharing for a type the composition/profile has **not** already
   configured shared via `Share<T>()`, and — because binding a parameter
   is how a `[Compose]` test retrieves any value at all (§6c) — naturally
   gives the test body a handle to that instance in the same declaration.
   It is a way to *establish* row-local sharing when nothing already has,
   never a required opt-in to *consume* a type `Share<T>()` already made
   shared.

`[Shared]` therefore remains valuable and fully supported, but for a
narrower, precisely-stated reason than "the only remaining use case": it's
the right tool exactly when **no** `Share<T>()` configuration exists yet
for that type in that composition/profile and centralizing one would be
unwarranted ceremony for a single test's local need — not a fallback
required whenever `Share<T>()` isn't enough on its own, because `Share<T>()`
is, by design, always enough on its own for every request of that type.

### 6h. Real concerns this graph-wide design surfaces (not reasons to reject it)

Making `Share<T>()` genuinely graph-wide and invisible to production code
— both explicitly required by the product owner, both correct given the
`ServiceA`/`ServiceB` example (§7) — has two honest costs worth stating
plainly rather than glossing over:

- **Blast radius when added to a shared/reused profile.** A single
  `[Shared]` parameter's effect is local: it changes one test method's own
  row. `builder.Share<TFoo>()` added to a profile several existing tests
  already use changes *every one of them*, silently, for any test that
  happens to structurally reach `TFoo` more than once. A test that
  currently (correctly, deliberately) depends on two composed dependents
  receiving *independent* `TFoo` instances — e.g. asserting they mutate
  separate state without interfering — would silently start failing (or
  worse, silently start passing for the wrong reason) the moment
  `Share<T>()` is added to their shared profile for some *other* test's
  benefit. This isn't a flaw in the design — it's the direct, necessary
  consequence of "graph-wide, no attribute needed" — but it means adding
  `Share<T>()` to an already-reused profile is a materially bigger-blast-
  radius change than adding a `[Shared]` parameter to one test method, and
  should be flagged as such wherever this ships (skill guidance, §10).
- **Discoverability moves from local to centralized.** Reading
  `ServiceA(TFoo dependency)` in isolation gives no signal that `TFoo` has
  shared identity — that information now lives only in whatever profile/
  builder configuration composed the graph. This is the explicit tradeoff
  of "no Compono-specific attribute on production constructors" (a
  hard requirement, not something this research weighs against) — worth
  naming as an accepted cost, not silently absorbing it as if there were
  no cost at all.

One question resolves cleanly rather than remaining a concern: **calling
`Share<T>()` more than once for the same type** (directly, or once from a
profile and again from another) is naturally harmless, unlike
`Register<T>()`'s strict duplicate-throw contract (ADR-0019) — because the
underlying store is a set of shared types, not a single factory slot,
adding the same type twice is idempotent by construction, not a conflict
requiring new validation.

## 7. Multiple roots and composition rows

Already answered precisely in §1/§4 from real, current source. To restate
against the exact example given:

```csharp
void Test(
    [Shared] Foo foo,
    ServiceA a,
    ServiceB b)
```

All three parameters are bound within **one** `CompositionRow`, wrapping
**one** `CompositionContext`/`CompositionScope`. `foo` composes first
(step 6), stores into scope because `IsShared`. `a` and `b` compose next
(step 7); if either's constructor graph structurally requests `Foo`
(directly or nested), stage 2's unconditional scope-read hands back the
exact same instance `foo` holds — no `[Shared]` marker needed on that
nested request. `Share<T>()` preserves this exactly, and — because it
raises the *effective* `IsShared` flag for a builder-configured type on
**every** request regardless of source — actually strengthens the
guarantee: today, if `a` and `b` both structurally need `Foo` but *neither*
theory parameter is marked `[Shared]`, they get two independent `Foo`
instances (no root parameter ever established one in scope for them to
find). With `builder.Share<Foo>()` configured, `a`'s own first nested
request for `Foo` establishes it, and `b`'s subsequent request finds it —
identity across the whole graph is preserved **without needing any
`[Shared]` theory parameter at all**. This is the concrete new capability
`Share<T>()` adds beyond what `[Shared]` already does.

Mapping onto the enumerated call shapes:

- **One `Composer.Create<T>()` call:** one graph, `Share<T>()` types
  cached across `T`'s own transitive constructor graph. No cross-call
  sharing (a second, independent `Create<T>()` call is a new graph, new
  scope, per §1 — `Share<T>()` does not change this; it is not a
  composer-wide singleton).
- **Multiple `Create<T>()` calls:** each independent, as today. If a
  consumer needs identity *across* multiple top-level `Create<T>()` calls,
  that is a materially different (composer-wide) lifetime this research
  explicitly recommends against defaulting to (§4) — not something
  `Share<T>()` as designed here solves, and no evidence in this research
  suggests it should.
- **`CreateMany<T>()`:** each item is its own independent graph/scope
  (unchanged, ADR-0011) — a `Share<T>()`-configured type is cached within
  one item's own graph, never across items.
- **xUnit `[Compose]` rows / TUnit equivalent:** the whole row is one
  graph — identical behavior for both, since both route through the same
  `CompositionRow`.

A "graph" is therefore precisely definable, and already is: **the set of
every request reachable from one `CompositionContext`** — one root
`Create<T>()` call, one `CreateMany` item, or one row's full parameter set.

**The case with no test-method parameter of the shared type at all —
plain production constructors reaching it purely as nested
dependencies:**

```csharp
builder.Share<IMyInterface>();

public sealed class ServiceA(IMyInterface dependency) { /* ... */ }
public sealed class ServiceB(IMyInterface dependency) { /* ... */ }

public sealed class Root(ServiceA a, ServiceB b) { /* ... */ }
```

`composer.Create<Root>()` — one root operation, one `CompositionContext`.
`ServiceA`'s own constructor parameter is the first nested request for
`IMyInterface`; because `IMyInterface` is builder-configured shared, the
existing write-gate (§4: `isShared || SharedTypes.Contains(requestedType)`)
fires for this request exactly as it would for a top-level `[Shared]`
parameter, even though this request is neither top-level nor attributed
with anything at all — it's an ordinary nested constructor request,
indistinguishable at the pipeline level from any other. `ServiceB`'s own
constructor parameter is the second request for `IMyInterface`; stage 2's
unconditional scope-read (already unconditional today, §1) hands back the
identical instance. **`ServiceA` and `ServiceB` never reference Compono,
never carry an attribute, and never know sharing is happening** — the
entire mechanism is invisible to production code by construction, because
the gating logic lives entirely in `CompositionContext`, one layer below
every constructor `ConstructorSelector` ever calls. This is not a new
capability requiring new engine work beyond what §4 already specifies —
it's the direct, mechanical consequence of the write-gate being keyed on
the *requested type* rather than on *how* or *by whom* the request was
made.

## 8. Disposal/lifetime constraint (analysis only, not solved here)

ADR-0022 Amendment 4 already recorded a directly relevant, hard-won
finding: an earlier attempt at automatic disposal tracking for
`CompositionRow`-produced values was **reverted**, not fixed, because
`Resolve`/`ResolveShared` give no visibility into which pipeline stage
produced a value — a freshly-generated instance is indistinguishable from
one returned by an exact registration or an externally-owned
`IServiceProvider` value, and disposing the latter would violate ADR-0019's
"the caller owns the provider's entire lifetime" contract, "possibly a
shared singleton reused across many tests." **This constrains `Share<T>()`
directly:** keeping its lifetime boundary matched to the existing
graph/row scope (§4) means a shared value's disposal question is *exactly*
the same open question `[Shared]` values already pose today — no new
category of problem, no regression. Had this research instead recommended
a composer-instance-wide lifetime, it would have created a **new** shape
of disposal problem the future disposal ADR does not currently have to
solve: a shared value that outlives the graph/row that first produced it,
potentially referenced by an unbounded number of later, otherwise-unrelated
`Create<T>()` calls against the same `Composer` — graph-scoped avoids this
entirely by construction. **Recommendation: do not solve disposal here;**
this section exists only to confirm the chosen boundary (§4) does not
foreclose or complicate the future disposal ADR's options, and — if
anything — keeps the eventual disposal design space exactly as open as it
is today.

## 9. Compatibility

- **`[Shared]` semantics can remain unchanged** from the consumer's
  perspective — no breaking change to any existing test. The reframing in
  §5 (`[Shared]` as sugar over `Share<T>()` + retrieval) is an internal
  conceptual unification, not an observable behavior change.
- **`Share<T>()` is purely additive** — a new `CompositionBuilder` method,
  new `CompositionConfiguration` field, one new internal check in the
  resolution path. Nothing about existing `Register<T>()`/provider/
  generated-plan behavior changes for a type that isn't `Share<T>()`-
  configured.
- **No current ordering behavior needs correcting before 1.0** as a
  consequence of this design — the one precedence question this research
  raised (`Register<T>()` vs. `Share<T>()` ordering) resolves cleanly to
  "order never matters" (§4) without touching `Register<T>()`'s existing,
  correct, `Accepted` duplicate-detection contract.
- **Source-generation:** no generator changes appear necessary. A
  generated plan's own `context.Resolve<T>(descriptor)` calls already flow
  through the identical `ICompositionContext` surface `Share<T>()`'s
  scope-check lives behind — the generator has no visibility into, and
  needs none, of whether a given `T` is builder-configured as shared;
  that's entirely a runtime `CompositionContext` concern, invisible to
  compile-time plan emission. This should be confirmed with a small
  targeted spike (§15) before an ADR, not assumed purely from reading.
- **Native AOT/trimming:** no new reflection, no new generic-closing
  pattern — `Share<T>()` needs the same `typeof(T)` capture
  `Register<T>()`/`WithSeed`-style builder methods already use today, no
  `MakeGenericMethod`/`Activator.CreateInstance` anywhere near it. Expected
  AOT-neutral; still worth a smoke-test pass before an ADR is finalized
  (§15), matching this repo's own evidence-before-freezing convention.

## 10. Documentation and skill impact

- `docs/architecture.md`/`docs/architecture/current/` — the composition
  model and provider-pipeline pages will need a `Share<T>()` section
  once implemented; not before, per this repo's "don't describe
  unimplemented behavior as current" convention (`design-decisions.md`).
- `docs/public-api.md` — new public surface entry.
- `skills/compono/SKILL.md` — the "When to use Compono" / mechanism-choice
  guidance already lists `[Shared]`; needs a `Share<T>()` row once shipped,
  and explicit guidance on *when* to reach for `Share<T>()` (profiles,
  plain `Composer.Create` usage, or "several `Create<T>()` calls need one
  instance" — which this research found `Share<T>()` as designed here does
  **not** solve, see §7 — the skill must not imply it does).
- `skills/compono/references/registrations-profiles-and-scopes.md` — the
  natural home for `Share<T>()` usage guidance, since it already documents
  `Register<T>()`/`.For<T>()`/profiles.
- A new or extended reference file may be warranted specifically for
  sharing semantics (`[Shared]` + `Share<T>()` together) if the combined
  material grows large enough to justify splitting out of
  `registrations-profiles-and-scopes.md` — a call for the implementation
  phase, not this research.
- `skills/compono-evals/evals.json` — at least one new eval distinguishing
  correct `Share<T>()` usage (graph-scoped, lazy) from an incorrect
  assumption (composer-wide singleton, or "solves cross-`Create<T>()`-call
  identity") — the exact wrong assumption this research spent real effort
  ruling out is exactly the kind of thing a model could plausibly get
  wrong without skill guidance.
- `docs/packages/compono-logging.md`/`skills/compono/references/logging.md`
  — once `Share<T>()` ships, the `PerformanceLoggingBehavior`-style example
  could be revisited to show

  ```csharp
  builder
      .UseLogging(...)
      .Share<ILogger<PerformanceLoggingBehavior>>();
  ```

  paired with an **ordinary, undecorated** test parameter —
  `ILogger<PerformanceLoggingBehavior> logger`, no `[Shared]` on it — per
  §6g/§6d and Spike 3a's empirical confirmation (§11c). The example must
  **not** show `[Shared]` still retrieving the logger after `Share<T>()`
  is configured; that would contradict the validated contract and
  misrepresent `[Shared]` as still required where it isn't.

## 11. Focused spikes — results

**All four spikes have now been run against a real, compiled prototype and
all four passed, including their control cases. §11a below records the
empirical results; the case descriptions immediately following are kept
as originally written (they describe what was tested, still accurate) with
each case's result appended.**

This research initially relied on direct, careful reading of shipped, tested source
(`CompositionScope`, `CompositionContext`, `CompositionRow`, `Composer`,
`CompositionBuilder`) and existing `Accepted` ADRs (0011, 0017, 0019, 0021,
0022, 0040) rather than fresh executable spikes, because the mechanisms in
question are already fully implemented, documented, and covered by
existing tests — re-deriving them experimentally would not have produced
stronger evidence than reading the real, running implementation directly.
Before drafting an ADR, four things should still be proven empirically
rather than assumed from reasoning alone:

1. **A real, compiled generated plan's nested `context.Resolve<T>(descriptor)`
   call correctly observes a builder-configured `Share<T>()` type** —
   i.e., the scope-read at stage 2 firing for a *generated* nested request
   (not just a hand-written `Resolve<T>()` call) exactly as it does for an
   existing `[Shared]`-established value today. High confidence this
   already works unchanged (nested requests already transparently observe
   `[Shared]` values via the identical code path, §1) — but a genuine
   before/after spike against a real `[Compose]`-driven test removes all
   doubt before the ADR locks the design in.
2. **`Register<T>()`/`Share<T>()` ordering is genuinely order-independent
   in a real build**, not just by static-analysis reasoning about two
   separate dictionaries — a small spike exercising both call orders
   against the same type and asserting identical resolved output would
   convert §4's precedence conclusion from "provably true by inspection"
   to "empirically confirmed," matching this repo's own standing practice
   of proving uncertain behavior rather than trusting docs/reasoning
   alone.
3. **§6g's graph-wide contract itself — the most important spike, split
   into two independent cases, both required, since they prove different
   halves of the claim ("every request participates" is broader than "an
   ordinary theory parameter can retrieve the value").** Neither is
   already proven by existing `[Shared]`/`ResolveShared` behavior or any
   existing test in this repo — this is the one genuinely new mechanism
   this research proposes.

   - **3a. Declaration-order independence with zero `[Shared]`
     attributes, asserting reference identity, not just equal output.**
     One profile calling `builder.Share<TFoo>()`. Two theory methods
     against the same profile:

     ```csharp
     // Case 1
     public void Test1(TFoo dependency, MyService sut) { /* assert below */ }

     // Case 2
     public void Test2(MyService sut, TFoo dependency) { /* assert below */ }
     ```

     `MyService`'s constructor takes `TFoo` and exposes it back
     (`sut.Dependency`) purely so the assertion has something to compare
     against. Both cases assert `ReferenceEquals(dependency,
     sut.Dependency)` — not merely `.Should().Be(...)`/value equality,
     reference identity specifically, since the whole point is "the exact
     same instance," and a reference-equality assertion is the only one
     that can't be accidentally satisfied by two independently-composed-
     but-equal values. **Zero `[Shared]` attributes anywhere in either
     method** — this is the literal test of §6g's "never required to
     participate" claim, not a variant of an already-proven case.
   - **3b. The graph-wide property independent of any retrieval
     parameter at all.** `builder.Share<TFoo>()` configured; a composed
     root reaches `ServiceA(TFoo dependency)` and `ServiceB(TFoo
     dependency)` purely as ordinary, unattributed nested constructor
     parameters, with **no theory/test parameter of `TFoo` anywhere** —
     e.g. `Root(ServiceA a, ServiceB b)`, composed via
     `composer.Create<Root>()` or an equivalent `[Compose]` root
     parameter. Assert `ReferenceEquals(root.A.Dependency,
     root.B.Dependency)`. This is the specific case §7's "no test-method
     parameter retrieving T at all" analysis reasons through — it must be
     exercised directly, not inferred from 3a, because it's the case with
     the least existing precedent (§1's own unconditional-scope-read
     mechanism has only ever been proven against a `[Shared]`-established
     value; 3b is the first proof of it firing when *nothing* in the row
     ever marked anything `[Shared]` or even requested `TFoo` at the top
     level at all).

Both 3a and 3b should run against a real `Compono.XunitV3` (or plain
`Composer.CreateRow`/`Composer.Create`, for 3b specifically, which needs
no test-framework parameter binding at all) compilation — not a
theoretical trace through the source — since a `ReferenceEquals` assertion
is exactly the kind of claim this repo's own standing practice requires
proving directly rather than trusting from reading the pipeline code,
however carefully.

All four spikes (1, 2, 3a, 3b) are small and targeted; (1) and (2) convert
already-high-confidence reasoning (supporting the already-`Accepted`-
direction parts of this research) into direct proof, while 3a/3b confirm
the one genuinely new mechanism before it's frozen into an ADR.

### 11a. What was actually built and run

A minimal prototype of the write-gate shape from §4
(`effectiveIsShared = isShared || SharedTypes.Contains(requestedType)`)
was added to real `Compono` source, marked `// SPIKE (RESEARCH-0014...)`
at every touch point:

- `CompositionBuilder.cs` — a `_sharedTypes` `HashSet<Type>` field and a
  public `Share<T>()` method (`_sharedTypes.Add(typeof(T)); return this;`),
  wired into `Build()`'s `CompositionConfiguration` construction.
- `CompositionConfiguration.cs` — a new `required IReadOnlySet<Type>
  SharedTypes` property.
- `CompositionContext.cs` — a new `_sharedTypes` field, threaded through
  the constructor overloads `Composer.Create<T>()`/`CreateMany<T>()`/
  `CreateRow` actually use (default `null`→empty set for every other,
  test-seam-only constructor, so nothing else in the engine's own test
  suite is affected). `ResolveCore` computes `effectiveIsShared` once and
  uses it everywhere the request's *write* side needs it.
- `Composer.cs` — `_configuration.SharedTypes` threaded into every
  `CompositionContext` construction site (`Create<T>()`, the per-item
  context inside `ComposeMany`, and `CreateRow`).

Two new spike test files were added (both temporary, see §11c):
`test/Compono.XunitV3.Tests/SPIKE_ShareSemanticsTests.cs` (all four spikes
plus three control cases) and a matching `Analyzer`-only `ProjectReference`
to `Compono.Generators.csproj` added to `Compono.XunitV3.Tests.csproj` (a
plain `ProjectReference` doesn't transitively propagate the generator as
an active analyzer — a pre-existing, already-documented repo-wide gap, not
new to this spike). **An identical attempt on `Compono.Tests.csproj`
instead** — tried first, since it's the more natural home for a pure-core
spike with no test-framework dependency — **broke 348 pre-existing tests**
compilation (`CMP0002`, "no accessible instance constructor") the moment
the generator's discovery walk reached that project's many internal-seam-
only fixture types, which were never meant to be composed through a real
generated plan. Reverted immediately; the spikes moved to
`Compono.XunitV3.Tests` instead, which built and ran clean with the
identical analyzer wiring (0 regressions, confirmed before adding any
spike code).

### 11b. A real bug found and fixed during the spike — reported, not silently patched around

**Spike 2 (`Register<T>()`/`Share<T>()` ordering) failed on the first
run, in both orderings**, with `ReferenceEquals(root.A.Leaf,
root.B.Leaf)` false. Diagnosis: `ResolveCore`'s exact-registration stage
and its configured-`IServiceProvider`-fallback stage each call their own
`StoreSharedValue(requestedType, isShared, result)` — and **both read the
raw `isShared` parameter directly, not `request.IsShared`** (which is
where `effectiveIsShared` had been applied). Every other write path in
`ResolveCore` (the generated-plan branch, the provider branches at stages
4-7) reads `request.IsShared`, which *did* correctly carry
`effectiveIsShared` — only these two registration-stage call sites bypass
`request` entirely and close over the outer method's own `isShared`
parameter. This is exactly the kind of thing you asked to be told about
rather than quietly engineered around: **the simplest version of the
proposed shape did not work cleanly on the first attempt** because it
missed two call sites that don't go through the field it was applied to.

This is a real implementation-completeness bug in the prototype, not a
sign the §4 write-gate concept itself is unsound — the fix was mechanical
(route both `StoreSharedValue` calls through `effectiveIsShared` instead
of the raw parameter, two one-line changes) and required no change to the
underlying design: `Register<T>()`/`UseServiceProvider` results still need
to participate in graph-wide sharing exactly like every other stage's
result, and now do. After the fix, all four spikes (and their three
control cases) pass, and the full existing suite (`Compono.Tests` 1100,
`Compono.XunitV3.Tests` 316, full solution 3020) remains green.

**What this does mean for the eventual ADR:** the "single boolean OR"
framing in §4 was correct in spirit but incomplete in its stated
implementation detail — an ADR (or the real implementation task after it)
should say explicitly that *every* stage's write path must be audited for
which value it reads (`request.IsShared` vs. a stale, separately-captured
copy of the raw flag), not assume a single field mutation covers every
call site. This is a note for implementation rigor, not a semantic change
to the contract itself.

### 11c. Results per spike

1. **Generated nested resolution.** `SpikeRootWithDirectAndNested(SpikeSharedLeaf
   directLeaf, SpikeConsumerA a)`, `SpikeConsumerA(SpikeSharedLeaf leaf)`
   — a real generated plan's own nested `context.Resolve<SpikeSharedLeaf>
   (descriptor)` call (inside `SpikeConsumerA`'s own, separately-generated
   plan). **Passed**: `ReferenceEquals(root.DirectLeaf, root.A.Leaf)` is
   `true` with `Share<SpikeSharedLeaf>()` configured, and the control case
   (`Spike1Control`, identical shape, no `Share<T>()`) correctly asserts
   `false` — proving the sharing is actually caused by `Share<T>()`, not
   some other latent identity-preserving behavior already present in
   generated-plan dispatch. **Proved:** a real, compiled, generator-emitted
   plan's own nested request participates in the graph-wide contract
   exactly like any other request source, no different handling needed.
2. **`Register<T>()`/`Share<T>()` ordering.** Both orderings
   (`Register(...).Share<T>()` and `Share<T>().Register(...)`) tested
   against a registration whose factory result carries a distinguishing
   marker (`Origin = "registered"`, vs. the type's own default `"generated"`).
   **Passed** (after the §11b fix): both orderings resolve through the
   registration (`root.A.Leaf.Origin == "registered"` in both cases, not
   the generated-plan default) *and* establish that registered instance as
   the graph's shared value (`ReferenceEquals(root.A.Leaf, root.B.Leaf)` is
   `true` in both cases). **Proved:** call order has no semantic effect,
   confirming §4's precedence conclusion empirically rather than only by
   static-analysis reasoning about two separate dictionaries — no new
   precedence rule needed.
3. **Ordinary parameter retrieval, both declaration orders (3a).** Real
   `[Compose<SpikeShareProfile>]` xUnit v3 theories,
   `SpikeShareProfile.Configure` calling only `builder.Share<SpikeSharedLeaf>()`,
   zero `[Shared]` attributes anywhere in either test method. **Passed** in
   both declaration orders (`(dependency, sut)` and `(sut, dependency)`),
   `ReferenceEquals(dependency, sut.Dependency)` `true` in both. **Proved:**
   an ordinary, undecorated theory parameter automatically participates in
   builder-configured sharing regardless of declaration order relative to
   its structural dependent — exactly §6d/§6g's claim, not a weaker
   "can participate if it happens to" version of it.
4. **Graph-wide sharing with no retrieval parameter (3b).**
   `SpikeRoot(SpikeConsumerA a, SpikeConsumerB b)`, both ordinary,
   unattributed constructors, no `[Shared]`, no test/theory parameter of
   `SpikeSharedLeaf` anywhere, composed via plain `composer.Create<SpikeRoot>()`.
   **Passed**: `ReferenceEquals(root.A.Leaf, root.B.Leaf)` `true` with
   `Share<T>()` configured; the control case (`Spike3bControl`, identical
   shape, no `Share<T>()`) correctly asserts `false`. **Proved:** the
   strongest form of §6g's/§7's contract — `Share<T>()` is a genuine
   graph-wide resolution-semantics change, not merely a convenience for
   test-parameter retrieval; two production-shaped types that never
   reference Compono at all receive identical shared identity purely from
   builder configuration.

**Bonus, not originally scoped as a required spike but run for extra
confidence given how central the boundary claim is:** a `CreateMany<T>(3)`
boundary check — within each of three independent items, `A`/`B` share
(same graph); across items, no sharing at all (`ReferenceEquals` `false`
between any two items' own leaves). **Passed**, confirming §7's unchanged
`CreateMany` boundary holds under the new write-gate exactly as reasoned.

### 11d. Temporary spike changes remaining in the working tree

Nothing has been committed or pushed. As of this report, the working tree
(on `main`) contains, in addition to `RESEARCH-0014` itself:

- `src/Compono/CompositionBuilder.cs` — `Share<T>()` + `_sharedTypes` field
  (spike-marked).
- `src/Compono/CompositionConfiguration.cs` — `SharedTypes` property
  (spike-marked).
- `src/Compono/CompositionContext.cs` — `_sharedTypes` field,
  `effectiveIsShared` computation, and the two `StoreSharedValue` call-site
  fixes from §11b (all spike-marked).
- `src/Compono/Composer.cs` — `SharedTypes` threaded through three call
  sites (spike-marked).
- `test/Compono.XunitV3.Tests/Compono.XunitV3.Tests.csproj` — one added
  `Analyzer`-only `ProjectReference` to `Compono.Generators.csproj`
  (spike-marked, needed for spikes 1/3a/3b/CreateMany-boundary to exercise
  real generated plans).
- `test/Compono.XunitV3.Tests/SPIKE_ShareSemanticsTests.cs` — new file,
  all four spikes plus three control cases.

`test/Compono.Tests/Compono.Tests.csproj` was touched and then fully
reverted (the broken 348-test attempt, §11a) — it carries no diff. No
other file changed as part of this spike work.

## 12. Recommended design direction (summary)

- Add `CompositionBuilder.Share<T>()` — pure configuration, recorded into
  `CompositionConfiguration` as a set of shared types.
- Lifetime boundary: **one composition graph** (one `Create<T>()` root,
  one `CreateMany` item, or one `CompositionRow`) — identical to
  `[Shared]`'s existing, `Accepted` boundary. Never composer-instance-wide.
- Creation timing: **lazy**, on first real request within the graph — a
  natural consequence of the boundary choice and the existing pipeline
  shape, requiring the least new machinery of any option considered.
- Resolution source: unchanged — whichever stage (registration, provider,
  generated plan) would have produced the value normally, cached
  afterward. No new interaction rules needed with any existing provider.
- Precedence with `Register<T>()`: none needed — the two are independent,
  order-agnostic configuration.
- Relationship to `[Shared]`: `[Shared]` becomes conceptually "request
  `Share<T>()` for this type, and bind a parameter to retrieve it" — one
  underlying mechanism, unified across `Compono.XunitV3` and
  `Compono.TUnit` automatically (no per-package work), two (soon
  effectively three, combined) syntactic entry points.
- Observation/retrieval (revised, §6a-§6f): `Share<T>()` alone still
  returns nothing usable — a value must reach the test body through a
  declared parameter, full stop, for `[Compose]`-driven tests (no real
  xUnit v3/TUnit extension point supports anything else, §6c). But
  `Share<T>()` changes what that parameter needs to look like: once a
  type is builder-configured shared (typically via a reusable profile), an
  **ordinary, undecorated** parameter of that type gets the identical
  caching `[Shared]` provides today, in either declaration order relative
  to its dependents (§6d) — removing the `[Shared]` attribute (not the
  parameter) from the dominant real usage pattern (§3) for any test suite
  centralizing its sharing configuration in a profile. `[Shared]` is never
  required to participate in a type `Share<T>()` has already configured
  shared (§6g) — it remains a convenient declarative option for **ad hoc,
  row-local** sharing when the composition/profile has not already
  configured `Share<T>()` for that type. A config-time strongly-typed handle (`Shared<T>` returned
  by `Share<T>()` itself) was investigated and rejected outright — incoherent
  under `CreateMany<T>()` (proven, not asserted: no single value exists to
  hold), broken under `Composer` reuse/concurrency, and a service locator
  in substance regardless of its compile-time type safety (§6a). A
  graph-scoped `CompositionRow` accessor (e.g. `TryGetShared<T>()`) is
  architecturally sound and free of the handle's problems, but out of
  scope for this research (no production code proposed here) and does not
  help the actual `[Compose]`-attribute dogfood syntax at all — recorded
  as a candidate for hand-written `Composer.CreateRow` usage specifically,
  for the eventual ADR or a separately-scoped fast-follow (§6b). Framework
  auto-injection without a declared parameter (hidden extra parameter, or
  ambient/`AsyncLocal` retrieval) was investigated and rejected: the
  hidden-parameter shape has no real extension point given how xUnit v3
  invokes a theory method, and the ambient shape is a strictly worse,
  unproven-under-concurrency service locator (§6c).
- Disposal: not solved here; the chosen graph-scoped boundary keeps the
  disposal question identical in shape to the one `[Shared]` values
  already pose today (ADR-0022 Amendment 4), rather than introducing a new
  cross-graph lifetime the future disposal ADR would have to separately
  reason about.
- Compatibility: fully additive; no breaking change to `[Shared]`,
  `Register<T>()`, or any provider.

This is the smallest correct long-term design this research found: it
introduces one new, orthogonal configuration concept (a set of
builder-declared shared types) that plugs into exactly one existing
extension point (the `IsShared` check already gating `CompositionScope`'s
write side), reuses the identical lifetime/resolution/precedence rules
`[Shared]` has had since Milestone 2/4, and closes **both** real gaps the
Logging dogfood exposed: sharing configuration available outside a
`[Compose]` theory parameter (the first pass's finding), **and** the
`[Shared]` attribute no longer being the only way to express sharing
intent for the dominant retrieval-for-verification pattern once that
configuration is centralized (this pass's revised finding, §6d-§6f) — all
without inventing a service locator, a composer-wide singleton, or a
second sharing mechanism to keep in sync with the first.
