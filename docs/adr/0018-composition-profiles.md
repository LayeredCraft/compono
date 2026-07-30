# [ADR-0018] Composition Profiles

**Status:** Accepted

**Date:** 2026-07-29

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

`docs/mvp.md`'s Milestone 3 asks for "reusable profiles," with an explicit exit
criterion: "a project can define one reusable profile and use it for both
programmatic and test-framework composition." `docs/public-api.md` already sketches
a profile as a class overriding a `Configure(CompositionBuilder builder)` method:

```csharp
public sealed class ApplicationTestProfile : CompositionProfile
{
    protected override void Configure(CompositionBuilder builder)
    {
        builder
            .UseNSubstitute()
            .UseBogus(options => options.Locale = "en_US")
            .Register<IClock>(_ => new FakeClock(...));
    }
}
```

This ADR settles the profile authoring contract itself (interface vs. base class),
how a profile is instantiated and combined with others
(`builder.AddProfile<DomainProfile>().AddProfile<InfrastructureProfile>()`), what
pipeline stage 4 (`docs/architecture.md`, currently an always-empty
`ICompositionProvider` collection) actually gets populated with once M3 ships, and how
profiles that add other profiles avoid recursing forever. Per the terminology
correction in this design review, stage 4 is renamed **configuration rules**, not
"profile rules," throughout this ADR and its siblings — a profile is one *way* to
apply configuration rules (a reusable, named grouping), not the thing that owns the
stage. A direct `builder.For<T>()...` call outside any profile populates the exact
same stage through the exact same mechanism.

## Decision Drivers

- `design-decisions.md` rule 2: prefer composition over inheritance for the engine's
  object model; a new subclass of a core engine type should raise the question of
  whether it's actually a provider or configuration rule instead.
- Profiles composing other profiles (`AddProfile` from inside `Configure`) must not
  be able to recurse indefinitely — a cyclic profile graph is a configuration
  mistake, not a `StackOverflowException` waiting to happen.
- `docs/mvp.md`'s explicit exit criterion: one profile usable both programmatically
  and from a test framework — the contract can't assume anything about how it's
  invoked beyond "something calls `Configure`."
- Deterministic, order-based precedence (`design-principles.md`: "Predictability over
  magic") — combining two profiles must never depend on discovery order, reflection,
  or anything other than the explicit `AddProfile` call sequence.
- Reflection-free instantiation (`ADR-0001`) — a profile has to be constructible
  without `Activator.CreateInstance` or any other `Type`-keyed runtime lookup.

## Considered Options

### Authoring shape

1. **Interface: `ICompositionProfile`** — `void Configure(CompositionBuilder
   builder)`, no shared implementation, no state.
2. **Abstract base class: `CompositionProfile`** — matches `docs/public-api.md`'s
   existing sketch exactly (`protected override void Configure(...)`).

### Instantiation

1. **Generic, constrained to `new()`**: `builder.AddProfile<TProfile>()` where
   `TProfile : ICompositionProfile, new()`. The compiler emits an ordinary `new
   TProfile()` inside the generic instantiation — no `Activator.CreateInstance`, no
   `Type`-keyed lookup, fully reflection-free and AOT-safe.
2. **Instance-based only**: `builder.AddProfile(ICompositionProfile profile)` — the
   caller constructs the profile themselves.
3. **Both** — `AddProfile<TProfile>()` for the common no-arg case (matches
   `docs/public-api.md`'s `[Compose<ApplicationTestProfile>]` generic-attribute
   precedent from the Milestone 4 sketch), plus `AddProfile(ICompositionProfile
   profile)` for a profile that needs constructor parameters (e.g. a
   locale-parameterized profile) or is more convenient to unit-test as a plain
   instance.

## Decision Outcome

Chosen options: **interface (`ICompositionProfile`)** for authoring shape, confirmed
directly with the user — profiles define *behavior*, not shared *implementation*;
there's no base-class state or template-method logic this milestone (or any
currently-scoped one) actually needs, so an interface is the smaller, more honest
contract. This is a deliberate change from `docs/public-api.md`'s current sketch,
which predates this ADR and will be updated to match (see Links). If a genuine need
for shared base functionality shows up later (a `ProfileName` property for
diagnostics, say), a non-breaking convenience base class implementing the interface
can be added alongside it without touching the interface itself — the interface
staying minimal doesn't foreclose that.

Chosen option for instantiation: **both** — `AddProfile<TProfile>() where TProfile :
ICompositionProfile, new()` for the common case, `AddProfile(ICompositionProfile
profile)` for a profile that needs constructor arguments or is already an instance.
Neither needs reflection; both compile to ordinary generic/interface dispatch.

```csharp
public interface ICompositionProfile
{
    void Configure(CompositionBuilder builder);
}
```

### Profile application: eager, in call order

`AddProfile` runs the profile's `Configure(builder)` **immediately**, synchronously,
during the `AddProfile` call itself — not deferred to `Composer.Create(...)`'s
`Build()` step. A profile's `Configure` method receives the *same*
`CompositionBuilder` instance the surrounding `Composer.Create(builder => ...)`
callback is already configuring, and calls the same fluent methods
(`.Register()`, `.UseServiceProvider()`, the rule DSL) a direct caller would — a
profile is nothing more than a named, reusable chunk of builder configuration.

```csharp
var composer = Composer.Create(builder => builder
    .AddProfile<DomainProfile>()        // Configure runs here, in order
    .AddProfile<InfrastructureProfile>() // Configure runs here, second
    .Register<IClock>(_ => new FakeClock())); // direct registration, third
```

Precedence and conflict detection ([ADR-0017](0017-immutable-composer-configuration-and-builder-model.md)'s
`Build()` validation pass) both key off this same call order: the accumulated
registration/rule list simply records, per entry, whether it came from a direct
builder call or from a named profile (for the conflict exception's source-naming
requirement — see [ADR-0019](0019-registrations-and-service-provider-injection.md)),
in the exact sequence the entries were added. There is no separate "merge step" with
its own precedence rules to design: eager, in-order execution against one shared
mutable builder *is* the merge — the same reasoning `ADR-0017`'s "Option 3" rejection
already established (deferred/functional builders add ceremony without changing the
outcome). A later `AddProfile`/`Register` call doesn't silently override an earlier
one for the same type — per ADR-0019's decision, any duplicate exact registration for
the same type, regardless of source, is a build-time conflict, not a precedence rule
to resolve.

### Stage 4 (configuration rules) — what a profile actually populates

A profile's `Configure` body doesn't touch pipeline stage 4 directly or implement any
provider interface — it calls the *same* builder verbs a direct
`Composer.Create(builder => ...)` caller uses (`Register`, the type/member rule DSL
from [ADR-0020](0020-composition-configuration-rules.md), `UseServiceProvider`). Each
of those verbs is itself responsible for compiling into whatever internal engine
shape actually satisfies requests (an exact-registration table entry for
`Register`, an internal stage-4 provider for a type/member rule). A profile is purely
a *grouping and reuse* mechanism over the builder's existing surface — it introduces
no new engine concept of its own, and doesn't own stage 4 any more than a direct
builder call does. This is also why the deferred public provider-extensibility
question (Provider Registration, evaluated and explicitly deferred to Milestone 5 in
this design review) doesn't block profiles at all: nothing about
`ICompositionProfile` requires a user-authored type to implement any engine-internal
contract, public or otherwise.

### Profile recursion and provenance

`AddProfile` runs synchronously and can itself be called from inside a profile's
`Configure` (a profile bundling other profiles, per the Positive Consequences below).
Two consequences that don't fall out of the shape above automatically and need an
explicit rule:

- **Cycle detection.** `CompositionBuilder` maintains an internal stack of
  currently-applying profile types (an ordinary `Stack<Type>`, pushed before a
  profile's `Configure` runs and popped in a `finally` after it returns — the same
  push/pop-on-return shape the engine's own active-construction-frame stack already
  uses for recursion detection, per
  [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)).
  Identity for cycle purposes is the profile's declared CLR type
  (`profile.GetType()`), regardless of whether it reached the builder via
  `AddProfile<T>()` or `AddProfile(instance)` — two different instances of the same
  profile type nested inside each other are still treated as a cycle, deliberately
  conservative, the same reasoning the engine's own type-keyed frame stack already
  applies to construction recursion. If `AddProfile` is called for a type already on
  the stack (`ProfileA` → `ProfileB` → `ProfileA`), it throws
  `CompositionConfigurationException` immediately, naming the full cycle chain in
  application order — not a `StackOverflowException`, and not silently applying the
  outer profile's `Configure` a second time.
- **Provenance for diagnostics.** Every registration/rule entry `CompositionBuilder`
  accumulates records its full **source chain**, not just its immediate profile —
  `"direct"` for a call made outside any profile, or an ordered list of profile types
  for a call made from inside one (`["InfrastructureProfile", "DomainProfile"]` if
  `DomainProfile` was applied via `AddProfile` from inside
  `InfrastructureProfile.Configure`). [ADR-0019](0019-registrations-and-service-provider-injection.md)'s
  duplicate-registration exception, and this ADR's cycle exception, both use this
  chain to name exactly where each conflicting or cyclic entry came from — "`IClock`
  registered twice: once directly, once via `DomainProfile` (applied via
  `InfrastructureProfile`)" is a debuggable message; "`IClock` registered twice"
  alone is not, once profiles nest more than one level deep.

### Positive Consequences

- Matches `docs/mvp.md`'s exit criterion directly: one `ICompositionProfile`
  implementation, applied via `AddProfile<T>()` from both a programmatic
  `Composer.Create(...)` call and (in Milestone 4) an xUnit attribute, with no
  different code path for either caller.
- No reflection anywhere in profile instantiation or application.
- Precedence is exactly "the order you wrote the builder chain in" — nothing to
  memorize beyond ordinary C# evaluation order.
- Profiles compose freely (`AddProfile<A>().AddProfile<B>()`) without a distinct
  "composite profile" concept — a profile that wants to bundle other profiles just
  calls `builder.AddProfile<Other>()` from inside its own `Configure`.
- A cyclic profile graph fails fast with a named chain, at configuration time, rather
  than as an unrecoverable `StackOverflowException` a consumer can't catch or
  meaningfully debug.
- Conflict/cycle diagnostics can always name the real source, even through nested
  profile application, instead of pointing only at the outermost `AddProfile` call.

### Negative Consequences

- Eager, side-effecting `Configure` execution means a profile *can* (if badly
  written) do something order-dependent or non-idempotent inside `Configure` — no
  worse than any other imperative builder-callback API (ASP.NET Core's
  `IStartupFilter`, AutoFixture's `ICustomization`), but worth calling out as a
  convention profiles are expected to follow (pure configuration, no side effects
  beyond calling further builder methods), not something the type system enforces.
- Every accumulated entry now carries a source chain (a small list, not just a
  single profile reference) — a modest bookkeeping cost, paid once during
  configuration (never on the resolution hot path), in exchange for diagnostics that
  stay accurate under profile nesting.

## Pros and Cons of the Options

### Interface (`ICompositionProfile`)

- Good, because it matches the repo's composition-over-inheritance preference.
- Good, because it's the smallest possible contract — one method, no state.
- Good, because it mirrors AutoFixture's `ICustomization` (`void Customize(IFixture
  fixture)`) almost exactly — established prior art for "a reusable configuration
  unit is an interface with one `Configure`-shaped method," adopted deliberately here
  rather than reinvented, per `design-decisions.md` rule 5.
- Bad, because there's no shared base to hang future convenience members on without
  either a breaking interface change or a separate opt-in base class — mitigated
  above (a non-breaking base class can always be layered on later).

### Abstract base class (`CompositionProfile`)

- Good, because it matches `docs/public-api.md`'s existing sketch with zero doc
  rework.
- Good, because it leaves room for shared functionality (validation helpers, a name
  property) without a later breaking change.
- Bad, because `Configure` would need to be `protected`, which is a shape mismatch
  with every other builder-driven configuration callback in this API surface (a
  direct `Composer.Create(builder => ...)` callback, a rule factory) — those are all
  plain delegates/interface methods, not protected override points.
- Bad, because a class hierarchy exists here with no actual shared implementation to
  justify it yet — pure ceremony against the "don't design for hypothetical future
  requirements" standard.

## Links

- [docs/mvp.md](../mvp.md) — Milestone 3 scope, exit criterion
- [docs/public-api.md](../public-api.md) — Profiles section (to be updated: interface,
  not abstract base class — see the companion doc update in this design review)
- [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md) — the
  builder/configuration split `AddProfile` operates against
- [ADR-0019](0019-registrations-and-service-provider-injection.md) — duplicate-
  registration conflict behavior, which profile application order feeds directly
- [ADR-0020](0020-composition-configuration-rules.md) — the rule DSL a profile's
  `Configure` body calls into
- `design-decisions.md` rule 5 — AutoFixture's `ICustomization` as adopted prior art
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) — the
  active-construction-frame stack this ADR's profile-cycle detection mirrors the
  shape of (type-keyed, push-on-entry/pop-in-`finally`)
