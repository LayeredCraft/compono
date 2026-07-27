# [PLAN-0001] Milestone 1: Source-Generation Foundation

**Status:** In Progress

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
- `Compono.Xunit`/`Compono.NSubstitute`/`Compono.Bogus` — separate
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

### Phase 0 — Foundation (Done, not yet opened as a PR)

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

### Phase 1 — Transitive closure (Not started)

The real gap between "works for a flat type" and the exit criteria's
"representative record or class" — a `Customer` with an `Address`
property needs `Address` to get its own generated plan too, not just a
`context.Resolve<Address>()` call with nothing behind it yet.

**Open design question to settle before implementing** (light dive,
not a full ADR — the shape is basically decided by ADR-0004, this is
filling in a gap it left implicit): when the generator walks a
discovered type's constructor parameters, how does it decide which
parameter types get their *own* generated plan (recursive walk) versus
being left as a bare `context.Resolve<TParam>()` call for
`ICompositionContext`/Milestone 2 to handle? A parameter like `string`
or `int` doesn't have a sensible single-constructor shape the way a
`Customer`/`Address`-style type does, so blindly attempting constructor
selection on every parameter type would misfire on exactly the
primitives Milestone 2 owns. Leading option: only recurse into a
parameter type if constructor selection on it *succeeds cleanly*
(exactly one accessible constructor); anything that fails constructor
selection is left to `context.Resolve<TParam>()` silently (no `CMP0001`/
`CMP0002` diagnostic — those diagnostics are for the type actually
requested via `Create<T>()`, not for every leaf type incidentally
touched while walking its graph).

- [ ] Confirm the recursion-boundary rule above with the user before
      implementing (or replace it with whatever's actually decided)
- [ ] Walk each discovered type's constructor parameter types
      recursively, applying that rule
- [ ] Dedupe the resulting set (a type reachable from two different
      parents still gets exactly one generated plan)
- [ ] Emit a generated plan + `PlanCache<T>` registration for every type
      in the closure, not just the top-level requested type
- [ ] Snapshot test: a type with a nested composable property (e.g.
      `Customer(string Name, Address HomeAddress)`) produces plans for
      *both* `Customer` and `Address`

### Phase 2 — Escape-hatch attribute (Not started)

ADR-0004's second discovery path: a type that needs a plan but has no
local `Create<T>()` call site in the compilation.

**Open design question**: the attribute's name and exact shape were
explicitly left as "TBD, a Milestone 1 implementation detail" in
ADR-0004 — needs a quick decision (light dive) before implementing, not
a new architectural fork.

- [ ] Decide the attribute's name/namespace/shape
- [ ] Discover attributed types alongside call-site-discovered ones,
      feeding into the same constructor-selection/emission pipeline
- [ ] Snapshot test: an attributed type with no local `Create<T>()`
      call site still gets a generated plan

### Phase 3 — Required members and nullability (Not started)

- [ ] Emit required-member assignments (object-initializer-style
      `required` properties, not just constructor parameters)
- [ ] Emit nullability metadata onto generated request/resolve calls, so
      a future `ICompositionContext` implementation (Milestone 2) can
      tell a `string` parameter from a `string?` one
- [ ] Snapshot tests covering both

### Phase 4 — Benchmark harness (Not started)

- [ ] Add a benchmark project comparing generated construction against
      a reflection baseline (`docs/mvp.md`'s explicit Milestone 1 ask)
- [ ] Record a baseline result somewhere durable (this plan's Notes, or
      a `docs/*.md` subsystem doc once one exists for the generator)

### Phase 5 — Close-out

- [ ] Real manual verification against an actual consuming project (not
      just the test suite) — per `tasks/implement.md`'s "real manual
      verification for anything source-generator-facing" step: build a
      small throwaway project referencing `Compono`, call
      `composer.Create<Customer>()` for a representative nested type,
      confirm the call resolves to generated code
- [ ] Update `docs/architecture.md`/`docs/mvp.md` to reflect current
      (not just intended) state now that code exists
- [ ] Set this plan's `Status: Done`

## Critical Files

- `src/Compono.Generators/` — the generator project (ADR-0003):
  `ComponoIncrementalGenerator.cs` (pipeline entry point),
  `Discovery/CreateInvocationDiscovery.cs` + `ConstructorSelector.cs`,
  `WellKnownTypes/` (symbol cache, vendored `BoundedCacheWithFactory`),
  `Diagnostics/` (`DiagnosticDescriptors`, `DiagnosticInfo`),
  `Models/` (`DiscoveredTypeInfo`, `ConstructorParameterInfo`, `LocationInfo`),
  `Types/EquatableArray.cs` (vendored from `LayeredCraft.SourceGeneratorTools`,
  not referenced as a package — see attribution comment), `Emitters/`
  (`TemplateHelper`, `CompositionPlanEmitter`),
  `Templates/CompositionPlan.scriban`,
  `AnalyzerReleases.{Shipped,Unshipped}.md`.
- `src/Compono/` — `ICompositionPlan.cs`, `ICompositionContext.cs`
  (Milestone-1-only placeholder), `PlanCache.cs`, `Composer.cs` (minimal
  placeholder entry point). `Compono.csproj` — analyzer-only
  `ProjectReference` to `Compono.Generators`, packs its output into
  `analyzers/dotnet/cs` (Scriban is source-embedded into
  `Compono.Generators.dll`, so no separate DLL to pack for it).
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
- (Phase 4) a new `benchmarks/` or `test/Compono.Benchmarks/` project —
  exact location TBD when that phase starts.

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
