# [ADR-0026] Deterministic Seed Derivation for Providers and Registration Factories

**Status:** Accepted

**Date:** 2026-07-31

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

[ADR-0024](0024-public-provider-extensibility-model.md) gave stage 5/6 a public
extension point (`ICompositionValueProvider`), but its first real consumer
(`Compono.NSubstitute`, [ADR-0025](0025-compono-nsubstitute-package-design.md))
never needed randomness — a substitute has no "value" to generate, just a proxy to
construct. Milestone 6 (`Compono.Bogus`) is the first consumer that genuinely does:
every semantic value it produces (a first name, an email, a whole `Faker<T>`-
generated object) has to come from *somewhere* random, and `docs/mvp.md`'s MVP
success criterion #5 ("a failure produces a readable dependency path and
reproducible seed") only holds if that randomness is deterministic and reproducible
from `Composer`'s own root seed — exactly like every other value the engine
produces.

[ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)
already solved this problem once, for the engine's own internal providers
(`PrimitiveValueProvider` et al.): a structured `CompositionPath` is hashed
(FNV-1a, never a formatted string) into a fork key, so two requests never collide
and renaming a parameter/member (with no reordering) never changes its derived
value. That mechanism is internal — `IRandomSource`, `CompositionPath`, and the
forking logic all live inside `Compono` and are not exposed through
`ICompositionContext`, which today only offers `Resolve<T>(descriptor)` and the
descriptor-less `Resolve<T>()` ([ADR-0019](0019-registrations-and-service-provider-injection.md)).
Neither a public provider nor a registration/rule factory has any way to ask for
its own deterministic randomness — the exact gap Milestone 6's design review
surfaced as the first question to resolve, before any Bogus-specific design could
proceed.

This ADR is scoped to the mechanism only, mirroring
[ADR-0024](0024-public-provider-extensibility-model.md)'s own split from
[ADR-0025](0025-compono-nsubstitute-package-design.md): a core capability any
future provider or factory can use, not `Compono.Bogus`-specific.
[ADR-0027](0027-compono-bogus-package-design.md) is the first real consumer.

## Decision Drivers

- ADR-0012's path-independence guarantee ("renaming an unrelated member never
  changes another member's derived value," "adding an unrelated member doesn't
  perturb existing values") must extend to provider/factory-generated randomness,
  not stay a guarantee only the engine's own built-in providers get.
- Explicit user direction: do **not** expose `IRandomSource`, mutable random
  state, or `CompositionPath`/path internals through the public contract. The
  public surface is a deterministic **seed** (an `int`), not a random-value-
  generation service — the smallest capability that unblocks a consumer able to
  seed its own PRNG (Bogus's `Randomizer`, or anything else).
- `design-decisions.md` rule 3: this capability must be generic — usable by any
  future provider or factory, not shaped around Bogus's own vocabulary. Nothing
  in this ADR mentions `Faker`, `Randomizer`, or any Bogus type.
- Zero new pipeline mechanism, zero new allocation on the hot path for a caller
  that never calls it — `PrimitiveValueProvider`'s existing fork-key derivation
  already exists; this ADR exposes it, it doesn't reinvent it.
- Statelessness: calling the new method twice for the same active request must
  return the same value (idempotent), and must never advance or mutate the
  engine's own internal random stream — a caller reading a seed must not be able
  to perturb an unrelated built-in value derived from the same path.

## Considered Options

1. **A public method on `ICompositionContext`, `int DeriveSeed()`** — callable
   only while a request is actively being resolved (mid-`TryProvide` for a
   provider, mid-factory for a registration/rule), returning a value derived
   purely from the root seed and the current request's path, via the same
   FNV-1a hash [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)
   already uses for fork-key derivation. No stream, no mutable state, no
   `IRandomSource` exposure.
2. **An eager field on `CompositionProviderRequest`** — `ADR-0024`'s public
   request struct grows a `Seed` field, computed by `PublicProviderAdapter` for
   every stage-5/6 request regardless of whether the provider reads it.
3. **Expose `IRandomSource` (or a public wrapper around it) directly** — give a
   provider/factory the engine's own forkable random stream, not just a seed.

## Decision Outcome

**Chosen: Option 1** — confirmed directly with the user, who explicitly rejected
exposing `IRandomSource`/path internals (Option 3) and explicitly preferred a
method a caller invokes on demand over an eagerly-computed field every request
carries whether used or not (Option 2 — rejected because most stage-5/6 providers,
including `NSubstituteProvider`, never need randomness at all, and stamping every
request with a derived seed would blur `CompositionProviderRequest`'s existing role
as plain descriptive metadata with real per-request computation cost).

```csharp
public interface ICompositionContext
{
    T Resolve<T>(in CompositionRequestDescriptor descriptor);
    T Resolve<T>();

    /// <summary>
    /// Derives a deterministic <see langword="int"/> seed from this context's root seed and the
    /// request currently being resolved — usable to seed a caller-owned PRNG (e.g. a
    /// <c>Bogus.Randomizer</c>) without exposing the engine's own internal random source or path
    /// representation. The same root seed and the same request path always derive the same value;
    /// a different path (a different member, a different constructor parameter, a different
    /// element of a collection) always derives independently. Calling this method repeatedly for
    /// the same active request returns the same value every time — it does not advance any
    /// stream, and does not perturb any other value's own derivation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No request is currently being resolved on this context — the same misuse this interface's
    /// existing descriptor-less <see cref="Resolve{T}()"/> overload already guards against.
    /// </exception>
    int DeriveSeed();
}
```

### Where it's callable from

`DeriveSeed()` is valid exactly where the descriptor-less `Resolve<T>()` already is
— mid-invocation of whichever context-owned mechanism is actively resolving one
request:

- **A public provider's `TryProvide`** (stage 5/6, [ADR-0024](0024-public-provider-extensibility-model.md)) —
  the primary motivating caller. `CompositionContext.InvokeProvider`
  ([ADR-0024 Amendment 1](0024-public-provider-extensibility-model.md#amendment-1-2026-07-31-nested-resolution-and-self-recursion-fixed-before-any-external-consumer-existed))
  already tracks which request a provider invocation is resolving; `DeriveSeed()`
  reads that same request's path.
- **A registration factory** (`Register<T>(context => ...)`, stage 3,
  [ADR-0019](0019-registrations-and-service-provider-injection.md)) or a
  **configuration-rule factory** (`.For<T>().Use(context => ...)`/`.Member(...).Use(...)`,
  stage 4, [ADR-0020](0020-composition-configuration-rules.md)) — `InvokeFactory`
  already pushes a frame around exactly this call; `DeriveSeed()` reads the
  request that triggered the factory, not the factory's own internal
  manual-resolve ordinal counter (which is a separate, pre-existing mechanism for
  the factory's own *nested* `context.Resolve<T>()` calls — unrelated to this
  method).
- **Outside any active request** (a context reference that escaped its call and
  is invoked later, or called against a context not currently dispatching
  anything) — throws `InvalidOperationException`, identical framing to
  `Resolve<T>()`'s existing guard for the same misuse shape.

### Derivation

No new hashing mechanism — `DeriveSeed()` reuses
[ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)'s
existing FNV-1a path-hash exactly as `PrimitiveValueProvider` already does
internally, with one difference: it hashes with a distinct, fixed salt/namespace
value from whatever key the engine's own internal forking uses for the same path,
so a public caller's derived seed is never accidentally identical to (or
correlated with) a value the engine itself might separately derive from the same
path for its own purposes. This costs one extra hash input, not a new algorithm.
The result is a pure function of `(root seed, current request path, this fixed
salt)` — no `IRandomSource` instance is constructed, read, or advanced, which is
what makes repeated calls idempotent and non-perturbing by construction rather
than by convention.

### Interaction with existing mechanisms

- **Path-independence.** A `DeriveSeed()`-backed value inherits ADR-0012's
  guarantee directly, for free: it's derived from the exact same structured path
  representation, so the same rename-without-reorder / add-an-unrelated-member
  stability properties apply to it as to any built-in value.
- **Recursion / manual-resolve frames.** Unrelated. `DeriveSeed()` reads path
  state already tracked for diagnostics/forking; it doesn't push, pop, or
  interact with the active-construction-frame or manual-resolve-frame stacks at
  all.
- **Concurrency.** `Composer`'s configuration (and therefore any `Composer`-served
  composition) already supports concurrent `Create<T>()`/`CreateMany<T>()`/`CreateRow`
  calls, each with its own `CompositionContext`. `DeriveSeed()` reads only that
  one context's own root seed and current path — both already per-context,
  immutable-after-construction state — so it introduces no new shared mutable
  state and needs no locking beyond what already protects the rest of the
  context.
- **Diagnostics.** Not affected — `DeriveSeed()` produces a value for a caller to
  consume, not a diagnosable outcome of its own; a failure downstream (e.g. a
  provider that calls it and then still can't produce a value) surfaces through
  the same existing failure path as any other provider decline.

### Package Boundaries

Defined in core `Compono`, on the already-public `ICompositionContext` — no new
type, no new package dependency, and (per the Decision Drivers above) no
Bogus-specific vocabulary anywhere in this ADR's contract. `Compono.NSubstitute`
is unaffected; it never calls this method (its provider needs no randomness, per
[ADR-0025](0025-compono-nsubstitute-package-design.md)'s explicit non-goal).

### Positive Consequences

- Closes the exact gap Milestone 6's design review identified: a provider or
  factory can now get deterministic, path-independent randomness without the
  public contract exposing `IRandomSource` or any engine-internal type.
- Reuses ADR-0012's hashing mechanism entirely — no new algorithm, no new
  performance characteristic to benchmark separately from what
  `PrimitiveValueProvider` already pays.
- Zero cost for any caller that never invokes it — the method is on-demand, not
  an eagerly-computed field every stage-5/6 request now carries.
- Generic: usable by `Compono.Bogus` (ADR-0027) and by any future public provider
  or factory that needs deterministic randomness — not a special case wired only
  for Bogus.

### Negative Consequences

- One more public method on `ICompositionContext`, an interface generated code
  already crosses the assembly boundary to call — accepted as the smallest
  addition that satisfies the Decision Drivers; the alternative (Option 3) costs
  strictly more public surface for a capability nothing in this milestone's scope
  needs.
- A caller that captures a `Faker`/PRNG seeded once via `DeriveSeed()` and then
  reuses it across *multiple* requests (rather than deriving fresh state per
  request) can silently reintroduce path-dependence on its own — this ADR
  guarantees the *seed* is independent per path; a consumer of that seed is still
  responsible for actually using it per-request, not caching it across requests.
  [ADR-0027](0027-compono-bogus-package-design.md) follows this correctly (a
  fresh `Faker`/`Randomizer` per handled request), noted here so the constraint
  is visible at the mechanism layer too.

## Pros and Cons of the Options

### A public `DeriveSeed()` method (chosen)

- Good, because it costs nothing for a caller that never uses it.
- Good, because it keeps `CompositionProviderRequest` a plain, cheap descriptor
  with no per-request computation baked in.
- Good, because "ask the context for a capability" reads more clearly than
  "read a field that happens to already be populated."
- Bad, because it's one more method on an already-public interface — accepted,
  see Negative Consequences.

### An eager `CompositionProviderRequest.Seed` field

- Good, because it needs no new interface method — the value is just already
  there.
- Bad, because it computes a fork-hash for every stage-5/6 request regardless of
  whether any registered provider actually reads it — real, unconditional cost
  for a value most providers (all of Milestone 5's `NSubstituteProvider`) never
  touch.
- Bad, because it blurs `CompositionProviderRequest`'s existing role as
  descriptive metadata (`RequestedType`/`DeclaringType`/`Name`/`Nullability`) with
  a computed capability — a field that looks like data but is secretly doing
  work every time a request is constructed.

### Exposing `IRandomSource` (or a public wrapper) directly

- Bad, because it's exactly what the user explicitly ruled out: the internal
  random-source abstraction should remain free to evolve independently of the
  public provider model, per [ADR-0024](0024-public-provider-extensibility-model.md)'s
  own precedent of keeping engine-internal types internal.
- Bad, because a stream-based contract (draw the next value) is a stronger,
  stateful promise than a caller in this milestone's scope actually needs — a
  seed is sufficient to construct an independent PRNG (Bogus's `Randomizer`) on
  the caller's own side.

## Links

- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) —
  the path-hashing mechanism this ADR exposes a narrow slice of
- [ADR-0019](0019-registrations-and-service-provider-injection.md) — the
  manual-resolve frame / descriptor-less `Resolve<T>()` precedent this ADR's
  "callable only mid-request" framing follows
- [ADR-0024](0024-public-provider-extensibility-model.md) — the public provider
  contract this ADR's primary motivating caller (`TryProvide`) belongs to;
  `InvokeProvider`'s Amendment 1 fix, which this ADR's request-tracking builds on
  directly
- [ADR-0027](0027-compono-bogus-package-design.md) — `Compono.Bogus`, the first
  real consumer of this capability
