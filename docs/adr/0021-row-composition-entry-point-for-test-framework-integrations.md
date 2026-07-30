# [ADR-0021] Row Composition Entry Point for Test-Framework Integrations

**Status:** Accepted

**Date:** 2026-07-30

**Decision Makers:** solo (attribute-shape and profile-scope forks confirmed with user)

## Context

Milestone 4 needs `Compono.Xunit` to compose several *sibling* top-level
values for one xUnit v3 theory row — the test method's own parameters —
inside one shared scope, one shared seed, and one shared diagnostic path,
so that a `[Shared]` parameter's composed instance is the exact same
instance a later parameter's generated composition plan receives. Neither
existing public entry point fits:

- `Composer.Create<T>()`/`CreateMany<T>()` each start a brand-new root
  `CompositionContext` — one type, one scope, done. There is no way to
  call either of them twice for the same theory row and have the second
  call see anything the first one shared, because each call's
  `CompositionScope` ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
  is discarded with its context.
- `CompositionContext` itself (the type that actually owns scope, path,
  and the provider pipeline) is `internal`. Per `Compono.csproj`'s own
  documented policy, `InternalsVisibleTo` is deliberately never granted to
  a real consumer package — only to `Compono.Tests` — specifically so an
  unsigned assembly name can't forge access to internal surface a shipped
  package didn't intend to expose. `Compono.Xunit` is a real, published
  consumer, not a test project, so it cannot reach `CompositionContext`
  this way, and shouldn't.

A second, less obvious problem surfaced while tracing exactly how a
`[Shared]` parameter's value would reach a *nested* request (e.g. the
`IRepository` constructor parameter inside `OrderService`'s generated
plan). `CompositionContext.ResolveCore`'s stage-2 (shared/scoped values)
check today is:

```csharp
if (request.IsShared)
{
    if (_scope.TryGet(requestedType, out var sharedValue)) { ... }
    ...
}
```

`IsShared` is a property of the *current* request, not a "does anything
of this type already live in scope" check — and the only public
descriptor-based `Resolve<TValue>(in CompositionRequestDescriptor)`
generated code calls always resolves with `IsShared: false` (hardcoded
inside `CompositionContext`, never settable by generated code). So even
if a value were placed in scope, an ordinary *nested* constructor-parameter
request for the same type — like `OrderService`'s own `IRepository`
parameter — would never consult scope at all under the current gate. It
would fall through to stage 3+ and compose (or fail to compose) its own,
independent value. Milestone 4's headline scenario ("the repository
injected into `OrderService` must be the same instance as the parameter")
is unreachable without changing this.

This is not a violation of [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) — it's the ADR *reaching its
intended consumer*. ADR-0011 explicitly deferred "name/qualifier-based
sharing" to "a Milestone 4 `[Shared]`-attribute use case," and no public
API before Milestone 4 ever populates `CompositionScope` at all (`Create<T>()`/`CreateMany<T>()`
never call the request-marked-shared path; only the internal
`ResolveSharedForTesting` test seam does). That means every observable
behavior of `Create<T>()`/`CreateMany<T>()` for every pre-Milestone-4
caller is governed entirely by whether `_scope` is empty — and it always
is, today, on every path except the test seam. Loosening the *read* gate
is therefore provably a no-op for every existing public code path, not a
breaking change to `Accepted` Milestone 2/3 behavior.

## Decision Drivers

- Per `design-decisions.md` rule 3, `Compono` core must not know
  `Compono.Xunit` exists, and `Compono.Xunit` must reach the engine
  through a genuinely public extension point — the same shape every other
  integration point in this repo already takes (`CompositionBuilder`,
  `ICompositionProfile`, `Register<T>`).
- No runtime reflection on the generated-composition hot path
  ([ADR-0001](0001-source-generation-first.md)) — this ADR only adds a
  new *entry point* into the existing pipeline; it must not touch how
  values are actually constructed.
- The new surface should be small and general — usable by any future
  test-framework integration (not hardcoded to xUnit vocabulary), per
  `docs/public-api.md`'s "small enough to learn" goal and this repo's
  standing preference for minimal public surface.
- Safe under parallel test execution: no shared mutable state between
  rows, matching `CompositionContext`'s existing one-instance-per-root-
  operation lifetime.

## Considered Options

**Reaching the engine from `Compono.Xunit`:**
1. Grant `InternalsVisibleTo("Compono.Xunit")` from `Compono`, and let
   `Compono.Xunit` call `CompositionContext`'s existing internal test
   seams directly.
2. Add a new, small, public entry point to `Compono` itself — a
   `CompositionRow` type — that wraps one `CompositionContext` and
   exposes exactly the operations row composition needs.
3. Have `Compono.Xunit` call `Composer.Create<T>()` once per parameter,
   independently, each with its own forked seed it derives itself.

**Stage-2 (shared/scoped values) read gate:**
1. Leave the read gate as `request.IsShared`-only; give `[Shared]`
   parameters some other, `Compono.Xunit`-local mechanism for reaching
   nested dependents (e.g. a registration-like override, or requiring the
   SUT's own constructor parameter to also be marked shared).
2. Remove the read gate — stage 2 always attempts a scope lookup for the
   current request's type; only the *write* side stays restricted to
   requests explicitly marked shared.

## Decision Outcome

**Reaching the engine: Option 2.** A new public sealed class,
`CompositionRow`, and a new `Composer.CreateRow(Type declaringType)`
method that constructs one:

```csharp
public sealed class CompositionRow : ICompositionContext
{
    public int Seed { get; }

    // Inherited from ICompositionContext, unchanged contract - an ordinary
    // composed (non-shared) parameter.
    public TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor);
    public TValue Resolve<TValue>();
    public int ResolveCollectionSize();

    // New: composes TValue through the same pipeline as Resolve<TValue>(descriptor),
    // and additionally stores the successful result into this row's scope so a
    // later request for the same type - including one made by a nested generated
    // plan - reuses it.
    public TValue ResolveShared<TValue>(in CompositionRequestDescriptor descriptor);

    // New: stores an already-known value (an inline theory argument) directly into
    // this row's scope. Skips pipeline dispatch entirely (there is nothing left to
    // compose, so no random fork is consumed) but runs the exact same authoritative
    // validation (ValidateAuthoritativeValue - null-for-non-nullable, runtime-type-
    // not-assignable) ResolveShared's successful pipeline result gets, through one
    // shared validate-then-store helper both members call - so the two paths cannot
    // silently drift apart.
    public void ShareExplicit<TValue>(in CompositionRequestDescriptor descriptor, TValue value);
}
```

`CompositionRow` implements `ICompositionContext` by forwarding to the
internal `CompositionContext` it wraps, so a value that itself needs
further nested composition (e.g. `OrderService`'s generated plan calling
`context.Resolve<CreateOrder>(descriptor)` internally) is unaffected —
generated code never sees `CompositionRow`, only the `ICompositionContext`
interface it already targets. `ResolveShared`/`ShareExplicit` are new
members with no equivalent on `ICompositionContext` — they exist only on
the concrete `CompositionRow` a row-composing integration holds directly,
never surfacing on the interface generated code programs against.

`CompositionRow`/`Composer.CreateRow` are declared in `Compono` and have
ordinary (same-assembly) access to `CompositionContext`'s internals — no
`InternalsVisibleTo` grant to `Compono.Xunit` is needed or added.
`Compono.Xunit` (and any future test-framework integration) reaches the
engine exclusively through `CompositionRow`'s public surface, the same
"integration calls through a public extension point core owns" pattern
`CompositionBuilder` and `ICompositionProfile` already establish.

A new `CompositionRequestKind.TestParameter` and matching
`PathSegment.TestParameter(int Ordinal, string Name)` extend [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)/[ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)'s
existing descriptor/segment shapes — deliberately named generically
("test parameter," not "xUnit theory parameter") since nothing about the
concept ("a value a test-framework integration is filling in for one of
the test method's own parameters, as opposed to a constructor parameter
or required member the *generated plan* is filling in") is xUnit-specific:

```csharp
internal sealed record TestParameter(int Ordinal, string Name) : PathSegment;
```

`TestParameter` is the **seventh** distinct `PathSegment` kind — the five
original structured segments (`ConstructorParameter`, `RequiredMember`,
`CollectionElement`, `DictionaryKey`, `DictionaryValue`) plus
`ManualResolve` ([ADR-0019](0019-registrations-and-service-provider-injection.md))
make six existing kinds; `TestParameter` is the seventh. `RandomSource`
gets a matching seventh fork tag, `TestParameterTag = 6` (the next unused
zero-based tag value after the six already in use: `0`–`4` for the
original five, `5` for `ManualResolveTag`), so a `TestParameter` segment
can never collide with any other segment kind at the same ordinal — the
same tag-collision guarantee [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)'s
Amendment 2 already requires and tests for the other six.
`CompositionRequestDescriptor.DeclaringType` for a `TestParameter`
descriptor is the type whose *method* declares the parameter (a test
class, in `Compono.Xunit`'s case) — a direct, natural extension of the
field's existing "the type whose constructor/required member declares
this parameter/member" contract to a third kind of declaring construct
(constructor, required member, method), not a new field or a repurposed
one.

`Composer.CreateRow(Type declaringType)` establishes the row's context
with its path *pre-rooted*, so every subsequent `Resolve`/`ResolveShared`
call is a *child* of that root — never itself treated as a root the way
`Create<T>()`'s first call is:

```csharp
public CompositionRow CreateRow(Type declaringType)
{
    // Unlike Create<T>()/CreateMany<T>() (CompositionSeed.Generate(), full ulong
    // range), an unseeded row generates within int's non-negative range specifically
    // - see the Seed type consistency note below.
    var seed = _configuration.Seed ?? CompositionSeed.GenerateRowSeed();
    var context = new CompositionContext(
        seed, _configuration.Registrations, _configuration.ServiceProvider,
        _configuration.Rules, _configuration.CollectionSizePolicy,
        rootType: declaringType); // new: pre-establishes _path/_random instead of
                                  // leaving them null/uninitialized for the first
                                  // Resolve call to claim as "root"
    return new CompositionRow(context, unchecked((int)seed.Value));
}
```

**Seed type consistency.** `CompositionRow.Seed` is `int`, matching the
*only* two public seed-typed members it needs to stay interchangeable
with: `CompositionBuilder.WithSeed(int)` ([ADR-0017](0017-immutable-composer-configuration-and-builder-model.md))
and [ADR-0022](0022-compono-xunit-package-design.md)'s `ComposeAttribute.Seed`
(also `int`) — a value read from `CompositionRow.Seed` must be pastable
directly into `[Compose(Seed = ...)]` and reproduce the exact same row.
`CompositionSeed`'s internal field stays `ulong`
([ADR-0009](0009-deterministic-seed-and-forkable-random-source.md)/[ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md),
unchanged, `internal`) — `WithSeed(int)` already stores a configured seed
as `unchecked((ulong)seed)`, so reading it back via `unchecked((int)seed.Value)`
round-trips exactly (an `int` → `ulong` → `int` unchecked cast preserves
the same two's-complement bits both ways) whenever the row's seed
originated from a configured `WithSeed(int)`/`Seed = ...` value.

An *unseeded* row is the case that needs its own decision: `Create<T>()`/`CreateMany<T>()`'s
existing `CompositionSeed.Generate()` draws from the full 64-bit range
(`Random.Shared.NextInt64(long.MinValue, long.MaxValue)`, unchecked into
`ulong`) — a value that does not, in general, fit in `int` at all, which
would make a reported `CompositionRow.Seed` for an unseeded row either
lossy or misleading. Rather than accept that for a type whose entire
purpose is round-trip reproduction, `Composer.CreateRow` uses a distinct
`CompositionSeed.GenerateRowSeed()` (new, `internal`) that draws only from
`int`'s **non-negative** range (`Random.Shared.Next(0, int.MaxValue)`,
unchecked into `ulong`) for the unseeded case — so `CompositionRow.Seed`
is *always* the complete, exact, reproducible seed for that row, never a
truncated view of a wider one. This is scoped to `CreateRow` alone:
`Create<T>()`/`CreateMany<T>()`'s own unseeded generation is unchanged,
since neither exposes a public per-call seed a caller could paste
anywhere.

Non-negative, not the full `int` range, is deliberate, and is what makes
[ADR-0022](0022-compono-xunit-package-design.md)'s "propagate the
underlying `CompositionException` un-wrapped" choice for pipeline
failures actually safe: `CompositionDiagnostic.Seed` (`ulong`,
[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md),
`Accepted`/implemented in Milestone 2, unchanged by this ADR) is rendered
by the engine's own `CompositionDiagnostic.ToString()` as a raw decimal
`ulong` — text `Compono.Xunit` does not intercept or rewrite for a
pipeline-diagnosed failure. `unchecked((ulong)value)` sign-extends a
*negative* `int` into a large 64-bit unsigned value before display, so a
negative seed's `ulong` and `int` decimal forms would print differently —
exactly the mismatch this ADR exists to avoid. A **non-negative** `int`'s
`unchecked` conversion to `ulong` changes no bits worth displaying
differently (the sign-extended high bits are all zero already), so its
decimal text is identical read as `ulong` or `int`. For the case this
milestone actually optimizes for — an auto-generated seed surfaces on
failure, gets pasted into `[Compose(Seed = ...)]` to reproduce it — the
number the user reads and the number they paste are guaranteed identical,
with no rewriting needed anywhere.

This ADR does **not** independently guarantee the same for an
*explicitly configured* seed, since `CompositionBuilder.WithSeed(int)`
([ADR-0017](0017-immutable-composer-configuration-and-builder-model.md),
`Accepted`/implemented, a general programmatic API this ADR does not
revisit) accepts the full `int` range including negative values, and
`unchecked((ulong)value)` sign-extends a negative `int` before display —
a negative configured seed's `ulong` and `int` decimal forms would print
differently. Restricting `WithSeed(int)` itself to non-negative values,
or rewriting core's diagnostic rendering, are both out of scope here (the
former would be a breaking change to an already-`Accepted` public API
well beyond this ADR; the latter touches [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
own `Accepted` diagnostics contract). Instead, [ADR-0022](0022-compono-xunit-package-design.md)
closes this at the one call site that actually needs the guarantee:
`ComposeAttribute.Seed` rejects a negative value before a row is
composed, so every row `Compono.Xunit` ever creates — configured or
generated — has a non-negative seed, and the `ulong`/`int` print-identical
property this section establishes holds unconditionally for every
`Compono.Xunit` failure, with no accepted exception.

This is what makes two sibling `Resolve<TValue>(descriptor)` calls on the
same `CompositionRow` fork *independently* by ordinal (each pushes its own
`TestParameter(ordinal, name)` child segment from the same pre-established
root random state) instead of both being treated as literal roots and
forking identically from the raw seed — the bug this ADR's Context section
identified: two different top-level parameters, both "root," would
otherwise draw from the exact same `RandomSource.FromSeed(seed)` stream
and could produce identical output if their own first-requested member
happened to share a structural position. `CompositionDiagnostic.RootType`
for a row failure is `declaringType` (the test class) — a failure renders
as "Unable to compose `OrderServiceTests`," with the failing parameter as
the tree's first child node, the same shape `docs/architecture.md`'s
existing Diagnostics example already uses for a constructor parameter.

**Stage-2 read gate: Option 2, removed.** `ResolveCore`'s shared/scoped
lookup becomes unconditional — every request checks scope for a match
first, regardless of that request's own `IsShared` flag:

```csharp
if (_scope.TryGet(requestedType, out var sharedValue))
{
    var result = ValidateAuthoritativeValue(sharedValue, request, "shared value");
    _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, OutcomeOf(result));
    var value = Authoritative<TValue>(result);
    _trace.Rewind(checkpoint);
    return value;
}
_trace.Record(PipelineStage.SharedOrScopedValue, provider: null, CompositionAttemptOutcome.NotHandled);
```

The *write* side (`StoreSharedAndReturn`, and `ResolveViaGeneratedPlan`'s
existing `if (!request.IsShared) { ... } else { ...StoreSharedAndReturn... }`
branch) is **unchanged** — a value only ever gets written into scope when
the request that produced it was explicitly marked shared
(`CompositionRow.ResolveShared`/`ShareExplicit`, or the existing internal
`ResolveSharedForTesting` seam). This keeps [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)'s
"two sibling constructor parameters of the same type fork independently"
guarantee fully intact for the overwhelmingly common case (nothing marked
`[Shared]`, scope stays empty, the read always misses) while making an
established shared value visible to *every* later same-typed request in
the operation, not only ones that also opted in — which is exactly
`docs/public-api.md`'s existing description of the feature ("reused for
compatible requests **later in the same test composition**," not "reused
only by requests that also ask for it").

### Positive Consequences

- `Compono.Xunit` (Milestone 4) is buildable entirely on genuinely public
  `Compono` surface — no `InternalsVisibleTo` carve-out for a real
  consumer package, preserving the existing, deliberate policy.
- The stage-2 change is provably behavior-preserving for every existing
  public caller (scope is always empty on every path but the new one), so
  it needs no exception to "an `Accepted` ADR's Decision Outcome is a
  historical record" — this ADR extends ADR-0011's stage into its first
  real consumer rather than reversing a shipped behavior.
- `TestParameter`/`CompositionRow` are generic enough that a hypothetical
  future non-xUnit test-framework integration reuses the identical
  mechanism, rather than each integration inventing its own row-scoping
  story.

### Negative Consequences

- `CompositionContext` gains a second constructor parameter
  (`rootType`) and a `ResolveShared`/`ShareExplicit`-capable code path
  that only `CompositionRow` ever calls — a small amount of surface that
  exists for exactly one caller today. Mitigated by keeping both new
  internal members private to the `Resolve...` family rather than
  generalizing them further until a second caller actually exists.
- Removing the stage-2 read gate means a *type-based* `[Shared]` mistake
  (declaring two `[Shared]` parameters of the same type, or a `[Shared]`
  parameter whose type coincidentally matches something a registration
  would otherwise have produced) now silently changes what an unrelated
  later request receives, rather than being structurally impossible.
  [ADR-0022](0022-compono-xunit-package-design.md) addresses the
  duplicate-shared-type case with an explicit, pre-composition validation
  error in `Compono.Xunit` itself; a registration/shared-value collision
  is accepted as ordinary pipeline precedence (stage 2 still runs before
  stage 3, so a shared value wins over a registration for the same type,
  same as it always has for a request that was already marked shared).
- `CompositionRow.Seed`'s `ulong`/`int` print-identical guarantee (the
  Seed type consistency note above) depends on every row's seed being
  non-negative — which `Composer.CreateRow` itself cannot enforce for an
  explicitly-configured seed, since `WithSeed(int)` accepts the full
  range. This ADR narrows the guarantee to "holds for every row
  `Compono.Xunit` creates" rather than "holds for `CreateRow` regardless
  of caller," relying on [ADR-0022](0022-compono-xunit-package-design.md)'s
  `ComposeAttribute.Seed` rejecting negative values before calling
  `CreateRow` at all. A future non-xUnit consumer of `CreateRow` would
  need the same discipline (or its own equivalent guarantee) to keep the
  property holding for its own rows.

## Pros and Cons of the Options

### Reaching the engine — `InternalsVisibleTo("Compono.Xunit")`

- Good, because it needs no new public API at all.
- Bad, because it directly reverses a deliberate, documented policy
  (`Compono.csproj`'s own comment) adopted specifically to keep a shipped
  package's internal surface from becoming a de facto public contract for
  "trusted" consumers — `Compono.Xunit` is exactly the kind of real
  consumer that policy exists to say no to.

### Reaching the engine — independent `Create<T>()` per parameter, self-forked seed

- Good, because it needs zero new core API for the common (non-shared)
  case.
- Bad, because it cannot satisfy the milestone's headline scenario at
  all: each `Create<T>()` call gets its own independent `CompositionScope`
  ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)),
  so there is no way for a value composed in one call to be visible to a
  generated plan running inside a different call, no matter how the seeds
  are related.

### Reaching the engine — `CompositionRow` (chosen)

- Good, because it's a genuinely public, minimal, generic extension
  point, consistent with every other integration surface this repo has
  designed so far.
- Good, because it reuses `ICompositionContext.Resolve<TValue>(descriptor)`
  as-is for the ordinary (non-shared) case — no duplicated dispatch logic.
- Bad, because it's new public surface on core `Compono` for a capability
  only one milestone currently needs — mitigated by keeping it to exactly
  three members beyond what `ICompositionContext` already has.

### Stage-2 read gate — leave `IsShared`-only, find another mechanism for `[Shared]`

- Good, because it changes nothing about `ResolveCore`.
- Bad, because no alternative mechanism was found that doesn't either
  reintroduce reflection-based rewriting of a generated plan's own
  `Resolve` calls (impossible without breaking [ADR-0001](0001-source-generation-first.md))
  or require every SUT constructor parameter that should receive a shared
  value to *also* carry some marker — which no mechanism exists to apply
  to a type Compono doesn't own, defeating the entire feature.

### Stage-2 read gate — remove it (chosen)

- Good, because it's the only option that lets an *ordinary*, unmarked
  constructor parameter (like `OrderService`'s own `IRepository`
  parameter) transparently pick up a value shared elsewhere in the same
  operation — exactly what `docs/public-api.md` already describes.
- Good, because it's provably a no-op for every pre-Milestone-4 caller.
- Bad, because it removes a structural impossibility (two same-typed
  shared establishments colliding) in favor of an explicit validation
  check living one layer up, in `Compono.Xunit` — a weaker guarantee than
  "the type system/engine makes this impossible," though still a clear,
  pre-composition failure rather than a silent one.

## Links

- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) —
  `CompositionRequestDescriptor`/`CompositionRequestKind`, extended here
  with `TestParameter`.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  `CompositionScope`'s type-keyed, per-root-operation shape, unchanged;
  this ADR is the deferred "Milestone 4 `[Shared]`-attribute use case"
  that ADR-0011 explicitly named as the reason name/qualifier-based
  sharing stayed out of scope then.
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) —
  `PathSegment`/`RandomSource` forking, extended here with `TestParameter`
  (the seventh segment kind) and a matching seventh fork tag.
- [ADR-0022](0022-compono-xunit-package-design.md) — the `Compono.Xunit`
  package this entry point exists for; owns the duplicate-`[Shared]`-type
  validation this ADR's Negative Consequences defers to it.
