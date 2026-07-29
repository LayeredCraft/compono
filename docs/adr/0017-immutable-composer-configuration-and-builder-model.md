# [ADR-0017] Immutable Composer Configuration and Builder Model

**Status:** Accepted

**Date:** 2026-07-29

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

Milestone 2 shipped `Composer.Create<T>()`/`Composer.CreateMany<T>(count)` against a
`CompositionContext` with no public configuration surface at all — registrations and
scope existed only behind an internal test seam (`Composer.CreateRootForTesting<T>`),
per [PLAN-0002](../plans/0002-milestone-2-core-composition-engine.md)'s explicitly
deferred scope. `docs/mvp.md`'s Milestone 3 asks for "immutable composer
configuration" as its first scope line, and `docs/public-api.md` already sketches the
target shape:

```csharp
var composer = Composer.Create(builder => builder
    .WithSeed(4219)
    .WithCollectionSize(3)
    .Register<IClock>(_ => new FakeClock())
    .AddProfile<CustomerProfile>());
```

This ADR settles what a `Composer` actually *is* once it has real configuration behind
it: what's built once, what's immutable, what's reused across every `Create<T>()`/
`CreateMany<T>()` call, and how configuration mistakes (the "configuration conflict
diagnostics" line in `docs/mvp.md`'s Milestone 3 scope) get reported. Every other M3
ADR (Profiles, Registrations/Service Injection, Configuration Rules) builds on the
builder/configuration split this one defines — it has to be settled first.

## Decision Drivers

- `docs/public-api.md`'s explicit API Design Rule: "Prefer immutable configuration
  after composer creation."
- `docs/mvp.md`'s Milestone 3 scope: "configuration conflict diagnostics" as a named
  deliverable, not an afterthought.
- No mutable global state, no static singletons
  (`references/coding-standards.md`'s DI/composition rules) — a `Composer` has to be a
  plain, constructor-built instance a consumer holds onto, not a static accessor.
- `Composer` must stay reusable across many `Create<T>()`/`CreateMany<T>()` calls
  without any call mutating shared state a later call could observe — this is what
  "long-lived immutable configuration" (`docs/public-api.md`'s Naming Vocabulary)
  actually requires structurally, not just as a description.
- Deterministic, fail-fast behavior over permissive-but-surprising behavior
  (`design-principles.md`: "Predictability over magic").

## Considered Options

1. **Mutable builder, immutable compiled configuration.** `CompositionBuilder` is a
   plain mutable accumulator during the `Composer.Create(builder => ...)` callback.
   When the callback returns, the builder's accumulated state is validated and frozen
   into an immutable `CompositionConfiguration` that `Composer` wraps. Every
   `Create<T>()`/`CreateMany<T>()` call reads the same frozen configuration to build a
   fresh per-call `CompositionContext`; nothing about a `Create<T>()` call can mutate
   the configuration a later call sees.
2. **`Composer` itself is the mutable builder.** Collapse builder and composer into
   one type — `Composer.Create()` returns a `Composer` that's directly configured via
   fluent calls, no separate callback/builder object.
3. **Fully immutable functional builder** (each fluent call returns a *new* builder
   instance rather than mutating in place, e.g. `builder.WithSeed(x)` returns a new
   `CompositionBuilder`). No freeze/compile step — the builder itself never contains
   mutable state at any point.

## Decision Outcome

Chosen option: **1, mutable builder / immutable compiled configuration**, because it
matches the shape `docs/public-api.md` already sketches (a builder callback, not a
directly-configured `Composer`), gives configuration validation exactly one clear
place to run (the freeze step), and keeps the mutable surface — where accumulation
naturally wants to mutate a list/dictionary as `.Register()`/`.AddProfile()` calls
stack up — cleanly separated from the immutable surface every `Create<T>()` call
actually depends on.

Concretely:

- **`CompositionBuilder`** (`public sealed class`) — mutable, exists only for the
  duration of the `Composer.Create(builder => ...)` callback. Exposes the fluent
  configuration surface (`WithSeed`, `WithCollectionSize`, `Register`, `AddProfile`,
  the type/member rule DSL from [ADR-0020](0020-composition-configuration-rules.md),
  `UseServiceProvider` from
  [ADR-0019](0019-registrations-and-service-provider-injection.md)). Backed by
  ordinary mutable collections (`List<T>`, `Dictionary<TKey, TValue>`) internally —
  there's no reason to fight C#'s natural accumulation shape during a short-lived
  builder phase nobody else observes concurrently.
- **`CompositionConfiguration`** (`internal sealed class`, despite the "record-like"
  data it holds — see `coding-standards.md`'s constructor-parameters-vs-required-
  properties rule: this type's *construction* is itself a meaningful operation,
  validating and compiling the builder's accumulated state, not just data assembly) —
  produced exactly once, by a `Build()` step `Composer.Create(...)` calls immediately
  after the configuration callback returns. Holds the frozen seed policy, collection
  size default, exact-registration table, configured `IServiceProvider` (if any), and
  compiled stage-4 provider list (from profiles/rules). Every field is a readonly,
  already-validated snapshot — no further mutation is possible after `Build()`
  returns, structurally, not just by convention.
- **`Composer`** (`public sealed class`, unchanged in kind from Milestone 2) — a thin,
  long-lived wrapper holding one `CompositionConfiguration`. `Create<T>()`/
  `CreateMany<T>(count)` each construct a fresh per-call `CompositionContext` from
  that shared configuration, exactly as Milestone 2 already does for the (currently
  always-empty) provider stages — only the source of that per-call context's inputs
  changes, not the context's own per-call lifetime model
  (`docs/architecture.md`'s Context Lifetime section, unaffected by this ADR).
- **Build-time validation.** `Build()` is where every cross-cutting configuration
  check runs, once, over the fully-accumulated builder state — not incrementally as
  each fluent call happens. This is a deliberate choice: incremental validation (check
  for a conflict the moment `.Register()` is called) can only ever see registrations
  added so far, so it reports conflicts one at a time, in whatever order the user
  happened to write the builder chain in. A single end-of-accumulation pass sees the
  whole picture and can report every conflict a single `Composer.Create(...)` call
  introduced in one structured exception, per
  [ADR-0019](0019-registrations-and-service-provider-injection.md)'s duplicate-
  registration decision. A build-time failure throws
  `CompositionConfigurationException` (a new, distinct exception type from the
  existing `CompositionException` — a *configuration* mistake, caught once at startup,
  is a different failure mode from a per-value *composition* failure inside a running
  `Create<T>()` call, and conflating them would make `catch (CompositionException)`
  around ordinary composition calls also catch configuration bugs that should have
  failed loudly at startup instead).

### Positive Consequences

- Configuration validation has exactly one place to live and one moment to run,
  making "configuration conflict diagnostics" (`docs/mvp.md`'s M3 scope line) a
  natural consequence of this shape rather than a bolted-on check.
- `Composer` stays trivially safe to share/reuse across parallel `Create<T>()` calls
  (a real scenario under parallelized test execution) — there's no mutable state on
  the hot path at all, only reads of an already-frozen snapshot.
- Matches `docs/public-api.md`'s sketched shape exactly; no public API surface
  described there needs to change because of this ADR.

### Negative Consequences

- Two distinct types (`CompositionBuilder`, `CompositionConfiguration`) exist where a
  naive design might use one — mitigated by `CompositionConfiguration` staying
  `internal`; a consumer only ever sees `CompositionBuilder` (during the callback) and
  `Composer` (after it), never the compiled type directly.
- A configuration mistake isn't caught until `Composer.Create(...)` actually runs
  (there's no compile-time check for a duplicate registration) — this is the same
  tradeoff every runtime-DI-container-style API accepts, and full compile-time
  registration validation is out of scope for what a source generator can reasonably
  check without inspecting arbitrary builder-callback logic.

## Pros and Cons of the Options

### Option 1 — Mutable builder, immutable compiled configuration

- Good, because it matches `docs/public-api.md`'s existing sketch with no API
  reshaping.
- Good, because validation gets one clear, whole-picture place to run.
- Good, because the mutable/immutable boundary is structural, not conventional — a
  `Create<T>()` call physically cannot see a mutation a builder made after `Build()`
  ran, because the builder and the frozen configuration are different objects.
- Bad, because it's two types instead of one — acceptable, since only one
  (`CompositionBuilder`) is ever public-facing to a configuring caller.

### Option 2 — `Composer` is the mutable builder

- Good, because it's one fewer type.
- Bad, because it directly violates "prefer immutable configuration after composer
  creation" — a `Composer` that's still fluently mutable after `Create()` returns
  invites exactly the "later `Create<T>()` call sees different behavior than an
  earlier one" bug class immutability is meant to rule out structurally.
- Bad, because there's no natural "now it's frozen" moment to hang validation on
  without an explicit `.Build()`/`.Seal()` call the consumer has to remember to
  invoke — reintroducing the two-phase shape Option 1 already models cleanly, just
  without the type boundary to enforce it.

### Option 3 — Fully immutable functional builder

- Good, because it avoids mutable state even during the short builder phase.
- Bad, because every fluent call allocates a new builder instance, and a long fluent
  chain (`WithSeed().WithCollectionSize().Register().Register().AddProfile()...`) —
  the exact shape `docs/public-api.md` shows — allocates one intermediate object per
  call for no observable benefit, since nothing outside the callback ever holds a
  reference to an intermediate builder state.
- Bad, because it complicates `AddProfile`'s callback-into-the-same-builder shape
  ([ADR-0018](0018-composition-profiles.md)): a profile's `Configure(CompositionBuilder
  builder)` mutating-in-place is simple; a profile's `Configure` needing to return a
  new builder and have the caller thread it back through is measurably more
  ceremony for the exact common case (`docs/public-api.md`'s `ApplicationTestProfile`
  example) this API needs to stay simple.

## Amendment (2026-07-29): scalar-configuration fail-fast, a structured exception model, and eager-versus-aggregated failure

Follow-up design review flagged three gaps in this ADR's Decision Outcome, all
closed before implementation begins.

**1. Repeated scalar configuration is a build-time conflict, consistently — and
deliberately, not incidentally.** This ADR's original text left
`WithSeed(...)`/`WithCollectionSize(...)` unspecified and treated
`UseServiceProvider(...)`'s duplicate-call behavior as a one-off decision local to
[ADR-0019](0019-registrations-and-service-provider-injection.md). Stated here as one
general rule instead, since it's actually the same question asked three times:
**every scalar (singleton-valued) configuration verb —
`WithSeed(int)`, `WithCollectionSize(int)` (the *global* default; a member-scoped
override is a keyed rule, not a scalar, and already follows
[ADR-0020](0020-composition-configuration-rules.md)'s keyed-conflict rule), and
`UseServiceProvider(IServiceProvider)` — may be called at most once across a
`Composer.Create(builder => ...)` callback, counting both direct calls and calls
made from inside any applied profile. A second call to the same scalar verb is a
build-time `CompositionConfigurationException`, not last-write-wins.**

Worth stating explicitly why, since this is a deliberate departure from a pattern
most .NET developers already know well — the "options builder" convention
(`services.Configure<TOptions>(...)` called more than once, or an options object
mutated repeatedly, where the last write is simply the effective value, no error).
Compono doesn't follow that convention here on purpose. `WithSeed(...)` is the
clearest case: two different seeds configured for the same `Composer` isn't
"the second one wins," it's a sign the configuration itself is contradictory —
there is no coherent reading of "this composer's seed is both 4219 and 8080." Once
that's accepted for `WithSeed`, applying a different rule to `WithCollectionSize`/
`UseServiceProvider` purely because *those* verbs could plausibly support last-wins
would make the failure mode depend on which verb happened to be called, not on
whether the configuration is actually contradictory — inconsistent in exactly the
way `design-principles.md`'s "predictability over magic" exists to rule out. The
same reasoning [ADR-0019](0019-registrations-and-service-provider-injection.md)
already applied to duplicate registrations generalizes cleanly to every scalar verb:
last-wins would make a scalar's effective value depend on `AddProfile` call order
the same way a silently-overridden registration would. `Build()`'s single validation
pass (below) checks scalar verbs the same way it checks keyed conflicts — one call
site, one rule, not three special cases, and not a case-by-case judgment call about
which verbs "feel like" they should support overriding.

**2. `CompositionConfigurationException` carries a structured, discriminated error
list, not just a message.** A consumer (or a test) needs to inspect *what*
conflicted without parsing prose. Rather than one flat record with a `Kind` enum and
a grab-bag of nullable fields only some kinds use, this follows the same
discriminated-union shape this codebase already establishes for a result type with
several distinct cases (`PathSegment` — [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md);
`CompositionResult` — this ADR's own sibling, ADR-0010): a sealed abstract base with
one sealed record per case, each carrying exactly the fields relevant to it.

```csharp
public sealed class CompositionConfigurationException : Exception
{
    public IReadOnlyList<CompositionConfigurationError> Errors { get; }
}

public abstract record CompositionConfigurationError
{
    private CompositionConfigurationError() { }

    public sealed record DuplicateRegistration(
        Type Type, IReadOnlyList<ConfigurationSource> Sources) : CompositionConfigurationError;

    public sealed record DuplicateRule(
        Type DeclaringType, string? MemberName, IReadOnlyList<ConfigurationSource> Sources)
        : CompositionConfigurationError; // MemberName null for a duplicate *type* rule

    public sealed record DuplicateConfigurationOption(
        string OptionName, IReadOnlyList<ConfigurationSource> Sources) : CompositionConfigurationError;

    public sealed record ProfileCycle(IReadOnlyList<Type> Chain) : CompositionConfigurationError;
}
// exact case set may grow during implementation; the four above are the floor
```

`DuplicateConfigurationOption` is this amendment's name for the scalar-conflict case
from point 1 above — chosen over an earlier "`DuplicateScalar`" working name, which
read as "two scalar *values*" rather than "the same configuration *option* specified
more than once"; `OptionName` is `"WithSeed"`/`"WithCollectionSize"`/
`"UseServiceProvider"`, naming the verb, not a type. The exception's `Message` is
*rendered from* this structured list (one line per `CompositionConfigurationError`,
via a `switch` over its case, per `coding-standards.md`'s pattern-matching
preference) — the human-readable text is a view over the structured data, not the
other way around. This is what lets a test assert
`exception.Errors.Should().ContainSingle(e => e is DuplicateRegistration { Type:
var t } && t == typeof(IClock))` instead of depending on exact message wording, and
what lets each case expose only the fields that actually apply to it (`ProfileCycle`
has no dangling `AffectedType`/`AffectedMember` it doesn't need, `DuplicateRule`
naturally carries both a type and a member where `DuplicateRegistration` only needs
a type).

**3. Eager profile-cycle failures are distinct from aggregated build-time
conflicts — both surface from `Composer.Create(...)`, but they don't share a
failure path.** [ADR-0018](0018-composition-profiles.md)'s cycle detection runs
*during* `AddProfile`, synchronously, as part of `configure(builder)` — before
`Build()` is ever reached. A cycle throws `CompositionConfigurationException`
**immediately**, with exactly one `CompositionConfigurationError` (`ProfileCycle`,
naming the full chain) — it does not get queued alongside whatever other conflicts
might exist elsewhere in the same builder chain, because configuration has stopped:
the profile that would have contributed the rest of its `Configure` body never
finished running, so there's nothing further to aggregate yet. This is different in
kind from `Build()`'s own validation pass (duplicate registrations, duplicate rules,
duplicate scalars), which only ever runs *after* `configure(builder)` returns
successfully — by construction, a `Build()`-time `CompositionConfigurationException`
can aggregate multiple `CompositionConfigurationError`s (every conflict found across
the *complete* accumulated state), while a cycle exception is always exactly one
error, thrown from a different call site (`AddProfile`, not `Build()`). Both are the
same exception *type* — a consumer catching `CompositionConfigurationException`
doesn't need to know which — but the plan implementing this should not attempt to
route cycle detection through the same aggregation buffer `Build()`'s conflict scan
uses; they're structurally different moments.

## Links

- [docs/mvp.md](../mvp.md) — Milestone 3 scope
- [docs/public-api.md](../public-api.md) — Configuration and Programmatic Composition
  sections
- [docs/architecture.md](../architecture.md) — Context Lifetime, Package Boundaries
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) — the
  Milestone 2 precedent for a context-owned deterministic pipeline stage, which this
  ADR's `CompositionConfiguration` now feeds real data into for the first time
- [ADR-0018](0018-composition-profiles.md), [ADR-0019](0019-registrations-and-service-provider-injection.md),
  [ADR-0020](0020-composition-configuration-rules.md) — build directly on the
  builder/configuration split this ADR defines, and their duplicate-conflict rules
  are now the general Amendment above applied to each verb, not independent
  decisions
