# [ADR-0028] Configurable Bogus Member-Name Conventions

**Status:** Accepted

**Date:** 2026-08-01

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

[ADR-0027](0027-compono-bogus-package-design.md) shipped `BogusMemberNameProvider`
with a fixed, hardcoded allowlist of ten member names (`FirstName`, `LastName`,
`FullName`, `Email`, `PhoneNumber`, `StreetAddress`, `City`, `State`,
`PostalCode`, `CompanyName`) — deliberately conservative, per `docs/mvp.md`'s own
caution against guessing ambiguous names aggressively. That allowlist has no
consumer-facing configuration surface at all today: a project whose domain uses
`GivenName`/`Surname` instead of `FirstName`/`LastName`, or that wants a
package-wide semantic convention for a domain-specific name like `Sku`, has no
way to extend the convention provider — the only escape hatches are the
member-level `.Member(...).UseBogus(faker => ...)` sugar (one member at a time)
or the whole-object `UseBogus<T>(...)` sugar (one type at a time), both of which
require repeating the same mapping at every call site rather than declaring it
once, package-wide.

This ADR is scoped to a single new capability — configurable conventions on
`BogusOptions` — and is deliberately a **new** ADR, not an amendment to
ADR-0027: ADR-0027's own accepted Decision Outcome (the three-model split, the
fixed built-in allowlist, its Considered Options and Pros/Cons) is unchanged by
this design and stays exactly as originally written, per
`design-decisions.md`'s rule that an amendment is for a correction to an
existing decision, not a genuinely new one layered on top of it. ADR-0027
remains the accepted foundation this ADR builds on.

## Decision Drivers

- `docs/mvp.md`'s explicit caution against aggressive/fuzzy name matching
  applies equally to this extension: exact match, case-sensitive, no
  substring/pattern/priority matching, carried over from ADR-0027 unchanged.
- `design-decisions.md` rule 3: core `Compono` must never reference or know
  about Bogus. Every mechanism below has to be achievable entirely inside
  `Compono.Bogus`, with zero new core surface — a constraint this design
  explicitly tested against (see Considered Options, below) and confirmed
  holds.
- ADR-0026's determinism contract: every value this feature produces —
  built-in, alias, or custom — must remain reproducible from the same root
  seed, independent of unrelated members, via the same `context.DeriveSeed()`
  mechanism ADR-0027 already established.
- Keep the feature bounded and predictable, matching the user's own explicit
  framing for this design: exact-name matching only, fail-fast configuration
  errors over silent last-wins, no replacing/removing built-in conventions in
  this version, no fuzzy/pattern/priority matching, no new Bogus-specific
  rules language.
- Cost-proportionality: a capability whose cost (new core surface, deferred
  validation, hidden mutable state) exceeds its demonstrated need should be
  deferred until a second real consumer justifies it, matching this repo's
  own established restraint elsewhere (e.g. ADR-0024's Alpha Compatibility
  Policy, ADR-0027's `.DependsOn(...)` deferral).

## Considered Options

### Where alias/custom-convention entries live relative to the built-in allowlist

1. **One merged, immutable lookup inside a single `BogusMemberNameProvider`** —
   built-ins, aliases, and custom conventions all populate one
   `FrozenDictionary<string, Func<Faker, string>>`, built once per
   `UseBogus(configure)` call, after `configure(options)` returns. A name can
   only ever map to one generator; no runtime ambiguity, no provider
   registration order to reason about.
2. **A separate provider (or providers) for aliases/custom conventions**,
   registered alongside the existing `BogusMemberNameProvider` via a second
   `AddSemanticProvider` call. Collisions between a custom name and a
   built-in name would only surface at request-match time (registration-order
   first-match-wins), not as a configuration-time diagnostic.

### Conflict-detection scope across multiple `UseBogus(...)` calls

1. **Scoped to one `UseBogus(configure)` call only.** Each call independently
   builds and validates its own merged lookup; a second, separate
   `UseBogus(...)` call (e.g. from a different profile) registers its own
   independent provider, composing via the pipeline's existing, unrelated
   registration-order/first-match-wins semantics — exactly how calling
   `UseBogus()` twice already behaves today, with or without this ADR.
2. **Cross-call accumulation, validated when the `Composer`'s configuration is
   built** — every `UseBogus(...)` call (direct or via a profile) contributes
   to one builder-scoped accumulator, frozen and validated at `Build()` time.
   Investigated in depth (see the design review this ADR's Links section
   points at): achieving literal `Build()`-time validation requires
   `CompositionBuilder` to expose some way for an integration package to defer
   work until `Build()` actually runs — today `AddSemanticProvider` registers
   an already-built provider instance immediately, with no such hook. Two
   sub-options were evaluated for supplying that hook:
   1. A new, small, generic, Bogus-agnostic `CompositionBuilder`
      build-finalization capability, in its own core-extension ADR — mirroring
      this repo's own established split (ADR-0024/ADR-0025,
      ADR-0026/ADR-0027).
   2. A `ConditionalWeakTable<CompositionBuilder, ...>`-keyed accumulator
      entirely inside `Compono.Bogus`, freezing on first use (first
      composition request that actually needs Bogus) instead of literally at
      `Build()` — functionally equivalent in final configuration outcome, but
      surfaces a configuration-typo error later (first relevant
      `composer.Create<T>()` call) than at the `Composer.Create(builder => ...)`
      call site itself, and introduces hidden mutable state keyed off object
      identity.

### `BogusConvention`'s public representation

1. **A plain `enum`** — `FirstName`, `LastName`, etc. — one member per
   built-in convention, no behavior or metadata beyond identity.
2. **A richer strongly-typed value object** ("smart enum" pattern: a sealed
   class or `readonly record struct` with static readonly named instances)
   allowing per-value behavior/metadata beyond a bare name.

### Custom exact-name conventions' supported value type

1. **`string` only** — `AddConvention(string memberName, Func<Faker, string> generate)`,
   matching `BogusMemberNameProvider`'s existing `RequestedType == typeof(string)`
   gate exactly.
2. **Arbitrary `TValue` per entry** — `AddConvention<TValue>(string memberName, Func<Faker, TValue> generate)`,
   broadening the provider's type gate to a per-entry comparison.

### Validation mechanics for `AddAlias`/`AddConvention`

1. **Eager, immediate validation performed when `AddAlias`/`AddConvention` is
   called** — a duplicate or collision throws `ArgumentException` from the
   exact call that introduced it, entirely inside `Compono.Bogus`.
2. **Deferred batch validation**, reusing or extending core's
   `CompositionConfigurationException`/`CompositionConfigurationError` — every
   conflict found during one `UseBogus(configure)` call reported together.

## Decision Outcome

**Chosen: Option 1 in every category above** — one merged provider/lookup,
conflict detection scoped to a single `UseBogus(...)` call (explicitly **not**
cross-call/cross-profile), a plain `BogusConvention` enum, `string`-only custom
conventions, eager per-call `ArgumentException`-based validation. All five
confirmed directly with the user during design review, including a real
mid-review reversal on cross-call detection: it was initially requested, then
explicitly walked back once the mechanism cost (a new core capability, or
hidden `ConditionalWeakTable` state with deferred-to-first-use validation) was
weighed against a single, milestone-scoped integration need — see this ADR's
Negative Consequences and Non-Goals for the resulting explicit limitation.

### The public contract

```csharp
namespace Compono;

/// <summary>
/// One of Compono.Bogus's fixed set of built-in, conservative member-name conventions - see
/// <c>docs/adr/0027-compono-bogus-package-design.md</c>'s Model 1. Deliberately not extensible: a new
/// built-in convention requires a new enum member, a generator mapping, documentation, and tests, not
/// a value a consumer can define themselves - custom behavior belongs in
/// <see cref="BogusOptions.AddConvention"/>, not in this enum.
/// </summary>
public enum BogusConvention
{
    /// <summary>Maps to <c>faker.Name.FirstName()</c>.</summary>
    FirstName,

    /// <summary>Maps to <c>faker.Name.LastName()</c>.</summary>
    LastName,

    /// <summary>Maps to <c>faker.Name.FullName()</c>.</summary>
    FullName,

    /// <summary>Maps to <c>faker.Internet.Email()</c>.</summary>
    Email,

    /// <summary>Maps to <c>faker.Phone.PhoneNumber()</c>.</summary>
    PhoneNumber,

    /// <summary>Maps to <c>faker.Address.StreetAddress()</c>.</summary>
    StreetAddress,

    /// <summary>Maps to <c>faker.Address.City()</c>.</summary>
    City,

    /// <summary>Maps to <c>faker.Address.State()</c>.</summary>
    State,

    /// <summary>Maps to <c>faker.Address.ZipCode()</c>.</summary>
    PostalCode,

    /// <summary>Maps to <c>faker.Company.CompanyName()</c>.</summary>
    CompanyName,
}
```

```csharp
public sealed class BogusOptions
{
    public string Locale { get; set; } = "en";
    public bool EnableMemberNameConventions { get; set; } = true;

    /// <summary>
    /// Adds an additional exact member name that resolves to the same generator as a built-in
    /// <see cref="BogusConvention"/> - e.g. <c>AddAlias("GivenName", BogusConvention.FirstName)</c>
    /// lets a domain that calls first names "GivenName" still get realistic values from
    /// <c>UseBogus()</c> alone. Validated and applied eagerly, immediately, against this
    /// <see cref="BogusOptions"/> instance's own accumulated state - see this ADR's Decision Outcome.
    /// </summary>
    /// <param name="aliasName">The additional exact member name to match.</param>
    /// <param name="target">The built-in convention this alias's matched requests should generate.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="aliasName"/> is null, empty, or all-whitespace; or already configured as a
    /// built-in convention name, an existing alias, or an existing custom convention.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="target"/> is not a defined <see cref="BogusConvention"/> value.</exception>
    public void AddAlias(string aliasName, BogusConvention target);

    /// <summary>
    /// Adds a custom exact-name convention: a member named exactly <paramref name="memberName"/>
    /// resolves to <paramref name="generate"/>'s result, called against a request-local,
    /// <c>context.DeriveSeed()</c>-seeded <see cref="Faker"/> - the same determinism contract every
    /// other value in this package follows. Validated and applied eagerly, immediately, against this
    /// <see cref="BogusOptions"/> instance's own accumulated state - see this ADR's Decision Outcome.
    /// </summary>
    /// <param name="memberName">The exact member name to match.</param>
    /// <param name="generate">Produces this member's value from a seeded <see cref="Faker"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="memberName"/> is null, empty, or all-whitespace; or already configured as a
    /// built-in convention name, an existing alias, or an existing custom convention.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="generate"/> is <see langword="null"/>.</exception>
    public void AddConvention(string memberName, Func<Faker, string> generate);
}
```

`AddAlias`/`AddConvention` return `void`, not a fluent `BogusOptions` — matching
`Locale`/`EnableMemberNameConventions`'s existing plain-property-setter shape;
nothing else on `BogusOptions` chains, and the worked examples in both Option A
and Option B are sequential statements, not a chain.

### Merged lookup construction

`BogusMemberNameProvider`'s hardcoded static `Conventions` dictionary
(ADR-0027) moves to a new internal, package-shared source of truth:

```csharp
internal static class BogusConventions
{
    /// <summary>Built-in name -> generator, for collision checks and the default lookup.</summary>
    internal static readonly FrozenDictionary<string, Func<Faker, string>> ByName = /* the original 10 entries */;

    /// <summary>Built-in convention -> generator, for resolving an alias's target.</summary>
    internal static readonly FrozenDictionary<BogusConvention, Func<Faker, string>> ByConvention = /* same 10 entries, keyed by enum */;
}
```

`BogusOptions` validates every `AddAlias`/`AddConvention` call immediately,
against `BogusConventions.ByName.ContainsKey(...)` plus its own private,
mutable accumulator (a plain `Dictionary<string, Func<Faker, string>>`) —
each call either succeeds and records the new entry, or throws
`ArgumentException` naming the duplicate or collision, the same eager-validation
shape the BCL's own `Dictionary<TKey, TValue>.Add` uses for a duplicate key.
`CompositionBuilderExtensions.UseBogus(Action<BogusOptions> configure)`
merges `BogusConventions.ByName` with that accumulator into one
`FrozenDictionary<string, Func<Faker, string>>` immediately after
`configure(options)` returns — no further validation needed at that point,
since `AddAlias`/`AddConvention` already guaranteed no duplicates or
collisions eagerly — and constructs `BogusMemberNameProvider` from that
merged snapshot:

```csharp
public CompositionBuilder UseBogus(Action<BogusOptions> configure)
{
    ArgumentNullException.ThrowIfNull(configure);
    var options = new BogusOptions();
    configure(options);

    if (!options.EnableMemberNameConventions)
        return builder;

    var merged = MergeConventions(BogusConventions.ByName, options.CustomConventions);
    return builder.AddSemanticProvider(new BogusMemberNameProvider(options.Locale, merged));
}
```

`BogusMemberNameProvider`'s constructor changes from `(string locale)` to
`(string locale, FrozenDictionary<string, Func<Faker, string>> conventions)` —
an ordinary signature evolution, not a breaking change requiring the Release
Drafter `breaking` label: `Compono.Bogus` itself has not shipped past `main`
yet (PLAN-0006 Phase 1 is still an open PR at the time of this ADR), so there
is no already-released contract to break.

### `EnableMemberNameConventions` remains an all-or-nothing switch

Setting `options.EnableMemberNameConventions = false` still means
`BogusMemberNameProvider` is not registered **at all** — including any
aliases/custom conventions configured in the same call. There is no partial
mode ("keep my custom conventions active, but disable the built-in allowlist")
in this version: it would require a second, independent enable/disable axis
for a case nothing has asked for yet, contradicting this ADR's own
cost-proportionality driver. Calling `AddAlias`/`AddConvention` while
`EnableMemberNameConventions` is (or later becomes) `false` is not an error —
those aliases/custom conventions are simply never registered, exactly as
harmless as any other configuration a disabled provider never reads.

### Coexistence with `Compono.NSubstitute` — unaffected

`BogusMemberNameProvider`'s type gate (`RequestedType == typeof(string)`) is
unchanged by this ADR — every alias resolves to an existing built-in
`string`-producing generator, and every custom convention is `string`-only by
this ADR's own Decision Outcome. ADR-0027's disjoint-by-construction
coexistence argument (Bogus only ever claims `string`; NSubstitute only ever
claims interface/delegate/abstract-class) holds exactly as before, with zero
new reasoning required.

### Positive Consequences

- Zero new core `Compono` surface — the entire feature lives inside
  `Compono.Bogus`, exactly satisfying `design-decisions.md` rule 3.
- One merged, immutable lookup keeps runtime matching a single dictionary
  read with no ordering/precedence rules to document or test beyond "exact
  name, first configured wins within one call."
- Reuses this repo's own established BCL-precedent validation shape (eager,
  immediate `ArgumentException` validation, matching `Dictionary<TKey, TValue>.Add`'s
  own duplicate-key behavior) rather than inventing a new configuration-error
  type or reusing one (`CompositionConfigurationException`) whose own
  contract doesn't fit a single-call validation scope.
- `BogusConvention` as a plain enum costs nothing beyond the type itself —
  no equality/hashing/allocation concerns a richer value type would introduce
  for a closed set of 10 names.

### Negative Consequences

- **No cross-call/cross-profile conflict detection or merging.** Two separate
  `UseBogus(...)` calls (direct, or from two different profiles) that each
  configure a colliding alias/custom name are **not** caught — each call
  independently registers its own `BogusMemberNameProvider`, and the pipeline's
  ordinary registration-order/first-match-wins semantics silently decide which
  one's mapping actually applies for a shared name. This is not a regression
  this ADR introduces — calling `UseBogus()` twice already behaves this way
  today, with or without configurable conventions — but configurable
  conventions make the consequence more visible, since a consumer now has a
  real reason to call `UseBogus(...)` from more than one profile. Documented
  explicitly (see Non-Goals) with the recommended mitigation: centralize
  Bogus configuration into one `UseBogus(...)` call, typically inside one
  reusable profile. Revisit only if a second, real integration-configuration
  need (beyond this one) justifies the cost of a genuine core capability.
- `EnableMemberNameConventions`'s all-or-nothing scope means a consumer who
  wants *only* their own aliases/custom conventions, without any built-in
  guessing at all, has no way to express that in this version — they get
  either "built-ins plus my extensions" or "nothing." Accepted as a
  deliberate scope cut (see Decision Outcome), not an oversight.
- `BogusMemberNameProvider`'s constructor signature changes (adds a required
  `conventions` parameter) — a real, if inconsequential (pre-release, see
  above), API churn one design iteration after ADR-0027 shipped its original
  shape.

## Non-Goals

- Cross-call/cross-profile alias/custom-convention conflict detection or
  merging (see Negative Consequences) — deferred, not designed here.
- A generic `CompositionBuilder` build-finalization/deferred-registration
  capability — evaluated during this design review specifically to unblock
  cross-call detection, explicitly not built: no second real integration
  consumer justifies it yet. Revisit only if one emerges.
- Replacing or removing a built-in convention (e.g. redefining what
  `FirstName` itself generates) — `docs/mvp.md`'s Milestone 6 scope and
  ADR-0027 both frame the built-in allowlist as fixed; this ADR only adds
  *additional* names alongside it, never changes what an existing built-in
  name does.
- Fuzzy, substring, pattern-based, or priority/specificity-based matching of
  any kind — exact, case-sensitive matching only, for aliases and custom
  conventions exactly as for the original built-in allowlist.
- Non-`string` custom conventions — the member-level
  `.Member(...).UseBogus(faker => ...)` sugar (ADR-0027 Model 2) already
  covers a non-`string` target member with full type safety.
- A partial `EnableMemberNameConventions` mode that disables built-ins while
  keeping consumer-configured aliases/custom conventions active.

## Pros and Cons of the Options

### One merged provider/lookup (chosen)

- Good, because a name maps to exactly one generator, by construction — no
  runtime ambiguity, no provider-registration-order reasoning for a consumer
  to hold in their head.
- Good, because collision detection is a single-pass dictionary build, not a
  cross-provider runtime race.
- Bad, because `BogusMemberNameProvider`'s constructor grows a parameter and
  its built-in dictionary moves to a shared internal type — more surface
  area to touch than leaving it untouched and bolting on a second provider.

### Separate provider(s) for aliases/custom conventions

- Good, because `BogusMemberNameProvider` itself stays completely unchanged.
- Bad, because a collision between a custom name and a built-in name
  surfaces only as silent, registration-order-dependent runtime behavior,
  never a configuration-time diagnostic — directly against this ADR's own
  fail-fast driver.

### Conflict detection scoped to one `UseBogus(...)` call (chosen)

- Good, because it requires zero new core capability and zero hidden
  package-owned mutable state.
- Good, because it matches today's existing (unrelated) multi-call behavior
  exactly, rather than introducing a special case only for
  aliases/conventions.
- Bad, because a real cross-profile collision goes undetected — see Negative
  Consequences.

### Cross-call accumulation validated at `Build()` time

- Good, because it would catch a real class of configuration mistakes
  (colliding conventions defined in two different profiles) that this
  ADR's chosen option cannot.
- Bad, because achieving literal `Build()`-time timing requires either a new
  generic core capability (disproportionate cost for one integration's
  need so far) or hidden, identity-keyed mutable state with weaker
  (first-use, not `Build()`-time) timing guarantees than what was actually
  asked for.

### `BogusConvention` as a plain enum (chosen)

- Good, because the built-in set is small, closed, and carries no behavior
  beyond identity — exactly what an enum is for.
- Good, because it costs zero extra type/equality/hashing ceremony.
- Bad, because it can never be extended by a consumer — intentional, per
  this ADR's own Decision Outcome (custom behavior belongs in
  `AddConvention`, not in this enum).

### `BogusConvention` as a richer value object

- Good, because it would leave room for per-value metadata or behavior
  beyond a bare name.
- Bad, because nothing in this design needs that — pure speculative
  extensibility for a closed set of 10 names.

### Custom conventions, `string`-only (chosen)

- Good, because it keeps `BogusMemberNameProvider`'s type gate a single
  `RequestedType == typeof(string)` check, with no per-entry type
  bookkeeping.
- Good, because it keeps the coexistence-with-`Compono.NSubstitute` argument
  exactly as simple as ADR-0027 already established it.
- Bad, because a non-`string` custom convention has to use the member-level
  sugar instead — a different (if already-documented) API shape for that
  case, not a limitation of this ADR's own mechanism.

### Custom conventions, arbitrary `TValue`

- Good, because it would let a consumer express a non-`string` package-wide
  convention (e.g. `Sku` as an `int`) without falling back to per-type member
  rules.
- Bad, because it reopens the coexistence-with-`Compono.NSubstitute`
  reasoning (a non-`string`, interface-shaped custom convention could
  collide with what `NSubstituteProvider` claims) for a case Model 2 already
  covers.

### Eager per-call `ArgumentException` validation (chosen)

- Good, because it mirrors an established BCL precedent —
  `Dictionary<TKey, TValue>.Add`'s own eager duplicate-key validation —
  directly, with no new exception type and no aggregation machinery.
- Good, because it stays entirely inside `Compono.Bogus`, consistent with
  this ADR's "no core touch" outcome.
- Bad, because a consumer configuring several conflicting entries in one
  callback sees only the first conflict, not a batch of everything wrong —
  accepted, since batching would need the deferred/aggregated shape this ADR
  explicitly declined.

### Deferred batch validation via `CompositionConfigurationException`

- Good, because it would report every conflict in one call together,
  matching `CompositionBuilder.Build()`'s own "report everything" UX.
- Bad, because `CompositionConfigurationError`'s existing cases are shaped
  for cross-source duplicates (always ≥2 contributing sources) — a
  single-call conflict doesn't fit that shape without either extending core
  (a touch this ADR's own driver rules out) or awkwardly forcing a
  single-source conflict into a ≥2-source type.

## Links

- [ADR-0026](0026-deterministic-seed-derivation-for-providers.md) — the
  `DeriveSeed()` capability every alias/custom-convention value's `Faker`
  still seeds through, unchanged
- [ADR-0027](0027-compono-bogus-package-design.md) — the accepted foundation
  this ADR extends; its Decision Outcome, Considered Options, and Pros/Cons
  are unchanged by this design, per this ADR's own Context section
- [docs/mvp.md](../mvp.md) — Milestone 6 scope, the "ambiguous names
  shouldn't be guessed aggressively" caution this ADR's exact-match
  constraint inherits
- [docs/plans/0006-milestone-6-bogus-integration.md](../plans/0006-milestone-6-bogus-integration.md) —
  amended with this ADR's implementation phase
