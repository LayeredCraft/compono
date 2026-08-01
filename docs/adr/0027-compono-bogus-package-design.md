# [ADR-0027] Compono.Bogus Package Design

**Status:** Accepted

**Date:** 2026-07-31

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

[ADR-0024](0024-public-provider-extensibility-model.md) settled the mechanism a
stage-5/6 package plugs into, and validated (by sketch, not commitment) that it
would hold up for Bogus. [ADR-0026](0026-deterministic-seed-derivation-for-providers.md)
closed the one real gap that sketch exposed: nothing gave a provider or factory
deterministic randomness. This ADR turns `docs/mvp.md`'s Milestone 6 scope
("Semantic value-provider contract, Shared deterministic seed, Bogus `Faker`
access, Locale configuration, Conservative member-name conventions, Explicit
member rules, Initial correlated-value experiment") into a real design, and
corrects `docs/public-api.md`'s existing Bogus Integration sketch, which predates
both ADR-0024 and ADR-0026 and shows a `context.Semantic.Email()` accessor that
would require core `ICompositionContext` to carry Bogus-shaped vocabulary — ruled
out during this milestone's design review (`design-decisions.md` rule 3).

`Compono.Bogus` ends up with **three** distinct, independently useful
customization models, not one:

1. A built-in, conservative **member-name convention provider** (stage 5) — the
   "just call `UseBogus()` and get realistic values" default case.
2. An explicit **member-level rule** — `.For<T>().Member(x => x.Y).UseBogus(faker => ...)`
   (stage 4) — for a specific member the conventions don't (or shouldn't) guess.
3. An explicit **whole-object `Faker<T>`** registration — `UseBogus<T>(faker => faker.RuleFor(...))`
   (compiles to stage 3) — for a type a consumer wants Bogus to construct
   entirely, correlated rules and all, instead of the generated constructor plan.

All three share one deterministic-randomness story (`DeriveSeed()`, ADR-0026) and
one coexistence story with `Compono.NSubstitute` (disjoint type claims — see
below) — this ADR designs all three together since they're one package's public
surface, but keeps them conceptually separate rather than forcing them into a
single mechanism.

## Decision Drivers

- `docs/mvp.md`'s explicit caution: "Ambiguous member names such as `Name` should
  not be guessed aggressively" — the convention provider's allowlist must be
  small, exact-match, and type-checked, never fuzzy/substring matching.
- `docs/mvp.md`'s MVP success criterion #7 / `design-decisions.md` rule 3: the
  core `Compono` package must never reference or know about Bogus. Every design
  choice below routes through core capabilities that already exist (stage 5
  providers, stage 4 rules, stage 3 registrations, `DeriveSeed()`) rather than
  asking core for anything Bogus-shaped.
- Coexistence with `Compono.NSubstitute`, without either package depending on the
  other: `docs/architecture.md`'s Resolution Pipeline already fixes stage 5
  (semantic) before stage 6 (test-double), and a request each package's provider
  can plausibly both want to claim must not arise. This ADR achieves that by
  construction — see Coexistence with `Compono.NSubstitute`, below — not by
  either provider special-casing the other.
- Determinism (ADR-0026): every Bogus-produced value must be reproducible from
  the same root seed, independent of unrelated members added elsewhere in the
  graph, and safe under concurrent composition (no shared mutable `Faker`/
  `Randomizer` state across requests).
- `docs/mvp.md`'s explicit non-goal framing (mirroring ADR-0025's own "no
  recursive auto-configuration"/"no framework API wrappers" restraint): this
  package activates and configures Bogus; it does not re-expose or wrap Bogus's
  own `Faker`/`Faker<T>` API surface beyond what activation requires.

## Considered Options

### Whole-object correlated values: `.DependsOn(...)` vs. `Faker<T>`

1. **Design a new Compono-native member-dependency mechanism** —
   `.For<T>().Member(x => x.Email).DependsOn(x => x.FirstName, x => x.LastName).UseBogus((faker, first, last) => ...)`,
   requiring new core semantics: member composition ordering, retaining
   already-composed sibling values, dependency-cycle detection, and interaction
   with constructor-parameter/required-member composition order.
2. **Use Bogus's own `Faker<T>` for the correlated case**, which already threads
   the partially-populated object into a later `RuleFor` callback
   (`.RuleFor(x => x.Email, (f, x) => f.Internet.Email(x.FirstName, x.LastName))`)
   — no new Compono mechanism at all, since `Faker<T>` already solves exactly
   this problem inside Bogus itself.

### Whole-object `Faker<T>` registration: new pipeline concept vs. sugar over `Register<T>`

1. **A first-class `UseBogus<T>(...)` API that is purely ergonomic sugar over
   the existing stage-3 `Register<T>(Func<ICompositionContext, T>)`
   registration mechanism, with no hidden pipeline stage and no special
   runtime behavior of its own** — no new pipeline stage, no new
   conflict-detection rule; duplicate registration for the same `T` is the
   same build-time `CompositionConfigurationException` `Register<T>` already
   produces.
2. **Document the raw `Register<T>(context => faker.UseSeed(context.DeriveSeed()).Generate())`
   pattern only, with no package-owned convenience API at all.**

### Locale coupling between package-wide activation and `UseBogus<T>()`

1. **Fully independent** — `UseBogus<T>()` takes its own optional locale (or
   defaults to Bogus's own default), with no dependency on whether/how
   `UseBogus()`/`UseBogus(options => ...)` was called elsewhere in the same
   configuration.
2. **`UseBogus<T>()` requires prior `UseBogus()` activation** and reads the
   shared `BogusOptions.Locale`, making the two calls order-dependent.

## Decision Outcome

**Chosen: Option 2 (`Faker<T>` for correlated values, not `.DependsOn(...)`),
Option 1 (`UseBogus<T>()` as sugar over `Register<T>`), Option 1 (fully
independent locale)** — all three confirmed directly with the user during
design review.

**When to reach for each model** — the three are complementary, not competing,
and a real profile typically uses more than one at once (see `docs/public-api.md`'s
Bogus Integration section for the worked example):

- **`UseBogus()`** — project-wide conventions: "most `FirstName`/`Email`/etc.
  members across the whole graph should just look realistic," with zero
  per-type setup.
- **`.Member(...).UseBogus(faker => ...)`** — a handful of members need
  something the convention allowlist doesn't (or shouldn't) guess — an
  ambiguous name, a domain-specific format, a member the convention provider
  would otherwise miss entirely.
- **`UseBogus<T>()`** — a type's values are meaningfully correlated with each
  other (an email derived from a name, a full address whose fields agree) and
  the whole object is more naturally described as "Bogus owns this type" than
  as several independent member rules.

### Model 1: Conservative member-name conventions (stage 5)

```csharp
public sealed class BogusOptions
{
    /// <summary>
    /// The Bogus locale used by the package-wide member-name convention provider
    /// (<see cref="BogusMemberNameProvider"/>) only. <c>UseBogus&lt;T&gt;()</c> is independent of this
    /// option and does not read it — it defaults to <c>"en"</c> on its own, or takes an explicit
    /// <c>locale</c> parameter. Defaults to Bogus's own default ("en").
    /// </summary>
    public string Locale { get; set; } = "en";

    /// <summary>Whether the conservative member-name convention provider is active. Defaults to <see langword="true"/>.</summary>
    public bool EnableMemberNameConventions { get; set; } = true;
}

public sealed class BogusMemberNameProvider : ICompositionValueProvider
{
    private readonly string _locale;

    public BogusMemberNameProvider(string locale) => _locale = locale;

    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (request.RequestedType != typeof(string) || request.Name is not { } name)
            return CompositionProviderResult.NotHandled;

        if (!Conventions.TryGetValue(name, out var generate))
            return CompositionProviderResult.NotHandled;

        var faker = new Faker(_locale) { Random = new Randomizer(context.DeriveSeed()) };
        return CompositionProviderResult.Handled(generate(faker));
    }

    // Immutable, built once — a FrozenDictionary, not a plain Dictionary, since this is a
    // fixed, read-only lookup table shared across every request this provider ever handles.
    private static readonly FrozenDictionary<string, Func<Faker, string>> Conventions =
        new Dictionary<string, Func<Faker, string>>
        {
            ["FirstName"] = f => f.Name.FirstName(),
            ["LastName"] = f => f.Name.LastName(),
            ["FullName"] = f => f.Name.FullName(),
            ["Email"] = f => f.Internet.Email(),
            ["PhoneNumber"] = f => f.Phone.PhoneNumber(),
            ["StreetAddress"] = f => f.Address.StreetAddress(),
            ["City"] = f => f.Address.City(),
            ["State"] = f => f.Address.State(),
            ["PostalCode"] = f => f.Address.ZipCode(),
            ["CompanyName"] = f => f.Company.CompanyName(),
        }.ToFrozenDictionary();
}
```

- **Exact match only, case-sensitive, against `CompositionProviderRequest.Name`**
  — no substring/prefix/fuzzy matching, no attempt at pluralization or synonym
  handling. `Name` (ambiguous per `docs/mvp.md`'s own callout) is deliberately
  absent from the allowlist.
- **Type-gated to `string` only** — `RequestedType != typeof(string)` declines
  immediately. This is also what makes coexistence with `Compono.NSubstitute`
  automatic (below): every convention in `docs/mvp.md`'s list produces a
  `string`, so this provider can never claim an interface, delegate, or abstract
  class — the exact request shapes `NSubstituteProvider` claims instead.
- **On by default** whenever `UseBogus()`/`UseBogus(options => ...)` is called —
  matching `Compono.NSubstitute`'s own "substitutable by default, only the
  narrower abstract-class case needs its own toggle" precedent. A consumer who
  wants Bogus active only for explicit rules sets
  `options.EnableMemberNameConventions = false`.
- **A fresh `Faker`/`Randomizer` per handled request**, seeded from
  `context.DeriveSeed()` — never a package-lifetime-shared instance. This is
  what keeps every produced value both deterministic (same seed, same request
  path → same value) and safe under concurrent composition (no instance is ever
  touched from more than one request).
- **Yields to everything earlier in the pipeline** — stage 3 (registrations),
  stage 4 (configuration rules, including Model 2's own member-level `UseBogus(...)`
  rule) both run before stage 5, so an explicit rule for the same member always
  wins over the convention guess, with zero special-casing in this provider —
  purely a consequence of `docs/architecture.md`'s already-fixed stage order.

### Model 2: Explicit member-level rule (stage 4, sugar over `.Use(...)`)

```csharp
namespace Compono;

public static class MemberRuleExtensions
{
    extension<T, TMember>(MemberRuleBuilder<T, TMember> builder)
        where TMember : notnull
    {
        public CompositionBuilder UseBogus(Func<Faker, TMember> configure, string locale = "en")
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.Use(context =>
            {
                var faker = new Faker(locale) { Random = new Randomizer(context.DeriveSeed()) };
                return configure(faker);
            });
        }
    }
}
```

(Exact generic member-rule-builder type name — `MemberRuleBuilder<T, TMember>`
above — is illustrative, matching whatever
[ADR-0020](0020-composition-configuration-rules.md)'s `.For<T>().Member(x => x.Y)`
chain actually returns; not a new type this ADR introduces.)

- No `context.Semantic` accessor, no core change beyond
  [ADR-0026](0026-deterministic-seed-derivation-for-providers.md)'s already-generic
  `DeriveSeed()`. `Compono.Bogus` owns `Faker` construction, locale, and
  `Randomizer` seeding entirely on its own side of the `.Use(context => ...)`
  boundary [ADR-0020](0020-composition-configuration-rules.md) already
  established for every other stage-4 rule.
- Compiles into an ordinary stage-4 rule, so it automatically wins over Model 1's
  convention guess for the same member (member rules already take precedence
  over type rules and, by pipeline order, over stage 5 entirely) and is itself
  overridden by an exact stage-3 registration for the same type, exactly like
  any other rule.
- Takes its own `locale` parameter (default `"en"`, matching Bogus's own
  default) — deliberately independent of `BogusOptions.Locale`, consistent with
  Model 3's own locale independence, below.

### Model 3: Whole-object `Faker<T>` registration (compiles to stage 3)

```csharp
namespace Compono;

public static class CompositionBuilderExtensions
{
    extension(CompositionBuilder builder)
    {
        public CompositionBuilder UseBogus() => builder.UseBogus(static _ => { });

        public CompositionBuilder UseBogus(Action<BogusOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var options = new BogusOptions();
            configure(options);
            if (options.EnableMemberNameConventions)
                builder.AddSemanticProvider(new BogusMemberNameProvider(options.Locale));
            return builder;
        }

        public CompositionBuilder UseBogus<T>(Action<Faker<T>> configureFaker)
            where T : class =>
            builder.UseBogus("en", configureFaker);

        public CompositionBuilder UseBogus<T>(string locale, Action<Faker<T>> configureFaker)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(configureFaker);
            return builder.Register<T>(context =>
            {
                var faker = new Faker<T>(locale);
                configureFaker(faker);
                return faker.UseSeed(context.DeriveSeed()).Generate();
            });
        }
    }
}
```

- **Fully independent of `UseBogus()`/`UseBogus(options => ...)`** — no ordering
  dependency, no implicit read of `BogusOptions.Locale`. A consumer who wants
  both to share a locale centralizes the string themselves (a profile constant,
  per the design-review example), rather than this package inferring it through
  hidden coupling. This is deliberate, not an oversight worth "fixing" with
  shared state later: `UseBogus<T>()` is purely ergonomic sugar over the
  existing `Register<T>()` registration mechanism (stage 3) — a plain exact
  registration, no hidden pipeline stage, no special runtime behavior of its
  own — while `UseBogus()` activates `BogusMemberNameProvider` (stage 5).
  They're different pipeline stages solving
  different problems, and neither needs the other to function; a consumer can
  call `UseBogus<T>()` alone, with `UseBogus()` never called at all, and it
  works exactly the same. Coupling them (requiring one before the other, or
  silently reading shared options) would blur that separation for a benefit
  that only shows up when a consumer wants the same locale in both places —
  handled instead by that consumer sharing one string constant, per the
  worked example in `docs/public-api.md`'s Bogus Integration section.
- **`locale` is a plain `string`, not an options type, and this is deliberate.**
  A `BogusGenerationOptions`-style type was considered during review and
  rejected: `Locale` is the only per-registration setting `Faker<T>`'s own
  constructor takes today, and `docs/public-api.md`'s own API Design Rules
  already caution against speculative surface — a one-property options type
  exists only to look extensible, not because a second option is needed now.
  If a real per-registration knob emerges (Milestone 7 dogfooding, or later),
  an `Action<BogusGenerationOptions>`-based overload can be added then,
  alongside the existing `string`-based one, without breaking it — this ADR's
  Alpha Compatibility Policy (inherited from
  [ADR-0024](0024-public-provider-extensibility-model.md)) already covers
  exactly this kind of justified, evidence-driven addition. Call sites should
  use the named argument for readability: `UseBogus<Customer>(locale: "fr",
  configureFaker: faker => faker.RuleFor(...))`.
- **`configureFaker` is `Action<Faker<T>>`, not `Func<Faker<T>, Faker<T>>`.** The
  callback *configures* a `Faker<T>` instance; it doesn't transform one into
  another. `Faker<T>`'s own `RuleFor`/`CustomInstantiator`/`FinishWith` API is
  fluent and returns `this` purely as a chaining convenience — a `Func`-shaped
  callback would only ever accidentally work if a caller forgot to return the
  same instance (or deliberately returned a *different* one, silently
  discarding the original). `Action<Faker<T>>` makes returning anything
  impossible to get wrong, and matches this package's own `Action<BogusOptions>`/
  `Action<NSubstituteOptions>`-shaped configuration callbacks elsewhere in
  Compono, rather than introducing the one `Func`-shaped configuration
  callback in the whole public surface.
- **Compiles to `Register<T>(Func<ICompositionContext, T>)`** — no new pipeline
  stage, no new conflict rule. Two `UseBogus<T>()` calls (or one `UseBogus<T>()`
  plus one direct `Register<T>(...)`) for the same `T` hit the exact same
  build-time `CompositionConfigurationException` any duplicate registration
  already produces ([ADR-0019](0019-registrations-and-service-provider-injection.md)).
- **A fresh `Faker<T>` per resolved request, never a shared instance.**
  `configureFaker` (the `Action<Faker<T>>` callback) is captured once at
  `Build()` time, same as any other `Register<T>` factory closure — but it is
  *invoked* once per `T` resolution, and each invocation constructs its own new
  `Faker<T>(locale)` before handing it to `configureFaker`. There is no `Faker<T>`
  instance alive before the first request, and no instance is retained or
  reused after a request completes: `context.DeriveSeed()` (this specific
  request's own derived seed) and `Generate()` both happen against that
  request's own fresh instance, inside the same `Register<T>` factory call.
  This matches `NSubstituteProvider`'s own "stateless per call" shape exactly —
  no mutable state survives from one request to the next, so two concurrent
  `Create<T>()` calls for the same `T` never touch the same `Faker<T>` object at
  all, let alone race on it.

  **Caching a configured `Faker<T>` across requests was considered and
  deliberately rejected.** The obvious "optimization" — construct one
  `Faker<T>`, run `configureFaker` on it once at `Build()` time, and reuse that
  same instance on every subsequent `Generate()` call, reseeding it per request
  via `UseSeed(...)` — looks cheaper (one `Faker<T>` construction instead of
  one per request) but is unsafe: `Faker<T>` carries mutable generation state
  (its own internal `Randomizer`, rule-evaluation state `UseSeed`/`Generate`
  read and write), and Bogus does not document or guarantee that a single
  `Faker<T>` instance tolerates concurrent `Generate()` calls. Reusing one
  instance across requests would reintroduce exactly the shared-mutable-state
  race this section's "never a shared instance" design avoids, the first time
  someone reached for it as a performance improvement. This section's design —
  a fresh instance per request — is the version to keep; a future contributor
  proposing the cached-instance shape as a same-behavior optimization should
  treat this paragraph as the record of why it was already rejected, not
  rediscover the tradeoff from scratch.
- **Correlated values already work** — `Faker<T>.RuleFor((f, x) => ...)`,
  `CustomInstantiator`, `FinishWith`, and nested `Faker<TNested>` generation are
  all ordinary `Faker<T>` API, entirely inside the `configureFaker` callback; this
  package adds nothing on top of them. This satisfies `docs/mvp.md`'s "Initial
  correlated-value experiment" scope item without a new Compono mechanism — see
  the `.DependsOn(...)` deferral below.

### `.DependsOn(...)` correlated-rule API — deferred

`docs/mvp.md`'s "Initial correlated-value experiment" is satisfied by Model 3's
`Faker<T>` (above), not by a new Compono-native member-dependency mechanism.
`.For<T>().Member(...).DependsOn(...).UseBogus(...)` is **not** built in this
milestone: it would require new core semantics (member composition ordering,
retaining already-composed sibling values for a later rule to read, dependency-
cycle detection, interaction with constructor-parameter/required-member ordering)
that are a materially larger scope than "integrate Bogus," and would duplicate
capability `Faker<T>` already provides natively. `docs/public-api.md`'s existing
`.DependsOn(...)` sketch is retained as an explicitly deferred future
possibility, not deleted — see Links.

### Coexistence with `Compono.NSubstitute`

No shared code, no reference between the two packages in either direction —
cooperation falls out entirely of the existing fixed pipeline order
(`docs/architecture.md`) plus each provider's own narrow type claim:

- **Stage order is fixed**: semantic providers (stage 5, Bogus) are always tried
  before test-double providers (stage 6, NSubstitute), for every request,
  regardless of registration order between `UseBogus()`/`UseNSubstitute()` in a
  profile or builder chain.
- **Disjoint type claims, by construction, not by coordination.** `BogusMemberNameProvider`
  only ever claims `RequestedType == typeof(string)`; `NSubstituteProvider` only
  ever claims an interface, delegate, or (optionally) unsealed abstract class —
  `string` is none of those. Neither provider needs to know the other package
  exists for this to hold; it's a consequence of what each provider's static
  type check actually is.
- **Explicit registrations and configuration rules still win over both** — stage
  3/4 run before stage 5/6 unconditionally, so a `Register<T>`/`.For<T>().Use(...)`
  for a member both packages might otherwise touch always takes precedence,
  with no special-casing needed in either package.
- **`[Shared]` reuse composes across both**: a `[Shared]` interface parameter
  substituted by `NSubstituteProvider` and an ordinary string member supplied by
  `BogusMemberNameProvider` both write into the same `CompositionScope`, exactly
  like any other stage's successful result
  ([ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)) —
  no interaction to design, since scope reuse has never been provider-specific.
- **`UseBogus()` and `UseNSubstitute()` in the same profile, any call order** —
  since neither reads the other's configuration and stage placement is fixed
  independent of registration order, `builder.UseNSubstitute().UseBogus()` and
  `builder.UseBogus().UseNSubstitute()` compose identically.

### Package boundary

`Compono.Bogus` depends on `Compono` and `Bogus` only, matching the existing
package-dependency diagram (`docs/architecture.md`). `BogusMemberNameProvider`/
`BogusOptions`/`CompositionBuilderExtensions`/`MemberRuleExtensions` are the
package's entire public surface. `Compono.Bogus` never references
`Compono.NSubstitute`, and vice versa.

### Positive Consequences

- All three customization models compile to existing pipeline mechanism (stage
  5 provider, stage 4 rule, stage 3 registration) — zero new core mechanism
  beyond ADR-0026's `DeriveSeed()`.
- Coexistence with `Compono.NSubstitute` requires no coordination code in either
  package — provably disjoint by each provider's own static type check, verified
  by test rather than by mutual awareness.
- Correlated values are satisfied by Bogus's own `Faker<T>`, not a new,
  independently-maintained Compono mechanism that would duplicate it.
- Locale is never implicitly coupled between activation and whole-object
  generation — no hidden call-order dependency to document or get wrong.

### Negative Consequences

- `.DependsOn(...)` remains undesigned — a real gap for a consumer who wants
  member-level (not whole-object) correlated rules without adopting `Faker<T>`
  for that entire type. Accepted as a deliberate scope cut, revisit only if
  Milestone 7 dogfooding surfaces a real need `Faker<T>` doesn't already cover.
- Two separate locale parameters (`BogusOptions.Locale` for Model 1, a per-call
  `locale` parameter for Models 2/3) means a consumer wanting one consistent
  locale across all three models repeats the string — a small, deliberate
  ergonomics cost in exchange for no hidden coupling (see Decision Drivers).

## Pros and Cons of the Options

### Correlated values via `Faker<T>` (chosen)

- Good, because it needs zero new Compono mechanism.
- Good, because it's exactly what Bogus's own API is already designed for.
- Bad, because it only covers the whole-object-generation case, not a
  correlated *member*-level rule layered onto an otherwise-generated-plan type.

### Correlated values via a new `.DependsOn(...)` mechanism

- Good, because it would let a consumer correlate one member against a sibling
  without adopting whole-object `Faker<T>` generation for the entire type.
- Bad, because it requires new core ordering/retention/cycle-detection semantics
  well beyond "integrate Bogus" — a separate design problem in its own right.
- Bad, because it duplicates capability `Faker<T>` already provides natively.

### `UseBogus<T>()` as sugar over `Register<T>` (chosen)

- Good, because duplicate-registration conflict behavior, build-time validation,
  and stage-3 precedence all come for free, already tested by
  `Compono.Tests`' existing `Register<T>` coverage.
- Good, because it's a first-class, discoverable Bogus-specific entry point
  despite adding zero new pipeline concept.
- Good, because constructing a fresh `Faker<T>` inside the factory (rather than
  once, outside it, and reused) means the concurrency question `Register<T>`'s
  "factory closure, invoked repeatedly" shape might otherwise raise never
  arises — no instance is ever shared across requests to begin with.

### `UseBogus<T>()` fully independent of package-wide locale (chosen)

- Good, because behavior never depends on builder call order.
- Good, because "own locale, no dependency" is the same posture core's own
  scalar-configuration rules already use (set once, no implicit cross-reads).
- Bad, because a consumer wanting one shared locale across every Bogus feature
  states it twice rather than once.

## Amendment 1 (2026-08-01): `UseSeed()` must run before `configureFaker`, not after

PR #33 review (Codex, one P2 finding against Phase 1's implementation of this
ADR's Model 3) caught a real defect in this ADR's own `UseBogus<T>()` code
sketch: `configureFaker(faker)` ran *before* `faker.UseSeed(context.DeriveSeed())`.
`Faker<T>.RuleFor` factories (`f => f.Internet.Email()`) are lazy — they don't
draw randomness until `Generate()` runs, so those are unaffected — but a
`configureFaker` callback that eagerly reads randomness at configuration time
(e.g. `RuleFor(x => x.Id, faker.Random.Guid())`, an already-evaluated value
rather than a lazy factory delegate) draws from `faker.Random` *before* this
ADR's own seed had been applied, using Bogus's own default, unseeded
`Randomizer` state instead. Two `Create<T>()` calls with the same Compono seed
could then produce different objects for that one eagerly-read member —
exactly the determinism contract this whole design exists to guarantee,
broken by the sketch's own statement order, not by anything a consumer did
wrong.

**Fix:** apply `UseSeed(context.DeriveSeed())` immediately after constructing
`Faker<T>`, *before* calling `configureFaker`, then call `Generate()` last:

```csharp
var faker = new Faker<T>(locale).UseSeed(context.DeriveSeed());
configureFaker(faker);
return faker.Generate();
```

`UseSeed(...)` sets the instance's `Random` immediately (not lazily) and that
state persists for both `configureFaker`'s own execution and the later
`Generate()` call, so one seed application covers both an eager read inside
`configureFaker` and every lazy `RuleFor` factory `Generate()` evaluates. This
is a *statement-order* correction only — every architectural guarantee this
ADR's Model 3 section commits to (fresh `Faker<T>` per request, never shared/
cached, no new pipeline mechanism) is unchanged. `BogusMemberNameProvider`
and the member-rule `UseBogus(...)` sugar (Models 1/2) were already correct —
both construct their `Faker`/apply its `Random` before calling into user code,
via an object initializer (`new Faker(locale) { Random = new Randomizer(...) }`)
rather than a separate statement, so this defect was scoped to Model 3 only.

Caught and fixed within PLAN-0006 Phase 1, before any external consumer or
its test-suite phase existed — `docs/plans/0006-milestone-6-bogus-integration.md`'s
own Phase 3 (Test suites and verification — renumbered from Phase 2 after
ADR-0028's later Phase 2 insertion) task list now includes explicit
regression coverage for this ordering (an eager-random-read `configureFaker`
callback, asserted deterministic for the same seed).

## Links

- [docs/mvp.md](../mvp.md) — Milestone 6 scope, non-goals, exit criterion
- [docs/public-api.md](../public-api.md) — Bogus Integration section (this ADR
  replaces its `context.Semantic.Email()` sketch and its `.DependsOn(...)`
  sketch's status, from "design goal" to "explicitly deferred, see this ADR")
- [docs/architecture.md](../architecture.md) — `Compono.Bogus` Package
  Boundaries entry, stage 5 (semantic value providers)
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  the shared-scope mechanism the coexistence section relies on unchanged
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) —
  the path-independence guarantee this package's values inherit via
  `DeriveSeed()`
- [ADR-0019](0019-registrations-and-service-provider-injection.md) — the
  `Register<T>` mechanism `UseBogus<T>()` compiles into, including its
  duplicate-registration conflict behavior
- [ADR-0020](0020-composition-configuration-rules.md) — the `.For<T>().Member(...).Use(...)`
  mechanism the member-level `UseBogus(faker => ...)` sugar wraps
- [ADR-0024](0024-public-provider-extensibility-model.md) — the public provider
  contract `BogusMemberNameProvider` implements; the Bogus sketch this ADR
  supersedes with a real design
- [ADR-0025](0025-compono-nsubstitute-package-design.md) — `Compono.NSubstitute`,
  the package this ADR's Coexistence section verifies against with no code
  shared in either direction
- [ADR-0026](0026-deterministic-seed-derivation-for-providers.md) — the
  `DeriveSeed()` capability every model in this ADR builds its determinism on
