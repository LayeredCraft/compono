# [PLAN-0070] xUnit v3 Fixture Accessor (`RequireFixture<TFixture>()`)

**Status:** Done

**Implements:** [ADR-0069](../adr/0069-xunitv3-require-fixture-accessor.md)
(issue [#162](https://github.com/LayeredCraft/compono/issues/162))

## Goal

`Compono.XunitV3` and `Compono.XunitV3.Aot` both ship the identical public
`FixtureContextExtensions.RequireFixture<TFixture>(this ITestContext)`,
compiled from one linked source file, implementing ADR-0069's behavior
contract. It's proven by unit tests, real-runner tests on both packages, a
real Native AOT publish and run, an xUnit 3.2.2 runtime check, and (optional)
consumer dogfood.
The docs, cookbook, and `compono` skill describe it alongside the
framework-neutral alternative.

## Scope

**In:** the shared source file and per-package wiring, CI path-filter
updates, tests, packaged sample examples, docs, cookbook, skill, API
reference regeneration, and validation (see ADR-0069 Decision Outcome).

**Out:** profile constructor injection, a framework-neutral runtime-value
abstraction, fixture creation/ownership/disposal, TUnit/NUnit/MSTest
equivalents, AOT assembly-fixture support (blocked upstream by
[xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664)), AOT seed
reporting ([#166](https://github.com/LayeredCraft/compono/issues/166)),
and any change to core `Compono`, `Compono.Generators`, composition
semantics, or enumeration timing.

## Implementation specification

Implementers follow this section verbatim. Don't redesign it.

### Files and wiring

- **New:** `src/Shared/Compono.XunitV3/FixtureContextExtensions.cs`, the only
  implementation.
- **`src/Compono.XunitV3/Compono.XunitV3.csproj`:** add
  `<Compile Include="..\Shared\Compono.XunitV3\FixtureContextExtensions.cs" Link="FixtureContextExtensions.cs" />`.
- **`src/Compono.XunitV3.Aot/Compono.XunitV3.Aot.csproj`:** add the same
  `<Compile … Link=… />`, and add `COMPONO_XUNITV3_AOT` to
  `DefineConstants`: `<DefineConstants>$(DefineConstants);COMPONO_XUNITV3_AOT</DefineConstants>`.
  Add a short XML comment there pointing to ADR-0069.
- **Namespace selection** at the top of the shared file:

  ```csharp
  #if COMPONO_XUNITV3_AOT
  namespace Compono.XunitV3.Aot;
  #else
  namespace Compono.XunitV3;
  #endif
  ```

- `using Xunit;` resolves `ITestContext` from each package's own xUnit
  reference. Add no new `PackageReference` anywhere in `src/`.

### Code shape (normative)

```csharp
/// XML docs required on the type and the method: purpose, "call from a registration factory, not Configure/ctor",
/// exact-type matching, never-blocks guarantee, each exception, and a <code> usage example.
public static class FixtureContextExtensions
{
    public static TFixture RequireFixture<TFixture>(this ITestContext context)
        where TFixture : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var fixtureType = typeof(TFixture);
        var testClass = context.TestClass;
        if (testClass is null)
            throw new InvalidOperationException(/* M1 */);

        var pending = context.GetFixture(fixtureType);   // non-generic overload: returns the ValueTask directly so IsCompleted can be checked (never-wait), with identical semantics across 3.2.2/4.x
        if (!pending.IsCompleted)
            throw new InvalidOperationException(/* M2 */);

        // Already completed, so this never blocks. A faulted/canceled ValueTask rethrows xUnit's original exception unchanged.
        var fixture = pending.GetAwaiter().GetResult();

        if (fixture is null)
            throw new InvalidOperationException(/* M3 */);

        return fixture as TFixture
            ?? throw new InvalidOperationException(/* M4 */);
    }
}
```

No reflection beyond `typeof`, no `Activator`, no LINQ, no catch blocks,
no async. Use the repo's guard/early-return style
(`references/coding-standards.md`).

### Exception messages (exact)

Use `F` = `typeof(TFixture).FullName ?? typeof(TFixture).Name`,
`C` = `testClass.TestClassName`, and `A` = `fixture.GetType().FullName ?? fixture.GetType().Name`.

- **M1, no test class:**
  `$"RequireFixture<{F}>() requires a running xUnit test class, but none is active (pipeline stage: {context.PipelineStage}). Fixtures are only available while a test class is executing, for example inside a profile registration factory invoked by a [Compose]-attributed theory."`
- **M2, incomplete:**
  `$"Fixture '{F}' for test class '{C}' has not finished initializing. RequireFixture<{F}>() never waits for fixture initialization; declare the fixture concretely (IClassFixture<T>, ICollectionFixture<T>, or [assembly: AssemblyFixture(typeof(T))]) so xUnit initializes it before the test runs."`
- **M3, missing:**
  `$"Test class '{C}' has no fixture of exact type '{F}'. Declare it with IClassFixture<{F}> on the test class, ICollectionFixture<{F}> on the test's collection definition, or [assembly: AssemblyFixture(typeof({F}))]. Fixture lookup matches the exact type only; base types and interfaces don't match."`
- **M4, unexpected type:**
  `$"Fixture lookup for '{F}' in test class '{C}' returned an instance of '{A}', which is not a '{F}'."`

### CI path filters

`src/Shared/Compono.XunitV3/` changes must trigger both packages'
validation. In `.github/workflows/aot-validation.yaml`:

- Add `src/Shared/Compono\.XunitV3/` to the `xunitv3_aot_applicable`
  regex.
- Add a matching `grep -q '^src/Shared/Compono\.XunitV3/' && add_leg "Compono.XunitV3"`
  line.

- In `.github/workflows/docs.yml`, add `'src/Shared/**'` to **both** the
  `push.paths` and `pull_request.paths` lists, next to the existing
  `src/Compono.XunitV3*/**` entries. Without it, a change to the shared
  file alone would skip the API-reference drift check.

Planning confirmed that `pr-build.yaml` and `package-validation.yaml`
have no per-package path filters, and that `publish-preview.yaml` uses
only `paths-ignore` for docs. Leave them unchanged.

## Tasks

Execution is **strictly sequential** in one working tree: Bead 1, then 2,
then 3, then 4, then 5. The beads' dependencies enforce this order.

### Bead 1 (`compono-qle.1`): shared implementation, wiring, unit tests

- [x] `src/Shared/Compono.XunitV3/FixtureContextExtensions.cs` per the specification, with full XML docs
- [x] Wire both `.csproj`s (`<Compile Link>`, plus `COMPONO_XUNITV3_AOT` in the AOT project)
- [x] CI path-filter updates
- [x] `test/Compono.XunitV3.Tests/PublicApiSurfaceTests.cs`: add `"Compono.XunitV3.FixtureContextExtensions"` to the exact-set list
- [x] `test/Compono.XunitV3.Tests/FixtureContextExtensionsTests.cs`: unit tests against `Substitute.For<ITestContext>()` (see Test Plan, unit)

### Bead 2 (`compono-qle.2`, after 1): real-runner tests on both packages

- [x] `test/Compono.XunitV3.Tests/RequireFixtureIntegrationTests.cs` (+ assembly fixture file)
- [x] `test/Compono.XunitV3.Aot.Tests/RequireFixtureTests.cs` (no assembly fixture, with a comment linking xunit/xunit#3664)

### Bead 3 (`compono-qle.3`, after 2): packaged samples and Native AOT publish

- [x] `test/Compono.XunitV3.SampleTests/FixtureProfileTests.cs`
- [x] `test/Compono.XunitV3.Aot.SampleTests/FixtureProfileTests.cs`
- [x] Local Native AOT publish and run of `Compono.XunitV3.Aot.SampleTests`, 0 IL warnings

### Bead 4 (`compono-qle.4`, after 3): docs, cookbook, skill (incl. `Compono.XunitV3.Aot` detection), API reference

- [x] New recipe `docs/cookbook/use-xunit-fixture-values-in-a-profile.md` and an entry in `docs/cookbook/index.md` (alphabetical)
- [x] `docs/packages/compono-xunitv3.md`: a "Using xUnit fixtures in profiles" section. Reword the "No fixture object" bullet so it plainly concerns AutoFixture's `IFixture`, not xUnit fixtures
- [x] `docs/packages/compono-xunitv3-aot.md`: the same section, plus a "Known limitation: assembly fixtures" note linking xunit/xunit#3664
- [x] `docs/troubleshooting/common-errors.md`: a runtime entry for the M1/M3 messages
- [x] `skills/compono/references/xunit-v3.md`: replace "No fixture object" with accurate guidance, add a `RequireFixture` section (identical in `Compono.XunitV3.Aot`), and add a short "Compono.XunitV3.Aot differences" section
- [x] `skills/compono/SKILL.md`: detect `Compono.XunitV3.Aot` so an agent in an AOT-only project finds the xUnit guidance. That means a dedicated detection-table row (the `Include="Compono.XunitV3"` pattern does not match `.Aot`), the reference-loading row for `xunit-v3.md`, and the frontmatter/"SCOPES TO" package list, all routing to `references/xunit-v3.md`. Approved scope expansion (2026-11-04)
- [x] Full-file grep of the touched docs and skill for stale negative claims about fixtures
- [x] Regenerate `docs/reference/api`

### Bead 5 (`compono-qle.5`, after 4): validation gate and closeout

- [x] `dotnet build` (0 warnings, 0 errors) and `dotnet test` green
- [x] xUnit 3.2.2 runtime floor and xUnit 4.0.2 checks (throwaway consumer outside the repo)
- [x] `scripts/dogfood-validate.sh` against trivia-platform and alexa-vox-craft —
      **dropped by policy, not completed**: external-repo dogfooding is **no longer a
      mandatory gate** (product-owner policy change, 2026-11-04; recorded in `AGENTS.md`).
      The earlier dogfood runs (alexa-vox-craft PASS; trivia-platform's post-deploy
      acceptance tests blocked by missing AWS credentials) are retained as historical
      evidence only and are not part of closeout. No environmental exception was granted
      or recorded.
- [x] Record results in Notes, tick tasks, set Status: Done

### Review corrections (2026-11-04, after independent Claude review)

- [x] Rename the accessor class `TestContextExtensions` → `FixtureContextExtensions` (file, both csproj `<Compile Link>`, unit-test file/class, `PublicApiSurfaceTests` expected set, docs, skill, API reference); method, behavior, and exception messages unchanged. Recorded as ADR-0069 Amendment 1.
- [x] Correct the xUnit 3.2.2 compatibility claim: the generic `GetFixture<TFixture>()` lookup **does** exist on the 3.2.2 floor (as `Xunit.TestContextExtensions.GetFixture<T>()`, an async extension). Implementation still uses the non-generic `GetFixture(Type)` for identical cross-version semantics and the never-wait completion check. Recorded as ADR-0069 Amendment 2; RESEARCH-0037 corrected.
- [x] Documentation corrections (ADR-0069 Amendment 3): framework-neutral `Composer.Create(b => b.AddProfile(new P(fixture)))` alternative; exact-type wording (`GetFixture` returns `null` when the exact type is unavailable, `RequireFixture` throws); assembly-fixture wording "reported fixed upstream in xUnit 4.0.3-pre.19; not yet validated by Compono"; example resource ownership (dispose the `WebApplicationFactory`/`HttpClient`); formatting fixes.
- [x] Keep the compiler-generated extension-block type filter in `PublicApiSurfaceTests` (independently verified as correct).
- [x] Regenerate `docs/reference/api` after the rename; confirm `FixtureContextExtensions` + `RequireFixture` pages under both packages.
- [x] Re-run validation: full build/test, both xUnit integration test projects, xUnit 3.2.2 + 4.0.2 scratch consumers, real Native AOT publish/run, `mkdocs build --strict`.
- [x] Update the engineering-workflow policy (`AGENTS.md` §"Consumer/dogfood validation (optional)") to make external-repo dogfooding optional, not mandatory.
- [x] PR scope (product owner approved, 2026-11-04): the `AGENTS.md` dogfooding-policy update ships **inside the #162 PR**, not a separate branch/PR, so the eventual PR description can explain the added scope alongside the fixture accessor.
- [x] Set `Status: Done`; close `compono-qle.5` and the epic.

## Critical Files

- `src/Shared/Compono.XunitV3/FixtureContextExtensions.cs`: new, the single implementation
- `src/Compono.XunitV3/Compono.XunitV3.csproj`, `src/Compono.XunitV3.Aot/Compono.XunitV3.Aot.csproj`: link and define
- `.github/workflows/aot-validation.yaml`, `.github/workflows/docs.yml`: path filters
- `test/Compono.XunitV3.Tests/{FixtureContextExtensionsTests,RequireFixtureIntegrationTests,PublicApiSurfaceTests}.cs`, plus an assembly-fixture file
- `test/Compono.XunitV3.Aot.Tests/RequireFixtureTests.cs`
- `test/Compono.XunitV3.SampleTests/FixtureProfileTests.cs`, `test/Compono.XunitV3.Aot.SampleTests/FixtureProfileTests.cs`
- `docs/cookbook/use-xunit-fixture-values-in-a-profile.md`, `docs/cookbook/index.md`, `docs/packages/compono-xunitv3.md`, `docs/packages/compono-xunitv3-aot.md`, `docs/troubleshooting/common-errors.md`
- `skills/compono/references/xunit-v3.md`, `skills/compono/SKILL.md` (`Compono.XunitV3.Aot` detection)
- `docs/reference/api/**` (regenerated)

## Test Plan

Existing suites stay green unchanged. They already cover `[Compose]`
binding, profiles, seeds, concurrency, and AOT generation, and none of
that changes. New tests cover only the new API. Edge-case branches are
tested once, in the JIT unit tests: the logic is a single shared file, so
AOT duplication would be redundant. Real-runner behavior is tested on
both packages because each compiles against a different xUnit engine.

**Unit, `Compono.XunitV3.Tests/FixtureContextExtensionsTests.cs`** (NSubstitute
fake `ITestContext` and `ITestClass`; AwesomeAssertions, matching the
project's existing style):

1. `null` context throws `ArgumentNullException` with `ParamName == "context"`.
2. `TestClass == null` throws `InvalidOperationException` whose message
   contains the fixture's full name and the `PipelineStage` value, and
   `GetFixture` is never called.
3. Incomplete (`new ValueTask<object?>(new TaskCompletionSource<object?>().Task)`):
   throws `InvalidOperationException` (M2) synchronously, and the TCS task
   is still incomplete afterwards (proves no wait).
4. Faulted (`ValueTask.FromException<object?>(original)`): the thrown
   exception is the **same instance** as `original`.
5. Canceled (`ValueTask.FromCanceled<object?>(new CancellationToken(true))`):
   throws `OperationCanceledException`.
6. Missing (`ValueTask.FromResult<object?>(null)`): throws M3, and the
   message contains the class name, the fixture full name,
   `IClassFixture<`, `ICollectionFixture<`, `AssemblyFixture`, and `exact type`.
7. Unexpected type (returns an instance of a different class): throws M4
   containing both type names.
8. Success: returns the same reference. `GetFixture(typeof(TFixture))` was
   received exactly once, and the generic `GetFixture<TFixture>()` was
   never received (the implementation intentionally uses the non-generic
   lookup for identical semantics across supported xUnit versions and the
   never-wait completion check — not because the generic lookup is absent).

**Real runner, `Compono.XunitV3.Tests/RequireFixtureIntegrationTests.cs`**
(all through `[Theory, Compose<TProfile>]`, where the profile calls
`TestContext.Current.RequireFixture<…>()` **inside a `Register` factory**):

- **Class fixture** (`IAsyncLifetime`, value assigned in
  `InitializeAsync` after `await Task.Yield()`): the composed value equals
  the constructor-injected fixture's value, which proves it was
  initialized.
- **Collection fixture** via `[CollectionDefinition]` +
  `ICollectionFixture<T>` and `[Collection(...)]`: same assertion.
- **Assembly fixture:** a new file
  `test/Compono.XunitV3.Tests/AssemblyFixtures.cs` declares
  `[assembly: AssemblyFixture(typeof(RequireFixtureAssemblyFixture))]`
  (trivial, no I/O). The test asserts the composed value comes from it.
- **Parallel isolation:** four test classes, each with
  `IClassFixture<IdFixture>` where `IdFixture.Id` comes from a static
  `Interlocked.Increment`. Each asserts that the composed `Id` equals its
  own constructor-injected fixture's `Id`. Classes run in parallel by
  default (separate collections), so don't disable parallelization.
- **Missing fixture through the real context:** a `[Fact]` in a class
  with no fixtures calls `TestContext.Current.RequireFixture<IdFixture>()`
  inside `Assert.Throws`/`Should().Throw` and asserts the M3 content.
  (Don't add a deliberately failing composed theory.)

**Real runner, `Compono.XunitV3.Aot.Tests/RequireFixtureTests.cs`**
(plain `Assert`, since this project has no AwesomeAssertions or
NSubstitute): class fixture, collection fixture, parallel isolation (four
classes), and missing via `Assert.Throws`. No assembly fixture: add a
comment explaining that xunit/xunit#3664 makes `[assembly: AssemblyFixture]`
fail to compile under the xUnit AOT generator in 4.0.1/4.0.2.

**Packaged consumer samples** (Bead 3): one class-fixture and one
collection-fixture example per sample project, written as documentation
-quality usage. The AOT sample is the permanent Native AOT proof, because
CI's `aot-xunitv3-aot-pipeline` job publishes and runs it.

## Validation commands

Run from the repo root unless stated otherwise. Each bead lists which of
these it must run.

```bash
# V1: full build/test (must be 0 warnings, 0 errors, all green)
dotnet build
dotnet test

# V2: focused
dotnet test test/Compono.XunitV3.Tests
dotnet test test/Compono.XunitV3.Aot.Tests
dotnet test test/Compono.XunitV3.SampleTests
dotnet test test/Compono.XunitV3.Aot.SampleTests

# V3: real Native AOT publish + run (mirror of CI's aot-xunitv3-aot-pipeline job; use your RID, e.g. osx-arm64 / linux-x64)
dotnet publish test/Compono.XunitV3.Aot.SampleTests -c Release -f net10.0 \
  -p:RuntimeIdentifier=osx-arm64 -p:SelfContained=true -p:PublishAot=true -p:UseAppHost=true \
  -o test/Compono.XunitV3.Aot.SampleTests/bin/aot-publish 2>&1 | tee /tmp/aot-publish.log
grep -E "warning (IL|AOT)[0-9]+" /tmp/aot-publish.log && echo "FAIL: AOT/trim warnings" || echo "OK: no AOT/trim warnings"
./test/Compono.XunitV3.Aot.SampleTests/bin/aot-publish/Compono.XunitV3.Aot.SampleTests

# V4: API reference regeneration (any public XML doc change)
for pkg in Compono Compono.XunitV3 Compono.XunitV3.Aot Compono.NSubstitute Compono.Bogus Compono.TUnit \
           Compono.TestDoubles Compono.DependencyInjection Compono.Http Compono.Logging Compono.MSTest Compono.NUnit; do
  dotnet build "src/$pkg/$pkg.csproj" -c Release -f net10.0
done
dotnet tool restore
.github/scripts/generate-api-reference.sh
git status --short docs/reference/api   # expect new FixtureContextExtensions pages under both Compono.XunitV3 and Compono.XunitV3.Aot

# V5: xUnit runtime floor (3.2.2) and current (4.0.2), in a throwaway consumer OUTSIDE the repo
#   1. Pack: dotnet pack src/Compono/Compono.csproj and src/Compono.XunitV3/Compono.XunitV3.csproj
#      -c Release -o <scratch>/feed -p:Version=0.0.0-local.<timestamp>
#   2. Scratch net10.0 test project: PackageReference Compono + Compono.XunitV3 at that version,
#      xunit.v3.mtp-v2 Version="3.2.2"; nuget.config with <scratch>/feed first, plus nuget.org.
#      Tests: class fixture + collection fixture via [Compose<TProfile>] + RequireFixture in a factory,
#      and a missing-fixture Assert.Throws.
#   3. dotnet test (or dotnet run); confirm obj/project.assets.json resolved
#      "xunit.v3.extensibility.core/3.2.2".
#   4. Repeat with xunit.v3.mtp-v2 Version="4.0.2" (confirm 4.0.2 resolved).
#   Record versions, commands, and results in Notes. Delete nothing in the repo; the scratch dir stays outside it.

# V6 (optional, no longer a mandatory gate): dogfood against a real consumer repo,
#     per AGENTS.md "Consumer/dogfood validation (optional)". Not required for closeout.
scripts/dogfood-validate.sh                       # trivia-platform, default packages (incl. Compono.XunitV3)
scripts/dogfood-validate.sh --consumer-repo ../alexa-vox-craft \
  --packages "Compono Compono.XunitV3 Compono.XunitV3.Aot Compono.Http Compono.TestDoubles Compono.Bogus Compono.Logging Compono.Options"
```

## Notes

- **2026-11-03 (planning):** ADR and plan written from RESEARCH-0037 rounds
  1 and 2 plus the product-owner decisions. Plan number 0070 differs from
  ADR-0069 because PLAN-0069 was already taken (implementing ADR-0068).
- Constraint carried forward to #163 (ADR-0069 "Constraint carried
  forward"): composed rows must remain delayed-enumerated.
- The `alexa-vox-craft` checkout was on branch
  `fix/apl-model-context-coverage-198` at planning time. V6 runs against
  whatever branch is checked out, so record which one.
- Beads: epic `compono-qle` with children `.1`–`.5` in the local beads database
  (`bd show compono-qle`). The database is local-only: excluded via
  `.git/info/exclude`, with no Dolt remote configured.
- **2026-11-04 (handoff, product-owner decisions):**
  - **Branch.** The approved branch is `feat/162-require-fixture`. The
    Bead 1 implementer creates it from `main`. The planning docs travel
    with it.
  - **Sequential execution.** Beads run strictly in order (.1 through
    .5), in one working tree. There are no parallel agents.
  - **Beads stay local.** Beads are local agent orchestration state:
    excluded via `.git/info/exclude`, never tracked, no Dolt remote
    (`bd init` had auto-added the code repo as a remote; it was removed,
    `sync.remote` unset, and `dolt.auto-push=false` set), no git sync,
    never pushed. Never run `bd dolt push`/`pull`/`sync`/`remote add`.
  - **Skill scope.** Bead 4 now includes `Compono.XunitV3.Aot` detection
    in `skills/compono/SKILL.md`.
  - **Reporting failures.** Environment or external-service failures
    (Docker, NuGet/network, RID toolchain) are reported exactly as such,
    with the error text. They're never marked passed and never treated
    as Compono regressions.
  - **#163.** The delayed-enumeration constraint was posted on #163.
- **2026-11-04 (Bead 1 findings):**
  - **Extension declaration form.** The plan's "Code shape" showed the
    legacy `static class` + `this`-parameter form, but
    `references/coding-standards.md` requires C# 14 extension-block syntax
    for extension methods, and `Compono.NSubstitute`/`Compono.Bogus`/
    `Compono.TestDoubles` all already use it. Confirmed with the product
    owner: implemented as an extension block
    (`extension(ITestContext context)`), which produces the same public
    API surface and call sites (`context.RequireFixture<TFixture>()`).
    Because extension blocks lower to a public nested marker type,
    `PublicApiSurfaceTests` also gained the `!type.Name.Contains('<')`
    exclusion its three sibling packages already carry.
  - **Upstream AOT assembly-fixture fix.** xUnit **reported** xunit/xunit#3664
    (the AOT assembly-fixture generator `CS1593` bug) fixed in
    **4.0.3-pre.19**; not yet validated by Compono.
    Stable dependency constraints are unchanged (Compono adds no preview
    dependency). Bead 4 must describe the affected upstream versions and
    the preview fix instead of "permanently unsupported", and Bead 5 adds
    an isolated 4.0.3-pre.19 compatibility check plus a real AOT
    assembly-fixture publish/run when that preview is obtainable from its
    documented feed; if the feed is unavailable, report the blocker rather
    than substituting a version. RESEARCH-0037 gets the same update.
- **2026-11-04 (Bead 2 findings):**
  - **Real-runner coverage landed.** `RequireFixtureIntegrationTests.cs`
    (+ `AssemblyFixtures.cs`) in `Compono.XunitV3.Tests` and
    `RequireFixtureTests.cs` in `Compono.XunitV3.Aot.Tests` cover the
    documented call site - reading the fixture from *inside* a profile
    registration factory (`builder.Register(() => new Wrapper(
    TestContext.Current.RequireFixture<TFixture>()...))`) - through real
    `[Theory, Compose<TProfile>]` runs. Cases in both projects: class
    fixture (async, value set in `InitializeAsync` after `Task.Yield()`),
    collection fixture (`[CollectionDefinition]` +
    `ICollectionFixture<T>` + `[Collection]`), four-class parallel
    isolation (each class its own `IClassFixture<IdFixture>` with an
    `Interlocked`-assigned id), and the missing-fixture path as a plain
    `[Fact]` asserting the M3 guidance. Assembly fixture is JIT-only,
    with the AOT file carrying the xunit/xunit#3664 explanatory comment.
  - **Verification.** `Compono.XunitV3.Tests`: 376 passed / 0 failed
    (94 x 4 TFMs; +8 tests/TFM over Bead 1). `Compono.XunitV3.Aot.Tests`:
    39 passed / 0 failed (13 x 3 TFMs; +7 tests/TFM). Both run green
     twice from their own project directories (path-less `dotnet test`,
     per the documented `test/<proj>` doubled-path workaround). Build 0
     warnings / 0 errors.
- **2026-11-04 (Bead 3 findings):**
  - **Packaged-sample examples landed.** `FixtureProfileTests.cs` in both
    `Compono.XunitV3.SampleTests` and `Compono.XunitV3.Aot.SampleTests`
    (both still `PackageReference`, never `ProjectReference`) now contain a
    documentation-quality DB-style class fixture (`IAsyncLifetime`, value
    set in `InitializeAsync` after `Task.Yield()`), a profile registering a
    settings record from `TestContext.Current.RequireFixture<TFixture>()`
    inside a `Register` factory, and a `[Theory, Compose<TProfile>]`
    asserting the composed value matches the class fixture; plus a
    collection-fixture example (`[CollectionDefinition]` +
    `ICollectionFixture<T>` + `[Collection]`). The AOT file uses
    `using Compono.XunitV3.Aot;` + plain `Assert` and intentionally omits
    an assembly fixture (xunit/xunit#3664).
  - **Verification.** `Compono.XunitV3.SampleTests`: 48 passed / 0 failed
    (12 x 4 TFMs), run from its own directory with the CI invocation
    `dotnet test -c Release -- --filter-not-class "Compono.XunitV3.SampleTests.Failing*"`.
    `Compono.XunitV3.Aot.SampleTests` (single TFM net10.0): JIT run via
    `dotnet run -c Release` -> 5 passed / 0 failed (the three pre-existing
    compose theories + the two new fixture-profile theories). Note:
    `dotnet test` on the AOT sample project is rejected by the SDK's
    MTP/VSTest runner check (this project is never run by `dotnet test` in
    CI either); `dotnet run` is the working JIT invocation.
  - **Native AOT publish + run (V3).** `dotnet publish ... -c Release -f net10.0
    -p:RuntimeIdentifier=osx-arm64 -p:SelfContained=true -p:PublishAot=true
    -p:UseAppHost=true -o .../bin/aot-publish` succeeded with **zero
    `warning IL*`/`warning AOT*` lines** (log scanned explicitly). The native
    binary `Compono.XunitV3.Aot.SampleTests` then ran: **5 passed / 0
    failed**. RID `osx-arm64`; SDK `11.0.100-rc.3.26522.148`; runner
    `xUnit.net v3 Microsoft.Testing.Platform v2 Runner v4.0.1`.
- **2026-11-04 (Bead 4 findings):**
  - **Docs landed.** New recipe
    `docs/cookbook/use-xunit-fixture-values-in-a-profile.md` (+ alphabetical
    index entry); a "Using xUnit fixtures in profiles" section on both
    `docs/packages/compono-xunitv3.md` (which also rewords its "No fixture
    object" bullet to explicitly concern AutoFixture's `IFixture`, not xUnit
    fixtures) and `docs/packages/compono-xunitv3-aot.md` (which also gains a
    "Known limitation: assembly fixtures" note linking xunit/xunit#3664,
    worded as temporary/upstream, not permanent); a `RequireFixture<TFixture>()`
    runtime entry in `docs/troubleshooting/common-errors.md` covering all
    four exception messages.
  - **Skill.** `skills/compono/references/xunit-v3.md` replaces "No fixture
    object" with an accurate `RequireFixture` section (call inside the
    `Register` factory, exact-type matching, never-waits, fixture stays
    xUnit's) plus a "Compono.XunitV3.Aot differences" section (same method in
    namespace `Compono.XunitV3.Aot`; assembly fixtures blocked by
    xunit/xunit#3664). `skills/compono/SKILL.md` gains a dedicated
    `Compono.XunitV3.Aot` detection-table row (the `Include="Compono.XunitV3"`
    pattern does not match `.Aot`), extends the `references/xunit-v3.md`
    loading row, updates the frontmatter package list (XunitV3/XunitV3.Aot),
    and adds `Compono.XunitV3.Aot` to the shipped-packages guardrail list.
  - **No stale claims.** Full-file grep of every touched docs/skill file
    found no remaining negative fixture statements — remaining "fixture"
    hits are AutoFixture references or the new positive xUnit guidance.
  - **API reference regenerated (V4).** Built all 12 publishable packages
    Release/net10.0, `dotnet tool restore`, ran
    `.github/scripts/generate-api-reference.sh`. New pages:
    `docs/reference/api/Compono.XunitV3/Compono.XunitV3.FixtureContextExtensions{,.RequireFixture_TFixture_(thisXunit.ITestContext)}.md`
    and the two `.Aot` equivalents; the two package `index` pages updated.
  - **Docs site verified.** `uv run mkdocs build --clean --strict` exits 0
    with zero warnings/errors (validates the new recipe links, API
    reference links, and nav).
  - RESEARCH-0037 amended with the 4.0.3-pre.19 addendum (assembly-fixture
    limitation expected temporary; reported fixed upstream on the preview
    line, not yet validated by Compono).
- **2026-11-04 (Bead 5 findings / validation results):**
  - **V1 — full build/test (green).** `dotnet build` (whole solution) = 0
    warnings / 0 errors. `dotnet test` (whole solution) = **4092 passed /
    0 failed / 0 skipped** (34s 980ms). Includes `Compono.XunitV3.Tests`
    (94 x 4 TFMs) and `Compono.XunitV3.Aot.Tests` (13 x 3 TFMs).
  - **V3 — Native AOT publish + run (green).** `dotnet publish
    test/Compono.XunitV3.Aot.SampleTests -c Release -f net10.0
    -p:RuntimeIdentifier=osx-arm64 -p:SelfContained=true -p:PublishAot=true
    -p:UseAppHost=true -o test/Compono.XunitV3.Aot.SampleTests/bin/aot-publish`.
    `grep -E "warning (IL|AOT)[0-9]+"` over the publish log = **no matches**
    (0 total `warning` lines). Native binary
    (`.../bin/aot-publish/Compono.XunitV3.Aot.SampleTests`, Mach-O arm64)
    ran: **5 passed / 0 failed**. Runner `xUnit.net v3
    Microsoft.Testing.Platform v2 Runner v4.0.1`; SDK
    `11.0.100-rc.3.26522.148`.
  - **V5 — xUnit floor/current (green).** Scratch consumer **outside** the
    repo (`/var/folders/.../opencode/issue162-v5-<ts>/consumer`), net10.0,
    `PackageReference` Compono + Compono.XunitV3 at
    `99.0.0-local` packed from this tree, `xunit.v3.mtp-v2` pinned per run.
    Tests: class fixture + collection fixture via `[Compose<TProfile>]` +
    `RequireFixture` inside a `Register` factory, plus a missing-fixture
    `Assert.Throws`. With `xunit.v3.mtp-v2` **3.2.2**: `obj/project.assets.json`
    resolved `xunit.v3.extensibility.core/3.2.2`; `dotnet run -c Release` →
    **3 passed / 0 failed** (runner v3.2.2). With **4.0.2**: resolved
    `xunit.v3.extensibility.core/4.0.2`; **3 passed / 0 failed** (runner
    v4.0.2). (`dotnet test` alone is blocked by the .NET 10 SDK's
    "VSTest target is no longer supported … opt in to the new dotnet test
    experience" guard, so the MTP executable was run via `dotnet run`, the
    plan's documented alternative.)
  - **V5 addendum — 4.0.3-pre.19 (not obtainable; reported, not
    substituted).** The preview carrying the xunit/xunit#3664 fix is **not
    currently obtainable**: `xunit.v3.mtp-v2`/`xunit.v3.aot.mtp-v2` on
    nuget.org top out at **4.0.2** (no `4.0.3-pre.*`), and the xUnit
    MyGet preview feed (`https://www.myget.org/F/xunit/...`) now returns
    **HTTP 404**. Per the Bead 1 addendum, the isolated 4.0.3-pre.19
    compatibility check and AOT assembly-fixture publish/run were therefore
    **skipped and reported as a feed-availability blocker** rather than
    substituting a version. Stable constraints (`[3.2.2, 5.0.0)` JIT,
    `[4.0.1, 5.0.0)` AOT) are unchanged.
  - **V6a — trivia-platform dogfood: historical evidence only (no longer
    part of any gate).** The following run predates the 2026-11-04 policy
    change that made external-repo dogfooding optional (see `AGENTS.md`
    §"Consumer/dogfood validation (optional)"). It is retained purely as a
    historical record and is **not** an acceptance criterion for this plan;
    no environmental exception was granted or recorded. `scripts/dogfood-validate.sh`
    (default; auto-detected `Trivia.Platform.slnx`; consumer checkout branch
    `dependabot/nuget/dotnet-minor-patch-e52cbd65fd`, commit `8428cde`)
    packed and **version-verified all four default packages** to
    `99.0.0-local.20261104082644-60079-692`. Docker was running. The suite
    then ran **749 passed / 37 failed**, where **all 37 failures are in
    `Disney.Trivia.Skill.AcceptanceTests`** and every one is the AWS
    credential chain failing at `AWSSecretsManager` configuration load:
    `AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY/AWS_SESSION_TOKEN were not
    set`, `webIdentityTokenFile must be an absolute path`, `profile
    [default] is an SSO profile … not allowed here`, `Failed to connect to
    EC2 instance metadata`. Per trivia's own ADR-0017/README, those are
    **post-deploy** acceptance tests requiring live AWS resources and are
    **explicitly not run in PR CI**. No failure references Compono, the
    generator, or `RequireFixture`. Exit code 2. The consumer's tracked
    tree was left byte-identical (verified `git status --porcelain` empty
    before and after). → Historical evidence only; not part of closeout.
    - Supplementary evidence: in that same full run every non-acceptance
      trivia project passed against the freshly packed packages (engine
      unit tests 546 + 16 + 8 + 4 + 67, architecture 34, APL 29, infra 33,
      Disney host 9, ThreeOaths host 3). A follow-up run against the
      PR-validation subset (`--consumer-solution Disney.Trivia.Skill.slnf`)
      was attempted but the script's version check tripped over
      `ThreeOaths.Trivia.Skill.Host.Tests/obj/project.assets.json` retaining
      the previous run's local version (that project is outside the Disney
      filter), i.e. a stale-obj artifact of running the earlier full
      solution first — again not a Compono issue, and not part of the
      criterion, so not pursued.
  - **V6b — alexa-vox-craft dogfood: historical evidence only (PASS).**
    The following run predates the 2026-11-04 policy change and is retained
    as history, not as a gate. `scripts/dogfood-validate.sh
    --consumer-repo ../alexa-vox-craft --packages "Compono Compono.XunitV3
    Compono.XunitV3.Aot Compono.Http Compono.TestDoubles Compono.Bogus
    Compono.Logging Compono.Options"` (consumer branch
    `fix/apl-model-context-coverage-198`, commit `ec6dc3c`) packed and
    version-verified all **8** packages to
    `99.0.0-local.20261104082814-61242-10386`, then **PASS — 2847 passed /
    0 failed**. Consumer tracked tree left clean.
  - **2026-11-04 (review corrections + closeout).** After independent Claude
    review, the accessor class was renamed `TestContextExtensions` →
    `FixtureContextExtensions` (ADR-0069 Amendment 1), the xUnit 3.2.2
    generic-`GetFixture<T>()` claim was corrected (ADR-0069 Amendment 2;
    RESEARCH-0037), and the docs were corrected for the framework-neutral
    alternative, exact-type wording, assembly-fixture status, and example
    resource ownership (ADR-0069 Amendment 3). Validation was re-run after
    the rename: full `dotnet build`/`dotnet test`, both xUnit integration
    test projects, xUnit 3.2.2 + 4.0.2 scratch consumers, real Native AOT
    publish/run, `mkdocs build --strict`, and API-reference regeneration.
    External-repo dogfooding was made optional by product-owner policy
    (`AGENTS.md`), so the V6a/V6b runs above are historical only and no
    environmental exception was recorded. All required checks pass →
    **`Status: Done`**; `compono-qle.5` and epic `compono-qle` closed.
  - **2026-11-04 (post-rename validation results, exact).**
    - `R1 — build/test (green).` `dotnet build` (whole solution) = 0
      warnings / 0 errors. `dotnet test` (whole solution) = **4092 passed /
      0 failed / 0 skipped** (32s 495ms).
    - `R2 — focused xUnit projects (green).` `Compono.XunitV3.Tests` (from
      its own dir, path-less `dotnet test -c Debug`) = **376 passed / 0
      failed**; `Compono.XunitV3.Aot.Tests` = **39 passed / 0 failed**.
    - `R3 — Native AOT (green).` `dotnet publish
      test/Compono.XunitV3.Aot.SampleTests -c Release -f net10.0
      -p:RuntimeIdentifier=osx-arm64 -p:SelfContained=true -p:PublishAot=true
      -p:UseAppHost=true` → **0 `warning IL*`/`warning AOT*` matches, 0 total
      `warning` lines**; native Mach-O arm64 binary ran **5 passed / 0
      failed**.
    - `R4 — xUnit floor/current (green).` Fresh scratch consumer **outside**
      the repo (`.../issue162-v5-1793800500/consumer`), net10.0,
      `PackageReference` Compono + Compono.XunitV3 at
      `0.0.0-local.1793800500` packed from this tree. With `xunit.v3.mtp-v2`
      **3.2.2**: `obj/project.assets.json` resolved
      `xunit.v3.extensibility.core/3.2.2` → **3 passed / 0 failed**. With
      **4.0.2**: resolved `xunit.v3.extensibility.core/4.0.2` → **3 passed /
      0 failed**. (`4.0.3-pre.19` remains unobtainable — see the V5 addendum;
      its upstream-fix behavior is recorded as not yet validated by Compono.)
    - `R5 — docs (green).` `uv run mkdocs build --clean --strict` = exit 0,
      no warnings/errors.
    - `R6 — API reference (green).` Rebuilt all 12 publishable packages
      Release/net10.0 (0 warnings each), `dotnet tool restore`,
      `.github/scripts/generate-api-reference.sh`. Generated
      `Compono.XunitV3.FixtureContextExtensions{,.RequireFixture_TFixture_(thisXunit.ITestContext)}.md`
      and the two `.Aot` equivalents; the stale `TestContextExtensions` pages
      are gone (the script `rm -rf`s each package output dir first); only the
      two package `index` pages changed.
    - `R7 — dogfood (skipped by policy).` Not re-run; optional per the
      updated `AGENTS.md` policy.
