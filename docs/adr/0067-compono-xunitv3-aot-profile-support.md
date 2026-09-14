# [ADR-0067] Compono.XunitV3.Aot: Profile-Based Composition (`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`)

**Status:** Accepted

**Date:** 2026-09-14

**Decision Makers:** Nick Cipollina (product owner), agent (design dive via `/engineering-workflow`, Phase 2 of
[ADR-0066](0066-compono-xunitv3-aot-package-architecture.md)/[PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md))

## Context

[ADR-0066](0066-compono-xunitv3-aot-package-architecture.md) shipped `Compono.XunitV3.Aot` Phase 1: plain
`[Compose]` only, via a `Compono.Generators`-emitted `RegisteredEngineConfig.RegisterTheoryDataRowFactory`
registration per test method, with every composed parameter's type known and closed at compile time
(`row.Resolve<T>(descriptor)` calls with a literal `T`). Explicitly deferred: `[Compose<TProfile>]` and
`[Compose<TProfile, TConfig>]` (ADR-0066's Decision Outcome, PLAN-0066's Scope section).

`alexa-vox-craft` is the intended real dogfood consumer once profile support ships (its Native AOT
validator conversion was explicitly deferred for this reason - see the `xunitv3aot-dogfood-sequencing`
project memory). Real evidence from that repo: 42 of its `[Compose<TProfile>]` call sites use
`[Compose<LambdaTestProfile>]` - the fixed, default-constructed (`new()`) form only, applying
`.UseBogus()` and several `.Register<T>()` calls inside `Configure`. No `[Compose<TProfile, TConfig>]`
usage exists there today, but `Compono.XunitV3`'s own public surface supports it
([ADR-0036](0036-parameterized-composition-profile-selection.md)), so this ADR designs both forms rather
than only the one with a current consumer.

This investigation traced the real `Compono.XunitV3` (JIT/reflection-mode) implementation end to end:

- `ComposeAttribute<TProfile> : ComposeAttribute where TProfile : ICompositionProfile, new()` -
  `ApplyProfile` calls `builder.AddProfile<TProfile>()`
  (`src/Compono/CompositionBuilder.cs:157`), which is `ApplyProfile(new TProfile())` - a plain,
  compile-time-closed generic constructor call. **No reflection anywhere in this path today, in either
  mode** - `new()` on a generic type parameter compiles to a direct `Activator`-free construction, and
  Native AOT has never had a problem with it (proven by every one of this repo's 9 `*.AotSmokeTest`
  console apps under ADR-0041, which already exercise `AddProfile<TProfile>()`).
- `ComposeAttribute<TProfile, TConfig> : ComposeAttribute where TProfile : ICompositionProfile` -
  `ApplyProfile` calls `ConfigProfileBinder.BindConfig(typeof(TConfig), _configArguments)` then
  `ConfigProfileBinder.BuildProfile<TProfile, TConfig>(config)`, both of which use
  `Type.GetConstructors()`/`ConstructorInfo.Invoke` - genuine runtime reflection, annotated with
  `[DynamicallyAccessedMembers(PublicConstructors)]` at every `Type`/generic-parameter call-chain site
  (added after a real Native AOT trimming failure, issue #119, per that file's own remarks) so it
  survives the linker under a JIT-mode Native AOT publish. This reflection is bounded to once per
  attribute instance (inside the base class's existing `Lazy<Composer>` caching), never the per-row
  `GetData` path (ADR-0036 Amendment 1).
- `ConfigProfileBinder`'s validation rules - `TConfig` must have exactly one public constructor;
  `TProfile` must have exactly one public constructor accepting exactly one `TConfig`-typed parameter;
  supplied profile configuration arguments must match that constructor's parameter count/nullability/
  assignability - are **deterministic, not a "best match" heuristic** (ADR-0036), and are a **runtime**
  `CompositionException` specifically because C#'s generic-constraint system cannot express "has a
  constructor accepting exactly this type" (ADR-0036's "What this form deliberately gives up" section
  states this explicitly).

The key finding this ADR is built on: **xUnit's AOT source-generated pipeline changes what's possible
here, in Compono.XunitV3.Aot's favor, not against it.** Under `Compono.XunitV3.Aot`, `[Compose<TProfile,
TConfig>]`'s constructor arguments are attribute-constructor arguments - by C#'s own language rule, they
are **compile-time constants**, fully visible to `Compono.Generators` via Roslyn's
`AttributeData.ConstructorArguments` (a `TypedConstant` per argument). Combined with full compile-time
visibility into `TProfile`'s and `TConfig`'s declared constructors (`INamedTypeSymbol.Constructors`),
`Compono.Generators` can perform every one of `ConfigProfileBinder`'s validation checks **at compile
time**, against the real symbols and the real literal argument values, and then emit a **direct**
`new TConfig(literal, literal, ...)` / `new TProfile(config)` construction in generated source - not a
`Type.GetConstructors()`/`ConstructorInfo.Invoke` call, and not a single `[DynamicallyAccessedMembers]`
annotation anywhere in this path. This is precedented by two things already in this codebase:
`AotComposeMethodDiscovery.IsNullable` already ports `BindingPlan.GetNullability`'s runtime logic to
Roslyn symbols for an analogous reason (RESEARCH-0032 §9/PLAN-0066), and `CMP0040`
(`AotComposeMethodDiscovery`'s unsupported-shape diagnostic) already establishes the precedent that this
attribute family - having no `DataAttribute.GetData` runtime fallback at all - must catch every
unsupported shape as a compile-time diagnostic rather than let bad generated code reach the C# compiler
or fail at runtime.

## Decision Drivers

- [ADR-0001](0001-source-generation-first.md) - no reflection fallback by default. Reflection is not
  merely undesirable here; the JIT-mode reflection this ADR would otherwise have to replicate exists
  *only* because C#'s generic-constraint system can't express `TProfile`'s constructor shape - a
  limitation that doesn't apply to a source generator with full compile-time symbol access.
- ADR-0066's already-established precedent: this attribute family has no runtime fallback, so an
  unsupported shape must be a compile-time diagnostic (`CMP0040`), not a runtime exception - the same
  reasoning extends naturally to `TConfig`/`TProfile` shape validation.
- Backward compatibility / zero impact on `Compono.XunitV3` and core `Compono` - unchanged from
  ADR-0066's own driver; this ADR touches only `Compono.XunitV3.Aot` and `Compono.Generators`.
- Smallest architecture: extend the already-proven `Compono.XunitV3.Aot`/`AotComposeMethodDiscovery`/
  `AotTheoryDataRowRegistrationEmitter` path from PLAN-0066, following the exact
  `AttributeMetadataName`/`GenericAttributeMetadataName`/`TwoTypeParameterAttributeMetadataName` pattern
  `ComposeMethodDiscovery` already uses for all five other integration packages' `[Compose<TProfile>]`/
  `[Compose<TProfile, TConfig>]` forms - no new package, no new core capability.
- Real dogfood evidence (`alexa-vox-craft`'s 42 `[Compose<LambdaTestProfile>]` call sites) grounds the
  no-`TConfig` form as the priority target, without narrowing this ADR's scope away from
  `[Compose<TProfile, TConfig>]`, which `Compono.XunitV3`'s public surface already supports and which
  this ADR's mechanism handles for no extra architectural cost (same compile-time-symbol-visibility
  argument applies to both forms).

## Considered Options

1. **Compile-time-verified, direct construction** (chosen) - `Compono.Generators` performs
   `ConfigProfileBinder`'s validation checks against Roslyn symbols/`TypedConstant`s at compile time,
   emits direct `new TConfig(...)`/`new TProfile(...)` construction, reports any shape/argument mismatch
   as a new `CMP004x` diagnostic at the real attribute use site.
2. **Reimplement `ConfigProfileBinder` as reflection, annotated for trimming** - mirror the JIT-mode
   binder's runtime `Type.GetConstructors()`/`ConstructorInfo.Invoke` shape exactly inside the generated
   factory closure, with the same `[DynamicallyAccessedMembers(PublicConstructors)]` annotations the JIT
   path needs.
3. **Defer `[Compose<TProfile, TConfig>]` to a later phase; ship only `[Compose<TProfile>]` now.**

## Decision Outcome

**Chosen option: 1, compile-time-verified direct construction**, for both `[Compose<TProfile>]` and
`[Compose<TProfile, TConfig>]`.

Option 2 is rejected: it would introduce the exact reflection ADR-0001 rules out by default, for no
benefit - the compile-time information needed to avoid it entirely is already sitting in
`Compono.Generators`' hands (the attribute's own generic type arguments and constructor-argument
`TypedConstant`s), unlike the JIT-mode binder, which genuinely has no compile-time visibility into a
`ComposeAttribute<TProfile, TConfig>` use site at all.

Option 3 is rejected on evidence: nothing found during this investigation makes
`[Compose<TProfile, TConfig>]` harder to support than `[Compose<TProfile>]` under this mechanism - both
resolve to a direct, compile-time-verified constructor call, so there is no real complexity reduction
from deferring it, only an arbitrary capability gap against `Compono.XunitV3`'s existing public surface.

### Shape

**`[Compose<TProfile>]`** - new `Compono.XunitV3.Aot.ComposeAttribute<TProfile>`, marker-only,
`where TProfile : ICompositionProfile, new()` (identical constraint to `Compono.XunitV3`'s form - the
compile-time `new()` enforcement already works today and needs no new mechanism). Generated factory
closure changes from Phase 1's `global::Compono.Composer.Create()` to
`global::Compono.Composer.Create(b => b.AddProfile<{{ fully_qualified_profile_type_name }}>())`.

**`[Compose<TProfile, TConfig>]`** - new `Compono.XunitV3.Aot.ComposeAttribute<TProfile, TConfig>`,
marker-only, `where TProfile : ICompositionProfile` (no `new()` - matches `Compono.XunitV3`'s form).
`AotComposeMethodDiscovery` (extended) reads the attribute's `TypedConstant` constructor arguments plus
`TProfile`'s/`TConfig`'s declared constructors via the semantic model and performs, at compile time, the
same three checks `ConfigProfileBinder`/`ComposeAttribute<TProfile, TConfig>.ApplyProfile` perform at
runtime today:

1. `TConfig` has exactly one public constructor.
2. `TProfile` has exactly one public constructor accepting exactly one `TConfig`-typed parameter.
3. The attribute's supplied constructor arguments match `TConfig`'s single constructor's parameter
   count/nullability/assignability.

Any failure is a new compile-time diagnostic (below), reported at the `[Compose<TProfile, TConfig>]`
use site - never a generated call the C# compiler discovers is broken, and never a runtime exception.
On success, the generated factory closure constructs directly:

```csharp
var config = new {{ fully_qualified_config_type_name }}({{ rendered_literal_args }});
var profile = new {{ fully_qualified_profile_type_name }}(config);
var composer = global::Compono.Composer.Create(b => b.AddProfile(profile));
```

`{{ rendered_literal_args }}` is each constructor argument's `TypedConstant` rendered back to C# literal
syntax against `TConfig`'s real constructor parameter types (a string via `SymbolDisplay.FormatLiteral`,
a `System.Type`-typed argument via `typeof(...)`, an enum via its fully-qualified member access, a
primitive via its literal form) - new rendering logic in `Compono.Generators`, not previously needed
since Phase 1's emitter only ever rendered parameter *types*, never argument *values*.

### New diagnostics

Continuing `CMP004x`'s existing series (`CMP0040` is Phase 1's unsupported-test-method-signature
diagnostic):

| Code | Condition |
|---|---|
| `CMP0041` | `TConfig` does not have exactly one public constructor |
| `CMP0042` | `TProfile` does not have exactly one public constructor accepting exactly one `TConfig`-typed parameter |
| `CMP0043` | A supplied `[Compose<TProfile, TConfig>]` constructor argument's count/nullability/type doesn't match `TConfig`'s single constructor's parameters |

All three mirror `ConfigProfileBinder`'s exact JIT-mode rules (same "no best-match heuristic, exact shape
only" discipline ADR-0036 establishes) - same conditions, same "exactly one" language, just reported at
compile time instead of at first `GetData` call.

### Discovery: three new metadata names, following the established pattern exactly

`ComposeMethodDiscovery` already registers three metadata names per integration package
(`X.ComposeAttribute`, `` X.ComposeAttribute`1 ``, `` X.ComposeAttribute`2 ``) for all five existing
integration packages. `Compono.XunitV3.Aot` gains its own `` Compono.XunitV3.Aot.ComposeAttribute`1 ``/
`` `2 `` constants, registered the same way, for the same reason (a closed generic attribute's metadata
name is distinct from its open non-generic base, per `ForAttributeWithMetadataName`'s exact-match
semantics) - both for `ComposeMethodDiscovery`'s own ordinary `PlanCache<T>` parameter-type discovery
(unchanged mechanism; a profile doesn't change which parameter types a test method composes) and for
`AotComposeMethodDiscovery`'s per-method factory-registration discovery (extended to also capture the
profile/config shape).

### No `[DynamicallyAccessedMembers]` needed anywhere in this design

The headline consequence of the compile-time-verified approach: nothing in the generated code path
performs runtime reflection, so nothing needs trimming/AOT annotation at all - a stronger guarantee
than JIT-mode's own annotated-reflection approach, not merely an equivalent one.

### One accepted, explicit divergence from `Compono.XunitV3`'s behavior

An invalid `TConfig`/`TProfile` shape, or a mismatched profile-configuration-argument count/type, is a
**compile-time diagnostic** under `Compono.XunitV3.Aot`, versus a **runtime `CompositionException`**
under `Compono.XunitV3`. This is a real, deliberate behavioral difference - not an oversight, not a
capability reduction (the opposite: earlier, IDE-visible feedback instead of a test-execution-time
failure) - forced by ADR-0066's own established precedent that this attribute family has no runtime
fallback path to report through at all. Documented explicitly here and in the package guide so it is
never mistaken for accidental drift from `Compono.XunitV3`'s documented behavior.

### Positive Consequences

- Full profile-based composition parity with `Compono.XunitV3`'s public syntax - same attribute shapes,
  same constraint on `TProfile`, same profile-configuration-argument binding rules - reachable under
  Native AOT for the first time.
- Strictly stronger AOT-safety guarantee than the JIT-mode path it mirrors: zero reflection, zero
  `[DynamicallyAccessedMembers]` annotations, in the new code this ADR introduces.
- `alexa-vox-craft`'s real, dogfooded `[Compose<LambdaTestProfile>]` usage pattern (42 call sites) is
  directly served by this design with no gaps identified during this investigation.
- No changes to `Compono.XunitV3`, core `Compono`, or Phase 1's existing plain-`[Compose]` mechanism -
  purely additive, following ADR-0066's own backward-compatibility precedent.

### Negative Consequences

- New, real generator complexity: rendering a `TypedConstant` back to valid C# literal syntax has not
  been needed by any existing emitter in this codebase (Phase 1's emitter only ever rendered *types*,
  never argument *values*) - genuinely new logic, not a copy of an existing pattern, and the one piece
  of this design not already proven by a working spike the way Phase 1's core mechanism was
  (RESEARCH-0032 §9). Mitigated by a bounded, well-precedented problem (attribute-legal argument types
  are a small, closed set: primitives, strings, enums, `System.Type`, one-dimensional arrays of those)
  and by treating its first real exercise as implementation-time verification, the same way Phase 1's
  two real corrections (namespace resolution, publish-invocation form) surfaced during implementation
  rather than during design.
- Two new public attribute types added to `Compono.XunitV3.Aot`'s surface
  (`ComposeAttribute<TProfile>`, `ComposeAttribute<TProfile, TConfig>`) - accepted, mirrors
  `Compono.XunitV3`'s existing three-attribute-family shape exactly, no new naming/design surface
  invented.
- The compile-time-vs-runtime diagnostic-timing divergence from `Compono.XunitV3` (above) must be
  documented clearly enough that it doesn't read as unexplained inconsistency between the two packages.

## Pros and Cons of the Options

### Compile-time-verified, direct construction (chosen)

- Good, because it needs zero reflection and zero trimming annotations - strictly stronger than the
  JIT-mode path it mirrors.
- Good, because it reuses `Compono.Generators`' existing symbol-inspection/diagnostic patterns
  (`CMP0040`'s precedent) rather than inventing a new validation mechanism.
- Bad, because literal-value-to-C#-source rendering is genuinely new generator logic, not yet proven by
  a working spike.

### Reimplement `ConfigProfileBinder` as annotated reflection

- Good, because it would be a closer, more literal mirror of the JIT-mode implementation.
- Bad, because it reintroduces the exact reflection ADR-0001 exists to avoid by default, for no reason -
  the compile-time information needed to avoid it is already available and unused by this option.

### Defer `[Compose<TProfile, TConfig>]`

- Good, because it narrows Phase 2's scope to the form with current real dogfood evidence
  (`alexa-vox-craft`'s usage is `[Compose<TProfile>]`-only today).
- Bad, because nothing in this investigation found `[Compose<TProfile, TConfig>]` to be meaningfully
  harder to support once the compile-time-verified mechanism exists for `[Compose<TProfile>]` - deferring
  it would be an arbitrary capability gap against `Compono.XunitV3`'s shipped surface, not a
  complexity-driven decision.

## Amendment 1 (2026-09-14): `ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile, TConfig>` do not
inherit from the non-generic `ComposeAttribute`

This ADR's "Shape" section above shows `ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile,
TConfig>` deriving from `Compono.XunitV3.Aot.ComposeAttribute` (unsealing it to allow this), mirroring
`Compono.XunitV3.ComposeAttribute`'s own design. Implementation review found this mirroring doesn't
survive scrutiny: `Compono.XunitV3.ComposeAttribute`'s generic siblings extend real, shared runtime
state - a cached `Composer`, cached binding delegates, a real `GetData` override - inheritance is
load-bearing there (ADR-0022). `Compono.XunitV3.Aot.ComposeAttribute` is a pure marker with zero
functional state for a subtype to extend, and AOT discovery matches purely by each closed attribute
type's own fully qualified metadata name (`AttributeData.AttributeClass`), never by inheritance or
assignability (`AotComposeMethodDiscovery`'s own remarks, unchanged by this amendment). Sharing a base
class therefore bought nothing functionally, while opening a real hazard specific to this marker-only
attribute family: a consumer subclassing any of the three forms would compile cleanly but never be
discovered by `Compono.Generators` (a derived type's own metadata name matches no registered discovery
provider), silently never running under xUnit's AOT pipeline at all - precisely the failure mode this
ADR's own Decision Drivers and ADR-0066's `CMP0040` exist to prevent for every other unsupported shape
in this attribute family.

**Correction:** `Compono.XunitV3.Aot.ComposeAttribute` stays `sealed`, exactly as PLAN-0066 shipped it -
no public API change to that already-released type after all. `ComposeAttribute<TProfile>`/
`ComposeAttribute<TProfile, TConfig>` derive directly from `Xunit.v3.DataAttribute` instead - three
independent marker siblings, unified only by naming convention and by `Compono.Generators`' shared
discovery pattern (the same "one string, one registration" convention every other `[Compose]`-family
integration in this repo already uses), never by CLR inheritance. This is a correction to this ADR's
own "Shape" illustration, not a reversal of its Decision Outcome (Option 1, compile-time-verified
direct construction, remains chosen) - PLAN-0067's own Notes section records the implementation-time
discovery in full. No discovery or code-generation logic changed as a result; both new attribute types'
own `[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]` declarations were already
independently self-declared (never relying on inherited `AttributeUsage`), so this correction is a pure
type-hierarchy change with zero behavioral difference to any already-verified test.

## Amendment 2 (2026-09-14): `CMP0044`-`CMP0049` - additional compile-time shape restrictions found by
implementation review

This ADR's Decision Outcome originally recorded only `CMP0041`-`CMP0043` - the three checks directly
mirroring `ConfigProfileBinder`'s own runtime rules (constructor count/shape, argument match). PR #140's
Codex review (ten rounds) found six further real shapes where the compile-time-verified, direct-
construction design (this ADR's chosen Option 1) needed its own additional restrictions beyond what
`ConfigProfileBinder`'s JIT-mode reflection ever had to consider - JIT's `ConstructorInfo.Invoke` simply
doesn't encounter most of these problems, since reflection bypasses the C# compiler checks and analyzer
hints a direct `new T(...)`/generic `new T()` call site is subject to. These are externally observable
restrictions on what constructor shapes are accepted, not implementation details, so they belong in this
ADR's own decision record - PLAN-0067's Notes section has the full round-by-round discovery and fix
narrative for each; this amendment records only the resulting diagnostics.

| Code | Condition |
|---|---|
| `CMP0044` | A type referenced by `[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` (`TProfile`, `TConfig`; a `typeof`/enum-typed profile configuration argument's own type; or, for an array-typed argument, its declared element type or any `typeof`/enum-typed value recursively embedded in it) isn't accessible from the generated top-level registration |
| `CMP0045` | More than one Compose-family attribute (`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`) on one test method - these three share no common base class (Amendment 1), so nothing else stops stacking them |
| `CMP0046` | The selected `TConfig`/`TProfile` constructor doesn't satisfy the type's `required` members (no `[SetsRequiredMembers]`) |
| `CMP0047` | The selected `TConfig`/`TProfile` constructor is marked with an attribute that makes any *use* of it a compiler error - `[Obsolete("...", error: true)]`, `[Experimental("...")]`, or non-optional `[CompilerFeatureRequired("...")]` |
| `CMP0048` | An accessible sibling constructor marked with a higher `[OverloadResolutionPriority]` could silently supersede the selected `TConfig`/`TProfile` constructor at the generated call site - the explicit-cast guard this design otherwise uses to defeat ordinary overload hijacking cannot defend against priority-based pruning |
| `CMP0049` | `[Compose<TProfile>]`'s `TProfile` has a public parameterless constructor (guaranteed by the `new()` constraint) marked `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`/`[RequiresAssemblyFiles]` - the generated `AddProfile<TProfile>()` call closes a generic `new T()` construction over the real `TProfile` at that call site, surfacing the corresponding `IL3050`/`IL2026`/`IL3002` warning for a trim/AOT-analyzed consumer |

None of these required revisiting this ADR's Decision Outcome (Option 1 remains chosen) or its Shape
section (Amendment 1's correction stands unchanged) - each is an additional compile-time restriction
within the already-accepted design, not an architectural change. `docs/reference/diagnostics.md` and
`docs/packages/compono-xunitv3-aot.md` carry the full message/cause/fix detail for each code.

## Links

- [ADR-0066](0066-compono-xunitv3-aot-package-architecture.md) - Phase 1, the package/generator
  architecture this ADR extends.
- [PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md) - Phase 1's execution
  record; explicitly scoped profile support out (see its Scope section).
- [ADR-0022](0022-compono-xunit-package-design.md) - `Compono.XunitV3`'s full binding algorithm,
  including `[Compose<TProfile>]`'s existing `new()`-constrained design.
- [ADR-0036](0036-parameterized-composition-profile-selection.md) - `[Compose<TProfile, TConfig>]`'s
  full design, including `ConfigProfileBinder`'s exact validation rules this ADR's compile-time checks
  mirror.
- [ADR-0018](0018-composition-profiles.md) - `ICompositionProfile`/`AddProfile`, unchanged by this ADR.
- [ADR-0041](0041-aot-safe-row-binding-dispatch.md) - the `[DynamicallyAccessedMembers]`-annotated
  reflection pattern this ADR's design deliberately avoids needing.
- [PLAN-0067](../plans/0067-compono-xunitv3-aot-profile-support-impl-plan.md) - the phased execution
  tracker for this decision.
