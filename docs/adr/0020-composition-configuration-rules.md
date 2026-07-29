# [ADR-0020] Composition Configuration Rules

**Status:** Accepted

**Date:** 2026-07-29

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

`docs/mvp.md`'s Milestone 3 scope lists three related but distinct configuration
surfaces: "collection-size configuration," "type/member rule prototype," and
(implicitly, as the mechanism behind both) whatever shared abstraction they end up
built on. `docs/public-api.md` sketches the target shape:

```csharp
builder.For<Customer>()
    .Member(x => x.Status)
    .Use(CustomerStatus.Active);

builder.For<Customer>()
    .Member(x => x.Email)
    .Use(context => context.Semantic.Email());
```

This ADR settles four things: how a **type rule** (an unqualified `.For<T>().Use(...)`)
and a **member rule** (`.For<T>().Member(...).Use(...)`) reach pipeline stage 4
(renamed **configuration rules** in this design review — see
[ADR-0018](0018-composition-profiles.md)'s terminology correction); how a member
rule's matching identity avoids colliding across similarly-shaped members of
different types; how type-rule and member-rule precedence works; and — the one
genuine correction from this ADR's first draft — why **collection-size
configuration is not a stage-4 rule at all**.

## Decision Drivers

- Pipeline stage 4 is an ordered `ICompositionProvider` collection
  ([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)) — a
  provider produces the *value itself* for the request it claims. Collection size is
  not a value; it's *policy that stage 7's collection machinery consumes while
  building* a value. Forcing a size rule through stage 4 would mean either stage 4
  constructing the collection itself (duplicating stage 7's element-resolution/
  retry logic, per [ADR-0013](0013-collection-generation-semantics.md)) or giving
  `ICompositionProvider` a second, undocumented kind of result — both rejected.
- The public provider-authoring interface question was explicitly deferred to
  Milestone 5 during this design review — rules cannot require a user-authored type
  to implement any public engine contract, or they'd reopen that deferred question.
- `docs/mvp.md`: "Evaluate whether these should share common abstractions" — value
  rules (type and member) should share one mechanism where the two are genuinely the
  same shape; collection size shouldn't be forced to share it where it isn't.
- `ADR-0012`'s reproducibility contract (structural path identity, not type
  identity) still governs every value a rule produces, and matching must not
  silently collide across two different declaring types with similarly-named/-typed
  members.
- Member identity has to be something a hand-written builder callback can actually
  express — it cannot depend on the generator's compile-time-assigned `Ordinal`
  (ADR-0012 Amendment 1), which no user-facing API exposes.

## Considered Options

### Where collection size lives

1. **Immutable configuration policy, queried by stage 7.** `WithCollectionSize(n)`
   (global) and `.For<T>().Member(...).WithCollectionSize(n)` (member-scoped) are
   captured as plain data on `CompositionConfiguration`
   ([ADR-0017](0017-immutable-composer-configuration-and-builder-model.md)), not
   compiled into a stage-4 provider. Generated collection plans (ADR-0013/ADR-0014)
   ask the context for the size to build, at the point they'd otherwise use their
   current hardcoded literal `3`.
2. **A stage-4 provider that "produces" a size**, requiring stage 7 to detect and
   specially interpret an `int`-shaped stage-4 result as a size hint rather than a
   value.
3. **A stage-4 provider that constructs the entire collection value directly**,
   duplicating stage 7's element resolution and `UniqueValueResolver` retry logic
   inside a rule-authored provider.

### Value-rule mechanism (type and member rules)

1. **Compile into internal, Compono-authored `ICompositionProvider` instances** — a
   user never implements `ICompositionProvider` directly; `.For<T>().Use(...)` and
   `.For<T>().Member(...).Use(...)` are pure data/delegate capture on the public
   builder surface, compiled by `Build()` into small internal provider
   implementations registered into stage 4.
2. **A distinct, non-provider lookup mechanism**, bypassing stage 4's existing
   ordered-collection dispatch and diagnostics tracing entirely.
3. **Require the user to implement a public provider interface directly** — pulling
   the Milestone-5-deferred question forward early.

### Member-rule matching identity

1. **(Declaring type, member name), read directly off the request's explicit
   metadata.** A member rule captures the CLR declaring type and member name (via
   `MemberInfo`, extracted from the `x => x.Email` expression) at the point
   `.Member(...)` is called; at match time, an incoming request matches when its
   `CompositionRequest.DeclaringType` equals the captured declaring type **and**
   its `Name` equals the captured member name.
2. **(Requested value type, member name) only** — the shape this ADR's first draft
   implied, omitting the declaring type from the key.
3. **(Declaring type, member name), inferred from the parent path node** — this
   ADR's first draft: instead of a request-carried field, read the declaring type
   off the *parent* `CompositionPathNode.RequestedType` in the path chain.
4. **(Declaring type, generator-assigned `Ordinal`)** — reusing ADR-0012's canonical
   member identity directly instead of `Name`.

## Decision Outcome

### Collection size — Option 1, immutable policy queried by stage 7

Confirmed per design-review feedback: a size rule doesn't produce the requested
collection value, so it doesn't belong in `ICompositionProvider`'s value-producing
contract at all. `CompositionConfiguration` holds a `CollectionSizePolicy` — a global
default (`WithCollectionSize(n)`, falling back to ADR-0013's existing default of `3`
if never set) plus a member-scoped override table
(`(declaring type, member name) → size`, same identity shape as a member rule's
matching key, established below). `ICompositionContext` gains exactly **one** new
public member — not a root/member pair — since a root-level collection request
(`Composer.Create<List<int>>()`) never crosses the generated-code boundary through
`ICompositionContext` at all: `ResolveRoot<T>()`
([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) Amendment)
is hand-written runtime code living inside `CompositionContext` itself, so a
root-level collection plan's size lookup can read `CompositionConfiguration`'s
`CollectionSizePolicy` global default directly, with no public API call at all — a
second, symmetry-only overload would be public surface with no real caller.
Generated (non-root) collection plans, which *do* cross the assembly boundary, get
the one overload that matters to them:

```csharp
public interface ICompositionContext
{
    // ...Resolve<T> overloads unchanged...
    int ResolveCollectionSize(in CompositionRequestDescriptor descriptor);
}
```

Precedence is member-scoped override, then global default, then ADR-0013's built-in
`3` — a plain, three-level data lookup with no randomness or hashing involved (it
never advances `IRandomSource`, never pushes a path segment — it's a configuration
read, not a resolved value). This is a **parameterization** of ADR-0013's
previously-fixed constant, not a change to that ADR's retry/uniqueness/ordering
semantics, which remain exactly as decided — noted here explicitly since
ADR-0013/ADR-0014 are `Accepted` and this ADR doesn't reopen either.

**One internal lookup implementation, not two.** Having exactly one public overload
(above) doesn't by itself guarantee there's only one implementation of the
three-level precedence rule — the root-dispatch code path inside
`CompositionContext` still needs to apply it. Rather than duplicating that logic
inline at the root call site, `CompositionContext` implements the precedence lookup
**once**, as a private/internal method taking an *optional* member key (`(Type
declaringType, string memberName)?`):

```csharp
private int ResolveCollectionSizeCore((Type DeclaringType, string MemberName)? memberKey)
{
    if (memberKey is { } key && _configuration.CollectionSizePolicy.MemberOverrides.TryGetValue(key, out var overrideSize))
        return overrideSize;

    return _configuration.CollectionSizePolicy.GlobalDefault ?? CollectionDefaults.Size; // ADR-0013's 3
}
```

The public `ResolveCollectionSize(in CompositionRequestDescriptor descriptor)`
overload extracts `(descriptor.DeclaringType, descriptor.Name)` and calls this
core method; the internal root-dispatch path calls the same core method directly
with `memberKey: null` (a root request has no declaring type/member to look up an
override for — it can only ever fall through to the global default or the built-in
`3`). This is the "single internal implementation" the design review asked to
confirm: the *public API surface* is one method (root dispatch never gets a second
public overload, per the reasoning above), and the *lookup logic* is also one
method — the two call sites differ only in how they obtain the optional member key,
not in how precedence is resolved once they have it.

The public builder surface stays unified even though the internal mechanism now
differs from value rules:

```csharp
builder.WithCollectionSize(3);                              // global default
builder.For<Customer>()
    .Member(x => x.PastOrders)
    .WithCollectionSize(5);                                  // member-scoped override
```

### Value rules — Option 1, compiled into internal Compono-authored providers

Unchanged from this ADR's first draft: no public provider contract needed, stage 4's
existing ordered-dispatch and diagnostics-tracing behavior is reused as-is, and it
keeps the Milestone-5 provider-interface deferral intact.

### Type rules — the gap in the first draft, now specified

`.For<T>().Use(value)` / `.For<T>().Use(context => value)` — **without** a
`.Member(...)` call — registers a **type rule**: matches any stage-4 request for
exactly type `T`, regardless of which member/position requested it.

```csharp
builder.For<IClock>().Use(_ => new SystemClock());
```

- **API.** `For<T>()` returns `CompositionTypeRuleBuilder<T>` (per ADR-0017's
  builder-scoping precedent — a thin view over the same shared `CompositionBuilder`
  state, not a second configuration root). Calling `.Use(...)` directly on it (no
  `.Member(...)` in between) registers a type rule; calling `.Member(x => x.Y)` first
  returns a further-scoped builder whose `.Use(...)`/`.WithCollectionSize(...)`
  register a member rule instead, per the original sketch.
- **Precedence: member rule beats type rule for the same effective request,
  specificity-based, not call-order-based.** If a member rule exists for
  `Customer.Email` (itself a `string` request) and a type rule exists for `string`,
  the member rule wins regardless of which was registered first — stage 4's compiled
  provider list places every member rule ahead of every type rule internally,
  because specificity is a property of the rule itself, not of when a builder call
  happened to run. This is a deliberate, narrow departure from
  [ADR-0018](0018-composition-profiles.md)'s "precedence is call order" framing:
  that framing describes *conflict* resolution among rules that could both claim the
  *identical* key (still call-order/throw-based, unchanged), not dispatch order
  among rules of genuinely different specificity that were never in conflict with
  each other to begin with.
- **Matching: exact type only, no assignability, for M3.** A type rule for `IClock`
  does not also satisfy a request for `SystemClock` (a concrete type assignable to
  `IClock`) unless the request's requested type is literally `IClock`. Chosen
  per the user's explicit direction ("exact type matching unless a broader match has
  a concrete requirement") — no concrete M3 requirement motivates assignability
  matching, and it introduces real ambiguity (which of several assignable type rules
  wins for a diamond-shaped interface hierarchy?) that exact matching avoids
  entirely by construction. Deferred, not designed, pending a real need.
- **Duplicate/conflict behavior: same as ADR-0019's registrations.** Two type rules
  for the exact same type (whether from the same profile, different profiles, or a
  profile and a direct call) is a build-time `CompositionConfigurationException`,
  naming both sources via ADR-0018's provenance chain. Two member rules for the
  exact same `(declaring type, member name)` pair, likewise. A member rule and a
  type rule are never a conflict with each other even when both could match the same
  request — they're different specificity (see Precedence above), not the same key.

### Member-rule matching identity — Option 1, (declaring type, member name), read directly off the request

Confirmed as the corrected design, revised once more per a second design-review
pass: a member rule's key is the **pair** of the declaring/containing type and the
member name — `(typeof(Customer), "Email")`, not merely `("Email")` or
`(typeof(string), "Email")`. This closes the exact collision the design review
flagged: two unrelated types each having a `string Email` member no longer share a
key, because the declaring type is part of it.

**Declaring-type identity is read from a new `DeclaringType` field this ADR adds to
`CompositionRequestDescriptor`/`CompositionRequest`** — not inferred from the parent
`CompositionPathNode.RequestedType` in the path chain, this ADR's own first-draft
approach:

```csharp
public readonly struct CompositionRequestDescriptor
{
    public CompositionRequestKind Kind { get; }
    public int Ordinal { get; }
    public string Name { get; }
    public Type DeclaringType { get; }   // new
    public Nullability Nullability { get; }

    public CompositionRequestDescriptor(
        CompositionRequestKind kind, int ordinal, string name, Type declaringType, Nullability nullability)
    {
        Kind = kind;
        Ordinal = ordinal;
        Name = name;
        DeclaringType = declaringType;
        Nullability = nullability;
    }
}
```

`DeclaringType` is the type whose constructor/required-member declares this
parameter/member — for `Customer(string firstName, ...)`, every constructor
parameter's descriptor carries `DeclaringType = typeof(Customer)`, generator-emitted
exactly like `Ordinal`/`Name` already are (no new generator capability needed — the
declaring type is already in scope at descriptor-emission time, since it's the type
the whole plan is being generated for, or an ancestor type for an inherited required
member per [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)
Amendment 2's base-to-derived ordinal algorithm — `DeclaringType` is the type that
*declares* the member, which for an inherited required member is the base type, not
the composed leaf type). `CompositionContext` carries `DeclaringType` forward
unchanged onto the internal `CompositionRequest` it expands the descriptor into, so
this ADR's compiled rule matching reads it directly off the request rather than
inferring it from path state. `CompositionRequestKind.ConstructorParameter`/
`RequiredMember` are the only kinds that populate it meaningfully — it's unused (and
unread) for a request with no member identity to speak of (a collection element/
dictionary key/value, or a `ManualResolve` invocation, per
[ADR-0019](0019-registrations-and-service-provider-injection.md)). This is purely
additive to `CompositionRequestDescriptor`'s existing shape (ADR-0010's `Accepted`
Decision Outcome, unedited by this ADR — see that ADR's own text for the fields this
extends) — every existing field is unchanged, and `DeclaringType` is never an input
to random-fork hashing (ADR-0012's existing ban on type identity feeding hashing is
unaffected; `DeclaringType` is used only for rule *matching*, evaluated at stage 4,
before any hashing for that request happens). `Compono.Generators`' descriptor-
emission templates and every existing generated-plan snapshot need updating to emit
the new argument — a mechanical, `global::`-qualified `typeof(...)` reference
alongside the existing `Kind`/`Ordinal`/`Name`/`Nullability` arguments.

Path-parent inference happened to be correct
for straightforward cases but is indirection a rule shouldn't need: it silently
assumes "the type that requested this member" and "the type whose path node is the
immediate parent" always coincide, which is true today but is exactly the kind of
assumption a future generated-plan change (or a not-yet-designed request shape)
could break without anything here failing loudly. Carrying `DeclaringType` as
explicit request metadata means matching reads a field whose meaning is fixed by the
generator at the point the request is emitted, not re-derived from unrelated
plumbing (the path chain) that happens to currently correlate with it. This is more
reliable specifically for nested graphs (a `Customer` reachable three levels deep
still carries the same `DeclaringType`, regardless of path shape), constructor
parameters and required members alike, records (a positional record constructor
parameter is exactly as explicit here as a required member), manual resolves (which
have no declaring type at all — `DeclaringType` is simply unused for
`PathSegment.ManualResolve` and any other non-member request kind, never
defaulted/inferred), and any future generated-plan change, since the field's value
is fixed at generation time rather than depending on how the pipeline happens to be
threading path state at match time.

**Why `Name`, not the generator-assigned `Ordinal`, is the matching identity —
addressed directly, since it's a deliberate departure from ADR-0012's ordinal-for-
hashing preference, not an oversight.** `Ordinal` (ADR-0012 Amendment 1) is assigned
by `Compono.Generators` at compile time, per composed type, as an internal detail no
public API exposes — a hand-written `builder.For<Customer>().Member(x => x.Email)`
call has no way to know or express `Customer.Email`'s generator-assigned ordinal.
`Name`, by contrast, is directly recoverable from the `x => x.Email` expression tree
(a `MemberExpression` whose `Member.Name` is `"Email"`) and is already present on
every request specifically for human-facing use (ADR-0012: "`Name` is carried for
diagnostic display only, never for hashing"). This ADR is a second, equally
legitimate consumer of that same field for the same reason it exists — human-facing
identification — extended from "diagnostic display" to "diagnostic display and rule
matching," both non-hashing uses. **This does not touch or weaken ADR-0012's hashing
rule**: `Ordinal` remains the only input to random-fork key derivation,
unconditionally; `Name`/`DeclaringType`-based matching is a completely separate
concern (stage-4 dispatch, evaluated before any hashing happens for that request)
that happens to reuse fields already on the request — never fed into any hash,
exactly as ADR-0012's existing type-identity-never-hashed rule already establishes
for `DeclaringType` specifically.

### Expression parsing: at `.Member(...)`/`.Use(...)` call time, not deferred to `Build()`

Corrected inconsistency from this ADR's first draft, which stated both. Settled:
**`x => x.Email` is parsed into `(declaring type, member name)` immediately, at the
point `.Member(...)` is called** — not deferred to `Build()`. This matches
[ADR-0018](0018-composition-profiles.md)'s eager-profile-application philosophy (a
rule's shape is fully known the moment the builder call that describes it runs), and
means a malformed expression (anything other than a direct member access, e.g.
`x => x.Email.Length` or `x => SomeMethod(x)`) throws immediately at the call site
with the offending lambda still in scope for a clear error message, rather than
resurfacing generically at `Build()` time once the original call-site context is
gone. `Build()`'s validation pass (ADR-0017) still runs — but only over the
already-extracted `(declaring type, member name)` identities, checking for
cross-rule conflicts (the duplicate-key case above); it performs no expression
parsing of its own.

### Positive Consequences

- Collection-size configuration no longer strains `ICompositionProvider`'s
  value-producing contract — it's data, read as data, by exactly the stage-7
  machinery that already needs it.
- One internal mechanism (a small, Compono-owned provider family) backs both type
  and member value rules; adding a future rule kind (a semantic-hint rule for Bogus
  in M6) is "add another compiled-provider case."
- No public provider contract needed for any of this — the M5-deferral decision
  stays intact.
- Member-rule matching can no longer collide across two types with similarly-named,
  similarly-typed members — the exact bug class flagged during review.
- Compile-time-safe member references (`x => x.Email`) instead of stringly-typed
  member names, with immediate, call-site-local error reporting for a malformed
  expression.

### Negative Consequences

- Two internal execution paths exist for what looks like one unified public DSL
  (value rules → stage-4 providers; collection size → queried policy) — a
  deliberate, documented split, but real enough to flag: someone extending this
  system later has to know which bucket a new rule kind belongs in, not assume
  "everything under `.For<T>()` becomes a provider."
- `Expression<Func<T, TMember>>` parsing at call time is a small amount of
  reflection-adjacent work (walking an expression tree the caller wrote directly,
  not runtime type discovery) — doesn't conflict with ADR-0001's no-reflection-
  fallback rule (which concerns *construction*, not parsing a lambda the caller
  handed the builder), but worth stating explicitly rather than leaving an implicit
  "no reflection anywhere at all" assumption unqualified.
- `CompositionTypeRuleBuilder<T>`'s fluent shape is a third fluent-builder flavor
  alongside `CompositionBuilder` itself and a profile's `Configure` — more surface
  to document, even though each is a thin wrapper over the same underlying state.

## Pros and Cons of the Options

### Collection size — immutable policy queried by stage 7 (chosen)

- Good, because it doesn't stretch `ICompositionProvider`'s contract to cover a
  second, structurally different kind of result.
- Good, because it reuses ADR-0013's existing size-application point (generated
  collection plans) almost unchanged — a parameter instead of a literal.
- Bad, because it's a second mechanism alongside stage-4 providers rather than one
  uniform "everything is a provider" story — accepted, since the alternative
  (below) is worse.

### Collection size — provider "produces" a size (rejected)

- Bad, because it requires stage 7 to special-case an `int` stage-4 result as a size
  hint rather than a value, an undocumented dual meaning for `ICompositionProvider`'s
  return type that every future provider author would need to know about.

### Collection size — provider constructs the whole collection (rejected)

- Bad, because it duplicates ADR-0013's element-resolution and
  `UniqueValueResolver` retry/uniqueness logic inside a second, rule-authored code
  path — exactly the kind of duplication the "share common abstractions" driver
  warns against, in the opposite direction.

### Value rules — Compono-authored internal providers (chosen)

Unchanged reasoning from this ADR's first draft — see Links for the original
pros/cons, still valid: needs no public provider contract, fits stage 4's existing
definition, keeps the M5 deferral intact.

### Member-rule identity — (requested value type, member name) only (rejected)

- Bad, because it's exactly the collision the design review flagged: two unrelated
  types each declaring a same-named, same-typed member would share a key.

### Member-rule identity — (declaring type, member name) inferred from the parent path node (superseded by the request-carried field, above)

- Good, because it needed no descriptor/request shape change — this ADR's first
  draft chose it for exactly that reason.
- Bad, because it's indirection: it depends on "the request's declaring type always
  equals its parent path node's requested type" holding, an assumption that's true
  today but isn't guaranteed by anything that would fail loudly if a future
  generated-plan or request-shape change broke it. A field the generator sets
  explicitly, once, at the point it knows the real answer, doesn't carry that risk.
- Bad, because it gave `ManualResolve` (which has no declaring type) no clean way to
  participate in the same matching code path without a special case — a
  request-carried field that's simply unset/unused for non-member request kinds is
  more uniform.

### Member-rule identity — (declaring type, generator `Ordinal`) (rejected for M3)

- Good, because it would reuse ADR-0012's canonical member identity exactly, with no
  second identity concept.
- Bad, because no public API surface currently exposes a generator-assigned ordinal
  to hand-written builder code — adopting this would require inventing a new
  mechanism just to let a rule author discover an internal compiler output, solely
  to satisfy identity purity, for no behavioral benefit over `Name` (which is already
  unique per type for ordinary C# members — two members of the same declaring type
  can't share a name).

## Links

- [docs/mvp.md](../mvp.md) — Milestone 3 scope: collection-size configuration,
  type/member rule prototype
- [docs/public-api.md](../public-api.md) — Type and Member Rules, Bogus Integration
  sections
- [docs/architecture.md](../architecture.md) — Resolution Pipeline stage 4
  (configuration rules), stage 7 (collection dispatch)
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) — stage 4's
  existing definition and diagnostics tracing behavior
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) — the
  in-process `Type`-equality precedent this ADR's declaring-type matching reuses
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) — the
  `Ordinal`-for-hashing / `Name`-for-display-and-matching split this ADR relies on
  and extends (non-hashing use only)
- [ADR-0013](0013-collection-generation-semantics.md) — the default-size-3 constant
  this ADR parameterizes without reopening its retry/uniqueness/ordering decisions
- [ADR-0014](0014-generator-emitted-collection-plans.md) — the generated collection
  plans that call the new `ResolveCollectionSize` query instead of a hardcoded
  literal
- [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md) — the
  `Build()` step where value-rule data compiles into providers, and where
  `CollectionSizePolicy` is assembled into `CompositionConfiguration`
- [ADR-0018](0018-composition-profiles.md) — profile-sourced rules follow identical
  conflict/provenance semantics to direct ones; the stage-4 terminology correction
- [ADR-0019](0019-registrations-and-service-provider-injection.md) — the
  `ICompositionContext` factory-resolve surface a `.Use(context => ...)` rule
  factory shares with registration factories
