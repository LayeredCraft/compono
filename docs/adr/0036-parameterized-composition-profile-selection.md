# [ADR-0036] Call-Site Values Influencing Nested Composition

**Status:** Accepted

**Date:** 2026-08-08

**Decision Makers:** Nick Cipollina, Claude (design review)

**Naming note:** this ADR keeps the identifier `ADR-0036` and its original
filename (`0036-parameterized-composition-profile-selection.md`) for
link-stability. Its title and Context were revised, before any design work
started, to avoid presupposing the eventual mechanism — the actual gap was
stated solution-neutrally, then a deep-design pass (below) evaluated four
genuinely different mechanisms before this ADR settled on one. Treat
"parameterized... profile selection" in the filename as a historical label
only, not a full description of the accepted shape — the accepted shape is
narrower and more specific than that filename suggests (a **typed
configuration object paired with a profile**, not a profile constructed
directly from an argument list); see Decision Outcome.

**Terminology — two distinct concepts, not one.** This ADR introduces
**profile configuration arguments**, and deliberately does not reuse
**inline values** to describe them — they are different concepts governed
by different code paths, and conflating the terms in documentation or
error messages would make both harder to reason about:

- **Inline values** (existing, [ADR-0022](0022-compono-xunit-package-design.md)) —
  `[Compose(42, "widget")]`'s constructor arguments, bound positionally to
  the **test method's own parameters**, partially or fully replacing
  composition for that row.
- **Profile configuration arguments** (new, this ADR) — bound positionally
  to a **`TConfig` type's constructor**, used only to construct the
  profile that then configures the `Composer` for the whole test method;
  never seen by, or bound to, the test method's own parameters at all.

## Context

[RESEARCH-0002](../research/0002-trivia-platform-comparison.md) — a
pre-migration capability survey of `ncipollina/trivia-platform`'s
AutoFixture-based test kit, run using
[ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
rubric — surfaced one finding with no clean answer in Compono's current
model, stated at the level the gap actually exists, not at the level of
any one candidate fix:

> Compono currently has no clean mechanism for compile-time-constant
> values supplied at a test call site to influence nested composition
> configuration for that specific test invocation.

`trivia-platform`'s ~16 custom `AutoDataAttribute` subclasses are the real
evidence for this — most take runtime constructor arguments that change
what the underlying fixture customization actually produces somewhere
*inside* the composed graph, not just which top-level type gets composed.
For example (real call-site shapes, not invented):

- `PersistenceAutoData(repositoryName)` — ~45 call sites, each supplying a
  different repository name that the attribute's customization logic
  switches on to configure a different DynamoDB table/persistence setup.
- `AnnouncementsAutoData(validConfig, gameOverEnabled, audienceEnabled, audienceItemEnabled, startOffsetDays, endOffsetDays, messageLocale, defaultLocale)` —
  8 constructor parameters, 18 call sites, each a distinct
  boolean/locale combination driving which `AnnouncementsOptions` gets
  built.
- `HandlerAutoData(requestType, aplSupported, locale, ...)`/
  `InterceptorAutoData(...)`/`PresenterAutoData(...)` — hundreds of call
  sites across the Alexa-handler test suites, each configuring the
  composed `IHandlerInput`/request shape differently per test.
- `InfraStackAutoData(region, account)`.

`cosmere-tracker`'s Milestone 7 dogfooding pass ([RESEARCH-0001](../research/0001-autofixture-comparison.md))
never surfaced this pattern — its custom attributes took no meaningful
runtime arguments, so this is new evidence, not a recurrence of an
already-decided question.

Compono's `[Compose<TProfile>]` ([ADR-0022](0022-compono-xunit-package-design.md))
selects a fixed, compile-time profile *type* — `TProfile` is a generic
type parameter, not a runtime value, and `ICompositionProfile.Configure(CompositionBuilder)`
takes no arguments of its own. `[Compose(42, "widget")]`'s inline-value
binding binds *test method parameters* positionally (per the migration
guide's "Migrate `[AutoData]` and `[InlineAutoData]`" section); it does
not thread a literal into configuration logic that runs somewhere *inside*
the composed graph. There is today no documented way for a value known at
the test call site to reach a `Register`/`.For()` decision made deeper in
composition — every such decision is either fully generic (the same for
every caller) or committed to one hard-coded configuration.

**What this gap is not.** Two adjacent capabilities that might look
related are already solved and are explicitly out of scope for whatever
closes this gap:

- **Requested type + resolution-site name.** `CompositionProviderRequest.Name`
  already lets a custom `ICompositionValueProvider` match on the
  requesting parameter/member's own name (`docs/concepts/providers.md`) —
  this is how, e.g., `trivia-platform`'s `SlotSpecimenBuilder`/
  `ProductSpecimenBuilder`-shaped patterns already have a clean Compono
  answer per RESEARCH-0002's Finding 2. Nothing about this ADR should
  re-solve that with a second request-descriptor abstraction.
- **Fixed member-specific override.** `.For<T>().Member(...)` already
  covers "this one member of this one type always gets this value/rule."
- **The actual gap** is a third, distinct case neither of the above
  reaches: a compile-time-constant value supplied at a specific test's
  call site needs to influence a composition decision made for *that test
  invocation only* — not a global provider rule, not a fixed member
  override, but a per-invocation input to otherwise-static configuration
  logic.

## Decision Drivers

- `docs/manifesto.md`'s explicit non-goal of AutoFixture feature parity —
  this finding still needs to survive the same "is this a real gap or an
  acceptable Compono-native alternative" question every other finding in
  RESEARCH-0002 was put through, not be assumed onto the roadmap because
  AutoFixture happens to support it.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
  evidence-driven restraint: a roadmap-candidate finding gets a
  `Proposed` ADR stating the problem only — the actual API design belongs
  to a later deep-design pass, not to this ADR.
- [ADR-0001](0001-source-generation-first.md)'s no-reflection-by-default
  posture and this repo's explicit-over-implicit bias — any eventual
  solution has to survive these, not just be convenient.
- [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md)'s
  immutable-builder model — a profile's `Configure` method runs once,
  declaratively, before composition; whatever closes this gap can't
  require mutating an already-built `Composer` mid-test.
- The evidence is high-frequency and structurally costly, not marginal:
  per RESEARCH-0002's Finding 1, none of Compono's current mechanisms —
  writing a distinct profile per configuration variant, or falling back to
  inline `Composer.Create(builder => ...)` in each affected test — let a
  call site keep the concise, declarative attribute-based idiom
  (`[Compose<TProfile>]` on the method, real composed values in the
  signature) without substantial duplication or hand-written boilerplate
  once the number of real variants grows past a couple. That cost, not any
  specific workaround's mechanics, is the actual evidence.

## The reframing finding

Before evaluating mechanisms, the deep-design pass established a fact that
changes the shape of this entire ADR: **core Compono already solves the
underlying problem.** `CompositionBuilder.AddProfile(ICompositionProfile
profile)` ([ADR-0018](0018-composition-profiles.md)) already exists, and
its own XML doc already states its purpose: *"for a profile that needs
constructor arguments or is otherwise not default-constructible."*
Programmatically, `Composer.Create(b => b.AddProfile(new
PersistenceTestProfile(repositoryName)))` already works today, with no new
code. The gap this ADR closes is entirely one layer up: `Compono.XunitV3`'s
`[Compose<TProfile>]` only supports `TProfile : ICompositionProfile, new()`
— there is no attribute-level path from a compile-time-constant literal to
a non-default-constructed profile instance. **This means the fix belongs
entirely in `Compono.XunitV3` — core `Compono` needs zero new capability.**
See "Considered Options" below for why this rules out treating the
underlying idea as generally useful outside xUnit attributes: it already
is, today, via `AddProfile(ICompositionProfile)`.

## Considered Options

Four genuinely different mechanisms were generated and compared, per
`design-decisions.md`'s deep-dive requirement (not a strawman plus a
preferred option):

### 1. Attribute arguments bind directly to `TProfile`'s own constructor

`[Compose<TProfile>(args...)]`, reflection-matching `args` against
`TProfile`'s constructor directly — no separate config type.

**Rejected — collides with shipped API, not on merit.**
`ComposeAttribute<TProfile>(params object?[] inlineValues)` already ships,
and its constructor arguments already mean "bind to the test method's
leading parameters" ([ADR-0022](0022-compono-xunit-package-design.md)).
Reusing that same argument slot to instead mean "construct `TProfile`
with these" is a silent, ambiguous, breaking redefinition of shipped
behavior — `[Compose<TProfile>(42, "widget")]` today binds `42`/`"widget"`
to test parameters; this option cannot reuse that syntax without breaking
it.

### 2. Ambient scenario/invocation values, resolved via `ICompositionContext`

`Configure(CompositionBuilder)`'s signature stays untouched; a new
per-row value bag is attached to `CompositionRow`, and a factory
registered inside `Configure` pulls a value from it at *resolve* time
(`context.ResolveScenarioValue<T>("name")`), rather than at
profile-construction time.

**Rejected — no evidence justifies the size of this option.** It is the
most general shape (it would also cover values varying *per row*, not
just per method), but nothing in RESEARCH-0002's evidence needs
per-row variation — every real `trivia-platform` call site is one
attribute instance, fixed arguments, applied once per method. It scores
worst on **API clarity/discoverability**: a profile's scenario
dependencies aren't visible in its `Configure` signature at all, only
discoverable by reading every factory body inside it. It is also by far
the largest new surface — a new context API, a new resolution-order
interaction, new diagnostics naming, and new determinism/seed interaction
to design from scratch, none of which RESEARCH-0002's evidence justifies
today. Per [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
evidence-driven restraint, this is named and shelved, not designed
further, until a real per-row-varying call site actually surfaces.

### 3. Source-generated specialization per call site

The generator recognizes `[Compose<TProfile>(literalArgs)]` and emits a
closed, literal-baked construction path per call site — zero runtime
dispatch of the arguments at all.

**Rejected — disproportionate to what it would save.** Profile
construction already happens once per test *method* (cached across every
row that method produces, per ADR-0022's Caching section), not once per
composed object — nowhere near the hot path [ADR-0001](0001-source-generation-first.md)'s
no-reflection rule actually targets (repeated per-object construction
reflection). Buying that already-cheap, already-bounded cost out entirely
would require real new generator complexity (understanding
attribute-literal semantics, routing them into type construction, new
snapshot-test surface) for a marginal runtime saving.

### 4. A typed configuration object paired with the profile (chosen)

A new, distinct attribute — `ComposeAttribute<TProfile, TConfig>` — binds
profile configuration arguments positionally to `TConfig`'s constructor
(reusing [ADR-0022](0022-compono-xunit-package-design.md)'s existing
inline-value positional-binding validation, retargeted rather than
reinvented), constructs `TConfig`, then constructs `TProfile` from that
`TConfig`, then hands the fully-built instance to the **already-existing,
unchanged** `AddProfile(ICompositionProfile)`. See Decision Outcome for
the full shape.

**Chosen** — smallest true addition to the system (one new attribute type
in `Compono.XunitV3`, zero core changes), reuses proven binding-validation
code instead of inventing new logic, and its one real cost (losing
`[Compose<TProfile>]`'s compile-time `new()` enforcement) is a narrow,
nameable tradeoff rather than a structural one. Full evaluation below.

## Decision Outcome

Chosen option: **4 — a typed configuration object paired with the
profile**, implemented entirely in `Compono.XunitV3`, with **zero changes
to core `Compono`** (`ICompositionProfile`, `AddProfile<TProfile>()`,
`AddProfile(ICompositionProfile)`, `ComposeAttribute`,
`ComposeAttribute<TProfile>`, and the existing inline-value binding
algorithm are all unchanged).

### Shape

```csharp
public sealed record PersistenceTestConfig(RepositoryKind Repository);

public sealed class PersistenceTestProfile : ICompositionProfile
{
    private readonly PersistenceTestConfig _config;

    public PersistenceTestProfile(PersistenceTestConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) =>
        builder.Register<IPlayerRepository>(_ => new PlayerRepository(_config.Repository));
}
```

```csharp
[Theory]
[Compose<PersistenceTestProfile, PersistenceTestConfig>(RepositoryKind.Player)]
public void Repository_Works(PlayerRepository sut) { }
```

A new attribute type, distinct from (not a subclass sharing a
constructor-argument slot with) the existing `ComposeAttribute<TProfile>`:

```csharp
namespace Compono.XunitV3;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile, TConfig> : ComposeAttribute
    where TProfile : ICompositionProfile
{
    // Base gets zero inline values - this attribute form composes every
    // test-method parameter in full; profile configuration arguments are
    // a completely separate binding target (see Terminology, above).
    public ComposeAttribute(params object?[] profileConfigurationArguments) : base()
    {
        // stored for use by the cached construction delegates described below
    }
}
```

`TProfile`'s `new()` constraint is dropped (it doesn't apply to this
form — see "What this form deliberately gives up," below); `TConfig` is
unconstrained beyond being a type profile configuration arguments can
bind to.

### Design principle: profile configuration arguments must not encourage stringly typed configuration

`params object?[]` is a **binding mechanism**, forced by C#'s
attribute-argument-must-be-a-compile-time-constant rule — it is not a
license to design `TConfig` types around loosely-typed primitives.
Documentation, samples, and this ADR's own examples use the strongest
meaningful attribute-legal C# type available for each value:

- A value that represents a finite, named choice → an `enum`
  (`RepositoryKind.Player`, not `"Player"` or `"PlayerRepository"`).
- A value that represents a CLR type → `typeof(...)`
  (`typeof(IntentRequest)`, not a type's string name).
- A value that's naturally boolean, numeric, or a genuinely free-form
  string (a locale tag, say) → the corresponding attribute-legal type
  directly, with no artificial enum/type wrapper forced onto it.

`TConfig` itself should be a `record` (per `coding-standards.md`'s
"DTOs... must be immutable" rule) whose constructor parameters are named
and typed to carry real domain meaning — the same discipline any other
strongly-typed configuration object in this codebase already follows, not
a special case for this feature.

### Constructor contracts — deliberately narrow, deterministic, no "best match"

Per the explicit requirement that this stay narrow and predictable rather
than reintroducing AutoFixture-style implicit resolution:

- **`TConfig` must have exactly one public constructor.** Zero or more
  than one is a binding-plan-cache-time failure (see Diagnostics below) —
  never a "pick the best/greediest one" heuristic. Profile configuration
  arguments bind to that one constructor's parameters positionally, using
  the identical validation ADR-0022 already built for inline values
  (count check, `Nullable.GetUnderlyingType`-unwrap-before-assignability
  check, clear per-parameter failure messages) — retargeted at `TConfig`'s
  constructor instead of the test method's parameters, not reimplemented.
- **`TProfile` must have exactly one public constructor accepting exactly
  one parameter of type `TConfig`.** Not "some constructor that could
  accept a `TConfig`," not the greediest overload — an exact,
  single-parameter, exact-type match. Zero or more than one qualifying
  constructor is a binding-plan-cache-time failure, same category as
  `TConfig`'s check.
- **No "best constructor match" algorithm exists anywhere in this
  design**, for either type. Ambiguity is always a hard, named failure,
  never a resolved-by-guessing outcome.

### Diagnostics

All three new checks are **pre-composition, computed once (per attribute
instance, at first `GetData` call), and cached** — the same place and
timing ADR-0022's existing signature-validation checks already run, never
re-checked per row:

| Failure | When | Mechanism |
|---|---|---|
| `TConfig` has zero or >1 public constructors | Binding-plan-cache construction | Plain-message `CompositionException`, naming `TConfig` and the exact-one-constructor rule |
| `TProfile` has no (or >1) public constructor with exactly one `TConfig`-typed parameter | Binding-plan-cache construction | Plain-message `CompositionException`, naming `TProfile`, `TConfig`, and the exact-shape rule |
| Profile configuration argument count/type/nullability mismatch against `TConfig`'s constructor | Binding-plan-cache construction | Same validation and message shape as today's inline-value mismatch diagnostics ([ADR-0022](0022-compono-xunit-package-design.md)), retargeted at `TConfig`'s parameters |

No new exception type — every case reuses the existing
`CompositionException` convention, consistent with "prefer existing
structured diagnostics" (`coding-standards.md`).

### Reflection is bounded and cached, never on the hot path

Building the `TConfig` constructor invoker and the `TProfile(TConfig)`
constructor invoker each happens **exactly once per attribute instance**,
at binding-plan-cache-construction time — the identical
close-once-cache-a-delegate shape ADR-0022 already uses for
`MakeGenericMethod`/`Delegate.CreateDelegate` in "Runtime-Typed
`CompositionRow` Invocation." Every subsequent `GetData` call for that
attribute instance reuses the cached delegates; nothing reflective runs on
the per-row composition path, consistent with
[ADR-0001](0001-source-generation-first.md)'s no-reflection-on-the-hot-path
rule.

### What this form deliberately gives up

**`[Compose<TProfile>]`'s compile-time `new()` enforcement does not carry
over.** Today, `[Compose<NotAProfile>]` for a type without a public
parameterless constructor is a **compile error** — nothing left to
validate at runtime. `ComposeAttribute<TProfile, TConfig>` cannot offer
that: "does `TProfile` have a constructor accepting exactly one `TConfig`"
is not expressible as a C# generic constraint, so it becomes a
**deterministic runtime check** instead (see Diagnostics above) — still
computed once, cached, and failing clearly before any test executes, but
a real, honest regression from a compile error to a pre-composition
runtime error. This is stated explicitly here per the requirement that it
not be glossed over: it is an accepted cost of the chosen shape, not an
oversight.

### Scope: `Compono.XunitV3` only, zero core changes

`ICompositionProfile`, `CompositionBuilder.AddProfile<TProfile>()`,
`CompositionBuilder.AddProfile(ICompositionProfile)`, `ComposeAttribute`,
`ComposeAttribute<TProfile>`, and the existing inline-value binding
algorithm are **all unchanged** by this ADR. The new
`ComposeAttribute<TProfile, TConfig>` type is additive, in
`Compono.XunitV3` only. This directly answers "does the underlying feature
belong in core or only `Compono.XunitV3`": the underlying capability
(building a profile from call-site-known values) is **already** a core
capability, reachable today from any C# call site via
`AddProfile(new Profile(...))` — what's missing is specifically an
attribute-to-instance bridge, which is inherently a problem of
attribute-based test-framework integration, not of the composition engine
itself. A future NUnit/MSTest integration would face the identical
bridging problem and solve it the identical way inside its own package —
not a reason to hoist this into core speculatively before a second
consumer exists.

### Positive Consequences

- Closes RESEARCH-0002's Finding 1 with the smallest true addition to the
  system evaluated — one new `Compono.XunitV3` attribute type, zero core
  changes.
- Reuses ADR-0022's proven positional-binding validation rather than
  inventing a second binding algorithm.
- Strong typing at the point that matters most — inside `Configure`, a
  profile author writes ordinary typed C# against `TConfig`, never
  `object[]` unpacking.
- The "no stringly typed configuration" principle, and the "inline
  values" vs. "profile configuration arguments" terminology split, are
  now first-class, documented parts of the design — not left implicit for
  a future doc pass to get wrong.
- Existing `[Compose]`, `[Compose<TProfile>]`, inline-value binding, and
  `AddProfile<T>()` are completely unaffected — no migration, no behavior
  change, for any test that doesn't opt into the new attribute form.

### Negative Consequences

- Loses `[Compose<TProfile>]`'s compile-time `new()` enforcement for this
  form specifically, replaced by a deterministic but runtime check — see
  "What this form deliberately gives up" above.
- Two attribute forms now exist for profile selection
  (`ComposeAttribute<TProfile>` and `ComposeAttribute<TProfile, TConfig>`)
  instead of one — an accepted, small increase in public-surface area for
  a capability real evidence demonstrates is needed.
- Options 2's more general "any call-site value, including per-row
  variation" capability remains unbuilt — accepted per ADR-0029's
  evidence-driven restraint; revisit only if a real per-row-varying call
  site surfaces.

## Pros and Cons of the Options

### Leave it as-is (not chosen)

- Good, because it requires no further Compono work.
- Bad, because it leaves a high-frequency, high-cost gap unaddressed for
  any real project (not just `trivia-platform`) whose tests need a
  call-site value to shape nested composition.

### Option 1 — args bind directly to `TProfile`'s constructor (rejected)

- Good, because it needs no separate `TConfig` type.
- Bad, because it collides with `ComposeAttribute<TProfile>`'s already-shipped
  inline-value constructor-argument meaning — not resolvable without
  breaking existing behavior.

### Option 2 — ambient scenario values via context (rejected, shelved)

- Good, because it's the most general shape, covering per-row variation
  option 4 doesn't.
- Bad, because nothing in the evidence needs per-row variation, and it's
  the largest new surface of any option considered, with the weakest
  API-discoverability story (a profile's dependencies aren't visible in
  its own signature).

### Option 3 — source-generated specialization (rejected)

- Good, because it's the most "true to source-gen-first" mechanism, with
  zero runtime argument dispatch.
- Bad, because profile construction is already a cheap, one-time-per-method
  cost, not a hot path — the generator complexity this option would add
  isn't proportionate to what it saves.

### Option 4 — typed configuration object paired with the profile (chosen)

- Good, because it reuses proven binding-validation code instead of
  inventing new logic.
- Good, because it requires zero core `Compono` changes.
- Good, because it keeps strong typing at the `Configure` boundary.
- Bad, because it loses `[Compose<TProfile>]`'s compile-time `new()`
  enforcement, replaced by a deterministic runtime check — an accepted,
  explicitly-stated cost.

## Amendment 1 (2026-08-09): direct `ConstructorInfo.Invoke`, not a cached delegate

"Reflection is bounded and cached, never on the hot path" (above)
specified the identical close-once-cache-a-delegate shape
[ADR-0022](0022-compono-xunit-package-design.md) uses for
`MakeGenericMethod`/`Delegate.CreateDelegate` — building a cached invoker
delegate for `TConfig`'s and `TProfile`'s constructors once, at
binding-plan-cache-construction time. PLAN-0036's implementation does not
do this: `ConfigProfileBinder` calls `ConstructorInfo.Invoke` directly
(via a small shared `Invoke` helper that also unwraps
`TargetInvocationException`, per PR #65 review round 3), with no
separate delegate-caching layer of its own.

This is a correction to that section's implementation detail, not a
reversal of the section's actual guarantee. The guarantee — reflection
bounded to once per attribute instance, never on the repeated per-row
`GetData` path — still holds, for a different reason than originally
assumed: `ComposeAttribute<TProfile, TConfig>.ApplyProfile` (the only
caller of `ConfigProfileBinder`'s methods) is itself only ever invoked
once per attribute instance, from inside the base `ComposeAttribute`'s
existing `Lazy<Composer>`-backed caching (`ComposeAttribute.cs`'s
`_composer` field) — a caching layer this ADR's original design already
relied on for the *composer* as a whole, but didn't originally credit
with also bounding the *constructor-resolution* reflection specifically.
`RowInvokers`' `MakeGenericMethod`/`Delegate.CreateDelegate` shape exists
for a genuinely different reason: it closes a generic method over a
parameter type known only at runtime (a test method's own
`ParameterInfo.ParameterType`, discovered per-parameter across
potentially many parameters), which needs a delegate cache to avoid
`MakeGenericMethod`/`MethodInfo.Invoke` cost repeating per row.
`TConfig`/`TProfile` need no equivalent: they are already
compile-time-closed generic arguments on
`ComposeAttribute<TProfile, TConfig>` itself, so there is no per-runtime-
discovered-type generic closure to cache in the first place — a direct
`ConstructorInfo.Invoke`, called once (per the `Lazy<Composer>` guarantee
above), already satisfies the no-reflection-on-the-hot-path requirement
without needing the heavier delegate-caching mechanism.

This does not change the ADR's Decision Outcome (Option 4 remains
chosen) or any of its stated tradeoffs — it corrects one implementation
detail this ADR specified more precisely than turned out necessary, per
`design-decisions.md`'s Amendment mechanic for a correction discovered
during implementation.

## Links

- [RESEARCH-0002](../research/0002-trivia-platform-comparison.md) —
  Finding 1, the evidence this ADR records
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the rubric/classification framework and evidence-driven-restraint rule
  this ADR follows
- [ADR-0022](0022-compono-xunit-package-design.md) — governs
  `[Compose<TProfile>]`'s current fixed-type-only selection
- [ADR-0018](0018-composition-profiles.md) — governs `ICompositionProfile`'s
  current no-argument `Configure` shape
- [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md) —
  the immutable-builder constraint a future solution must respect
- [ADR-0001](0001-source-generation-first.md) — the no-reflection-by-default
  constraint a future solution must respect
- `ncipollina/trivia-platform` — the repo whose real call sites motivate
  this ADR; not part of this monorepo
