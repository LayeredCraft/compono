# [ADR-0047] Compono.DependencyInjection: Configured-Resolution IServiceProvider Bridge

**Status:** Accepted

**Date:** 2026-08-20

**Decision Makers:** solo (Nick Cipollina)

## Context

A real consumer repository (`trivia-manager`, a Blazor app tested with bUnit v2 +
xUnit v3 + AutoFixture + AutoNSubstitute) was proposed as the dogfooding basis for a
hypothesized `Compono.BUnit` package. The starting product requirement was: bUnit
integration is worth investigating as a dedicated package if a coherent integration
boundary exists.

Research and dogfooding (bUnit's architecture, `trivia-manager`'s actual test code,
and Compono's existing internals) established the following, in order:

1. bUnit tests in `trivia-manager` repeat one pattern in every component test's
   constructor: compose a test double (`Fixture.Freeze<TSub>()`, AutoFixture +
   AutoNSubstitute), then manually push it into bUnit's `Ctx.Services`
   (`IServiceCollection`) via `AddSingleton`. This "compose, then push into a
   container" seam is real and repeated, but it is generic `IServiceCollection`
   behavior — nothing about it is bUnit-specific. It is *technically* expressible
   today with Compono's existing public API, but only through the descriptor-based
   `Resolve`/`ResolveShared` surface (point 6 below) — which requires hand-building a
   `CompositionRequestDescriptor` and carries sharing semantics designed for
   generated/framework-integration callers, not natural hand-written consumer code.
   "Technically possible via an existing API" is not the same claim as "already a
   good consumer experience"; this ADR's design gives hand-written code a genuinely
   better path to the same values (see Positive Consequences).
2. bUnit's more interesting extension point is its **fallback `IServiceProvider`**
   (`Services.AddFallbackServiceProvider`): a plain `IServiceProvider` consulted only
   when bUnit's own `Services` registration misses. This is also fully generic —
   `BunitContext.Services` is a plain `IServiceCollection`, and `AddFallbackServiceProvider`
   accepts a plain `IServiceProvider`. Nothing in bUnit needs to know Compono exists,
   and nothing Compono-side needs to know bUnit exists. There is no bUnit-specific
   wiring, lifecycle hook, or timing concern left to own once a generic
   Compono-backed `IServiceProvider` exists.
3. ADR-0019 already anticipated and explicitly deferred exactly this kind of package —
   a general Microsoft.Extensions.DependencyInjection-facing bridge — naming it as a
   plausible future `Compono.Extensions.DependencyInjection`, built on top of
   `UseServiceProvider`.
4. Compono's current `UseServiceProvider` is **pull-only**: it lets Compono ask an
   external `IServiceProvider` for a value it can't otherwise satisfy. bUnit's
   fallback-provider need is the reverse verb: bUnit asks Compono for a value. No
   existing mechanism serves that direction.
5. `CompositionRow` (ADR-0021) is genuinely framework-agnostic — both
   `Compono.XunitV3` and `Compono.TUnit` build on it — and is the right foundation
   for "several individually-addressable values sharing one deterministic scope,"
   which is structurally closer to what a rendered component tree needs than
   `UseServiceProvider`'s one-value-pull shape.
6. The obvious naive design — expose `CompositionRow.Resolve<TValue>()` directly to
   hand-written consumer code and wrap it as `IServiceProvider.GetService(Type)` —
   does not work:
   - The descriptor-less `Resolve<TValue>()` overload can only be called from inside
     a registration factory or `ICompositionValueProvider.TryProvide` implementation
     (it throws `InvalidOperationException` otherwise, per its `_manualResolveFrames`
     guard) — it is not callable from hand-written test/consumer code at all.
   - Even where a descriptor-based `Resolve`/`ResolveShared` call is legal, plain
     `Resolve<T>()` performs an independent, unshared composition on every call
     (`isShared: false`) — two calls for the same type return different instances by
     default; identity requires the descriptor-based `ResolveShared` path, which
     throws if a type is shared twice (a "establish once" contract, not an
     idempotent "get-or-create" one).
   - `IServiceProvider.GetService(Type)`'s BCL contract is "return `null` on a
     genuine miss" — `Resolve<T>()`'s contract is "throw a diagnosed
     `CompositionException` on any unsatisfiable or invalid result." These two
     contracts cannot be reconciled by a thin wrapper; nothing in Compono's public
     surface today distinguishes "nothing could handle this request" from "a stage
     tried and failed," even though that distinction exists internally
     (`CompositionProviderResult.Handled`/`NotHandled`).
   - Only stages 2 (scope cache), 3a (exact registrations), 3b (`UseServiceProvider`
     passthrough), and 4-6 (configuration rules, semantic/test-double/NSubstitute
     providers) are `Type`-keyed at runtime. Stages 7-8 (ordinary generated-plan and
     collection-plan composition) dispatch through `PlanCache<TValue>.Instance` /
     `CollectionPlanCache<TValue>.Instance` — closed-generic static fields reachable
     only with the target type known at compile time. Reaching them from a runtime
     `System.Type` would require `MakeGenericType` plus reflected field access —
     real reflection, ruled out by this repo's no-reflection-by-default stance
     (ADR-0001) and by the absence of any dogfooding evidence that arbitrary
     generated-type composition is even needed for this use case.

This ADR records the resulting design: a small, honestly-scoped core primitive, and a
thin, provider-neutral `IServiceProvider` bridge package built on it. It also records,
deliberately, that the investigation changed the shape of the original product
requirement — the evidence pointed at a general DI bridge, not a bUnit-specific
package, and the design followed the evidence rather than the originally hypothesized
package name.

## Decision Drivers

- Real, repeated dogfooding friction (compose-then-register-into-a-container) must be
  addressed by *something*, but only to the extent the evidence actually supports.
- Core `Compono` must never know an integration package exists (existing rule); any
  new capability that a DI/bUnit bridge needs must be added because it is a genuine,
  general Compono capability, not smuggled in to serve one integration.
- No reflection-based fallback by default (ADR-0001) — a new capability must not
  require `MakeGenericType`, `Activator.CreateInstance`, or `DynamicInvoke` to reach
  runtime-typed values.
- API honesty: a public method's name and behavior must not imply a broader
  capability than it actually has (`Resolve<T>()`-shaped naming for something that
  only covers a subset of the pipeline would be misleading).
- Don't manufacture a package to satisfy a stated product requirement if the evidence
  doesn't support a *dedicated* package — `Compono.BUnit` was the original
  hypothesis; the evidence redirected it.
- Keep new core surface area minimal and precedented where possible
  (`ICompositionValueProvider` and `RowInvokerRegistry`/ADR-0041 are the closest
  existing precedents for "public, integration-author-facing, not general-consumer"
  API).

## Considered Options

1. **`Compono.BUnit`** — a dedicated bUnit integration package wrapping
   `RenderComponent`/parameters/services.
2. **No new package; document the manual push pattern only** — `Ctx.Services.AddSingleton(...)`
   using values obtained via existing `CompositionRow`/`Composer` API.
3. **A public, general `CompositionRow.Resolve(Type)`** mirroring `Resolve<T>()`'s
   full contract (throwing, full-pipeline) as a non-generic overload.
4. **`Compono.DependencyInjection`** — a narrow core primitive
   (`CompositionRow.TryResolveConfigured(Type, out object?)`, reaching stage 2, stage
   3a, and stages 4-6 only, no new sharing semantics) plus a thin `IServiceProvider`
   bridge (`row.AsServiceProvider()`) with adapter-owned identity caching. *(chosen)*

## Decision Outcome

Chosen option: **4, `Compono.DependencyInjection`**, because it is the smallest
design that (a) actually solves the evidenced bUnit fallback-provider use case, (b)
stays fully provider-neutral and framework-agnostic (nothing bUnit-specific
survives), (c) requires no reflection anywhere, and (d) does not misrepresent its own
capabilities via naming or contract.

### Core primitive

```csharp
namespace Compono;

public sealed partial class CompositionRow
{
    /// <summary>
    /// Attempts to resolve <paramref name="type"/> using only Compono's
    /// configured/provider-backed resolution stages: this row's existing scope
    /// values, exact registrations, configuration rules, and registered
    /// <see cref="ICompositionValueProvider"/> instances (including
    /// Compono.TestDoubles and Compono.NSubstitute). This is NOT equivalent to
    /// <see cref="Resolve{TValue}(CompositionRequestDescriptor)"/> — it does not
    /// consult a configured <c>IServiceProvider</c> (<c>UseServiceProvider</c>),
    /// and it cannot perform ordinary generated-plan composition of arbitrary
    /// concrete types, because that dispatch requires the target type to be known
    /// at compile time.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> and the resolved value if a configured/provider stage
    /// satisfied <paramref name="type"/>; <see langword="false"/> if no such stage
    /// could handle it. A stage that is applicable but fails (e.g. a provider or
    /// configuration rule throws) still throws a diagnosed
    /// <see cref="CompositionException"/> rather than returning <see langword="false"/> —
    /// this method distinguishes "nothing could handle this" from "something tried
    /// and failed," it does not swallow the latter.
    /// </returns>
    public bool TryResolveConfigured(Type type, out object? value);
}
```

Behavior, precisely:

- Reaches: stage 2 (this row's existing scope values — read-only, unconditional, same
  as today), stage 3a (exact `Register<T>` registrations), stages 4-6 (configuration
  rules, semantic providers, `ICompositionValueProvider` implementations —
  `Compono.TestDoubles`, `Compono.NSubstitute`, and any future provider registered
  the same way).
- Does **not** reach: stage 3b (`UseServiceProvider`) or stages 7-8 (generated-plan /
  collection-plan composition of ordinary concrete types).
- Performs an unshared resolution per call (same `isShared: false` contract as plain
  `Resolve<T>()`) — introduces **no new sharing/caching semantics** into
  `CompositionScope`. If the row already holds a shared value for `type` from
  ordinary `[Shared]`/`ResolveShared` usage elsewhere, stage 2's existing read
  surfaces it — that's inherited coherence, not new behavior.
- Returns `false`, never throws, when no reachable stage can handle `type` — this is
  what makes it usable as the backing call for `IServiceProvider.GetService(Type)`'s
  null-on-miss contract.
- Still throws `CompositionException` (via the existing `BuildException` path) if a
  reachable stage is applicable but fails — a broken configuration rule or provider
  is a real bug, not an "absence," and must not be silently swallowed to `false`.
- No reflection: every stage it reaches is already `Type`-keyed internally
  (`CompositionProviderRequest.RequestedType`, `_registrations.TryGet(Type, ...)`,
  provider dictionaries) — this method is a thin, honest exposure of that existing
  dispatch, not new dispatch machinery.

### Compono.DependencyInjection package

New package, `Compono.DependencyInjection`, depending on `Compono` and
`Microsoft.Extensions.DependencyInjection.Abstractions` only.

```csharp
namespace Compono;

public static class CompositionRowServiceProviderExtensions
{
    /// <summary>
    /// Wraps <paramref name="row"/> as an <see cref="IServiceProvider"/> backed by
    /// <see cref="CompositionRow.TryResolveConfigured(Type, out object?)"/>, with
    /// stable per-<see cref="Type"/> identity for the lifetime of the returned
    /// instance. Do not configure a DIFFERENT row's <c>UseServiceProvider</c> with
    /// the result of this call on a row that itself (directly or transitively)
    /// resolves back into that same row — nothing in Compono detects a resolution
    /// cycle that crosses two rows, and it will overflow the stack rather than throw
    /// a diagnosed exception.
    /// </summary>
    public static IServiceProvider AsServiceProvider(this CompositionRow row);
}
```

Backed by an internal adapter — never a public type, matching the "prefer
implementation types internal when there's no consumer reason to construct them
directly" default:

```csharp
internal sealed class ComponoServiceProvider(CompositionRow row) : IServiceProvider
{
    private readonly Dictionary<Type, object?> _cache = [];

    public object? GetService(Type serviceType)
    {
        if (_cache.TryGetValue(serviceType, out var cached))
        {
            return cached;
        }

        if (!row.TryResolveConfigured(serviceType, out var value))
        {
            return null;
        }

        _cache[serviceType] = value;
        return value;
    }
}
```

Identity/caching rules, owned entirely by this adapter, not by `CompositionRow`:

- The adapter caches a **successful** `TryResolveConfigured` result by requested
  `Type`, and returns the cached value on every subsequent `GetService` call for that
  type. This is what makes "the test configures a double, the rendered component
  receives the same instance" work.
- Misses (`TryResolveConfigured` returns `false`) are **not** cached — a `false`
  result doesn't preclude a later registration or provider from satisfying the same
  type within the same test.
- `TryResolveConfigured` *can* legitimately return `(true, null)` for a
  nullable-annotated requested type — `ValidateAuthoritativeValue` accepts a `null`
  value from scope, an exact registration, or a stage 4-6 provider precisely when the
  requested type's `Nullability` is `Nullable`; it rejects `null` (as a thrown
  failure, not a quiet miss) for a non-nullable request. The adapter's
  `Dictionary<Type, object?>` handles this correctly on its own terms — `TryGetValue`
  distinguishes "key present with a `null` value" (a real, cached, handled result)
  from "key absent" (never attempted) — so a legitimately-`null` resolution is cached
  and not re-resolved on every call. What the adapter *cannot* do anything about is
  `IServiceProvider.GetService(Type)`'s own external contract: a caller outside the
  adapter sees `null` for both "resolved to null" and "no service." That ambiguity is
  inherent to `IServiceProvider` itself (the same ambiguity exists for any DI
  container that permits a null-valued registration) — this design does not introduce
  it and has no way to remove it.
- This is explicitly **not** Microsoft.Extensions.DependencyInjection lifetime
  modeling. There is no singleton/scoped/transient concept here — it is "one stable
  value per requested type, for the lifetime of this adapter instance," which exists
  to serve the evidenced test/integration use case, not to reimplement DI container
  semantics. The adapter never creates an `IServiceScope` and this package exposes no
  `IServiceScopeFactory` integration.
- The adapter does **not** dispose cached values, whether or not they implement
  `IDisposable`/`IAsyncDisposable`, and does not implement `IDisposable`/
  `IAsyncDisposable` itself. This follows directly from the rest of the pipeline:
  `CompositionScope`/`CompositionRow`/`CompositionContext`/`Composer` implement no
  disposal contract anywhere today, and ADR-0019 already established that Compono
  never takes ownership of a resolved value's lifetime, even when the value came from
  an external `IServiceProvider` it was handed. `AsServiceProvider()` extends that
  same stance rather than inventing a new one for this adapter specifically — the
  caller (the test, or `BunitContext`'s own disposal of whatever it registered) owns
  disposal of anything it obtains this way, exactly as it would for a value it
  constructed by hand.
- The rationale for caching here is about **semantics, not ownership**.
  `TryResolveConfigured` deliberately excludes `UseServiceProvider` (stage 3b)
  precisely because that stage explicitly delegates resolution *and* lifetime
  semantics to an external `IServiceProvider` the caller configured — caching its
  result in this adapter could silently flatten a legitimately transient/scoped
  external registration into "returned once, then cached forever here," which is not
  a claim this design is entitled to make about a value it doesn't own. For the
  stages `TryResolveConfigured` *does* reach (scope, exact registrations, stage 4-6
  providers), Compono producing the value through its own pipeline does not, by
  itself, mean Compono owns that object's lifetime either — a registration factory or
  a custom `ICompositionValueProvider` can just as easily return an externally
  constructed instance. What justifies the cache is not an ownership claim; it's that
  `AsServiceProvider()` deliberately *defines* stable per-`Type` identity, for this
  adapter instance, for values produced through the supported registration/provider
  pipeline — a scoped behavioral guarantee this package makes on purpose, not a fact
  about who owns what.

### Recursion

Same-row self-reference (`row.AsServiceProvider()` fed back into that same row's own
`UseServiceProvider`) is structurally impossible — `UseServiceProvider` is configured
before the row exists.

Cross-row recursion is not fully foreclosed, and this ADR does not claim it is. Every
stage `TryResolveConfigured` reaches that can be user-extended — exact registration
factories (`Register<T>(Func<ICompositionContext, T>)`), configuration rules
(`.For<T>().Use(...)`), and `ICompositionValueProvider.TryProvide` implementations —
hands arbitrary user/library code a delegate invocation, and nothing prevents that
code from closing over a *different* `CompositionRow`/`Composer` and calling
`.AsServiceProvider().GetService(...)` on it. Reentrance guards
(`_activeProviderRequests`, `_manualResolveFrames`) are instance state scoped to one
`CompositionContext` — they see nothing about another row's in-flight resolution.
Concretely: if Row A registers `IFoo` with a factory that calls into Row B's adapter,
and Row B was itself configured with `UseServiceProvider(rowA.AsServiceProvider())`,
resolving `IFoo` on Row A can recurse through Row B back into Row A's own `IFoo`
registration indefinitely — each hop is a fresh `CompositionContext` with empty guard
state, so nothing detects the cycle, and the result is a genuine
`StackOverflowException`, not a diagnosed `CompositionException`.

This requires deliberately unusual hand-written code — nobody wires two rows together
this way by accident, and `TryResolveConfigured` excluding `UseServiceProvider` means
the *bridge itself* never creates this path on its own. But the bridge doesn't
foreclose a consumer from creating one by hand, either, since the stages it reaches
still execute arbitrary user delegates with no cross-context awareness. No dogfooding
evidence supports anyone deliberately cross-wiring rows this way, so this ADR does not
add cross-context recursion tracking to guard against it; `AsServiceProvider()`'s XML
doc carries a warning describing the hazard accurately instead of asserting it can't
happen.

### Positive Consequences

- Solves the real, evidenced friction (compose-a-double, get-it-into-a-DI-container)
  for both the "push" pattern and the new "lazy fallback" pattern. For the push
  pattern specifically, this ADR gives hand-written consumer code its first natural
  path to a configured value at all: `provider.GetRequiredService<T>()` via
  `row.AsServiceProvider()`, replacing what was previously only reachable by
  hand-constructing a `CompositionRequestDescriptor` against the descriptor-based
  `Resolve`/`ResolveShared` surface — an API shape built for generated/framework
  callers, not recommended as a hand-written recipe.
- Fully provider-neutral: `Compono.TestDoubles` and `Compono.NSubstitute` work
  identically through `TryResolveConfigured`, since both are ordinary stage-6
  `ICompositionValueProvider`s.
- Fully framework-agnostic: nothing bUnit-specific exists anywhere in this design.
  The same package works for ASP.NET Core, a generic host, or any other
  `IServiceProvider`-consuming ecosystem — bUnit is a consumer example, not a
  dependency.
- No reflection anywhere in the new surface.
- Small core surface: one method, honestly named and scoped, immutable-once-shipped
  contract.
- Core package still knows nothing about `Compono.DependencyInjection` or any other
  integration package — `TryResolveConfigured` is a general capability on
  `CompositionRow`, not a DI-specific hook.

### Negative Consequences

- `TryResolveConfigured` cannot satisfy a request that only ordinary generated-plan
  composition could produce (an unregistered concrete type with no provider) — a
  consumer expecting `IServiceProvider`-style "anything Compono can compose" will be
  surprised by a `null`. Mitigated by clear XML-doc and ADR documentation of exactly
  which stages it reaches; no dogfooding evidence currently requires closing this gap,
  and closing it later would need either a new no-reflection mechanism or an
  explicit, separately-justified reflection opt-in (per ADR-0001's own escape hatch).
  Not attempted here.
  - Amendment note: closing this gap without reflection would need a generated,
    `Type`-keyed registry populated with an explicit opt-in list of types a consumer
    wants runtime-`Type`-reachable (conceptually similar in shape to
    `RowInvokerRegistry`/ADR-0041, but populated by consumer declaration rather than
    generator-discovered `[Compose]` parameters) — flagged here as a plausible future
    direction, not designed or committed to by this ADR.
- Deliberately cross-wiring two rows (one row's `AsServiceProvider()` fed into a
  different row's `UseServiceProvider`) can produce an undetected
  `StackOverflowException` rather than a diagnosed `CompositionException`, because
  reentrance guards are per-`CompositionContext` and don't span two rows. This
  requires hand-written code to deliberately construct the cross-wiring — no
  dogfooding evidence supports anyone doing so — and this ADR does not add
  cross-context recursion detection to guard against it; see the Recursion section
  for the full trace.
- A second package (`Compono.DependencyInjection`) exists in the ecosystem alongside
  `Compono.TestDoubles`/`Compono.NSubstitute`/`Compono.XunitV3`/`Compono.TUnit`;
  small ongoing maintenance/versioning surface, consistent with the existing
  integration-package pattern.

## Pros and Cons of the Options

### 1. `Compono.BUnit`

A dedicated package wrapping bUnit's `RenderComponent`, parameters, and services.

- Bad, because no bUnit-specific integration surface survived investigation — the
  only real seam (the fallback `IServiceProvider`) is fully generic; a bUnit-labeled
  package would just re-export the generic bridge under a narrower name, which is
  exactly the "recreating APIs under Compono names" anti-pattern this repo's
  integration packages avoid.
- Bad, because it would couple a genuinely general capability (a Compono-backed
  `IServiceProvider`) to one consumer ecosystem, reducing its reuse (ASP.NET Core,
  generic host, other test frameworks all want the same primitive).
- Good, because it would directly and unambiguously answer the original product
  question — but the evidence doesn't support building it just for that reason.

### 2. No new package; document the manual push pattern only

`row` obtained via existing public API, then `Ctx.Services.AddSingleton(...)` by
hand, no new package.

- Good, because it requires zero new surface area and is already fully correct
  (once the descriptor-based `Resolve`/`ResolveShared` usage is documented
  accurately).
- Bad, because it only solves the "push, known dependencies" case. It does nothing
  for the "lazy, don't-know-everything-the-component-needs" case that bUnit's
  fallback provider exists specifically to serve — leaving real, evidenced value on
  the table.

### 3. A public, general `CompositionRow.Resolve(Type)` mirroring `Resolve<T>()`'s full contract

A single non-generic method intended to be a drop-in equivalent of `Resolve<T>()`.

- Bad, because it cannot actually reach stages 7-8 without reflection, so a method
  named/shaped to imply full equivalence would misrepresent its own capability —
  exactly what the "API honesty" Decision Driver above rules out.
- Bad, because a throwing contract is the wrong shape for backing
  `IServiceProvider.GetService(Type)`, which must return `null` on a genuine miss.
- Good, because it would be simpler to explain if it worked — but it doesn't
  honestly work, so simplicity here is false economy.

### 4. `Compono.DependencyInjection` (chosen)

- Good, because it solves both the push and lazy-fallback cases with one small,
  honestly-scoped primitive.
- Good, because it stays fully general/provider-neutral/framework-agnostic — no
  wasted, single-consumer-shaped surface.
- Good, because it introduces zero reflection and zero new sharing semantics into
  core composition behavior.
- Bad, because it leaves the "arbitrary generated-type-by-runtime-Type" gap
  explicitly unsolved — acceptable per the Negative Consequences discussion, since no
  evidence currently demands closing it.

## Links

- ADR-0001 (Source Generation First) — no-reflection-by-default constraint this
  design stays within.
- ADR-0019 (Registrations and Service Provider Injection) — established
  `UseServiceProvider`'s pull-only direction and explicitly named this ADR's package
  as future scope.
- ADR-0021 (Row Composition Entry Point for Test-Framework Integrations) —
  `CompositionRow`'s framework-agnostic foundation, reused here.
- ADR-0012 (Composition Path Identity and Deterministic Random Forking),
  Amendment 3 — records `PathSegment.ConfiguredResolution`'s new tag/ordinal
  decision, per that ADR's own reproducibility-contract requirement.
- ADR-0041 (AOT-Safe Row-Binding Dispatch) — `RowInvokerRegistry`'s
  `Type`-keyed-registry pattern, precedent for a narrow integration-facing primitive;
  its generator-populated mechanism does not fit this use case (arbitrary runtime
  `[Inject]` types are not a compile-time-enumerable set), so it was not reused
  directly.
- bUnit v2 documentation (`bunit.dev`) — `BunitContext.Services`,
  `AddFallbackServiceProvider`, `BunitServiceProvider` fallback-only, non-caching
  `GetService` semantics.
- Dogfooding source: `trivia-manager` (`test/Trivia.Manager.Web.Tests`), specifically
  `MudBunitTestBase.FreezeAndRegister<TSub>()` and its call sites — the original
  evidenced friction this ADR traces back to.
- [RESEARCH-0007](../research/0007-trivia-manager-bunit-dependency-injection.md) —
  the full investigation record: bUnit architecture research, `trivia-manager`
  dogfooding, Compono-internals verification, and the adversarial re-verification
  rounds that corrected this ADR's caching, recursion, and API-honesty claims before
  acceptance.

## Amendment 1 (2026-08-21): No `Microsoft.Extensions.DependencyInjection.Abstractions` dependency

Implementation (PLAN-0047) found this ADR's stated package dependency
("depending on `Compono` and `Microsoft.Extensions.DependencyInjection.Abstractions`
only") was wrong: `Compono.DependencyInjection`'s only public surface,
`row.AsServiceProvider()`, returns a plain `System.IServiceProvider` — BCL,
not a type from the Abstractions package. Nothing in the package's actual
code references `Microsoft.Extensions.DependencyInjection` in any form. The
`PackageReference` was removed entirely (caught during PR review, per-TFM
dependency-range work for that reference surfaced the fact that it wasn't
needed at all, not just misconfigured) — every packed TFM's `.nuspec`
dependency group now lists only the exact-pinned `Compono` dependency,
matching `Compono.TestDoubles`'s own zero-third-party-dependency shape. A
consumer wanting `GetRequiredService<T>()`-style ergonomics against the
returned `IServiceProvider` already has that extension available from
their own app/test host's own reference to the Abstractions package
(ASP.NET Core, a generic host, bUnit, etc. all already carry it) — this
was never something `Compono.DependencyInjection` itself needed to
provide. No other part of this ADR's Decision Outcome changes.

## Amendment 2 (2026-08-21): Provider-thrown exceptions propagate raw, not wrapped

The Core primitive section's original text (`TryResolveConfigured`'s
sketched XML doc, above) says a reachable-but-failing stage "still throws
a diagnosed `CompositionException`." That is only true for an exact
registration factory (stage 3a), wrapped via `InvokeFactory`. A stage 4-6
`ICompositionValueProvider`'s own thrown exception propagates uncaught,
unwrapped, in its own original exception type — per
[ADR-0024](0024-public-provider-extensibility-model.md)'s existing
Provider Failure Semantics, which `TryResolveConfigured` never overrides
or downgrades for this entry point. This was caught during PR review
(#105) as an inconsistency between the ADR's own text and the shipped
code's actual XML docs (`CompositionRow.TryResolveConfigured`'s doc
comment was corrected earlier in that same review, but this ADR's own
Core Primitive section was not updated to match) and the implementation
itself, which has always behaved this way —
`TryResolveConfigured_Throws_WhenAReachableProviderThrows` asserts the
raw provider exception type, not `CompositionException`, and was written
before this amendment, not changed by it. No behavior changed; only this
ADR's own text is corrected to match what has always shipped.

## Amendment 3 (2026-08-21): `TryResolveConfigured` always validates as nullable

The "Behavior, precisely" list above still says the method "rejects
`null` (as a thrown failure, not a quiet miss) for a non-nullable
request" — implying `TryResolveConfigured` distinguishes nullable from
non-nullable requests the way `Resolve<TValue>()` does. It never has:
since a bare runtime `Type` carries no compile-time nullable-reference
annotation to read (unlike `Resolve<TValue>()`'s compile-time-known
`TValue`), the internal `CompositionContext` implementation always
constructs its request with `Nullability.Nullable` - every reachable
stage's `null` result is accepted, none are ever rejected as
"non-nullable," because there is no per-call way to know a bare `Type`
was "meant" non-nullable. PLAN-0047's own Tasks section already recorded
this correctly (the originally-scoped "non-nullable type still throws"
test was dropped as inapplicable), but this ADR's own prose was never
corrected to match — caught in PR review (#105). No behavior changed;
only this ADR's own text is corrected to match what has always shipped
and what PLAN-0047 already documented.

## Amendment 4 (2026-08-21): Row-wide adapter identity, and the recursion claim corrected

Two corrections, both caught in PR review (#105) after the design section
above and the "Recursion" section above had already been written and
shipped:

**Row-wide adapter identity.** The design sketch above (and the "Identity/
caching rules" list) describes `AsServiceProvider()` constructing a fresh
`ComponoServiceProvider` — with its own cache and, once locking was added,
its own lock — on every call. That was true when this ADR was accepted,
and shipped that way initially. PR review then found that two adapters
obtained for the *same* row, used concurrently, could each serialize their
own calls but not against each other's, racing inside the row's shared
`CompositionContext`. The fix, already recorded in PLAN-0047's Notes, is a
`ConditionalWeakTable<CompositionRow, IServiceProvider>` keyed on the row:
`AsServiceProvider()` now returns the same adapter instance (same cache,
same lock) for every call made on the same row, for as long as that row is
reachable. This changes the identity/lifetime contract this ADR describes
from "one stable value per type, for the lifetime of this adapter
instance" to "one stable value per type, for the lifetime of the row" —
calling `AsServiceProvider()` twice on the same row no longer produces two
independent caches. The row-keying itself still lives entirely in this
integration package (a static field on
`CompositionRowServiceProviderExtensions`); `CompositionRow`/
`CompositionContext` remain unaware of it, so the "owned entirely by this
adapter, not by `CompositionRow`" framing above still holds — only the
per-call-vs-per-row granularity changes. No other behavior in the
"Identity/caching rules" list is affected (miss-not-cached, `(true, null)`
caching, no disposal ownership, no DI-container lifetime modeling all
stand as originally written).

**The Recursion section's `StackOverflowException` claim was wrong.** That
section, and the "Negative Consequences" entry restating it, asserted that
"each hop is a fresh `CompositionContext` with empty guard state" for a
cross-row cycle, so nothing would detect it and the result would be an
undiagnosed `StackOverflowException`. This does not match
`CompositionRow`: a row's underlying `CompositionContext` is created once,
at `Composer.CreateRow`, and reused for every call made against that row
for its entire lifetime — including calls arriving indirectly through
another row's `UseServiceProvider`/adapter. It is not recreated per call,
and its reentrance guards (`_activeFactories`, `_activeProviderRequests`)
are therefore not "empty" on a call that arrives via a different row; they
carry whatever is already in flight on *this* row's own context.

Concretely, for the same example this ADR used (Row A registers `IFoo`
with a factory that calls into Row B's adapter, Row B is configured with
`UseServiceProvider(rowA.AsServiceProvider())`): once the cycle loops back
around to Row A trying to invoke its own `IFoo` factory a second time
while the first invocation is still on the stack, Row A's own
`IsFactoryActive` reentrance guard — the same one that already stops a
factory from directly recursing into itself — trips, and Compono raises
its existing diagnosed `Recursive registration or configuration-rule
factory detected` `CompositionException`. This was verified directly: a
test reproducing this exact two-row wiring
(`CrossRowCycle_IsDetectedAsARecursiveFactory_NotAStackOverflow` in
`CompositionRowTryResolveConfiguredTests`) throws that `CompositionException`,
not a `StackOverflowException`, every run. Since every reachable stage
`TryResolveConfigured` uses is backed by a finite set of registrations/
providers on each row, any true infinite cross-row cycle must eventually
revisit some (context, factory-or-provider) pair while it is still active
on that context — the existing guard is not merely lucky in this one
example, it structurally cannot be evaded by a longer chain of rows either.

The "Recursion" section and the matching "Negative Consequences" bullet
are superseded by this amendment: cross-row cycles that loop back through
a row's own registration factory or provider are diagnosed, not a stack
overflow. `AsServiceProvider()`'s XML doc `<remarks>` has been corrected
to match. This ADR does not claim the cross-row case is *recommended* —
deliberately wiring two rows together this way is still unusual,
hand-written, and unnecessary for the evidenced use case — only that the
specific undiagnosed-crash failure mode this ADR predicted does not, in
fact, occur.
