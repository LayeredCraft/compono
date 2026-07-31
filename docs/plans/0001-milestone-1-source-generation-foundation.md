# [PLAN-0001] Milestone 1: Source-Generation Foundation

**Status:** Done

**Implements:** [ADR-0002](../adr/0002-constructor-selection-algorithm.md) (constructor selection), [ADR-0003](../adr/0003-generator-package-distribution.md) (generator package distribution), [ADR-0004](../adr/0004-composition-plan-discovery-and-dispatch.md) (discovery/dispatch), [ADR-0005](../adr/0005-generator-implementation-conventions.md) (implementation conventions)

## Goal

`var customer = composer.Create<Customer>();` uses a generated
`ICompositionPlan<T>` — not runtime reflection — for a representative
record or class, including one with nested composable properties, per
`docs/mvp.md`'s Milestone 1 exit criteria.

## Scope

Per `docs/mvp.md`'s Milestone 1 section, mapped onto ADR-0004/0005's
concrete shape:

- The `Compono.Generators` project itself (ADR-0003's shape).
- Discovery of constructible source types, including the transitive
  closure of a discovered type's constructor parameter types (ADR-0004).
- Constructor selection (ADR-0002).
- Generated direct constructor invocation.
- Generated request metadata (the compile-time shape a generated plan
  needs to ask `ICompositionContext` for a value).
- A plan registration mechanism (`PlanCache<T>` + generated module
  initializer, ADR-0004).
- Compile-time diagnostics for unsupported or ambiguous construction.
- A benchmark harness comparing generated construction against a
  reflection baseline.

Explicitly deferred (later milestones per `docs/mvp.md`):

- `CreateMany<T>()` — this is Milestone 2 scope, not Milestone 1
  (`docs/mvp.md`'s Milestone 2 "Scope" list includes it explicitly).
  Milestone 1 only needs to make `Create<T>()` work.
- The full `CompositionContext`/provider resolution pipeline, built-in
  primitive generation, and everything else under Milestone 2's "Core
  Composition Engine" heading.
- The disambiguation attribute ADR-0002 calls out as a future escape
  hatch for genuinely multi-constructor types is in scope for *this*
  milestone (see Phase 4 below) but is its own slice, not required for
  the single-constructor happy path other phases depend on.
- `Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus` — separate
  milestones (4, 5, 6).

## Phases

Ordered so each phase either unblocks the next or closes a real gap
between the current state and the exit criteria. A phase's tasks are
checked off as work proceeds, same as before — the phase grouping is
what's new, so the whole remaining shape of the milestone stays visible
at a glance instead of living only in this file's edit history.

**Each phase ships as its own PR** (`design-decisions.md`'s "Writing a
Plan" section) — don't bundle two phases into one diff even if both are
finished by the time a PR gets opened.

### Phase 0 — Foundation (Done)

The generator exists, is wired into `Compono`'s own package, and can
discover a flat (no nested composable parameters) type from a
`Create<T>()` call site, select its constructor, emit a plan, and
register it into `PlanCache<T>`.

- [x] Create generator project (`src/Compono.Generators`, ADR-0003's
      shape; analyzer-only `ProjectReference` from `Compono.csproj`,
      confirmed packing into `Compono`'s own nupkg)
- [x] `WellKnownTypes` symbol cache (ADR-0005) — `Compono.Composer` so far
- [x] Define generated-plan contract: `ICompositionPlan<T>`, the minimal
      Milestone-1-only `ICompositionContext`, `PlanCache<T>`
- [x] Discover `Create<T>()` call sites, resolve closed generic `T`
- [x] Apply ADR-0002's constructor selection rule + ambiguity diagnostic
      (`CMP0001` ambiguous, `CMP0002` no accessible constructor)
- [x] Generate plan registration: `PlanCache<T>` + generated module
      initializer
- [x] Scriban template for plan-class assembly (ADR-0005)
- [x] Generator snapshot tests (`Compono.Generators.Tests`,
      `Verify.SourceGenerators`/`Verify.XunitV3` — happy path +
      ambiguous-constructor diagnostic)

### Phase 1 — Transitive closure (Done)

The real gap between "works for a flat type" and the exit criteria's
"representative record or class" — a `Customer` with an `Address`
property needs `Address` to get its own generated plan too, not just a
`context.Resolve<Address>()` call with nothing behind it yet.

**Recursion-boundary rule, confirmed with the user before implementing**:
recurse into a constructor parameter type (give it its own generated
plan) when it's a *concrete type eligible for generated composition* —
implemented as `LeafTypeClassifier.IsProviderResolved`, which leaves a
parameter as a bare `context.Resolve<TParam>()` call only for
interfaces, abstract types, delegates, enums, the built-in C# simple
types (`bool`/`byte`/.../`string`/`nint`/`nuint`), and a handful of BCL
value types with no meaningful composable constructor shape
(`DateTime`, `DateTimeOffset`, `Guid`, `TimeSpan`). A concrete type that
*is* eligible but fails constructor selection (ambiguous, no accessible
constructor, unsupported parameter kind, unassigned required members)
is diagnosed at the original `Composer.Create<T>()` call site instead
of silently left as `Resolve<TParam>()` — the diagnostic message names
the traversal path (e.g. `'Address' (reached via Customer.HomeAddress)
has 2 accessible constructors...`) so a nested failure isn't reported
with no context about where in the graph it came from.

- [x] Confirm the recursion-boundary rule above with the user before
      implementing (or replace it with whatever's actually decided)
- [x] Walk each discovered type's constructor parameter types
      recursively, applying that rule (`TransitiveClosureWalker`)
- [x] Dedupe the resulting set (a type reachable from two different
      parents still gets exactly one generated plan) — a
      `SymbolEqualityComparer`-backed visited set in the BFS walk
- [x] Emit a generated plan + `PlanCache<T>` registration for every type
      in the closure, not just the top-level requested type
- [x] Snapshot test: a type with a nested composable property (e.g.
      `Customer(string Name, Address HomeAddress)`) produces plans for
      *both* `Customer` and `Address`
      (`NestedComposableProperty_GeneratesPlansForBothTypes`), plus
      dedup-across-parents, leaf-type non-recursion, and nested-failure
      diagnostic coverage

### Phase 2 — Escape-hatch attribute (Done, PR #6 merged)

ADR-0004's second discovery path: a type that needs a plan but has no
local `Create<T>()` call site in the compilation.

**Attribute decision, confirmed with the user before implementing**:
`Compono.ComposableAttribute` (`[Composable]`), framed as an opt-in
marker rather than a fallback — the normal rule stays "reachable from a
`Create<T>()` call site gets a plan automatically," and `[Composable]`
exists for the cases discovery can't reach on its own. Two placements,
both equivalent plan-generation requests deduplicated alongside call-site
discovery: `[Composable]` directly on a type declaration this compilation
owns (preferred whenever the type is owned locally), and
`[assembly: Composable(typeof(SomeType))]` for a type in a referenced
assembly that can't be annotated directly. `[Composable(typeof(Other))]`
on a type declaration is also accepted as a request for `Other` rather
than the annotated type itself, for symmetry with the assembly-level form.

- [x] Decide the attribute's name/namespace/shape (`AskUserQuestion`,
      confirmed above)
- [x] Discover attributed types alongside call-site-discovered ones,
      feeding into the same constructor-selection/emission pipeline
      (`ComposedTypeAnalyzer` factors the shared closed-type validation +
      `TransitiveClosureWalker` hand-off out of `CreateInvocationDiscovery`
      so both discovery paths funnel through identical validation;
      `ComposableAttributeDiscovery` covers both placements —
      `ForAttributeWithMetadataName` for the type-level form,
      `CreateSyntaxProvider` over `[assembly: ...]` attribute lists for the
      assembly-level form, since `ForAttributeWithMetadataName` only
      matches attributes on declarations)
- [x] Snapshot test: an attributed type with no local `Create<T>()`
      call site still gets a generated plan (type-level and
      assembly-level cases, plus a dedupe case where both a call site and
      `[Composable]` discover the same type, plus `CMP0008` for
      assembly-level `[Composable]` missing its `typeof(...)` argument)

### Phase 3 — Required members and nullability (Done, PR #7 merged)

Design decided in [ADR-0006](../adr/0006-required-members-and-nullability-metadata.md):
object-initializer emission for required members (reusing the constructor
parameter pipeline), and a new `Nullability` enum parameter on
`ICompositionContext.Resolve<TValue>()` carrying each request's
nullable-annotation, since generic type arguments erase that information
at runtime.

- [x] Add `Compono.Nullability` enum (`NotNullable`/`Nullable`) and change
      `ICompositionContext.Resolve<TValue>()` to `Resolve<TValue>(Nullability
      nullability)`
- [x] `ConstructorParameterInfo` gains nullability; a new
      `RequiredMemberInfo` model (name, fully-qualified type, nullability)
- [x] `ConstructorSelector.HasUnassignedRequiredMembers` → collect
      required members instead of rejecting the type; reuse
      `ValidateParameterKinds`-style checks + `TransitiveClosureWalker`/
      `LeafTypeClassifier` per required-member type (new
      `RequiredMemberCollector`, since the collection now returns data on
      success instead of only ever failing)
- [x] `CompositionPlanEmitter`/`CompositionPlan.scriban` emit an object
      initializer block after the constructor call; every `Resolve<...>()`
      call site (constructor args and required members) passes an explicit
      `Nullability` argument
- [x] Narrow `CMP0007` per ADR-0006 rather than removing it
- [x] Snapshot tests: required properties, required fields, a
      `[SetsRequiredMembers]` constructor (no initializer emitted),
      nullable and non-nullable constructor parameters, nullable and
      non-nullable required members, a ref-like required member (CMP0007),
      and a required member reaching a composable nested type (recursion
      through required members, not just constructor parameters)

### Phase 4 — Benchmark harness (Done)

- [x] Add a benchmark project comparing generated construction against
      a reflection baseline (`docs/mvp.md`'s explicit Milestone 1 ask)
- [x] Record a baseline result somewhere durable (this plan's Notes, or
      a `docs/*.md` subsystem doc once one exists for the generator)

### Phase 5 — Close-out (Done)

- [x] Real manual verification against an actual consuming project (not
      just the test suite) — per `tasks/implement.md`'s "real manual
      verification for anything source-generator-facing" step. The
      original wording here ("call `composer.Create<Customer>()` for a
      representative nested type, confirm the call resolves to generated
      code") assumed nested construction fully succeeds - it doesn't yet,
      `PlaceholderCompositionContext.Resolve<TValue>()`
      (`src/Compono/Composer.cs`) unconditionally throws
      `NotSupportedException`, so that call was always going to throw. See
      Notes for what was actually verified instead, and why that's still
      the real proof the exit criteria needs.
- [x] Update `docs/architecture.md`/`docs/mvp.md` to reflect current
      (not just intended) state now that code exists - reviewed both;
      neither asserts anything false about Milestone 1's current state
      (`docs/architecture.md`'s "Open Architectural Decisions" already
      tracks resolved-vs-open items with strikethrough per ADR, and
      `docs/mvp.md` is scope/exit-criteria language, not a status
      tracker - that's this plan's job). No edits needed.
- [x] Set this plan's `Status: Done`

## Critical Files

- `src/Compono.Generators/` — the generator project (ADR-0003):
  `ComponoIncrementalGenerator.cs` (pipeline entry point — merges
  call-site, type-level `[Composable]`, and assembly-level
  `[Composable]` discovery before dedup/emission),
  `Discovery/CreateInvocationDiscovery.cs` (call-site discovery) +
  `ComposableAttributeDiscovery.cs` (Phase 2: both `[Composable]`
  placements) + `ComposedTypeAnalyzer.cs` (Phase 2: the closed-type
  validation + `TransitiveClosureWalker` hand-off shared by every
  discovery path) + `ConstructorSelector.cs` + `TransitiveClosureWalker.cs`
  (Phase 1's recursive parameter-type walk, Phase 3: also walks required
  members) + `LeafTypeClassifier.cs` (Phase 1's recurse-vs-`Resolve<T>()`
  rule) + `RequiredMemberCollector.cs` (Phase 3: collects/validates
  required properties/fields not satisfied by `[SetsRequiredMembers]`),
  `WellKnownTypes/` (symbol cache, vendored `BoundedCacheWithFactory`),
  `Diagnostics/` (`DiagnosticDescriptors`, `DiagnosticInfo`),
  `Models/` (`DiscoveredTypeInfo`, `ConstructorParameterInfo`,
  `RequiredMemberInfo` (Phase 3), `LocationInfo`),
  `Types/EquatableArray.cs` (vendored from `LayeredCraft.SourceGeneratorTools`,
  not referenced as a package — see attribution comment), `Emitters/`
  (`TemplateHelper`, `CompositionPlanEmitter`),
  `Templates/CompositionPlan.scriban` (Phase 3: emits an object-initializer
  block and an explicit `Nullability` argument on every `Resolve<...>()`
  call), `AnalyzerReleases.{Shipped,Unshipped}.md`.
- `src/Compono/` — `ICompositionPlan.cs`, `ICompositionContext.cs`
  (Milestone-1-only placeholder; Phase 3 changed
  `Resolve<TValue>()` to `Resolve<TValue>(Nullability nullability)`),
  `Nullability.cs` (Phase 3), `PlanCache.cs`, `Composer.cs` (minimal
  placeholder entry point), `ComposableAttribute.cs` (Phase 2's opt-in
  discovery marker). `Compono.csproj` — analyzer-only `ProjectReference`
  to `Compono.Generators`, packs its output into `analyzers/dotnet/cs`
  (Scriban is source-embedded into `Compono.Generators.dll`, so no
  separate DLL to pack for it).
- `Directory.Packages.props` — `Microsoft.CodeAnalysis.CSharp`/`Analyzers`
  (generator-only, private), `Scriban` (source-embedded via
  `PackageScribanIncludeSource`), `Meziantou.Polyfill`/`Microsoft.CSharp`
  (netstandard2.0 language/BCL polyfills, matching `dynamo-mapper`'s
  pattern), `Verify.SourceGenerators`/`Verify.XunitV3`/
  `Basic.Reference.Assemblies.Net100`/`Net110` (generator testing).
- `Compono.slnx` — `Compono.Generators`, `Compono.Generators.Tests`.
- `test/Compono.Generators.Tests/` — `GeneratorTestHelpers.cs` (drives
  the generator via `CSharpGeneratorDriver`, asserts generated code
  actually compiles, not just that it snapshots),
  `CompositionPlanVerifyTests.cs`, `Snapshots/*.verified.{cs,txt}`.
- `benchmarks/Compono.Benchmarks/` (Phase 4) — `BenchmarkDotNet` project,
  referencing `Compono.Generators` as an analyzer-only `ProjectReference`
  (same shape `test/Compono.Generators.Tests` and `Compono.csproj` itself
  use) so the generator actually runs against the types declared here.
  `BenchmarkTypes.cs` (the representative `Leaf` type), `ReflectionComposer.cs`
  (the reflection baseline), `AutoFixtureComposer.cs` (ecosystem-comparison
  reference point, added after initial Phase 4 completion at the user's
  request — see Notes), `ArchitectureBenchmarks.cs` (Direct/Generated/
  Reflection — the architecture question) + `EcosystemBenchmarks.cs`
  (Generated/AutoFixture — the ecosystem question, kept as a separate
  `[Benchmark]` class so it can't be mistaken for the architecture's
  success criterion), `Program.cs` (`BenchmarkSwitcher.FromAssembly`).

## Test Plan

- Generator snapshot tests (`Verify.SourceGenerators`) for every phase —
  each phase above lists its own representative cases.
- Every snapshot test asserts the generated code actually compiles back
  into the original compilation (`GeneratorTestHelpers.Verify`'s existing
  behavior), not just that it matches saved text.
- Phase 5's real, manual, build-a-consuming-project verification is the
  actual exit-criteria proof — the milestone isn't `Done` on green tests
  alone, per `tasks/implement.md`.

## Notes

Implementation history and lessons learned, kept for context — not a
task list; see **Phases** above for what's actually left to do.

- **PR #4 review feedback (Codex) surfaced six real Phase 0 bugs**, all
  fixed in the same PR rather than deferred, per `tasks/respond-to-pr-feedback.md`:
  - `CompositionPlanEmitter` used to hint files by simple `TypeName` alone
    — two same-simple-name types in different namespaces (`Sales.Customer`/
    `Support.Customer`) collided, and Roslyn requires unique hint names per
    generator run. Now derived from a sanitized `FullyQualifiedName`.
  - The Scriban template always wrapped output in a `namespace { }` block.
    A type with no namespace produced invalid C#. Worth remembering:
    `INamedTypeSymbol.ContainingNamespace.ToDisplayString()` returns the
    **literal string `"<global namespace>"`**, not an empty string, for a
    type with no namespace — `IsGlobalNamespace` is the actual check; this
    cost a debugging detour since the wrong assumption looked plausible
    and the template itself rendered fine in isolation.
  - `ConstructorSelector` filtered by `Accessibility.Public or Internal`,
    which doesn't account for cross-assembly visibility — an `internal`
    constructor on a type from a referenced assembly, with no
    `InternalsVisibleTo` grant, isn't actually callable from generated
    code living in a different assembly. Now uses
    `compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly)`,
    which correctly implements real C# accessibility-domain rules.
  - `ConstructorSelector` didn't reject abstract types — an abstract class
    with exactly one public constructor (legal, called only by derived
    classes) was reported as successfully selected, and the template
    emitted `new AbstractType(...)`, which is never legal C#. Added
    `CMP0003` and an `IsAbstract` check ahead of constructor selection.
  - `DiagnosticInfo.Equals`/`GetHashCode` only compared `Descriptor.Id` and
    `Location`, ignoring `MessageArgs` — an ambiguous type's constructor
    count changing (2 → 3) with the same location/descriptor read as
    "unchanged" to Roslyn's incremental caching, keeping a stale message.
    Now includes `MessageArgs` via `SequenceEqual`/incremental `HashCode`.
  - Discovery only matched `MemberAccessExpressionSyntax` (`.Create<T>()`),
    missing `composer?.Create<T>()` (`MemberBindingExpressionSyntax`, a
    different syntax node inside a `ConditionalAccessExpressionSyntax`).
    Both are now matched in the predicate.
  - Regression tests for all six added to `CompositionPlanVerifyTests.cs`
    (not a separate PR-specific test class — this is ongoing coverage of
    the same generator behavior the rest of that class already tests, not
    a one-off tied to this PR), including one that compiles a genuinely
    separate library assembly in-memory (`GeneratorTestHelpers.CompileLibrary`)
    to prove the
    cross-assembly accessibility case for real, not just in-compilation.
- **A second Codex review round on PR #4 surfaced four more real bugs**,
  fixed the same way, and produced a new repo standard (the "Generated
  code" section in `coding-standards.md`):
  - Same-simple-name plan classes collided (`Box<int>`/`Box<string>` both
    emit `BoxCompositionPlan` — `type.Name` drops generic args). Fixed by
    making every generated plan class `file`-scoped, now the standing rule
    for all generator-emitted types: file-scoped identity makes cross-file
    name collisions structurally impossible, no mangling scheme needed.
  - Emitted type names weren't `global::`-qualified — plain
    `ToDisplayString()` doesn't emit `global::` despite how it reads, and
    mis-binds when a consumer type shadows a namespace segment. All
    emitted names now use `SymbolDisplayFormat.FullyQualifiedFormat`.
  - A `ref`/`out`/`ref readonly` constructor parameter generated
    non-compiling code; now `CMP0004` (unsupported parameter kind). `in`
    stays allowed — callers may legally pass a plain value.
  - Hint-name sanitization was lossy (`N.Foo<int>` vs. literal
    `N.Foo_int_` collide) — hints now append an FNV-1a hash of the raw
    identity (not `string.GetHashCode()`, which is per-process randomized
    and would churn hints across builds).
  - Test-harness gotcha worth remembering: `GeneratorTestHelpers.Verify`
    re-parses generated trees to prove they compile, and must preserve
    each tree's `FilePath` when doing so — `file`-scoped type identity is
    per file path, so re-parsing with the default empty path makes
    same-named file-scoped types spuriously collide in the harness while
    being perfectly legal in a real build.
- **A third Codex review round on PR #4 surfaced three more real bugs**,
  addressed proactively rather than one at a time — the user asked to
  reject every constructor parameter shape the current
  `context.Resolve<T>()`-per-parameter codegen can't correctly handle, not
  just whatever Codex happened to flag:
  - `composer.Create<Box<T>>()` called inside a generic method: `T` is the
    enclosing method's own type parameter, a valid `INamedTypeSymbol` that
    sailed through the existing checks, but emitting `global::N.Box<T>`
    into a namespace-level plan references a `T` out of scope there. Fixed
    by a recursive `ContainsTypeParameter` walk (handles nested generics,
    arrays, pointers) in `CreateInvocationDiscovery`, rejecting any
    non-closed type argument with a new diagnostic, `CMP0005`.
  - `ValidateParameterKinds` only checked `RefKind`; broadened to also
    reject ref-like (ref struct, e.g. `Span<int>`) and pointer/function-pointer
    constructor parameters — both fail for the same underlying reason as
    ref/out (can't be written as, or can't compile as, a generic type
    argument to `Resolve<T>()`), so they reuse `CMP0004` with a
    parameter-shape-specific message.
  - The `[ModuleInitializer]` attribute in the template wasn't
    `global::`-qualified, so a consumer type named `System` in the
    composed type's namespace would shadow the real one and break the
    generated plan's compile. Qualified per the same "Generated code"
    standard as every other framework reference in the template.
- **A fourth round (Copilot + Codex) surfaced five more real issues**,
  triaged one at a time via `AskUserQuestion` before any implementation
  this time (a prior round had batched straight to implementation without
  checking in first — corrected per explicit user feedback):
  - `composer.Create<T>()` where `T` is *directly* the enclosing generic
    method's own type parameter (not nested inside a constructed type like
    `Box<T>`) is a narrower symbol shape — `ITypeParameterSymbol`, not even
    an `INamedTypeSymbol` — that fell through the existing
    `is not INamedTypeSymbol composedType` guard as a silent `return null`
    before `CMP0005` ever ran. Fixed by checking `ContainsTypeParameter` on
    the raw type argument *before* the `INamedTypeSymbol` cast in
    `CreateInvocationDiscovery.Transform`, so this shape is now diagnosed
    the same way as the nested case.
  - `Outer<T>.Inner` (a non-generic nested type inside a generic
    container): the unresolved `T` lives on `ContainingType`, not on
    `Inner`'s own (empty) `TypeArguments`, a path `ContainsTypeParameter`
    didn't walk. Fixed by also recursing through `ContainingType`.
  - Generator diagnostics pointed at `T`'s own declaration site (or
    nowhere, for referenced-assembly/external types) instead of the
    `Composer.Create<T>()` call site that actually triggered discovery —
    visible in snapshots as e.g. `CMP0001` pointing at a type name in its
    own declaration rather than the failing call. Fixed by capturing the
    type-argument syntax's location in `Transform` and threading it as a
    `LocationInfo?` parameter through `Analyze`/`ConstructorSelector`,
    replacing every `LocationInfo.From(type)` call with the threaded
    call-site location.
  - The `GeneratedCodeAttribute` version was hard-coded to `"1.0.0"` with
    no real versioning wired up yet. Fixed by reading
    `AssemblyInformationalVersionAttribute` (falling back to the assembly
    version, then `"0.0.0"`) off the generator's own assembly once,
    passed into the template model as `generator_version` — stays
    accurate automatically once real release versioning lands, no
    generator code change needed then.
  - ADR-0005 requires `.WithTrackingName(...)` on every named incremental
    pipeline stage so cache-hit behavior is assertable later; the
    discovery pipeline had none. Added tracking names for all four stages
    (`CreateInvocations`, `.NotNull`, `.Collected`, `.Distinct`) via a new
    `TrackingNames` class — no incremental-caching test exists yet to
    consume them (that's still open work, not done in this pass).
  - One Copilot finding was triaged as **no action**: flagged
    `test/Directory.Build.props`'s shared `<Using/>` `ItemGroup` as
    inconsistent with the comment on the *separate* `PackageReference`
    `ItemGroup` below it (which explicitly scopes to `IsTestProject`).
    Confirmed with the user this is intentional — every project under
    `test/` should get the shared usings unconditionally; the
    `IsTestProject` comment only ever applied to the package references,
    not the usings above them.
  - Another Copilot finding (the `PlaceholderCompositionContext` exception
    message reading as if recursive composition works today) was also
    triaged as **no action** per the user.
- **A fifth, final review round on PR #4 (3 Codex findings)**, triaged one
  at a time with the user before any implementation:
  - `Create<T>()` where `T` is a delegate type: not abstract, and Roslyn
    exposes a delegate's synthetic `(object, IntPtr)` constructor, so it
    passed `ConstructorSelector.Select` and the template emitted
    `new SomeDelegate(arg1, arg2)` - not legal delegate-construction
    syntax in C#. Fixed by rejecting `TypeKind.Delegate` alongside the
    existing abstract-type check, reusing `CMP0003` (generalized its
    message format from `'{0}' is abstract and...` to
    `'{0}' is {1} and...` so both reasons share one diagnostic ID).
  - **Deferred as an Open Item** (see below): two extern-aliased
    references defining the same metadata name reduce to the same
    `global::` name in generated code and could dedupe/collide.
  - **No action**: pinning the generator's compile-time
    `Microsoft.CodeAnalysis.CSharp` reference to the oldest
    supported Roslyn/SDK version - a real supply-chain concern, but the
    user deferred deciding an actual minimum-supported version for now.
- **A sixth review round (2 Codex findings, from an extra manually-triggered
  review) surfaced two more real gaps**, both fixed after per-item
  triage:
  - `composer.Create<Customer[]>()` (or any non-`INamedTypeSymbol` shape -
    array, pointer, function pointer): `Transform` silently returned
    `null`, so the call compiled clean with no plan generated and no
    diagnostic, failing only at runtime via `Composer`'s generic "no plan
    registered" message. Fixed with a new diagnostic, `CMP0006`, reported
    wherever the type-argument-isn't-`INamedTypeSymbol` check previously
    just returned `null`.
  - A type with a `required` member whose selected constructor isn't
    annotated `[SetsRequiredMembers]`: Phase 0 only ever emits bare
    `new T(...)` (no object initializer - required-member assignment is
    explicitly later-milestone scope), so this would produce CS9035 in
    the generated file. Fixed with a new diagnostic, `CMP0007`, checked
    in `ConstructorSelector.Select` right after the single accessible
    constructor is chosen (walks `type` and its base-type chain for any
    `IPropertySymbol`/`IFieldSymbol` with `IsRequired: true`, then checks
    the constructor's attributes for `SetsRequiredMembersAttribute`).
  - This round confirmed the review loop had converged: two clearly
    distinct, real gaps, no repeats of earlier-round territory - a good
    stopping point for this PR's review cycle.

- **Phase 4 (benchmark harness)**: `benchmarks/Compono.Benchmarks/`, a
  `BenchmarkDotNet` console project referencing `Compono.Generators` as an
  analyzer-only `ProjectReference` (the same shape
  `test/Compono.Generators.Tests` and `Compono.csproj` itself already use)
  so the generator genuinely runs against types declared in the benchmark
  project itself, rather than benchmarking against a pre-packed nupkg.
  - **A representative nested-composable type (`docs/mvp.md`'s exit
    criteria shape, e.g. `Customer(Address HomeAddress)`) turned out to be
    impossible to benchmark end-to-end in Milestone 1, discovered by
    actually trying it first**: every constructor argument in generated
    code — including one whose type has its own generated plan — is
    resolved via `context.Resolve<TParam>()`
    (`CompositionPlan.scriban`), and Milestone 1's placeholder
    `ICompositionContext` (`Composer.cs`) throws `NotSupportedException`
    unconditionally there, regardless of whether `PlanCache<TParam>` is
    populated. Dispatching `Resolve<TParam>()` to the matching
    `PlanCache<TParam>` is real provider-resolution — Milestone 2 scope,
    not yet wired up. Confirmed by generating and inspecting the actual
    `.g.cs` output for a `Root(Branch Left, Branch Right)` shape: both
    `Root` and `Branch` got their own correct generated plans, but
    `Root.Compose(...)` still throws at runtime because it calls
    `context.Resolve<Branch>()` rather than invoking `Branch`'s plan
    directly. So the benchmark only measures a flat, parameterless type
    (`Leaf`) — the one shape Milestone 1 can actually execute — rather
    than the nested shape originally planned; revisit once Milestone 2's
    real `CompositionContext` makes nested end-to-end composition
    possible.
  - **Baseline result** (Apple M3 Max, .NET 10.0.3 arm64 RyuJIT, Release,
    `BenchmarkDotNet` `DefaultJob`, recorded at this phase's completion):
    generated construction (`composer.Create<Leaf>()`) averaged 3.29 ns
    and 24 B allocated per op, vs. a reflection baseline
    (`ReflectionComposer.Compose<Leaf>()`, `Type.GetConstructors().Single()`
    + `ConstructorInfo.Invoke`) averaging 20.07 ns and 56 B allocated per
    op — generated construction ran ~6.1x faster and allocated ~43% as
    much as the reflection baseline.
  - Full solution build is still 0 warnings/0 errors; `dotnet test` is
    90/90 passing (unchanged - a benchmark project isn't a test project
    and adds no new tests).

## Open Items

Tracked but deliberately not fixed yet - valid points raised in review,
out of scope for the PR that surfaced them:

- **Extern-aliased type identity isn't preserved through discovery**
  (Codex, PR #4 round 5). Two aliased references defining the same
  metadata name (e.g. `A::Ns.Widget` and `B::Ns.Widget`) both reduce to
  `global::Ns.Widget` in `CreateInvocationDiscovery`, so their
  `DiscoveredTypeInfo` values could dedupe as "the same type" and the
  emitted reference would be unresolved or ambiguous. Niche - extern
  alias usage is uncommon - so deferred rather than fixed; revisit if it
  ever actually comes up in practice (either preserve alias/assembly
  identity through discovery and emit a corresponding `extern alias`, or
  diagnose the case explicitly instead of producing uncompilable code).

- Started with "Create generator project" as the first slice, since every
  later task depends on the project existing and being wired correctly
  (ADR-0003's packing shape in particular is easy to get subtly wrong and
  worth confirming in isolation before layering codegen logic on top of it).
- **`ICompositionContext`/`Composer` are Milestone-1-only placeholders**,
  confirmed with the user before writing them: `ICompositionContext` is a
  bare synchronous `Resolve<TValue>()` with no real implementation (throws
  `NotSupportedException`) — Milestone 2's `CompositionContext` replaces
  this wholesale, an accepted breaking change pre-1.0.
- **`Compono.Generators` references Meziantou.Polyfill and Scriban the same
  way `dynamo-mapper`'s generator does** — `Meziantou.Polyfill` (full
  `IncludeAssets`, `PrivateAssets="all"`) for `IsExternalInit`/nullable-
  and-trimming attributes, `Microsoft.CSharp` (Scriban's embedded source
  uses the C# dynamic runtime binder), and `Scriban` referenced with
  `IncludeAssets="Build"` + `PackageScribanIncludeSource=true` so its
  source compiles directly into `Compono.Generators.dll` — no separate
  `Scriban.dll` to ship/load at generator runtime. An initial attempt at
  this exact setup produced ~1000 duplicate-definition errors that looked
  like a fundamental Meziantou.Polyfill incompatibility — false lead: a
  stray `CompilerGeneratedFilesOutputPath` debug setting (copied from a
  different template, not present in `dynamo-mapper`'s csproj) wrote real
  `.cs` files to `src/Compono.Generators/generated/`, *outside* `obj/`,
  which got compiled twice. Removing that setting fixed it cleanly.
- `EquatableArray<T>` alone is still vendored locally in
  `Compono.Generators/Types` (per the user's explicit instruction not to
  take a package dependency on `LayeredCraft.SourceGeneratorTools`) — its
  `GetHashCode()` uses `System.HashCode` (from Meziantou.Polyfill) rather
  than a second vendored `HashCode` struct.
- List-pattern matching (`is [x]`, `switch { [] => ... }`) does compile
  fine with Meziantou.Polyfill installed (verified) — a few spots use
  plain `.Length`/index checks instead anyway, matching `dynamo-mapper`'s
  own `WellKnownTypes.GetTypeByMetadataNameInTargetAssembly`, which uses
  the same plain-if style rather than list patterns.
- As of Phase 0's completion: full solution build is 0 warnings/0 errors;
  `dotnet test` is 10/10 passing (`Compono.Tests` + `Compono.Generators.Tests`,
  both TFMs); `dotnet pack` on `Compono` verified to contain just
  `Compono.Generators.dll` under `analyzers/dotnet/cs`.
- **Phase 1 real manual verification**: `dotnet pack`'d `Compono` into a local
  feed, referenced it from a genuinely separate throwaway console project
  (not the repo's own test suite), and confirmed `PlanCache<Customer>.Instance`
  *and* `PlanCache<Address>.Instance` (Address only reachable through
  Customer's constructor parameter, no local `Create<Address>()` call site of
  its own) were both populated by generated module initializers, and that
  `Composer.Create<Customer>()` actually reaches the generated plan's
  `Compose()` method (which calls `context.Resolve<string>()` and throws the
  expected Milestone-1-placeholder `NotSupportedException`, not some other
  failure) — proves the transitive-closure codegen works in a real build, not
  just the snapshot test harness. As of Phase 1's completion: full solution
  build is still 0 warnings/0 errors; `dotnet test` is 50/50 passing
  (`Compono.Tests` + `Compono.Generators.Tests`, both TFMs).
- **Phase 2 design decisions, confirmed with the user via `AskUserQuestion`
  before implementing**: `[Composable]` (over `[GenerateCompositionPlan]`/
  `[Compose]`) framed as an opt-in marker for cases discovery can't reach
  on its own, not a fallback/workaround - the user's framing, kept
  verbatim in the plan's Phase 2 section above since it's the reasoning
  future readers need. Both a type-level and an assembly-level `typeof`
  placement are in scope for this phase (the user asked for both, not
  type-level-only as initially proposed), with both forms treated as
  equivalent requests and deduplicated against each other and against
  call-site discovery.
- **Shared discovery validation factored out as `ComposedTypeAnalyzer`**:
  `CreateInvocationDiscovery`'s closed-type-validation-then-
  `TransitiveClosureWalker`-handoff logic used to be call-site-specific;
  Phase 2 needed the identical validation for `[Composable]`-requested
  types too (an open-generic type argument, a non-`INamedTypeSymbol`
  shape), so it moved into its own class rather than being duplicated
  across `CreateInvocationDiscovery` and `ComposableAttributeDiscovery`.
  `CMP0005`/`CMP0006`'s message text was generalized from
  `Composer.Create<T>()`-specific wording to plain "Compono requires..."
  since both diagnostics can now be reported from either discovery path.
- **Assembly-level `[Composable(typeof(...))]` needs its own syntax
  provider**: `ForAttributeWithMetadataName` only matches attributes
  applied to a declaration, not `[assembly: ...]` attribute lists, so the
  assembly-level placement is discovered via a `CreateSyntaxProvider`
  predicate over `AttributeSyntax` nodes whose parent `AttributeListSyntax`
  targets the `assembly` keyword, resolving the attribute constructor
  symbol via `GetSymbolInfo` to confirm it's genuinely
  `Compono.ComposableAttribute` (not an unrelated type that happens to be
  named `Composable`) before extracting its `typeof(...)` argument.
- **Phase 2 real manual verification**: `dotnet pack`'d `Compono` into a
  local feed (after clearing the local NuGet package cache - a stale
  cached copy of the same package version silently masked the new
  `ComposableAttribute` type on the first attempt) and referenced it from
  a genuinely separate throwaway console project. Confirmed all three
  discovery paths populate `PlanCache<T>.Instance` and reach generated
  code in a real build: a plain `Create<Order>()` call site (Phase 1's
  existing path, still working), `[Composable]` directly on a
  `ReportRequest` type with no local `Create<T>()` call site anywhere in
  the project, and `[assembly: Composable(typeof(ExternalStandIn))]` for a
  type standing in for one this project doesn't own and can't annotate
  directly. As of Phase 2's completion: full solution build is still 0
  warnings/0 errors; `dotnet test` is 60/60 passing (`Compono.Tests` +
  `Compono.Generators.Tests`, both TFMs).
- **PR #6 review feedback (Codex + Copilot) surfaced two real Phase 2
  gaps**, both fixed after per-item triage with the user
  (`tasks/respond-to-pr-feedback.md`):
  - `ComposableAttributeDiscovery.IsAssemblyCandidate` filtered on the
    attribute's literal syntax name (`"Composable"`/`"ComposableAttribute"`)
    before `TransformAssemblyLevel`'s semantic check ever ran - a consumer
    aliasing the attribute (`using Marker = Compono.ComposableAttribute;`
    then `[assembly: Marker(typeof(SomeType))]`) had the syntax name
    silently fail the filter, so the escape hatch never even reached the
    semantic check that would have resolved the alias correctly, and no
    plan or diagnostic was produced. Fixed by dropping the name filter
    entirely - the predicate now admits every `[assembly: ...]` attribute
    syntactically and relies on the existing `GetSymbolInfo` +
    `wellKnownTypes.IsType` check downstream to do the real filtering,
    which already handles aliases correctly.
  - `ComposableAttribute`'s `[AttributeUsage]` excluded
    `AttributeTargets.Interface`, so `[Composable]` couldn't be written on
    an interface at all - the C# compiler rejected it with a bare `CS0592`
    before Compono's own generator ever saw it, even though the
    assembly-level form (`[assembly: Composable(typeof(ISomeInterface))]`)
    already handled the same case gracefully: `ConstructorSelector`
    reports `CMP0003` for an interface the same way it does for an
    abstract class, since `INamedTypeSymbol.IsAbstract` is `true` for
    interfaces in Roslyn too. Fixed by adding `AttributeTargets.Interface`
    to the usage list, so both placements now produce the same clean
    diagnostic instead of the type-level form hitting a raw compiler error
    with no explanation.
  - Regression tests added: an assembly-level `[Composable]` request via
    an aliased `using` directive still generates a plan
    (`ComposableAttributeAtAssemblyLevelViaAlias_GeneratesCompositionPlan`),
    and `[Composable]` on an interface reports `CMP0003`
    (`ComposableAttributeOnInterface_ReportsDiagnostic`). As of this
    round's completion: full solution build is still 0 warnings/0 errors;
    `dotnet test` is 64/64 passing (`Compono.Tests` +
    `Compono.Generators.Tests`, both TFMs).
- **A second round of PR #6 review feedback (Codex, via `@codex review`)
  surfaced two more real Phase 2 gaps**, both triaged with the user before
  implementing:
  - `ComposedTypeAnalyzer.Analyze` never checked whether the *requested
    type itself* is a ref struct (ref-like type) - a ref-like constructor
    *parameter* was already rejected by `ConstructorSelector`'s
    `ValidateParameterKinds` (`CMP0004`) whenever validating some other
    type's constructor, but that check only ever fires in a parameter
    context, so nothing stopped `[Composable]` (or the assembly-level
    form) from targeting a ref struct directly. `ICompositionPlan<out T>`
    and `PlanCache<T>` both declare a bare `T` with no
    `allows ref struct` constraint, so the resulting
    `ICompositionPlan<global::N.SomeRefStruct>` would fail to compile
    (CS9244) in the generated file. Fixed with a new diagnostic, `CMP0009`,
    checked in `ComposedTypeAnalyzer.Analyze` right after the named-type
    check and before handing off to `TransitiveClosureWalker` - a
    dedicated ID rather than folding into `CMP0004` (parameter-specific
    message shape) or `CMP0006` (would have required restructuring an
    already-tested static message), since this is a distinct failure mode:
    the type is a perfectly valid named type, just not usable as a type
    argument to Compono's own plan/cache types.
  - `ComposableAttributeDiscovery`'s assembly-level `ExtractTypeArgument`
    matched the argument expression against `is TypeOfExpressionSyntax`
    directly, so `[assembly: Composable((typeof(Customer)))]` (legal,
    redundant parens) wrapped the `typeof` in a
    `ParenthesizedExpressionSyntax` the pattern never unwrapped -
    `requestedType` came back `null` and a perfectly valid request
    incorrectly reported `CMP0008` ("missing type argument"). Fixed by
    unwrapping any `ParenthesizedExpressionSyntax` in a loop before the
    `TypeOfExpressionSyntax` check, rather than switching to a full
    semantic-binding lookup (the reviewer's suggested alternative) - the
    only realistic wrapper shape around a `typeof(...)` argument is
    redundant parens, so the small, targeted unwrap covers it without a
    bigger rewrite.
  - Regression tests added: `[Composable]` on a `ref struct` reports
    `CMP0009` (`ComposableAttributeOnRefStruct_ReportsDiagnostic`), and a
    parenthesized `typeof(...)` at assembly level still generates a plan
    (`ComposableAttributeAtAssemblyLevelWithParenthesizedTypeof_GeneratesCompositionPlan`).
    As of this round's completion: full solution build is still 0
    warnings/0 errors; `dotnet test` is 68/68 passing (`Compono.Tests` +
    `Compono.Generators.Tests`, both TFMs).
- **Phase 3 design and implementation**: nullability shape confirmed with
  the user via `AskUserQuestion` before writing [ADR-0006](../adr/0006-required-members-and-nullability-metadata.md)
  (a bare `bool` was considered and rejected in favor of a `Nullability`
  enum, per the user's stated rationale — see the ADR). Implementation
  notes not already in the ADR:
  - Required-member collection couldn't stay inside
    `ConstructorSelector.HasUnassignedRequiredMembers` once it needed to
    return data on success (not just fail) — moved to a new
    `RequiredMemberCollector.Collect`, called from
    `TransitiveClosureWalker.Analyze` right after constructor selection
    succeeds, mirroring how `ConstructorSelector.Select` itself is called.
  - `TransitiveClosureWalker`'s `EnqueueIfEligible` took an
    `IParameterSymbol` before this phase; generalized to take a bare
    `ITypeSymbol`/name pair so both constructor parameters and required
    members enqueue through the same eligibility check
    (`LeafTypeClassifier.IsProviderResolved`) without a second, parallel
    enqueue path.
  - The Scriban template's `required_members.size > 0` check only works
    because `CompositionPlanEmitter` materializes `Parameters`/
    `RequiredMembers` with `.ToArray()` before handing them to the
    template — a plain `IEnumerable<T>` from `.Select(...)` doesn't expose
    `.size` to Scriban (only `IList`-backed collections do), which first
    manifested as the required-member `if` block silently never rendering
    (no compile error, no verify-snapshot mismatch — the "New" received
    snapshot just showed a bare constructor call with the required member
    left unset, i.e. a real CS9035 compile failure caught by
    `GeneratorTestHelpers.Verify`'s recompile-and-check-zero-errors step).
  - **Manual verification**: same `dotnet pack` + local-feed + throwaway
    console project approach as Phase 2. A `Customer` type with a
    required, composable `Address` property (no `[SetsRequiredMembers]`)
    and a nullable constructor parameter built and ran against the packed
    `Compono` package: `composer.Create<Customer>()` reached the generated
    `Compose(...)` and threw the expected `NotSupportedException` from
    Milestone 1's placeholder `ICompositionContext` (proving the
    object-initializer block and the `Nullability` argument are real,
    executed code, not just something that type-checked), and both
    `PlanCache<Customer>.Instance` and `PlanCache<Address>.Instance` were
    non-null (proving required-member recursion generates and registers a
    plan for the nested type). Full solution build is 0 warnings/0
    errors; `dotnet test` is 80/80 passing (`Compono.Tests` +
    `Compono.Generators.Tests`, both TFMs).
- **PR #7 review feedback (Codex) surfaced three real Phase 3 gaps**, all
  triaged with the user one at a time before implementing
  (`tasks/respond-to-pr-feedback.md`):
  - **P1 — duplicate `AddSource` hint names could crash the whole
    generator run**: `Box<string>` and `Box<string?>` composed in the same
    compilation emit to the identical hint name
    (`SymbolDisplayFormat.FullyQualifiedFormat` erases the top-level
    nullable annotation), but Roslyn substitutes `Box<T>`'s constructor
    parameter's own `NullableAnnotation` differently for each closed
    generic, so the two `DiscoveredTypeInfo` records differ in
    `Parameters` and survived `ComponoIncrementalGenerator`'s plain
    `.Distinct()` (full structural equality) as two "different" entries -
    `AddSource` then threw on the duplicate hint name. Fixed by grouping
    on the actual emission identity (`Namespace`, `TypeName`,
    `FullyQualifiedName`) and keeping the first-discovered entry, so two
    discoveries that would emit to the same hint name always collapse to
    one regardless of what a later duplicate disagreed about.
  - **P2 — an overridden required property was collected twice**:
    `RequiredMemberCollector` walks a type and its base types outward:
    both a base's virtual required property and a derived override of it
    report `IsRequired: true` and share the same name, so both were
    collected, and the template emitted the same object-initializer
    target twice (a compile error in the generated file). Fixed by
    grouping the collected members by name and keeping the first
    occurrence - `EnumerateTypeAndBases` walks the type itself before its
    base types, so the derived override is always encountered first.
  - **P2 — an unescaped reserved-keyword required member name emitted
    invalid code**: `public required string @class { get; init; }`
    reports `member.Name` as the bare keyword text (`class`), which
    landed directly in the object initializer (`class = ...`) - a
    reserved word, not a valid identifier there. Fixed by escaping via
    `SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None` at collection
    time (a contextual keyword like `var` is a legal identifier unescaped
    and must not get an `@` prefix, hence the real keyword-kind check
    rather than a hand-maintained list).
  - Regression tests added: composing both `Box<string>` and `Box<string?>`
    dedupes to a single plan (later superseded - see below), an
    overridden required property is emitted once
    (`OverriddenRequiredProperty_EmittedOnlyOnce`), and a
    `@class`-named required member escapes correctly
    (`RequiredMemberNamedWithReservedKeyword_EscapesIdentifier`). Also
    added `AGENTS.md` at repo root in this same round (repo-orientation
    file for any coding agent, not Claude-specific) - designed with the
    user, incorporating a second round of external review feedback on the
    file itself. As of this round's completion: full solution build is
    still 0 warnings/0 errors; `dotnet test` is 86/86 passing
    (`Compono.Tests` + `Compono.Generators.Tests`, both TFMs).
- **A second Codex pass on PR #7 (triggered by `@codex review` against the
  fix commit above) caught a real gap in that same fix**, triaged with the
  user before implementing: the `group.First()` dedup stopped the
  duplicate-`AddSource`-hint-name crash, but didn't stop the *other* call
  site from silently getting the wrong `Nullability` for its own request -
  `Box<string>` and `Box<string?>` share exactly one generated plan and
  one `PlanCache<Box<string>>.Instance`, so if two call sites genuinely
  disagree about it, there's no value that's correct for both; picking
  one arbitrarily just makes the outcome depend on discovery order.
  Fixed by replacing "keep the first" with "diagnose the conflict": a
  group's entries are deduped via `.Distinct()` first (the legitimate case
  - the same type discovered identically via both a call site and
  `[Composable]`, say - still collapses to one with no diagnostic), and
  only if more than one *genuinely different* entry survives is a new
  diagnostic reported (`CMP0010`) and no plan emitted for that identity -
  the same "diagnose an ambiguity, don't guess" rule `CMP0001` (ambiguous
  constructor) already follows. Regression test renamed/repurposed to
  match: `NullableAndNonNullableGenericInstantiation_ReportsConflictDiagnostic`
  now asserts `CMP0010` instead of asserting a (silently wrong) generated
  plan. As of this round's completion: full solution build is still 0
  warnings/0 errors; `dotnet test` is 86/86 passing (`Compono.Tests` +
  `Compono.Generators.Tests`, both TFMs).
- **A third Codex pass on PR #7 (against the CMP0010 fix commit above)
  found two more real gaps in the same area**, both triaged with the user
  before implementing:
  - **P1 — the conflict check never ran for a conflict reached within a
    single closure walk**: CMP0010 (above) only compares entries *across*
    separate top-level discoveries (e.g. two different `Create<T>()` call
    sites), because `TransitiveClosureWalker`'s `visited` set used
    `SymbolEqualityComparer.Default`, which ignores nullable annotations -
    if the *same* type's closure reached both `Box<string>` and
    `Box<string?>` as sibling constructor parameters (or a parameter and a
    required member), the second variant silently failed `visited.Add(...)`
    and was dropped before it ever became its own `DiscoveredTypeInfo` -
    one layer upstream of where CMP0010's check could ever see it. Fixed
    by swapping the comparer to `SymbolEqualityComparer.IncludeNullability`
    (a purpose-built Roslyn comparer for exactly this) - both variants now
    get walked and each produces its own entry, which flows into the
    already-existing CMP0010 check rather than needing a second, separate
    detection path.
  - **P2 — the CMP0010 fix could itself erase real diagnostics**: `.Distinct()`
    on the grouped entries uses `DiscoveredTypeInfo`'s full structural
    equality, which includes each failure's embedded `DiagnosticInfo` -
    and `DiagnosticInfo.Equals` includes `Location`. So the *same failing*
    type (e.g. an ambiguous constructor) reached from two different
    `Create<T>()` call sites produced two "distinct" failure entries
    purely because their locations differed, which the previous round's
    conflict logic then folded into a single synthetic, locationless
    CMP0010 - silently replacing two legitimate `CMP0001`s with a
    misleading one. Fixed by excluding any entry that already carries a
    diagnostic from conflict-detection entirely: failures always pass
    through as-is, however many there are for the same identity: CMP0010
    now only ever fires among entries that all succeeded (no diagnostics)
    but disagree on metadata.
  - Regression tests added: a single closure reaching both `Box<string>`
    and `Box<string?>` via sibling constructor parameters reports CMP0010
    (`SameClosureReachesConflictingNullableInstantiations_ReportsConflictDiagnostic`),
    and the same ambiguous type reached from two different `Create<T>()`
    call sites still reports `CMP0001` at both locations rather than a
    swallowed `CMP0010`
    (`AmbiguousTypeReachedFromTwoCallSites_StillReportsAtBothLocations`).
    As of this round's completion: full solution build is still 0
    warnings/0 errors; `dotnet test` is 90/90 passing (`Compono.Tests` +
    `Compono.Generators.Tests`, both TFMs).
- **A fourth Codex pass on PR #7 found one more real gap**, unrelated to
  the nullable-conflict area above (a genuinely independent finding, not
  another layer of the same bug) - triaged with the user, who also asked
  directly whether this round of iteration had hit diminishing returns:
  `RequiredMemberCollector` never validated that a required member could
  actually be assigned from generated code - a required property with no
  accessible setter, or a required readonly/inaccessible field, would
  still be collected and emitted as an object-initializer assignment that
  fails to compile (CS0272/CS0191). `ConstructorSelector` already guards
  the analogous case for constructors
  (`compilation.IsSymbolAccessibleWithin`), and this just hadn't been
  carried over to the required-member path added in this same phase.
  Fixed by adding `IsAssignableFromGeneratedCode` (checks the property's
  `SetMethod` accessibility, or that a field is both non-readonly and
  accessible) alongside the existing ref-like/pointer checks. **No
  automated regression test**: the exact shape this defends against (no
  accessible setter, or a readonly required field) is one the C# compiler
  itself refuses to let any C#-authored type declare (confirmed by
  attempting it via `GeneratorTestHelpers.CompileLibrary` - CS9032 fired
  at the library's own declaration) - the gap only exists for a
  non-C#-compiler-authored assembly (hand-crafted IL, a different .NET
  language), which this test harness has no way to produce without adding
  real IL-emission infrastructure, judged disproportionate for one edge
  case. As of this round's completion: full solution build is still 0
  warnings/0 errors; `dotnet test` is 90/90 passing (`Compono.Tests` +
  `Compono.Generators.Tests`, both TFMs - unchanged count, since this
  round added no new test).
- **Phase 4 was extended, after its initial completion, to add an
  AutoFixture ecosystem comparison** at the user's request (citing outside
  advice that users will inevitably compare Compono to AutoFixture even
  though it's not marketed as a replacement). Kept as a genuinely separate
  `EcosystemBenchmarks` class rather than folded into the original
  `CompositionBenchmarks` (renamed `ArchitectureBenchmarks`) - mixing the
  two would let an "AutoFixture is much slower" number read as the
  architecture's proof point, when the actual architecture question is
  answered by `Direct`/`Generated`/`Reflection` alone.  `Direct` (`new
  Leaf()`) was added to `ArchitectureBenchmarks` at the same time, as the
  theoretical floor. The suggested `Customer -> Address` nested-graph
  comparison was **not** adopted - `Leaf` (flat, parameterless) remains
  the only type either engine can honestly construct end-to-end until
  Milestone 2's provider-resolution pipeline exists (see
  `docs/performance.md`'s "What's measured, and what isn't yet"), so a
  richer type would have made the comparison less fair, not more. This is
  a benchmark-project-only dependency - `Directory.Packages.props`'s
  existing "no AutoFixture" rule is about Compono's own test suite (test
  data generation would contradict the product), not benchmarking an
  external framework as a reference point, so it stays unchanged in
  intent, just annotated to avoid future confusion between the two.
- **Phase 5's manual verification used real dogfooding rather than a
  throwaway project**: `/Users/ncipollina/source/repos/ncipollina/lightsaber-skill`
  (a real, unrelated production repo not previously using AutoFixture)
  added `Compono` 0.1.0-alpha.8 from nuget.org as a normal `PackageReference`
  in its test project and added
  `test/Lightsaber.Skill.Tests/Compono/ComponoVerificationTests.cs` with
  two cases: a flat parameterless type (`LightsaberHilt`) constructs
  end-to-end via its generated plan, and a nested type (`Lightsaber`,
  taking a `Crystal`) dispatches to its own generated
  `ICompositionPlan<Lightsaber>` and throws `NotSupportedException`
  specifically from inside `Resolve<Crystal>()` - not earlier from a
  missing plan registration (`InvalidOperationException`) or a compile
  error. Both passed, in a real `dotnet build`/`dotnet test` run against
  the published NuGet package (not a `ProjectReference` back into this
  repo) - confirming discovery, transitive closure, constructor
  selection, and `PlanCache<T>` dispatch all work correctly end-to-end
  outside this repo's own test harness, for a nested type, which is
  exactly what the exit criteria needs even though full nested value
  resolution is honestly out of scope until Milestone 2. Committed
  locally on that repo's `main` (not pushed, pending the user's own
  review).
