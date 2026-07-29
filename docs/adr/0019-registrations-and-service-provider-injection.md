# [ADR-0019] Registrations and Service Provider Injection

**Status:** Accepted

**Date:** 2026-07-29

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

Pipeline stage 3 ("exact registrations") has existed since Milestone 2 as a
context-owned deterministic lookup, but Milestone 2 only exercised it through an
internal test seam — no public registration API exists yet
([PLAN-0002](../plans/0002-milestone-2-core-composition-engine.md)'s explicitly
deferred scope). `docs/mvp.md`'s Milestone 3 lists "exact type registrations" as
in-scope, and the milestone's headline new capability is **service injection** —
letting a composed graph pull a value from an externally-configured dependency
container rather than only from Compono's own registrations/providers.

Two questions this ADR settles together, because the second is a specific case of the
first: (1) what does an exact registration actually look like — factory shape,
duplicate handling — and (2) how does an external container (starting with the BCL's
own `System.IServiceProvider`, not necessarily `Microsoft.Extensions.
DependencyInjection`) participate in resolution without the core `Compono` package
taking on a dependency it shouldn't.

## Decision Drivers

- `design-decisions.md` rule 3: the core `Compono` package must never reference or
  know about an integration package. `Microsoft.Extensions.DependencyInjection` is a
  NuGet package like `NSubstitute`/`Bogus`/`xunit.v3` — core can't depend on it.
  `System.IServiceProvider`, by contrast, ships in the BCL itself (`System`
  namespace, no package reference at all) — referencing it costs core nothing and
  violates nothing.
- `docs/architecture.md`'s fixed 9-stage pipeline order — service injection has to
  slot into the existing stages, not add a tenth.
- `docs/mvp.md`'s explicit "configuration conflict diagnostics" scope line.
- `references/coding-standards.md`'s DI/composition rules: no service-locator
  pattern, all dependencies constructor-injected — a registration factory needs a
  narrow, deliberate resolve surface, not free rein to reach into arbitrary context
  internals.
- `ADR-0012`'s reproducibility contract: every resolved value's random identity comes
  from its structural path position alone. Any new way to resolve a value (a
  registration factory calling back into the context) has to fit this contract, not
  bypass it.

## Considered Options

### Registration API shape

1. **`Register<T>(Func<ICompositionContext, T> factory)`**, reusing the existing
   public `ICompositionContext` (unchanged surface from the consumer's point of
   view — it's the same interface generated plan code already calls) as the
   factory's resolve capability, plus a convenience `Register<T>(Func<T> factory)`
   overload for the common no-dependency case (`docs/public-api.md`'s
   `_ => new FakeClock()` example).
2. **`Register<T>(T instance)` only** — exact-instance registration, no factory
   indirection at all; anything needing construction-time logic is out of scope for
   M3.
3. **A dedicated, narrower factory-context type** distinct from
   `ICompositionContext` (e.g. `ICompositionRegistrationContext`), exposing only what
   a registration factory should be able to do.

### Duplicate exact registration for the same type

1. **Throw at build time.** `Composer.Create(...)` (via `CompositionConfiguration`'s
   `Build()` step, [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md))
   collects every duplicate-type conflict across the whole builder chain (direct
   registrations and every applied profile) and throws one
   `CompositionConfigurationException` naming every conflicting type and the source
   (direct call, or which profile) of each conflicting registration.
2. **Last registration wins, silently.**
3. **Last registration wins, with a non-throwing recorded diagnostic.**

### Service injection shape

1. **Native `IServiceProvider` fallback inside stage 3.** `CompositionBuilder`
   gains `UseServiceProvider(IServiceProvider provider)`. Stage 3's context-owned
   check becomes two ordered sub-steps: (a) the exact-registration table, (b) if
   unregistered and a service provider was configured, `provider.GetService(typeof(T))`
   — `null` means "not found," anything else (including a boxed default value type)
   is a genuine result. Only if both decline does resolution fall through to stage 4.
   No new pipeline stage, no public provider interface — this lives entirely inside
   the same context-owned stage 3 already responsible for exact-registration lookup.
2. **A provider-shaped bridge**, implementing whatever public provider-authoring
   interface a future ADR defines, registered into stage 4/5 like any other
   extensible-stage contributor.
3. **Defer service injection entirely** to whenever the public provider
   extensibility question (deferred to Milestone 5 by this design review) is
   resolved, since a "real" DI bridge arguably wants provider semantics.

## Decision Outcome

**Registration API — Option 1**, confirmed with the user during design review:
`Register<T>(Func<ICompositionContext, T> factory)` plus a `Register<T>(Func<T>
factory)` convenience overload. Reusing the existing public `ICompositionContext`
avoids introducing a second, parallel "resolve" surface for no real benefit — but it
requires one small, additive extension to that interface (see Amendment below), not
a new type.

**Duplicate registrations — Option 1, throw at build time**, confirmed directly with
the user: "Exact registrations should be unambiguous. Silent or diagnostic-only
last-wins behavior would make profile composition order-dependent and could hide
configuration mistakes." This is deliberately stricter than a typical DI container's
last-registration-wins convention — Compono's registrations are meant to be a small,
curated, exact-match set (`docs/architecture.md`'s stage 3, distinct from stage 4's
ordered/overridable provider rules), and an unintentional collision between two
profiles (or a profile and a direct call) is exactly the kind of mistake `docs/mvp.md`
calls out "configuration conflict diagnostics" to catch. An intentional override is a
distinct, explicit future API (e.g. a `TryRegister`/`Replace` verb) if a real need for
one appears — not the default behavior of `Register`.

**Service injection — Option 1, native `IServiceProvider` fallback inside stage 3**,
confirmed directly with the user, with the exact ordering they specified: "1. Exact
Compono registrations, 2. Configured IServiceProvider, 3. continue to configuration
rules (stage 4) if unresolved." This is the same *kind* of move ADR-0010's Amendment 3 already made for
stage 7 (a context-owned deterministic stage internally trying more than one thing in
order, before falling through to the next pipeline stage) — extending stage 3 the same
way keeps the top-level 9-stage contract completely unchanged, adds no new public
extensibility surface, and costs core `Compono` nothing beyond a BCL interface
reference:

```csharp
var composer = Composer.Create(builder => builder
    .UseServiceProvider(app.Services));
```

A richer `Microsoft.Extensions.DependencyInjection`-specific integration (auto-
registering every service in an `IServiceCollection`, scoping a request-scoped
`IServiceProvider` per composition, keyed-service support) is explicitly **out of
scope** for `Compono` core and this ADR — that belongs in a future, separate optional
package (a plausible `Compono.Extensions.DependencyInjection`, not designed here)
that itself calls `UseServiceProvider(IServiceProvider)` as its underlying mechanism,
the same way `Compono.NSubstitute`/`Compono.Bogus` build on core extension points
without core knowing they exist.

**Exact `IServiceProvider` fallback semantics**, stated explicitly per design review
(deliberately over-specified rather than left implicit, since a DI-bridge's edge-case
behavior is exactly the kind of thing that's expensive to change once consumers
depend on it):

- **Exact Compono registrations always win.** Stage 3's two sub-steps run in the
  fixed order shown above — the configured `IServiceProvider` is consulted only when
  no `Register<T>(...)` entry exists for the exact requested type. A registration
  never gets silently shadowed by a container entry, or vice versa.
- **`null` from `GetService` means "unresolved," and falls through to stage 4** —
  exactly like an ordinary registration miss. It is not treated as "the container
  affirmatively has no opinion and composition should fail here"; the pipeline simply
  continues.
- **An exception thrown by the configured `IServiceProvider` is authoritative, not a
  decline.** Stage 3 is a context-owned stage, which per
  [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) is allowed
  to report `Failure` (unlike an ordinary `ICompositionProvider`, which can only
  report `NotHandled`/`Success`). A container-thrown exception is never caught and
  silently downgraded to "not handled, keep trying stage 4" — it terminates
  resolution for that request, surfaced as `CompositionException` with the original
  exception preserved as `InnerException` (never `throw ex;`, per
  `coding-standards.md`'s exception rules — the original stack trace is never
  discarded). A misconfigured container should fail loudly, the same way a throwing
  exact-registration factory already does.
- **A non-null, wrongly-typed result is a configuration-shaped failure, not an
  `InvalidCastException`.** `IServiceProvider.GetService(Type)` returns `object?` with
  no compile-time guarantee the runtime value is actually assignable to `T` (a
  misbehaving or misconfigured container implementation could return anything). Stage
  3 checks `result is T` explicitly before use; a non-null, non-assignable result
  throws a structured `CompositionException` naming the requested type and the
  actual runtime type returned, rather than letting an unchecked cast throw an
  unrelated-looking `InvalidCastException` several frames away from the real cause.
- **Compono never creates, resolves, or disposes a scope.** `UseServiceProvider`
  stores exactly the `IServiceProvider` instance it's given and calls
  `GetService(Type)` on it directly — never `IServiceScopeFactory.CreateScope()`,
  never anything disposal-related. The caller owns the provider and its entire
  lifetime (including any scoping); `Compono` is a pure consumer of whatever
  `IServiceProvider` it's handed, for exactly as long as the enclosing `Composer` is
  used. A consumer wanting per-test-scoped container resolution is responsible for
  passing a differently-scoped `IServiceProvider` to `UseServiceProvider` themselves
  (e.g. per-test-class or per-test-case, at their own discretion) — `Compono` has no
  opinion on this and creates no scope of its own to have an opinion about.
- **Configuring more than one `IServiceProvider` is a build-time conflict** — the
  specific case of [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md)'s
  Amendment, which generalizes this to every scalar configuration verb
  (`WithSeed`/`WithCollectionSize`/`UseServiceProvider`) rather than deciding it
  ad hoc here: `UseServiceProvider` called twice (directly, or once directly and
  once from a profile, or from two different profiles) throws
  `CompositionConfigurationException` rather than silently using whichever call
  happened last. A genuine multi-container-fallback-chain need is out of scope for
  M3 and not designed here; if one materializes later, it gets its own ADR rather
  than silently falling out of "last call wins."

### `ManualResolve`: the descriptor-less `Resolve<T>()` overload

Registration factories (and, per [ADR-0020](0020-composition-configuration-rules.md),
rule factories) are hand-written by a consumer, not generated code — they can't
construct a `CompositionRequestDescriptor` naming a constructor-parameter/required-
member position, because there isn't one. `ICompositionContext` gains a second,
descriptor-less overload for exactly this case:

```csharp
public interface ICompositionContext
{
    T Resolve<T>(in CompositionRequestDescriptor descriptor); // unchanged, generated-code path
    T Resolve<T>();                                            // new: manual/factory path
}
```

This is purely additive to the `Accepted` ADR-0010 contract — generated code is
unaffected, still calls the descriptor overload exclusively. Internally, the new
overload expands to a `CompositionRequest` carrying a new
`PathSegment.ManualResolve` case — defined, and verified in full against
`ADR-0012`'s reproducibility contract (sibling-call distinctness, nested-factory path
correctness, recursion detection, cross-run reproducibility), in
[ADR-0012's Amendment 3](0012-composition-path-identity-and-deterministic-random-forking.md#amendment-3-2026-07-29-manualresolve-segments-for-factoryrule-authored-resolution) —
not specified here, per the design review's direction that this belongs alongside
`CompositionPath`'s other identity rules rather than duplicated into a second ADR.

### Positive Consequences

- Service injection ships with zero new public extensibility surface and zero new
  package dependency for core `Compono` — `System.IServiceProvider` is already part
  of the BCL every `Compono`-referencing project already has.
- Duplicate-registration conflicts fail loudly and specifically, at the moment a
  consumer actually made the mistake (`Composer.Create(...)`), not silently or three
  layers removed from the cause.
- Registration factories reuse the exact same public resolve surface generated code
  and (per ADR-0020) rule factories use — one `ICompositionContext` contract for
  every "code that needs to resolve a nested value" case, not three parallel ones.
- Explicitly deferring MEDI-specific integration (`IServiceCollection`, scoping,
  keyed services) keeps this ADR's actual surface small and matches the "avoid
  designing for hypothetical future requirements" standard — that richer package,
  if it's ever built, gets its own ADR against real requirements.

### Negative Consequences

- Strict throw-on-duplicate means an *intentional* override pattern (a test-specific
  `Composer.Create` wanting to override a profile's `IClock` registration) has no
  first-class API in M3 — a consumer has to avoid registering the same type twice
  across profiles/direct calls, or compose profiles more granularly. Accepted per the
  user's explicit direction; a deliberate `Replace`/`TryRegister` verb is a candidate
  for a future ADR if this friction turns out to be real once M3 ships.
- The `ManualResolve` per-node ordinal counter (ADR-0012 Amendment 3) is a small but
  genuine addition to `CompositionContext`'s internal per-request state — not free,
  though bounded (one `int` per active node, same lifetime as the frame already
  tracks).
- The single-`IServiceProvider` restriction means a consumer wanting a fallback
  chain across multiple containers has no supported way to express it in M3 —
  accepted as an explicit non-goal rather than a gap, since no concrete use case
  motivated it during this design review.
- `IServiceProvider.GetService(typeof(T))` is called for every stage-3 miss once
  `UseServiceProvider` is configured, even for ordinary domain types no container
  registration exists for — a real, if generally cheap, per-request cost that only
  applies when a consumer opts in.

## Pros and Cons of the Options

### Registration API — Option 1 (reuse `ICompositionContext`, plus `Func<T>` overload)

- Good, because it's zero new public types.
- Good, because a registration factory and generated code share identical resolve
  semantics — no special-cased "factory-only" behavior to document separately.
- Bad, because it means extending an `Accepted` ADR-0010 contract (additively) rather
  than a clean, standalone factory type — judged acceptable since the extension is
  purely additive and doesn't change the existing descriptor overload at all.

### Registration API — Option 2 (instance-only)

- Good, because it's the simplest possible shape.
- Bad, because it can't express `docs/public-api.md`'s own leading example
  (`Register<IClock>(_ => new FakeClock())`) — a registration whose value needs to be
  constructed, not just handed over as an already-built instance, is exactly the
  common case in practice (most fakes/doubles have some construction logic, even if
  trivial).

### Registration API — Option 3 (dedicated factory-context type)

- Good, because it could expose a deliberately narrower surface than
  `ICompositionContext`.
- Bad, because `ICompositionContext` is already deliberately minimal (one method,
  `Resolve<T>`, per ADR-0010) — there's nothing left to narrow. A second type with
  the identical shape is pure duplication.

### Duplicate registrations — Option 2 (silent last-wins)

- Good, because it's the most permissive, matches common DI-container convention.
- Bad, because it makes `AddProfile` call order load-bearing for correctness in a way
  nothing in the builder's public shape signals — directly rejected by the user for
  hiding configuration mistakes.

### Duplicate registrations — Option 3 (last-wins + diagnostic)

- Good, because it's deterministic and still surfaces the conflict somewhere.
- Bad, because "somewhere" (a log, an inspectable diagnostics list) is easy to miss
  entirely — a build-time throw is the only version of this that a consumer can't
  accidentally ignore.

### Service injection — Option 2 (provider-shaped bridge)

- Good, because it would reuse whatever extensibility mechanism M5 eventually builds.
- Bad, because it requires that mechanism to exist *now*, directly contradicting the
  "defer public provider extensibility to M5" decision this same design review just
  made — `IServiceProvider` support would then be blocked on a decision explicitly
  deferred for having no real M3 consumer yet, when `IServiceProvider` itself *is* a
  real M3 consumer need.

### Service injection — Option 3 (defer entirely)

- Good, because it avoids the ordering question above entirely.
- Bad, because "service injection" is `docs/mvp.md`'s (and the user's design-review
  prompt's) named headline capability for this milestone — deferring it wholesale
  would leave Milestone 3 without its own stated primary deliverable.

## Links

- [docs/mvp.md](../mvp.md) — Milestone 3 scope ("exact type registrations"), and this
  design review's service-injection framing
- [docs/public-api.md](../public-api.md) — Registrations section (`Register<T>`
  examples already shown there remain valid under this decision)
- [docs/architecture.md](../architecture.md) — Resolution Pipeline stage 3, and stage
  7's existing hybrid-stage precedent (`CollectionPlanCache<T>` alongside the
  ordered built-in provider list)
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) — the
  `ICompositionContext`/`CompositionRequestDescriptor`/`CompositionRequestKind`
  contract this ADR additively extends
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) — the
  structural-forking reproducibility contract `ManualResolve`'s ordinal counter
  exists to preserve
- [ADR-0017](0017-immutable-composer-configuration-and-builder-model.md) — the
  build-time validation pass duplicate-registration detection runs inside
- [ADR-0020](0020-composition-configuration-rules.md) — rule factories, the other
  consumer of the descriptor-less `Resolve<T>()` overload
