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

## Amendment 1 (2026-08-12): Native AOT requirement extended to the full attribute family

This ADR's original Decision Outcome deferred
`Compono.XunitV3.Binding.ConfigProfileBinder`'s `ConstructorInfo.Invoke`
(used for `[Compose<TProfile, TConfig>]`'s `TConfig` construction) to
"that phase's own design pass," on the reasoning that it doesn't exist in
`Compono.TUnit` yet.

That framing understated the requirement. `Compono.TUnit`'s Native AOT
compatibility is a v1 release requirement for the package as a whole, not
per-attribute — a consumer who is Native AOT/trimming compatible today
must not lose that property by adding `Compono.TUnit`, regardless of
which supported `[Compose]`-family attribute they use. Shipping
`[Compose<TProfile, TConfig>]` in PLAN-0040 Phase 1 with an unexamined,
possibly-AOT-unsafe construction path would mean the package's Native AOT
claim only holds for the subset of consumers who happen not to reach for
that attribute — the same category of gap this ADR exists to close for
row-binding dispatch.

Deferring the *implementation* to Phase 1 (this ADR's Scope stays row-
binding dispatch only, per its own "smallest maintainable design" driver
— `RowInvokerCache<T>` does not need to solve `ConfigProfileBinder`'s
problem too) was correct. Deferring the *analysis*, without making it an
explicit, non-optional gate on Phase 1 shipping, was not. PLAN-0040's
Phase 1 task list now carries this explicitly: perform the same AOT
analysis on `ConfigProfileBinder`/`ConstructorInfo.Invoke` this ADR
performed for `MakeGenericMethod`-based dispatch; if it is not AOT-safe,
design and implement the smallest AOT-safe replacement before
`[Compose<TProfile, TConfig>]` ships, not after; and extend PLAN-0041's
real `dotnet publish -p:PublishAot=true` + run smoke test to exercise
`[Compose<TProfile, TConfig>]` specifically, so the package's final Native
AOT claim is verified against every public Compose-family attribute it
ships, not just the one this ADR's own scope covers.

## Amendment 2 (2026-08-12): `RowInvokerCache<T>` corrected to a non-generic `RowInvokerRegistry`

PR #74 review caught two real flaws in this ADR's original mechanism, both
confirmed against the actual generator source before writing this
amendment (not accepted on the review's word alone):

**Flaw 1 — `RowInvokerCache<T>` cannot actually be read from the code that
needs it.** `PlanCache<T>.Instance` works as a closed-generic static field
because its only reader, `CompositionContext.ResolveCore<TValue>`, is
itself generic and always called with `TValue` bound at a real compile-time
call site (`composer.Create<OrderService>()`, written by a consumer).
`BindingPlan.Build` has no such call site — it only ever has
`parameter.ParameterType`, a `System.Type` obtained via reflection over
TUnit's/xUnit's own runtime metadata, with no compile-time `T` to name.
Reading `RowInvokerCache<T>` for a `T` known only as a runtime `Type`
requires exactly the same `MakeGenericMethod`-shaped reflection this ADR
exists to remove — the original design didn't eliminate the AOT problem,
it relocated it from `CompositionRow.Resolve<T>` to `RowInvokerCache<T>`
without solving it.

**Flaw 2 — the discovery data this needs doesn't exist where the original
design assumed it did.** `ComposeMethodDiscovery.TransformMethod` walks
every method parameter via `ComposedTypeAnalyzer.Analyze`, which delegates
to `TransitiveClosureWalker.Walk` — but `TransitiveClosureWalker` (both at
the root, `EnqueueRoot`, and for nested members, `EnqueueMember`)
explicitly returns without recording anything when
`LeafTypeClassifier.IsRuntimeProviderResolved`/`IsProviderResolved` is
true (`TransitiveClosureWalker.cs:135-136`, `:224-225`) — the exact set of
types (`string`, `int`, interfaces, delegates, anything satisfied by a
built-in/registration/semantic provider rather than a generated plan) that
never need a `PlanCache<T>` entry. `TransitiveClosureResult.Types` was
never meant to be a complete parameter-type inventory; it's a
plan-generation worklist that deliberately excludes exactly the types a
row-binding dispatch mechanism needs most (provider-resolved leaf types
are, in practice, extremely common `[Compose]` parameter types). Extending
the existing per-discovered-type emission, as originally described, would
silently leave every such parameter's dispatch unregistered.

### Corrected mechanism

Replace `RowInvokerCache<T>` with a **non-generic, `Type`-keyed
registry** in core `Compono`:

```csharp
public static class RowInvokerRegistry
{
    // Same non-generic delegate shapes Compono.XunitV3.Binding.RowInvokers/
    // Compono.TUnit.Binding.RowInvokers already define locally today - moved to core so both
    // packages (and the generator) share one definition instead of two.
    public static void Register(Type type, ResolveInvoker resolve, ResolveSharedInvoker resolveShared, ShareExplicitInvoker shareExplicit) { /* ... */ }
    public static bool TryGet(Type type, out ResolveInvoker resolve, out ResolveSharedInvoker resolveShared, out ShareExplicitInvoker shareExplicit) { /* ... */ }
}
```

populated by generated code exactly like this ADR's original sketch, just
targeting the registry instead of a closed generic field:

```csharp
RowInvokerRegistry.Register(typeof(OrderService),
    static (row, descriptor) => row.Resolve<OrderService>(descriptor),
    static (row, descriptor) => row.ResolveShared<OrderService>(descriptor),
    static (row, descriptor, value) => row.ShareExplicit(descriptor, (OrderService)value!));
```

This resolves both flaws without abandoning the "smallest maintainable
design" driver:

- **Flaw 1**: `BindingPlan.Build` calls `RowInvokerRegistry.TryGet(parameterType, ...)`
  — an ordinary `Dictionary<Type, ...>`-shaped lookup by a runtime `Type`
  value, not a generic-type-parameter problem at all. `Type` objects are
  ordinary runtime values under Native AOT; only *dynamic instantiation*
  of a generic method/type from one (`MakeGenericMethod`) is unsafe, and
  nothing here does that. Every `Resolve<T>()`/`ResolveShared<T>()`/
  `ShareExplicit<T>()` call the registry's entries actually make is still
  written directly, with a compile-time-known `T`, in generator-emitted
  source — the AOT-safety property this ADR's Decision Outcome already
  argued for is unchanged, it just required a non-generic storage shape to
  actually be reachable from `BindingPlan`.
- **Flaw 2**: `ComposeMethodDiscovery.TransformMethod` already iterates
  every method parameter directly (`foreach (var parameter in
  method.Parameters)`) before handing each one to
  `ComposedTypeAnalyzer.Analyze` for plan-eligibility walking. It now also
  records each parameter's own type directly, independent of what the
  eligibility walk decides — a small, additive change to a loop that
  already visits every parameter, not a new discovery pass. This is
  threaded through as its own field alongside the existing
  `TransitiveClosureResult`, not folded into `.Types`/`.Collections`,
  since those two remain exactly what they always were (a plan-generation
  worklist) — conflating them with a complete parameter-type inventory
  would be the same category of bug this amendment is fixing.

The registry needs one entry per distinct parameter type reachable through
either attribute family — whether or not that type also gets a
`PlanCache<T>`/`CollectionPlanCache<T>` entry — since dispatch and plan
generation are now explicitly two different, independently-populated
concerns, correctly reflecting that they always were two different
questions ("how do I call `Resolve<T>()`" vs. "how do I construct a `T`").

PLAN-0041 is revised to build `RowInvokerRegistry`, not `RowInvokerCache<T>`,
from its first task.

## Amendment 3 (2026-08-12): Idempotent registration required; Amendment 1's smoke-test instruction corrected

PR #74 review caught two more real gaps, both about this ADR's own text
lagging decisions already made elsewhere:

**Idempotent registration is a decision, not an implementation detail —
record it here, not only in PLAN-0041.** Two consumer assemblies loaded
into the same process that both discover, say, `string` as a `[Compose]`
parameter type will each run their own generated module initializer
against the same `RowInvokerRegistry`. Unlike `PlanCache<T>`'s own
already-documented, already-deferred cross-assembly collision (a plain
static field write — atomic and safe under concurrent module-initializer
execution, merely nondeterministic about *which* assembly's value ends up
winning), `RowInvokerRegistry`'s underlying `Dictionary<Type, ...>`
storage can have its *internal structure* corrupted by genuinely
concurrent writes from two module initializers running on different
threads — a strictly worse failure mode than "last write wins," not a
variant of the same one. This ADR's Amendment 2 sketch (`Register`/
`TryGet` over an unqualified `Dictionary<Type, ...>`) left this
unconstrained. It is now a firm requirement: `RowInvokerRegistry` uses a
`ConcurrentDictionary<Type, ...>` with an atomic `GetOrAdd` (or equivalent
`TryAdd`-shaped idempotent registration) — never a throwing or
blind-overwrite `Register`. This is safe specifically because every
registration for a given `Type` is functionally interchangeable
regardless of which assembly generated it (the emitted lambda is always
the same shape, `(row, descriptor) => row.Resolve<T>(descriptor)`, for the
same `T`) — unlike `PlanCache<T>`'s own genuine "which plan is correct"
ambiguity, there is no real question to defer here.

**Amendment 1's own smoke-test instruction is superseded, not just
PLAN-0040's copy of it.** Amendment 1 (above) directs Phase 1 to "extend
PLAN-0041's real `dotnet publish -p:PublishAot=true` + run smoke test to
exercise `[Compose<TProfile, TConfig>]`." Amendment 2's own correction to
PLAN-0041's scope (core + `Compono.XunitV3` only, never running anything
through the real `Compono.TUnit` package chain) makes that instruction
impossible to satisfy as written — extending the wrong harness would let
Phase 1's Native AOT release gate get checked off without ever actually
exercising `[Compose<TProfile, TConfig>]` through `Compono.TUnit`.
PLAN-0040's own copy of this instruction was already corrected (its Phase
1 task now points at that phase's own dedicated `Compono.TUnit` AOT
project); this ADR's Amendment 1 text should be read the same way —
Phase 1's Native AOT verification belongs to PLAN-0040 Phase 0's own
harness, never to PLAN-0041's, which is scoped away from `Compono.TUnit`
entirely and merges before that harness exists.
