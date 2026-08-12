# [ADR-0041] AOT-Safe Row-Binding Dispatch

**Status:** Accepted

**Date:** 2026-08-12

**Decision Makers:** ncipollina, solo (design dive via `/engineering-workflow`)

## Context

`Compono.XunitV3.Binding.RowInvokers` and its PLAN-0040-drafted counterpart
`Compono.TUnit.Binding.RowInvokers` both close `CompositionRow`'s generic
`Resolve<T>`/`ResolveShared<T>`/`ShareExplicit<T>` methods over a test
parameter's `System.Type` — known only at runtime, once `BindingPlan.Build`
reflects the test method's signature — via
`MethodInfo.MakeGenericMethod(parameterType)` followed by
`Delegate.CreateDelegate`. The resulting delegate is cached once per
attribute instance (effectively once per test method, not once per row),
so this has never been a runtime-performance problem.

It is a Native AOT/trimming problem. `MakeGenericMethod` called with a
`Type` computed at runtime has no statically-discoverable closed generic
instantiation for a Native AOT compiler to pre-compile — there is no JIT
under Native AOT to fall back on, so an instantiation the AOT compiler
never saw ahead of time throws at runtime (`MissingMetadataException`/
`NotSupportedException`, RyuJIT). `docs/adr/0001-source-generation-first.md`
already made trimming/Native AOT compatibility an explicit Decision Driver
and rejected reflection-based construction as the default architecture;
this pattern is a gap against that ADR's own stated intent that had gone
unnoticed because `Compono.XunitV3`'s consumers don't typically publish
their test hosts as Native AOT.

`Compono.TUnit` changes that. TUnit's own README leads with "work under
Native AOT" as its first sentence and publishes a dedicated "TUnit (AOT)"
benchmark scenario — Native AOT is a first-class, prominently marketed
TUnit capability, not a footnote, and a `Compono.TUnit` user is
meaningfully more likely to actually attempt `PublishAot=true` than a
`Compono.XunitV3` user. `docs/mvp.md` lists "Native AOT certification" as
an explicit MVP non-goal for Compono as a whole (no formal certification
process is being pursued), but that is a statement about certification
overhead, not about knowingly shipping a package that breaks under Native
AOT for the one integration whose own upstream framework stakes a headline
claim on it.

Compounding this: `publish-preview.yaml` triggers on every push to `main`
(`on: push: branches: [main]`, excluding only `docs/**`/`README.md`) and
packs+publishes every packable `src/` project to nuget.org automatically.
`Compono.TUnit.csproj` has no `IsPackable` override, so it inherits the
SDK's own default (`true`) — merging PLAN-0040's Phase 0 as originally
drafted would auto-publish a real, installable `Compono.TUnit` preview
package containing this gap on the very next push to `main`. In this repo,
merging to `main` and releasing to nuget.org are the same event, not
separable gates — so this decision has to land *before* PLAN-0040's Phase 0
PR merges, not as a follow-up once it has.

## Decision Drivers

- Native AOT compatibility is a release requirement for `Compono.TUnit`
  v1, not a future optimization — a TUnit consumer who is currently Native
  AOT/trimming compatible must not silently lose that property by adding
  `Compono.TUnit`.
- Keep performance and Native AOT compatibility as separate concerns — the
  existing `MakeGenericMethod` path is not a performance problem (bounded,
  cached once per test method), and fixing the AOT gap must not be
  justified or dismissed on performance grounds in either direction.
- Prefer the smallest maintainable AOT-safe design — do not over-engineer
  the generator, and do not redesign binding validation
  (`BindingPlan.ValidateSignature`) or discovery
  (`ComposeMethodDiscovery`) merely because this decision is in the
  neighborhood.
- Don't maintain two long-term implementations of the same conceptual
  problem ("runtime parameter type → strongly-typed
  `CompositionRow.Resolve<T>()` dispatch") across `Compono.XunitV3` and
  `Compono.TUnit` unless a real framework-specific reason requires it.
- The core `Compono` package must never know about an integration package
  (`references/design-decisions.md` rule 3) — any shared fix must live at
  a boundary core can own without referencing either framework.

## Considered Options

1. Accept the gap — ship `Compono.TUnit` on the same `MakeGenericMethod`
   path `Compono.XunitV3` already uses, document Native AOT as
   unsupported.
2. Generator-emitted `RowInvokerCache<T>` in core `Compono`, populated by
   `Compono.Generators` alongside its existing `PlanCache<T>` module
   initializer emission — replaces `MakeGenericMethod`/
   `Delegate.CreateDelegate` with ordinary compiled generic-type
   instantiations the AOT compiler can see statically.
3. Temporarily set `Compono.TUnit.csproj`'s `IsPackable` to `false`, merge
   PLAN-0040's Phase 0 without publishing it, do the AOT-safe redesign as
   a follow-up PR, then flip `IsPackable` back once it lands.

## Decision Outcome

Chosen option: **2, "Generator-emitted `RowInvokerCache<T>` in core"** —
applied to both `Compono.TUnit` and, retroactively, `Compono.XunitV3`
(Option 1 is rejected outright per the Decision Drivers above; Option 3 is
rejected as unnecessary bookkeeping once Option 2 is being built anyway —
see its own Pros/Cons below).

`CompositionRow.Resolve<T>()`/`ResolveShared<T>()`/`ShareExplicit<T>()`
are core Compono APIs; the "runtime parameter type → strongly-typed
dispatch" problem `RowInvokers` exists to solve is not an xUnit or TUnit
question at all — what *is* framework-specific (turning a
`MethodInfo`/`DataGeneratorMetadata` into a validated, ordered parameter
list) stays exactly where PLAN-0040 already put it, duplicated per
`BindingPlan`, not touched by this decision.

Core `Compono` already has the exact pattern this needs:
`PlanCache<T>.Instance` — "a closed generic static field is one field per
closed generic type in the CLR... not a `typeof(T)`-keyed dictionary
lookup, and not runtime reflection" (`src/Compono/PlanCache.cs`), populated
once by a generated module initializer in the consuming assembly. Add a
sibling `RowInvokerCache<T>` (three delegate fields: `Resolve`,
`ResolveShared`, `ShareExplicit`) to core `Compono`. Extend
`Compono.Generators`' existing per-discovered-parameter-type emission
(`ComposeMethodDiscovery`'s `TransformMethod` already walks every
parameter of every `[Compose]`-attributed method, for both attribute
families, to build `PlanCache<T>` registrations) to also emit, for every
distinct parameter type it sees — not only ones that need a generated
plan, since a built-in-composable type like `string`/`int` still needs a
`RowInvokerCache<T>` entry to dispatch through:

```csharp
RowInvokerCache<OrderService>.Resolve = static (row, descriptor) => row.Resolve<OrderService>(descriptor);
RowInvokerCache<OrderService>.ResolveShared = static (row, descriptor) => row.ResolveShared<OrderService>(descriptor);
RowInvokerCache<OrderService>.ShareExplicit = static (row, descriptor, value) => row.ShareExplicit(descriptor, (OrderService)value!);
```

Every closed generic instantiation here is a literal, compiled reference
in generator-emitted source — visible to Native AOT's static analysis the
same way `RowInvokerCache<OrderService>` would be if a consumer wrote it
by hand, categorically different from a `Type` object computed at runtime
and handed to `MakeGenericMethod`. Every test method signature `[Compose]`
attributes is fixed C# source the generator already parses, so the full
set of `T`s this needs is closed and enumerable at compile time by
construction — there is no scenario where a genuinely runtime-only-known
type flows through this path.

Both packages' own `BindingPlan`/`RowInvokers` shrink to a plain lookup
against `RowInvokerCache<T>`'s static fields instead of building delegates
via reflection themselves — `Compono.XunitV3`'s copy is migrated to the
same mechanism in the same architectural change, so this repo maintains
one implementation of the binding-dispatch problem, not two, and the older
package's own latent AOT gap closes for free.

**Explicitly out of scope for this ADR** (per the "smallest maintainable
design" driver): `BindingPlan.cs`'s `ReflectionInfo.GetCustomAttributes(
typeof(SharedAttribute), false)` `[Shared]`-detection check is a plain
attribute-presence metadata read, not dynamic code generation — a
different, lower-risk category of reflection than `MakeGenericMethod`.
This ADR doesn't redesign it, but PLAN-0040's real Native AOT
publish-and-run verification (see Consequences) is the thing that confirms
or refutes that assumption, rather than asserting it here.
`Compono.XunitV3.Binding.ConfigProfileBinder`'s `ConstructorInfo.Invoke`
(used for `[Compose<TProfile, TConfig>]`'s `TConfig` construction) is a
related, separate reflection surface that doesn't exist in `Compono.TUnit`
yet (Phase 1 hasn't been built) — left for that phase's own design pass,
not bundled in here.

### Positive Consequences

- `Compono.TUnit`'s first published version ships Native AOT-safe by
  construction, never shipping a throwaway reflection-based version first.
- `Compono.XunitV3`'s own latent AOT gap closes as a side effect, at no
  extra design cost.
- One implementation of "parameter type → dispatch delegates" instead of
  two, in core, where it belongs per the core-knows-nothing-about-
  integrations rule.
- Reuses an already-proven pattern (`PlanCache<T>`) rather than inventing
  a new one — small, bounded generator change, not a rewrite.
- No behavior change and no runtime-performance change — the fix is
  purely about *how* the same dispatch gets built, not what it does.

### Negative Consequences

- `Compono.Generators` grows a second per-discovered-type emission
  responsibility alongside `PlanCache<T>`'s — mitigated by piggybacking on
  the exact same discovery pass and emission point rather than adding a
  new one.
- `RowInvokerCache<T>` must be populated for every parameter type
  `[Compose]` reaches, including built-in-composable types that never get
  a `PlanCache<T>` entry — a slightly wider emission surface than
  `PlanCache<T>`'s own, but the same discovery loop already visits every
  one of those types today (`ComposeMethodDiscovery.TransformMethod`
  iterates `method.Parameters` unconditionally).
- This ADR's claim of AOT-safety is a design argument, not yet a proven
  one — PLAN-0040 must carry a real `dotnet publish -p:PublishAot=true` +
  run smoke test (not an assumption) before `Compono.TUnit` is considered
  release-ready, per this session's own established "prove it, don't
  assume it" pattern for TUnit-specific claims.

## Pros and Cons of the Options

### Accept the gap

Ship `Compono.TUnit` on the same `MakeGenericMethod` path
`Compono.XunitV3` already uses; document Native AOT as unsupported.

- Good, because it's zero additional work and matches existing,
  already-shipped precedent.
- Bad, because it contradicts TUnit's own headline positioning for the one
  package whose entire audience is most likely to test it.
- Bad, because a documented limitation doesn't stop a real consumer from
  hitting a runtime crash the moment they try `PublishAot=true` — the
  Decision Drivers explicitly reject this as acceptable for v1.

### Generator-emitted `RowInvokerCache<T>` in core (chosen)

See Decision Outcome above.

- Good, because it removes all `MakeGenericMethod`/`Delegate.CreateDelegate`
  reflection from both packages' binding-dispatch paths.
- Good, because it's framework-agnostic by construction — core only needs
  to know about `CompositionRow`/`CompositionRequestDescriptor`, not about
  either integration package.
- Good, because it reuses a pattern (`PlanCache<T>`) already proven in
  this codebase, rather than inventing a new dispatch mechanism.
- Bad, because it's more work than doing nothing — but this is the
  smallest design that actually satisfies the stated release requirement,
  not an over-engineered alternative to it.

### Temporarily unpublish, fix later

Set `Compono.TUnit.csproj`'s `IsPackable` to `false`, merge PLAN-0040's
Phase 0 without publishing it, do the AOT-safe redesign as a follow-up PR,
flip `IsPackable` back once it lands.

- Good, because it unblocks merging PLAN-0040's Phase 0 sooner.
- Bad, because it ships throwaway reflection-based code that gets replaced
  almost immediately, for no benefit once Option 2 has to be built anyway
  before any real release.
- Bad, because it adds a stateful flag to remember to flip back — exactly
  the kind of silent-drift risk this session's own PR #73 review process
  (docs left inconsistent with code) already demonstrated is real in this
  repo, not hypothetical.

## Links

- [ADR-0001: Source Generation First](0001-source-generation-first.md) —
  the trimming/Native AOT decision driver this ADR closes a gap against.
- [ADR-0040: Compono.TUnit Package Design](0040-compono-tunit-package-design.md) —
  the package this ADR's dispatch mechanism ships underneath; ADR-0040's
  Amendment section cross-references this ADR.
- [PLAN-0040](../plans/0040-compono-tunit-package-design.md) — Phase 0's
  binding-dispatch tasks are revised to build against `RowInvokerCache<T>`
  from the start, per this ADR.
- PR #73 (`feat/plan-0040-phase-0-compono-tunit-skeleton`) — the
  in-review Phase 0 implementation this ADR's design dive was triggered
  by, held pending this decision rather than merged with the
  `MakeGenericMethod` path.
