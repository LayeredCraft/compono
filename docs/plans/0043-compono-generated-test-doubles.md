# [PLAN-0043] Compono-Generated Test Doubles

**Status:** In Progress

**Implements:** ADR-0043 (design), ADR-0042 (admitted problem)

## Goal

`ComponoGeneratedTestDoubles=true` plus `builder.UseGeneratedTestDoubles()`
lets `composer.Create<T>()` automatically satisfy an otherwise-unresolvable
interface dependency with a generated, AOT-safe double — configurable via
`interfaceValue.Configure().Member().Returns(...)`/`.Throws(...)` (a
generator-emitted extension per discovered interface, per
[ADR-0043 Amendment 1](../adr/0043-compono-generated-test-doubles-design.md#amendment-1-2026-08-13-configure-must-be-generator-emitted-per-interface-not-a-runtime-generic-method)) —
with zero behavior change for any consumer who doesn't opt into both
gates, and a real `dotnet publish -p:PublishAot=true` execution test
proving the whole path is Native-AOT-safe.

## Scope

Builds exactly what [ADR-0043](../adr/0043-compono-generated-test-doubles-design.md)
(as corrected by
[Amendment 2](../adr/0043-compono-generated-test-doubles-design.md#amendment-2-2026-08-13-cross-assembly-bridge-generated-type-collision-safety-coreoptional-package-boundary-corrected-argument-matching-sample-struck))
decided — the generated code shape, the compile-time opt-in on
`LeafTypeClassifier`, the core registry/builder primitives,
`GeneratedTestDoubleProvider`, and the new `Compono.TestDoubles` package.
Explicitly deferred, per ADR-0042's Non-Goals (unchanged by ADR-0043):
verification, call recording, strict mode, argument matchers (struck
entirely by Amendment 2 — configuration is member-level and
argument-independent, not "a minimal closed shape" as the pre-Amendment
text once said), callbacks, sequential returns,
class/protected-member/static-abstract-member support,
indexers/events/generic methods/`ref`/`out`/`in` parameters. Standalone
(non-Compono) usability is included only if it falls out at the cost
ADR-0043's "Standalone usability" section already found (near zero) — not
worth its own phase if it turns out to need more than that.

## Phases

### Phase 0 — Core primitives and generator foundation

- [x] **Core `Compono`** (not `Compono.TestDoubles` — Amendment 2 moved
      these to fix a cross-assembly reference the original design got
      backwards): `ReturnConfig<T>` (`internal` backing fields, `public`
      readonly accessors — `HasConfiguredValue`/`HasConfiguredException`/
      `ConfiguredValue`/`ConfiguredException` — for cross-assembly generated
      dispatch code to read; Amendment 3 Finding A), `ReturnConfigBuilder<T>`
      (a `public readonly ref struct` holding a `ref ReturnConfig<T>`, public
      constructor — Amendment 3 Finding A — whose `Returns` sets **both**
      `Value` and `HasValue` (Amendment 3 Finding B) **and clears any
      previously-set `Exception`, and whose `Throws` clears `HasValue`**
      (last-configuration-wins — Amendment 7 Finding R), and
      `GeneratedTestDoubleRegistry` (`RegisterFactory<T>(Func<T> factory)`/
      `TryCreate(Type requestedType, out object? value)`, `Type`-keyed,
      first-registration-wins — Amendment 3 Finding C documents this as a
      known v1 limitation for multi-assembly same-interface scenarios, not
      something this phase needs to solve) — always present in core, inert
      unless a factory is ever registered.
- [x] Extend `LeafTypeClassifier` with the compile-time-gated third
      classification outcome (ADR-0043's "Generator architecture").
- [x] Ship a `CompilerVisibleProperty` declaration for
      `ComponoGeneratedTestDoubles` via core `Compono`'s own packaged build
      assets (Amendment 4 Finding F — a custom MSBuild property is
      **not** automatically visible to `AnalyzerConfigOptionsProvider`
      the way a built-in one like `InterceptorsNamespaces` is; without this
      declaration the opt-in can never activate, regardless of what a
      consumer sets).
- [x] Read `ComponoGeneratedTestDoubles` via `AnalyzerConfigOptionsProvider`;
      confirm zero generated-output diff when unset/`false` (a compile-diff
      regression test, not just a manual check).
- [x] Emit, per discovered interface, **one single generated file, no
      namespace declaration (global namespace)** — Amendment 11 Finding AA:
      `internal` accessibility alone doesn't make `Configure()`/the
      per-member extensions reachable from an arbitrary consumer namespace
      without an import, and Amendment 4 Finding G already retired the
      global-using injection on the assumption no import would ever be
      needed — true only once every generated type is unconditionally
      visible, i.e. in the global namespace. Containing (Amendment 2's
      verified-by-spike shape — do not file-scope any of the first three;
      `CS9051` blocks a file-local type from appearing in any
      non-file-local member's signature, even co-located):
      - **Discovery walks the interface's full transitive base-interface
        closure (`ITypeSymbol.AllInterfaces`), not just its own declared
        members** (Amendment 11 Finding Z — `IChild.GetMembers()` doesn't
        return a member `IChild : IBase` only inherits from `IBase`; a
        double emitted from `IChild`'s own members alone would fail to
        implement it, `CS0535`). Every unsupported-shape/collision
        diagnostic below applies across the full closure, not just the
        leaf interface. An inherited member's explicit-implementation
        accessor is qualified against the interface that actually
        **declares** it (`ReturnType IBase.Get()`), not the leaf interface
        requested (`ReturnType IChild.Get()` would not compile).
      - `internal sealed class <Hash>_Double : IRepository` — explicit
        interface implementation, one `ReturnConfig<T>` field per member.
        A `void` member's field is `ReturnConfig<Compono.Unit>`, where
        `Unit` (if core doesn't already have one) is introduced as
        `public readonly struct Unit` from the start — not `internal`,
        per Amendment 4 Finding H, applying Amendment 3's own
        cross-assembly-accessibility lesson up front rather than missing
        it for this one type too. **A read/write property gets real
        auto-property semantics** (Amendment 7 Finding Q, confirmed with
        the requester over two alternatives — properties-unsupported, and
        getter-only-with-no-op-setter): the getter and setter share one
        `ReturnConfig<T>` field, the getter returns whatever was last set
        or the deterministic default, `Configure().<PropertyName>().Returns(...)`/
        `.Throws(...)` still work as an explicit override. **The write
        accessor's exact kind (`set` vs. `init`) must match what the
        interface actually declares** (Amendment 9 Finding U — `init` and
        `set` are non-interchangeable; emitting the wrong one fails to
        implement the interface) — a `get`-only property gets no write
        accessor at all, configurable only via `Configure()`, same as a
        method. Neither write accessor touches `ReturnConfig<T>`'s
        internal fields directly (Amendment 7's own sketch did, and
        reintroduced Amendment 3's cross-assembly `CS0122` defect for
        writes — corrected by Amendment 8 Finding S) — both construct a
        `ReturnConfigBuilder<T>` (public constructor) and call its public
        `Returns` method, the same public surface the external
        `Configure()` path already uses. **A set-only property (a setter
        with no getter at all) is diagnosed as unsupported, not emitted**
        (Amendment 10 Finding W, confirmed with the requester) — v1's
        already-decided lack of call recording/verification means nothing
        could ever observe a value written through a set-only property, so
        there's no meaningful behavior to give it, not just a limited one.
      - `internal static class <Hash>_DoubleConfiguration` — per-member
        configuration extensions (`FindAsync()`/`Save()`/property names,
        no parameters — argument-independent per Amendment 2 Finding 4;
        a property's configuration extension is method-shaped exactly
        like an ordinary member's, no special-casing). Member names reuse
        `RequiredMemberCollector.EscapeIdentifier`'s existing
        `SyntaxFacts.GetKeywordKind`-based `@`-escaping convention
        (`src/Compono.Generators/Discovery/RequiredMemberCollector.cs`),
        generalized rather than duplicated (Amendment 6 Finding O — an
        interface member like `@new` must round-trip through its generated
        extension's name too, not just through generated type names).
        **The same escaping applies to the member name in the explicit
        interface implementation too** (Amendment 9 Finding V — Amendment 6
        only covered the configuration extension; `int IFoo.new()` is
        still invalid without it), not just the type-name half Amendment 5
        Finding J covers. **And to every emitted method *parameter* name**
        (Amendment 10 Finding X — `void Save(int @class)`'s parameter
        symbol name is the bare `class`; escaping only the member name
        left the parameter list itself invalid).
      - `internal static class <Hash>_ConfigureExtension` — the
        `Configure(this IRepository)` bridge (Amendment 1, corrected
        target type by Amendment 2), whose cast-failure exception message
        names the multi-assembly-collision scenario explicitly (Amendment 3
        Finding C).
      - `file static class <Hash>_DoubleRegistration` — `[ModuleInitializer]`
        registering the double's factory into `GeneratedTestDoubleRegistry`
        (this one *can* stay `file`-scoped — never called by name).
      - `<Hash>` uses a **new, identifier-specific sanitizer** (Amendment 5
        Finding J — `HintNameFor` itself is reused only for its FNV-1a hash
        over the original, unsanitized fully-qualified name; its own
        sanitized-name output deliberately preserves dots, which are
        illegal in a C# identifier, so a separate sanitizer replacing `.`
        with `_` alongside every other character `HintNameFor` already
        replaces is needed for the type-name half). `GeneratedFileNaming.cs`
        itself is unchanged — this is a sibling helper, not a modification.
      - Deduplicated per distinct interface symbol across the compilation
        (same `.Collect()` + `SymbolEqualityComparer` pattern used
        elsewhere in the generator).
- [x] Compile-time diagnostic for an interface **inaccessible to a
      top-level generated type** (Amendment 8 Finding T — a `private`/
      `protected` nested interface is a legal call-site request but a
      top-level double can never implement it) — reuse the existing
      `compilation.IsSymbolAccessibleWithin(...)` check already used for
      generated collection plans and row-invoker registrations
      (`TransitiveClosureWalker.ToDiscoveredCollectionInfo`), not a new
      mechanism; leaf still defers to the unchanged runtime-provider path.
- [x] Deterministic-default logic per ADR-0043's "Deterministic defaults"
      (primitives, nullable refs, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`,
      empty collections never `null`). **Non-nullable reference returns
      (`string`, a non-nullable `Customer`, `Task<Customer>`) have no
      deterministic default at all (Amendment 5 Finding K)** — diagnose and
      reject, per the decision below; do not emit `null` (violates the
      interface's own nullable annotation) or attempt real composition
      (out of scope, confirmed with the requester).
- [x] Compile-time diagnostics for unsupported member shapes (indexers,
      events, generic methods, `ref`/`out`/`in`, static abstract members,
      **overloaded members** — Amendment 3 Finding D: a zero-argument
      configuration extension can't disambiguate `Get(int)` from
      `Get(string)`, diagnose and reject rather than emit a duplicate-
      signature compile error) — leaf still defers to the unchanged
      runtime-provider path.
- [x] Compile-time diagnostic for an interface that declares its own
      member named `Configure` with a colliding signature (Amendment 3
      Finding E — an instance member always wins over the generated
      extension in overload resolution, silently making the bridge
      unreachable) — leaf still defers to the unchanged runtime-provider
      path.
- [x] Compile-time diagnostics for unsupported **return** shapes — ref-like
      (`Span<byte> Read()`, can't close the unconstrained generic
      `ReturnConfig<T>` at all), by-ref-returning members, pointer, and
      function-pointer returns (Amendment 4 Finding I — the original list
      only covered parameter modifiers, not returns), **and non-nullable
      reference returns** (Amendment 5 Finding K — no deterministic default
      exists for these; diagnose and reject rather than emit `null` or
      attempt real composition) — leaf still defers to the unchanged
      runtime-provider path.
- [x] Compile-time diagnostics for unsupported **parameter** shapes —
      pointer and function-pointer parameters (Amendment 10 Finding Y — the
      direct parameter-side counterpart to Amendment 4 Finding I's
      return-side check; an unhandled pointer/function-pointer parameter
      would need the generated method wrapped in `unsafe`, which nothing
      in this design emits, producing `CS0214` instead of a clean
      diagnostic) — leaf still defers to the unchanged runtime-provider
      path.
- [x] Compile-time diagnostic for an interface member whose **generated,
      zero-argument extension** collides with an inherited `object` member
      (`GetHashCode()`, `ToString()`, `Equals(object)`, `GetType()` —
      Amendment 5 Finding L, corrected by Amendment 6 Finding N) — same
      "instance member always wins over extension" shadowing as the
      `Configure()` collision above, just against `object` instead of the
      interface's own declared members. **Compare the generated (always
      zero-argument, per Amendment 2 Finding 4) extension's name against
      `object`'s members — not the interface member's own declared
      signature** — Amendment 6 Finding N: e.g. `int ToString(int format)`
      is not `object.ToString()` as declared, but its zero-argument
      generated extension collides with it; `Equals(object obj)`'s
      zero-argument generated extension does **not** collide with
      `object.Equals(object)` (one parameter), so checking the original
      interface signature both under- and over-diagnoses. Leaf still
      defers to the unchanged runtime-provider path.

### Phase 1 — Runtime package (`Compono.TestDoubles`)

- [ ] New `src/Compono.TestDoubles` project — **only**
      `GeneratedTestDoubleProvider : ICompositionValueProvider` (reads the
      core `GeneratedTestDoubleRegistry`) and `UseGeneratedTestDoubles()`
      builder extension ([ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
      `AddTestDoubleProvider`, `NSubstituteProvider`-sized). No
      `ReturnConfigBuilder<T>`, no registry, no `Configure(...)` here —
      all three live in core `Compono` or are generator-emitted per
      interface (Amendment 2).
- [ ] Precedence documentation: `UseGeneratedTestDoubles()` before
      `UseNSubstitute()` when both are installed (ADR-0043's "Runtime
      activation and precedence") — a real sample/test proving registration
      order produces the documented result, not just prose.
- [ ] **No global-using declaration.** Amendment 1's original
      `global using Compono.TestDoubles.Generated;` idea is retired by
      Amendment 4 Finding G, and validated (not just assumed) by Amendment
      11 Finding AA's global-namespace-placement fix: every type this
      feature generates lives in the global namespace (Phase 0), which is
      exactly what makes "no import needed at all" true rather than merely
      hoped-for. A real cross-namespace consumer test (Phase 2) is what
      actually proves this, not just the design sketch. Do not add a
      global-using back during implementation.

### Phase 2 — End-to-end verification

- [ ] A real packaged-consumer sample (matching `Compono.XunitV3.SampleTests`/
      `Compono.TUnit.SampleTests`' existing pattern) exercising
      `composer.Create<T>()` with a generated double satisfying an
      interface dependency, `[Shared] IRepository` reuse into the SUT, and
      `repository.Configure().Member().Returns(...)`/`.Throws(...)` called
      from the *test* file — the real cross-file case Amendment 2's spike
      verified in isolation, now proven against the actual generator. **The
      sample's test type lives in a real, non-global namespace**
      (Amendment 11 Finding AA) — this is what actually proves
      `Configure()` is reachable with no import, not just the design intent.
      **The composed interface dependency extends a base interface**
      (Amendment 11 Finding Z) — proves the full-closure walk, not just a
      single flat interface.
  - [ ] `dotnet publish -p:PublishAot=true` + real execution against that
        sample — the "prove it, don't assume it" standard `Compono.TUnit`
        (PLAN-0040) already set for this repo, applied here.
- [ ] Public-API-surface approval test for `Compono.TestDoubles` (now a
      much smaller surface post-Amendment-2: just the provider type and
      `UseGeneratedTestDoubles()`), matching
      `Compono.TUnit.Tests.PublicApiSurfaceTests`' pattern. Core `Compono`'s
      own public-API-surface test (if one exists) picks up
      `ReturnConfig<T>`/`ReturnConfigBuilder<T>`/`GeneratedTestDoubleRegistry`.

### Phase 3 — Docs and skill alignment

- [ ] `docs/packages/compono-testdoubles.md` (new Package Guide).
- [ ] `docs/packages/index.md` row.
- [ ] `skills/compono/SKILL.md` detection table + `references/testdoubles.md`
      (new reference file, following `references/nsubstitute.md`'s shape).
- [ ] `docs/roadmap/future-packages.md` — move this entry to shipped once
      the package exists, matching `Compono.TUnit`'s own graduation edit.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` status flips to `Done`.
- [ ] `docs/architecture/current/generated-plans-and-discovery.md`'s "Open
      questions" section gains a fourth item for
      `GeneratedTestDoubleRegistry`, matching `RowInvokerRegistry`'s
      existing collectible-`AssemblyLoadContext`-rooting entry (Amendment 5
      Finding M — identical shape, identical consequence: a plain
      `Type`-keyed dictionary entry has no closed-generic-instantiation
      home-context tie, so it roots its generated factory delegate, and the
      assembly that defined it, for the process's lifetime). Same
      disposition as the existing three items on that page — deferred,
      revisit together if collectible-ALC hosting becomes an actual target.

## Critical Files

- `src/Compono/` — new core primitives: `ReturnConfig<T>`,
  `ReturnConfigBuilder<T>`, `GeneratedTestDoubleRegistry`, `Unit` if not
  already present (ADR-0043 Amendment 2 — moved here from the
  originally-planned `Compono.TestDoubles` to fix a cross-assembly
  reference the generator couldn't otherwise make; all public from the
  start per Amendments 3 and 4).
- Core `Compono`'s packaged build assets (`.props`/`.targets` or
  equivalent) — the `CompilerVisibleProperty` declaration for
  `ComponoGeneratedTestDoubles` (Amendment 4 Finding F) — without this,
  the opt-in silently never activates.
- `src/Compono.Generators/Discovery/LeafTypeClassifier.cs` — the
  compile-time-gated third classification outcome.
- `src/Compono.Generators/Emitters/GeneratedFileNaming.cs` — reused (not
  modified) for the new hash-suffixed collision-safe type names.
- `src/Compono.Generators/` — new generated-code-emission logic: one file
  per discovered interface containing the double, its configuration
  extensions, its `Configure(...)` bridge, and its module-initializer
  registration (ADR-0043 Amendments 1 and 2).
- `src/Compono.TestDoubles/` — new project, deliberately small:
  `GeneratedTestDoubleProvider`, `UseGeneratedTestDoubles()`.
- `test/Compono.Generators.Tests/` — generator-output `Verify()` tests,
  including the "gate off → zero diff" regression test, and a real
  cross-file compile test (generated code in one file, a hand-written
  consumer file calling `Configure()` in another) proving Amendment 2's
  verified shape actually works end-to-end through the real generator, not
  just the standalone spike.
- `test/Compono.TestDoubles.Tests/`, `test/Compono.TestDoubles.SampleTests/` —
  new test projects.

## Test Plan

Matches `references/testing.md`'s existing pattern: `Verify()`-based
generator-output snapshot tests (including the opt-in-off no-op case),
unit tests for `GeneratedTestDoubleProvider`/`ReturnConfigBuilder<T>`/
`GeneratedTestDoubleRegistry` in isolation, a real packaged sample
exercising the full `composer.Create<T>()` path (including cross-file
`Configure()` usage), and a real `PublishAot=true` execution test — not a
claim, a run, per this repo's established AOT-verification standard.

## Notes

Not started. ADR-0043 is `Accepted`; this plan stays `Not Started` until
implementation is explicitly requested.

Pre-implementation review (Codex, PR #82) caught three P1 defects and one
P2 defect in ADR-0043's original design, all corrected via
[ADR-0043 Amendment 2](../adr/0043-compono-generated-test-doubles-design.md#amendment-2-2026-08-13-cross-assembly-bridge-generated-type-collision-safety-coreoptional-package-boundary-corrected-argument-matching-sample-struck)
before any implementation code was written:

1. The runtime provider couldn't reach a lookup generated into the
   consumer's own compilation (same class of cross-assembly defect
   Amendment 1 already fixed once, this time in the opposite direction) —
   fixed by moving the registry into core `Compono`, populated via
   `[ModuleInitializer]`, the same pattern this repo's own TUnit.Mocks
   investigation already proved.
2. The core generator would have needed to hardcode an optional package's
   type shape (`Compono.TestDoubles.ReturnConfigBuilder<T>`) — fixed by
   moving `ReturnConfigBuilder<T>` into core alongside the registry.
3. The original file-scoped-types fix was drafted, then **experimentally
   disproven twice** before landing on the correct shape (`internal` +
   hash-suffixed collision-safe names, reusing `GeneratedFileNaming`) — see
   Amendment 2's own account of both failed attempts (`CS0246`, then
   `CS9051`) so neither gets rediscovered during implementation.
4. An `Arg.Any<Guid>()` sample contradicted the requester's own already-
   decided v1 scope (no argument matchers) — struck; configuration is
   member-level and argument-independent.

A second review pass on Amendment 2's own corrected sketches caught four
more P1s and one P2, corrected via
[ADR-0043 Amendment 3](../adr/0043-compono-generated-test-doubles-design.md#amendment-3-2026-08-13-public-cross-assembly-state-contract-overloadname-collision-diagnostics-documented-multi-assembly-registry-limitation),
still before any implementation code was written:

1. `ReturnConfig<T>`'s fields and `ReturnConfigBuilder<T>`'s constructor
   were `internal`, unreachable from the consumer assembly the generated
   code actually lives in (`CS0122`) — fixed with public readonly
   accessors for reads, a public constructor, mutable state still confined
   to core.
2. `Returns` never set `HasValue`, so every configured return would have
   silently fallen through to the default — fixed.
3. A registry keyed only by `System.Type` breaks if two consumer
   assemblies both generate a double for the same shared interface —
   confirmed with the requester as a documented v1 limitation
   (first-registration-wins, a named diagnostic message on cast failure),
   not a core-engine redesign.
4. The zero-argument configuration-extension shape can't disambiguate
   overloaded interface members — fixed by diagnosing and rejecting
   overloaded members, matching the existing unsupported-shape pattern.
5. An interface declaring its own `Configure` member silently shadows the
   generated bridge (instance members always win over extensions) — fixed
   by diagnosing the collision.

A third review pass caught four more P1s, corrected via
[ADR-0043 Amendment 4](../adr/0043-compono-generated-test-doubles-design.md#amendment-4-2026-08-13-compiler-visible-opt-in-property-retired-stale-global-using-promise-accessible-void-marker-unsupported-return-shape-diagnostics),
still before any implementation code was written:

1. The compile-time opt-in was never declared `CompilerVisibleProperty` —
   without it, `AnalyzerConfigOptionsProvider` never sees a custom MSBuild
   property at all, so the feature could never activate regardless of what
   a consumer sets — fixed by shipping the declaration in core `Compono`'s
   own packaged build assets.
2. Amendment 1's `global using Compono.TestDoubles.Generated;` promise went
   stale the moment Amendment 2 moved every generated type into
   per-interface `internal` types — nothing was left in that namespace to
   import, gate on or off, and the unconditional `global using` was itself
   a compile error — retired entirely, not repaired; no `using` was ever
   actually needed under Amendment 2's design.
3. The `void`-member marker (`Compono.Unit`) was missed by Amendment 3's
   own cross-assembly-accessibility fix — introduced `public` from the
   start instead.
4. The unsupported-shape diagnostic list covered parameter modifiers but
   not return shapes (`Span<T>`-like ref-like returns, by-ref-returning
   members, pointers, function pointers) — added.

A fourth review pass caught two more P1s and two P2s, corrected via
[ADR-0043 Amendment 5](../adr/0043-compono-generated-test-doubles-design.md#amendment-5-2026-08-13-identifier-safe-type-names-non-nullable-reference-return-diagnostic-object-member-shadowing-diagnostic-documented-alc-rooting),
still before any implementation code was written:

1. The generated type names weren't valid C# identifiers — `HintNameFor`
   deliberately preserves dots (correct for file names, wrong for type
   names) — fixed with a distinct identifier-specific sanitizer, still
   hashing the original fully-qualified name for the collision-safe
   suffix.
2. Non-nullable reference returns had no deterministic default at all —
   confirmed with the requester as diagnose-and-reject, not an attempt at
   real composition.
3. A configuration extension can be shadowed by an inherited `object`
   member (`GetHashCode`/`ToString`/`Equals`/`GetType`), the same class of
   bug as the earlier `Configure()` collision — fixed by diagnosing it too.
4. `GeneratedTestDoubleRegistry` roots a collectible `AssemblyLoadContext`,
   the same documented consequence this repo already accepts for
   `RowInvokerRegistry` — added as a Phase 3 doc task (not made now, since
   the registry doesn't exist yet and `docs/architecture/current/*.md`
   describes only shipped behavior).

A fifth review pass caught two more real gaps in Amendment 5's own fixes
plus a stale cross-reference, corrected via
[ADR-0043 Amendment 6](../adr/0043-compono-generated-test-doubles-design.md#amendment-6-2026-08-13-object-collision-check-compares-generated-signatures-keyword-escaped-member-names),
still before any implementation code was written:

1. Amendment 5's `object`-collision check compared the *interface
   member's* declared signature — but every configuration extension is
   always zero-argument (Amendment 2 Finding 4), so the check needs to
   compare the *generated* signature instead: `int ToString(int format)`
   wasn't flagged but should have been (its generated extension collides);
   `Equals(object)` was flagged but shouldn't have been (its generated
   extension doesn't).
2. Amendment 5 added identifier escaping for generated type names but not
   member names — an interface member like `@new` needs the same
   treatment, reusing this repo's existing
   `RequiredMemberCollector.EscapeIdentifier` convention.

`future-packages.md`'s "two Amendments" reference was also corrected to
avoid hardcoding a count that will keep going stale.

A sixth review pass caught one genuine undecided design question and one
clean bug, corrected via
[ADR-0043 Amendment 7](../adr/0043-compono-generated-test-doubles-design.md#amendment-7-2026-08-13-property-accessor-semantics-decided-last-configuration-wins),
still before any implementation code was written:

1. Read/write properties were never actually specified — every generated-
   code sketch only covered methods, and properties were neither diagnosed
   as unsupported nor given a decided accessor contract. Confirmed with
   the requester over two alternatives (unsupported-for-v1, getter-only
   with a no-op setter): real auto-property semantics — the setter stores,
   the getter returns what was last set (or the default), `Returns`/
   `Throws` still work as an explicit override.
2. Repeated configuration left stale state — `Returns` after an earlier
   `Throws` on the same member was silently ignored, since `Returns` never
   cleared the exception and dispatch checks it first — fixed with
   last-configuration-wins semantics (each setter now clears the other's
   state).

A seventh review pass caught one repeat of an already-fixed defect class
and one real gap this repo already has precedent for closing, corrected via
[ADR-0043 Amendment 8](../adr/0043-compono-generated-test-doubles-design.md#amendment-8-2026-08-13-property-setter-routed-through-the-public-builder-inaccessible-interface-diagnostic),
still before any implementation code was written:

1. Amendment 7's property setter directly mutated `ReturnConfig<T>`'s
   `internal` fields — the exact cross-assembly `CS0122` defect Amendment 3
   fixed for reads, reintroduced for writes because Amendment 7 conflated
   "the struct instance is stored in the consumer's generated file" with
   "the struct's type is defined in that same assembly" (it isn't —
   `ReturnConfig<T>` is core). Fixed by routing the setter through the
   existing public `ReturnConfigBuilder<T>` constructor + `Returns` method
   instead — no new core API.
2. No diagnostic existed for an interface inaccessible to a top-level
   generated type (a `private`/`protected` nested interface) — fixed by
   reusing the exact `Compilation.IsSymbolAccessibleWithin` check this repo
   already applies identically to generated collection plans and
   row-invoker registrations.

An eighth review pass caught two more real gaps, both extensions of
already-decided mechanisms rather than new forks, corrected via
[ADR-0043 Amendment 9](../adr/0043-compono-generated-test-doubles-design.md#amendment-9-2026-08-13-preserve-initget-only-accessor-kind-escape-member-names-in-the-explicit-interface-implementation-too),
still before any implementation code was written:

1. `init` accessors weren't preserved — Amendment 7's property design
   assumed `{ get; set; }` uniformly, but `init` and `set` are
   non-interchangeable; a `{ get; init; }` interface property would have
   failed to be implemented. Fixed by preserving whichever accessor kind
   the interface actually declares, routing `init` through the same
   `ReturnConfigBuilder<T>.Returns` call the setter already uses.
2. Keyword escaping (Amendment 6) covered the configuration extension's
   member name but not the explicit interface implementation's — `int
   IFoo.new()` was still invalid. Fixed by applying the same escaping at
   every site a member name is emitted, not just the one Amendment 6
   happened to fix first.

A ninth review pass caught one shape never considered and two more
instances of "escape/diagnose every emission site," corrected via
[ADR-0043 Amendment 10](../adr/0043-compono-generated-test-doubles-design.md#amendment-10-2026-08-13-set-only-properties-diagnosed-parameter-names-escaped-unsafe-parameter-shapes-diagnosed),
still before any implementation code was written:

1. Set-only properties (`int Value { set; }`) were never specified —
   confirmed with the requester as diagnose-and-reject, since v1's already-
   decided lack of call recording/verification means nothing could ever
   observe a value written through one; there's no meaningful behavior to
   give it.
2. Explicit-implementation method parameter names were never escaped —
   `void Save(int @class)` would still emit an invalid bare `class`
   parameter. Fixed by extending the same escaping convention to
   parameters.
3. Unsafe pointer/function-pointer parameter shapes were never diagnosed,
   only the return-side equivalent was (Amendment 4) — fixed by adding the
   parameter-side counterpart to the same diagnostic list.

A tenth review pass caught two structural gaps — more fundamental than the
escaping/diagnostic refinements the previous several rounds had converged
on — corrected via
[ADR-0043 Amendment 11](../adr/0043-compono-generated-test-doubles-design.md#amendment-11-2026-08-13-walk-the-full-base-interface-closure-place-generated-types-in-the-global-namespace),
still before any implementation code was written:

1. Interface inheritance was never addressed — every sketch discovered
   only a leaf interface's own declared members, never its base
   interfaces (`IChild.GetMembers()` doesn't return what `IChild : IBase`
   inherits from `IBase`). Fixed by walking the full transitive
   base-interface closure (`AllInterfaces`), with every diagnostic already
   decided applying across that closure too, and inherited-member explicit
   implementations qualified against the interface that actually declares
   them.
2. No namespace was ever decided for the generated types, and Amendment 4's
   retirement of the global-using injection only holds if they're
   universally visible without one — fixed by placing every generated type
   in the global namespace, which is what actually validates (not just
   assumes) Amendment 4's "no import needed" reasoning.

**This closes the pure pre-implementation design-review loop.** Ten review
rounds surfaced real, load-bearing defects across ADR-0043 and its
Amendments — confirmed directly with the requester after this round that
severity had shifted from structural (Amendments 2-3, this round) toward
narrower edge-case escaping/diagnostic coverage (Amendments 5-10), and
that further refinement continues during actual implementation instead,
where `tasks/implement.md`'s build/test/PR-review cycle surfaces and
resolves remaining gaps empirically against real generated code rather
than through further prediction against a design that doesn't compile
anything yet.

This plan's task list above already reflects the fully-corrected shape.

## Phase 0 implementation notes (2026-08-13)

Phase 0 is implemented and every task above is checked off: core primitives
(`ReturnConfig<T>`/`ReturnConfigBuilder<T>`/`GeneratedTestDoubleRegistry`/`Unit`),
the packaged `CompilerVisibleProperty` opt-in, `LeafTypeClassifier`'s third
outcome threaded through `TransitiveClosureWalker` via a new `WalkContext`
(introduced to keep `EnqueueRoot`/`EnqueueMember`'s own parameter lists from
growing further — a fourth discovery kind alongside types/collections needed
somewhere to live), `TestDoubleAnalyzer` (fail-fast, one diagnostic per
interface leaf, matching `RequiredMemberCollector`/`ConstructorSelector`'s
existing convention), `TestDoubleDefaults`, `TestDoubleIdentifierNaming`, and
`TestDoubleEmitter` + `TestDouble.scriban`. Real end-to-end `Verify()` tests
(`TestDoubleVerifyTests`) prove: the opt-in-off zero-diff regression, a real
generated double actually compiling, `Configure()` reachable from a different
namespace with no `using` (Amendment 11's global-namespace claim), and five
of the diagnostics (event, `Configure` collision, set-only property, overload,
non-nullable-reference return). Full solution build + 1719-test run (every
project, both this feature's own tests and every pre-existing test) is green.

Two things this pass deliberately left for empirical follow-up rather than
designing further ahead of real feedback, per this plan's own closing
decision to move pre-implementation prediction into real build/test/PR-review:

- **Diagnostic test coverage is representative, not exhaustive** — one or two
  tests per diagnostic *category* (member-kind, collision, return-shape), not
  one per every shape Amendment 3–10 individually named (e.g. `ref`/`out`/`in`
  parameters, pointer/function-pointer returns, and `init`-accessor
  preservation each have analyzer logic but no dedicated `VerifyFailure` test
  yet — static abstract properties/operators do now, added during PR #83
  review round 1 below). The analyzer logic itself directly mirrors each
  Amendment's decided shape.
- **Same interface discovered from two call sites with two different,
  disagreeing diagnostics**: the merge step in `ComponoIncrementalGenerator`
  (`discoveredTestDoubles`) takes `group.Distinct().First()` rather than
  preserving every distinct failure at its own location, unlike
  `DiscoveredCollectionInfo`'s/`DiscoveredTypeInfo`'s own conflict-preserving
  merge. Low-impact (only matters if the same interface is independently
  reached from two request sites where the interface's *own* shape differs
  in accessibility between them, which it structurally can't), but worth
  tightening to match the existing pattern if Phase 2's real sample ever
  exercises it.

## PR #83 review round 1 (2026-08-13)

Codex caught five real gaps, all fixed before merge:

1. **(P1)** The Phase 0 notes above claimed verification but every check ran
   through the in-process generator test harness only, never the packaged
   `.nupkg` itself — an incorrectly packaged `build/Compono.props` (wrong
   `PackagePath`, missing from the `.nuspec`, whatever) could ship invisible
   and every existing test would still pass, since none of them go through
   real NuGet restore. Fixed by actually doing it: `dotnet pack` on core
   `Compono`, a throwaway consumer project referencing the packed `.nupkg`
   from a local feed with `<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>`,
   real `dotnet restore` + `dotnet build`. This caught a real, if
   environment-local, failure mode along the way: a stale global NuGet cache
   entry for a previously-restored `Compono 1.0.0` silently shadowed the
   newly packed content (NuGet trusts a cached id+version pair without
   re-inspecting bytes) — clearing `~/.nuget/packages/compono/1.0.0` and
   re-restoring produced the real `<Import Project="...buildTransitive/Compono.props">`
   and the property became visible. `IRepository_<hash>.TestDouble.g.cs` was
   generated for real, through the real packaged analyzer. (The subsequent
   `CompositionException` at runtime is expected and correct — Phase 1's
   `GeneratedTestDoubleProvider` doesn't exist yet.) `.github/scripts/inspect-packed-nupkgs.sh`
   also needed its own fix here (unrelated to Codex, caught by this PR's own
   CI): its hardcoded expected-file-listing allowlist didn't yet know about
   `build/Compono.props`/`buildTransitive/Compono.props`.
2. **(P2)** The overload/collision pre-pass only considered `IMethodSymbol`,
   so two same-named properties inherited from different base interfaces (a
   diamond shape) both passed through un-diagnosed and would have emitted
   the same backing field and configuration extension twice — a duplicate-
   member compile error instead of the intended `CMP0022`. Fixed by folding
   properties into the same duplicate-name pre-pass methods already used.
3. **(P2)** A static abstract property was silently skipped (`if (property.IsStatic) continue;`
   with no `IsAbstract` check), leaving the double failing to implement it
   (`CS0535`) instead of getting the `CMP0021` diagnostic every other
   unsupported shape gets. Fixed — mirrors the method-side static-abstract
   check that already existed.
4. **(P2)** A static abstract *operator* has `MethodKind.UserDefinedOperator`,
   not `Ordinary` — the existing `MethodKind: not Ordinary → continue` filter
   ran before the static-abstract check ever saw it, silently dropping it the
   same way. Fixed by moving the static-abstract check ahead of the
   `MethodKind` filter (excluding property/event accessor `MethodKind`s, so
   a static abstract property's own diagnostic still names the property, not
   its accessor method).
5. **(P2)** The `object`-member collision check (`ToString`/`GetHashCode`/`GetType`)
   was only applied to methods — a property with one of those names silently
   lost its `Configure()` surface to the inherited `object` member instead of
   getting `CMP0024`. Fixed by applying the same check to properties.

Four new `VerifyFailure` regression tests cover findings 2–5 directly (the
diamond-property collision, both static-abstract shapes, and the property-
side object collision) — `TestDoubleVerifyTests` is now 11 tests, all green
on both target frameworks.

Also fixed in this round, required by this PR's own CI rather than by Codex:
`AnalyzerReleases.Unshipped.md` needed entries for `CMP0020`-`CMP0027`
(Roslyn's release-tracking analyzer requires every declared diagnostic ID to
be listed), a handful of missing `<param>` XML doc tags on the new model
records, and `docs/reference/api/` needed regenerating for the new public
`Compono.ReturnConfig<T>`/`ReturnConfigBuilder<T>`/`GeneratedTestDoubleRegistry`/`Unit` surface.

## PR #83 review round 2 (2026-08-13)

Codex caught five more real gaps in `TestDoubleDefaults`/`TestDoubleAnalyzer`,
all fixed:

1. **`ValueTask<T>`/`ValueTask` are themselves structs**, so the generic
   `type.IsValueType → default` fallback fired before the `ValueTask`-specific
   branch further down the method ever ran - `ValueTask<string>` silently
   returned a `ValueTask` wrapping `null` instead of either the deterministic
   default for `string`'s own shape or the non-nullable-reference diagnostic.
   Fixed by moving the `Task`/`ValueTask` checks ahead of the generic
   value-type fallback.
2. **A nullable-annotated collection (`List<int>?`, `int[]?`) hit the
   nullable-reference fallback first** and returned `null`, contradicting
   "empty collections never null." Fixed by moving the collection-shape
   checks ahead of the nullable-reference fallback - a nullable-annotated
   collection now gets `[]` same as a non-nullable one.
3. **A multi-dimensional array (`int[,]`) matched the same `IArrayTypeSymbol → []`
   branch as an ordinary array**, but C# collection expressions only target
   rank-1 arrays - the generated double would have failed to compile. Fixed
   by restricting the `[]` default to `Rank: 1` and falling through to the
   unsupported-return-shape diagnostic otherwise.
4. **A private default-implemented interface method** (`private int Helper() => 1;`,
   a C# 8+ default interface member) was only excluded by the *static* check,
   not by accessibility - a private (or otherwise non-public) instance
   default member isn't part of any implementing type's contract and can't
   be explicitly implemented at all, so the double failed to compile. Fixed
   by skipping any non-abstract, non-public member (both methods and
   properties, for the same reason).
5. **The `Configure`-name collision check was name-only, not arity-aware.**
   Verified directly with a real compile spike before fixing (not taken on
   faith): an interface's own `Configure(int mode)`, explicitly implemented
   on a concrete type, alongside a zero-argument `Configure(this IFoo)`
   extension - `foo.Configure()` on an `IFoo`-typed receiver resolves to the
   extension without ambiguity or error. C# only falls back to extension-method
   resolution when ordinary member lookup finds no *applicable* candidate,
   not merely "no candidate with this name," so a differently-shaped
   `Configure` member never actually shadows the bridge. The blanket
   name-only check over-rejected valid interfaces. Fixed to flag a collision
   only when the interface's own `Configure` member is non-method (property/
   field/event - always collides, since member lookup never falls back to
   extensions for a non-method name at all) or a zero-parameter method.

Six new tests added (one Verify() golden-path test for finding 5's fix
doubled as proof both `Configure()` extensions - the bridge and the member's
own config extension - coexist without ambiguity, since they have different
receiver types). `TestDoubleVerifyTests` is now 16 tests, all green on both
target frameworks. Full solution: 1945/1945 tests pass.

## PR #83 review round 3 (2026-08-13)

A real `dotnet publish -p:PublishAot=true` verification (against the packed
`Compono` `.nupkg`, driving a generated double directly through
`GeneratedTestDoubleRegistry`/`Configure()` since Phase 1's runtime provider
doesn't exist yet) confirmed the AOT-safety claim empirically, not just by
inspection: zero `IL2xxx`/`IL3xxx` trim/AOT-analyzer warnings during native
code generation, and the published native binary ran standalone and passed.
Not a substitute for Phase 2's own real end-to-end `PublishAot` test against
the full sample (still unchecked in the Phase 2 task list above) - this was
scoped narrowly to "does the generated-code-and-core-primitives path itself
survive AOT," which is exactly the risk surface Phase 0 introduced.

Codex caught four more real gaps:

1. **(P1, docs)** `AGENTS.md`/`coding-standards.md`'s "every generator-
   emitted type is `file`-scoped" rule was never updated to record ADR-0043's
   own exception (test-double types reference each other across signatures,
   which `file`-scoping breaks with `CS9051` - already proven twice during
   design review). Left unchanged, a future change following that stale
   blanket rule would "fix" this back into a compile error. Documented the
   exception in both `AGENTS.md` and `references/coding-standards.md`'s
   "Generated code" section.
2. **(P2)** `TransitiveClosureWalker`'s `VisitedTestDoubleInterfaces` used
   `SymbolEqualityComparer.Default`, not `IncludeNullability` like the
   adjacent `VisitedTypes` field - `IProvider<string>` and `IProvider<string?>`
   collapsed to whichever was discovered first, silently deciding (by
   traversal order) whether the double was rejected or emitted with a
   possibly-wrong default. Fixed to `IncludeNullability`, matching
   `VisitedTypes`. This alone would have turned the bug into a worse one - a
   duplicate `AddSource` hint-name crash - since `ToDisplayString(FullyQualifiedFormat)`
   doesn't include nullable annotations either (verified directly with a
   real compile spike before touching anything: `IProvider<string>` and
   `IProvider<string?>` both display as `global::IProvider<string>`). Fixed
   properly by mirroring `DiscoveredTypeInfo`'s own `CMP0010` conflict-merge
   pattern exactly: `ComponoIncrementalGenerator`'s `discoveredTestDoubles`
   merge now groups by emission identity, passes through real per-location
   diagnostics when any exist (so two discoveries that disagree - one fails,
   one would succeed - now deterministically report the real failure,
   instead of an order-dependent silent pick), and only synthesizes the new
   `CMP0028` when every surviving entry succeeded but still disagrees
   structurally.
3. **(P2)** `HashSet<T>` was missing from `TestDoubleDefaults`'s known-
   collection-shapes whitelist - a member returning `HashSet<int>` was
   wrongly rejected (`CMP0025`) instead of getting `[]`. Added.
4. **(P2)** The overload/duplicate-name pre-pass in `TestDoubleAnalyzer`
   counted members the main emission loop already silently skips (a private
   or non-abstract-static default-interface member) - a public `Get()`
   sharing a name with an unrelated private default `Get()` helper falsely
   tripped `CMP0022` even though only one of them would ever generate
   anything. Fixed by filtering the pre-pass to the same instance-contract
   eligibility the emission loop already applies.

Four new tests (one for each of findings 2-4; finding 1 is docs-only). Full
solution: 1951/1951 tests pass.

Phase 1 (`Compono.TestDoubles` runtime package) is next.
