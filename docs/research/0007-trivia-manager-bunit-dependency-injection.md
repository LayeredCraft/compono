# RESEARCH-0007: `trivia-manager` bUnit Investigation — Compono.BUnit Gate

## Purpose

A seventh dogfooding-adjacent pass, run per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
capability-gap decision framework, but starting from a different trigger than
passes 1-6: not a migration of an existing AutoFixture-based suite, but a
**gating investigation** for a stated product requirement — "should
`Compono.BUnit` exist?" — using a real consumer repository
(`ncipollina/trivia-manager`, a Blazor app tested with bUnit v2 + xUnit v3 +
AutoFixture + AutoNSubstitute) as the evidence source. The gate, as posed: is
there a coherent, useful integration boundary between Compono and bUnit that
justifies a dedicated package, or does bUnit dogfooding actually point at a
different (or no) capability?

## Method

Four rounds of investigation, each closing gaps the previous round surfaced
rather than assuming the first design was right:

1. **bUnit architecture research** (`bunit.dev`, bUnit v2 docs/source) —
   `BunitContext`/`TestContext`, `Services` (`IServiceCollection`),
   `AddFallbackServiceProvider`/`BunitServiceProvider`, component parameter
   builders, `JSInterop`, component factories.
2. **`trivia-manager` dogfooding** — every bUnit test file inspected for
   recurring composition/DI patterns. Found one dominant, repeated pattern:
   `MudBunitTestBase.FreezeAndRegister<TSub>()` (compose an AutoFixture+
   AutoNSubstitute double, then `Ctx.Services.AddSingleton(...)`), called
   2-3× in every component test class constructor.
3. **Compono internals research** — `UseServiceProvider`'s actual pipeline
   stage/precedence, `CompositionRow`'s real ownership (core `Compono`,
   shared by both `Compono.XunitV3` and `Compono.TUnit`, confirmed
   framework-agnostic), the public `Resolve<TValue>()` surface's actual
   callability constraints, `CompositionScope`'s actual (lack of) automatic
   memoization, `Type`-keyed vs. closed-generic-static-field dispatch at
   each pipeline stage, existing reentrance guards, and the repo's actual
   `InternalsVisibleTo` precedent (test projects only, never sibling
   packages).
4. **Adversarial re-verification** — three additional rounds specifically
   attacking the emerging design (identity/caching claims, failure
   semantics vs. `IServiceProvider`'s BCL contract, cross-context recursion,
   null-handled-vs-miss ambiguity) before anything was accepted.

## Findings, classified

Per ADR-0029's five-way rubric (bug / roadmap candidate / acceptable
alternative / intentional design difference / project-local fix):

- **Not a bug.** No defect found in Compono's existing behavior.
- **Not `Compono.BUnit` as a roadmap candidate.** The only real bUnit-side
  extension point (`AddFallbackServiceProvider`) is fully generic — it
  accepts a plain `IServiceProvider`, and nothing on either side needs to
  know the other framework exists. A bUnit-labeled package would have had no
  bUnit-specific content to own; it would have re-exported a general
  capability under a narrower name.
- **Roadmap candidate, redirected: a general `IServiceProvider` bridge.**
  ADR-0019 had already named this exact shape as future scope
  (`Compono.Extensions.DependencyInjection`, "not designed here") and
  deferred it pending real evidence. `trivia-manager`'s `FreezeAndRegister`
  pattern is that evidence — real, repeated friction — but the friction
  itself (compose a value, get it into a DI container) is generic
  `IServiceCollection`/`IServiceProvider` behavior, not bUnit-specific.
- **One genuinely new, small core capability required**, discovered only
  by tracing actual code rather than trusting the obvious design: the
  descriptor-less `CompositionRow.Resolve<TValue>()` cannot be called from
  hand-written consumer code at all (`_manualResolveFrames` guard), and
  even the descriptor-based path performs independent, unshared resolutions
  by default. Neither shape backs `IServiceProvider.GetService(Type)`'s
  null-on-miss contract. This is `CompositionRow.TryResolveConfigured(Type,
  out object?)`, reaching stage 2 (scope), stage 3a (exact registrations),
  and stages 4-6 (configuration rules, `Compono.TestDoubles`/
  `Compono.NSubstitute` providers) — deliberately excluding stage 3b
  (`UseServiceProvider`, to avoid flattening an external provider's own
  transient/scoped lifetime into an adapter cache) and stages 7-8 (ordinary
  generated-plan composition, which dispatches through closed-generic
  static fields unreachable from a runtime `Type` without reflection).

## Decision

Recorded in [ADR-0047](../adr/0047-compono-dependencyinjection-configured-resolution-bridge.md)
(`Accepted`): `CompositionRow.TryResolveConfigured(Type, out object?)` in
core `Compono`, plus a new package `Compono.DependencyInjection`
(`row.AsServiceProvider()`, internal adapter, adapter-owned per-`Type`
identity caching, no reflection, no new sharing semantics in
`CompositionScope`, no disposal ownership). No `Compono.BUnit`.

## What this pass illustrates about the process

Unlike passes 1-6 (migrate an existing suite, find what breaks), this pass
started from a *hypothesized package name* and a stated product requirement
that the package should exist if a coherent boundary could be found. The
process held the line anyway: four successive rounds of "does the evidence
actually support this specific design" redirected the outcome away from the
originally hypothesized package (`Compono.BUnit`) toward a more general,
more reusable capability (`Compono.DependencyInjection`) — and, within that
redirected design, repeatedly corrected an initially-plausible but wrong
implementation detail (naive `Resolve<T>()` wrapping, "stages 2-6" framing,
an overstated recursion-impossibility claim, an ownership-based rather than
semantics-based caching rationale) before any of it was accepted. Dogfooding
here refined the product requirement rather than mechanically confirming it.

## Deferred: closing the loop in `trivia-manager`

Passes 3-6 (`lightsaber-skill`) established that "shipped" is not the same
as "the consumer repo actually migrated" — a capability can close the gap on
paper and still need a confirming dogfood pass against the real repo that
motivated it. `trivia-manager` lives outside this repository
(`ncipollina/trivia-manager`), so migrating its `FreezeAndRegister`-pattern
tests to `Compono.DependencyInjection` once shipped, and recording whether
it actually removes the friction observed here, is explicitly **not** part
of [PLAN-0047](../plans/0047-compono-dependencyinjection-configured-resolution-bridge.md)
— it is a follow-up dogfood pass to run after the package ships, the same
shape as this repo's other closing-dogfood passes, not before.

## Decisions

- [ADR-0047](../adr/0047-compono-dependencyinjection-configured-resolution-bridge.md) —
  `Compono.DependencyInjection`, `CompositionRow.TryResolveConfigured`,
  no `Compono.BUnit`.
