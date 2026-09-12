# [ADR-0066] Compono.XunitV3.Aot: A Separate Package for xUnit v3 Native AOT Support

**Status:** Accepted

**Date:** 2026-09-12

**Decision Makers:** Nick Cipollina (product owner), agent (design dive via `/engineering-workflow`,
following [RESEARCH-0032](../research/0032-compono-xunitv3-native-aot-package-architecture.md))

## Context

[RESEARCH-0031](../research/0031-native-aot-framework-native-testing-admission-research.md) found,
via a corrected spike, that `Compono.XunitV3` cannot be referenced alongside xUnit v3's real,
officially-supported Native AOT package family (`xunit.v3.aot.mtp-v2` and its `.aot`-suffixed
dependencies) at all — a compile-time `CS0433` conflict, not a runtime nuance, caused by
`Compono.XunitV3`'s shipped dependency on the reflection-mode `xunit.v3.extensibility.core` package.
xUnit v3 4.0 (2026-08-14) is the version that introduced official Native AOT support; `Compono.XunitV3`
predates it and was never updated to account for the parallel package family xUnit introduced alongside
it.

[RESEARCH-0032](../research/0032-compono-xunitv3-native-aot-package-architecture.md) investigated
whether this can be closed within the existing package, and if not, what the smallest correct
architecture is. It found, with direct evidence from xUnit's own source and a working end-to-end proof
spike (scratchpad only, discarded, no production code changed), that:

- A single package cannot support both modes — `ComposeAttribute : DataAttribute` is permanently bound
  to one `DataAttribute` assembly identity at Compono's own build time, and xUnit's AOT-only
  `RegisteredEngineConfig.RegisterTheoryDataRowFactory` API doesn't exist in the reflection-mode
  assembly at all, so no single piece of source compiles against both.
- xUnit's own AOT extensibility model (proven directly against its official `AotCsvDataSource` sample
  and xUnit's `DataAttributeGenerator` source) is: a near-empty marker `DataAttribute` subclass, plus
  compile-time-generated code calling `RegisteredEngineConfig.RegisterTheoryDataRowFactory` with a
  factory closure that supplies theory data rows. `DataAttribute.GetData`'s runtime override is never
  invoked in this path at all.
- A proof spike (hand-written stand-in for generator emission, calling only unmodified, already-shipped
  core `Compono` public API) published as Native AOT, ran as a native binary, and passed — the real
  xUnit v3 AOT pipeline discovered a `[Theory]`+`[Compose]`-shaped test, invoked the registered factory,
  and Compono composition executed correctly inside the native process.
- `Compono.Generators` already hardcodes each integration package's `[Compose]`-attribute metadata name
  as a string constant (`ComposeMethodDiscovery.AttributeMetadataName`, etc.) — a seventh constant for
  a new AOT-specific attribute follows an already-established pattern, requires no new assembly
  reference from `Compono.Generators` to any xUnit package, and generated source text can call
  `RegisteredEngineConfig.RegisterTheoryDataRowFactory` by fully-qualified name without the generator
  itself ever referencing the defining assembly (proven by the spike).

## Decision Drivers

- Native AOT compatibility is an explicit Compono design goal
  ([ADR-0001](0001-source-generation-first.md)); `Compono.XunitV3` is Compono's oldest, most
  established, most heavily dogfooded integration package, and cannot currently coexist with xUnit's
  own now-official Native AOT support at all.
- No reflection-based fallback by default (ADR-0001) — the chosen design must not introduce runtime
  reflection to bridge the two modes.
- The core `Compono` package, and `Compono.Generators`, must never take on a hard reference to an
  integration package or a specific test framework's assemblies
  (`references/design-decisions.md` rule 3) — string-metadata-name matching is the established,
  precedented exception; an actual assembly reference is not.
- Prefer the smallest maintainable design — do not attempt inline values, `[Shared]`, or profile-variant
  support in the same change as the core mechanism; do not invent a reflection-compatibility shim for
  the two xUnit package families.
- Backward compatibility for existing `Compono.XunitV3` consumers is non-negotiable unless a compelling
  reason exists to break it — product-owner decision (this session): purely additive, no breaking
  change, no major version bump.

## Considered Options

1. **Single `Compono.XunitV3` package supporting both modes** (via TFM/build-property/conditional
   dependency selection).
2. **A separate AOT-specific package** (`Compono.XunitV3.Aot`), referencing only xUnit's `.aot` package
   family, shipping its own `ComposeAttribute`, activated by `Compono.Generators`' extended emission.
3. **Internal reorganization only** — a shared, framework-independent composition core with thin
   reflection-mode and AOT-mode adapters, without necessarily creating a new *public* package.
4. **Do not support xUnit Native AOT at all.**

## Decision Outcome

**Chosen option: 2, a separate `Compono.XunitV3.Aot` package** (with option 3's internal-reorganization
insight folded in as an implementation detail, not a competing package-boundary choice — see below).

Option 1 is not merely riskier than option 2; it is **not achievable at all** through supported
NuGet/MSBuild mechanisms, confirmed by direct evidence (§RESEARCH-0032 §3): the two xUnit package
families target identical TFMs (no TFM axis to key a conditional dependency group on), and the
compile-time-bound `DataAttribute` identity plus the AOT-only `RegisterTheoryDataRowFactory` API make a
single compiled `ComposeAttribute` structurally impossible regardless of packaging cleverness. Option 4
is rejected — Gate A and Gate B both clear cleanly (RESEARCH-0032 §12/§13), and a real, working,
end-to-end proof already exists.

Option 3's "shared core, thin adapters" framing is correct and adopted **inside** option 2's package
boundary, not as an alternative to it: core `Compono` already is that shared, framework-independent
core (confirmed unmodified by the proof spike), so no *new* internal abstraction layer needs inventing
— `Compono.XunitV3` (reflection-mode `ComposeAttribute`, runtime `GetData`) and `Compono.XunitV3.Aot`
(AOT-mode marker `ComposeAttribute`, generator-emitted registration) are both already "thin adapters"
over the same unmodified core, satisfying option 3's actual intent without a redundant new package.

**Package name: `Compono.XunitV3.Aot`** (product-owner decision, this session) — mirrors xUnit's own
`xunit.v3` → `xunit.v3.aot` naming convention exactly.

**Namespace: `Compono.XunitV3.Aot`**, not a reuse of `Compono.XunitV3`'s own namespace — avoids any
possibility of a using-directive ambiguity for a consumer whose solution (never a single project)
contains both packages.

**Phase 1 scope: plain `[Compose]` only.** No inline values, no `[Shared]`, no
`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` in the first release — these require real,
additional generator work (reproducing `BindingPlan`'s inline-value validation and `[Shared]` ordering,
and `ConfigProfileBinder`'s profile-construction logic, all as compile-time-generated code) that the
proof spike did not attempt and that should not gate shipping the core, proven mechanism. See
[PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md) for phasing.

### Positive Consequences

- Closes a real, present compatibility gap for Compono's most established integration package, with a
  proven-working mechanism, not a design argument.
- Zero impact on existing `Compono.XunitV3` consumers — purely additive, no breaking change.
- `Compono.Generators` gains one new attribute-metadata-name constant and new emission logic, following
  an already-established pattern (six existing constants for the other four integration packages'
  attribute families) — no new project, no new package reference, no new category of coupling.
- The generated dispatch is, if anything, *more* AOT-idiomatic than the existing reflection-mode path:
  every `T` is known at compile time from the method symbol directly, so generated code can call
  `row.Resolve<T>(descriptor)` with a literal closed generic type — no `RowInvokerRegistry` runtime
  lookup needed for this path at all.
- Existing `[Theory]` + `[Compose]` test syntax remains unchanged; consumers opt into the AOT
  integration by changing package/reference imports to `Compono.XunitV3.Aot` (namespace and `using`
  included — only the attribute usage at the test method itself reads identically in source).

### Negative Consequences

- A second, real package to maintain, version, and document — mitigated by it being purely additive (no
  coordination burden with `Compono.XunitV3`'s own release cadence) and by the maintenance-cost factor
  already being weighed as part of Gate A (RESEARCH-0032 §12), not a standalone veto.
- Full `[Compose]` parity (inline values, `[Shared]`, profile variants) is deferred, meaning
  `Compono.XunitV3.Aot`'s first release will be less capable than `Compono.XunitV3`'s full surface —
  an explicit, accepted phasing decision (see Decision Drivers), not an oversight.
- `Compono.Generators` needs new per-compilation mode-detection logic (which `ComposeAttribute` metadata
  name matched already provides this for free, per RESEARCH-0032 §5, but it's still new surface
  requiring its own determinism/caching verification during implementation).

## Pros and Cons of the Options

### Single package, both modes

- Bad, because it is not achievable through any supported NuGet/MSBuild mechanism — confirmed by direct
  evidence, not merely disfavored on style grounds (RESEARCH-0032 §3).

### Separate `Compono.XunitV3.Aot` package (chosen)

- Good, because it mirrors xUnit's own package-split convention exactly — idiomatic, discoverable.
- Good, because it requires zero changes to existing `Compono.XunitV3`/core `Compono` — proven by the
  spike.
- Good, because `Compono.Generators`' extension follows an already-established, low-risk pattern.
- Bad, because it's a new package to maintain — accepted, weighed explicitly against real Gate A/B
  evidence, not a decisive objection on its own.

### Internal reorganization only, no new package

- Good, because it would minimize package count.
- Bad, because it doesn't actually solve the problem — the `ComposeAttribute` type itself still needs
  two distinct compiled identities regardless of how the logic underneath is factored; "no new package"
  isn't achievable without abandoning `[Theory]`+`[Compose]`'s existing shape (rejected per the design
  drivers) or breaking `Compono.XunitV3`'s existing consumers (rejected per backward-compatibility
  driver).

### Do not support xUnit Native AOT

- Good, because it's zero work.
- Bad, because Gate A and Gate B both clear cleanly, real dogfooding-grade evidence exists (this repo's
  own most-used integration package), and a working proof already exists — rejecting outright would
  discard real, already-validated value for no offsetting reason.

## Links

- [RESEARCH-0031](../research/0031-native-aot-framework-native-testing-admission-research.md) — the
  broader investigation that first surfaced this gap.
- [RESEARCH-0032](../research/0032-compono-xunitv3-native-aot-package-architecture.md) — the full
  evidence base and proof spike this ADR's decision is drawn from.
- [ADR-0041](0041-aot-safe-row-binding-dispatch.md) — the generated-module-initializer pattern this
  ADR's generator emission mirrors.
- [ADR-0040](0040-compono-tunit-package-design.md) / [ADR-0057](0057-compono-mstest-package-design.md) —
  precedent for a framework-specific integration package's own design pass and TFM-floor reasoning.
- [PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md) — the phased
  execution tracker for this decision.
