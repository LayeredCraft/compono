# RESEARCH-0037: Runtime Fixture Values in Composition Profiles (Issue #162)

**Status:** Concluded (2026-11-03). The product owner approved option B
(§10–§11), recorded as
[ADR-0069](../adr/0069-xunitv3-require-fixture-accessor.md) (Accepted).
Implementation is tracked by
[PLAN-0070](../plans/0070-xunitv3-require-fixture-accessor-impl-plan.md).
Round 1 recommended documentation only and is superseded (§9). The
upstream xUnit AOT assembly-fixture bug is filed as
[xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664).

**Issue:** [#162](https://github.com/LayeredCraft/compono/issues/162)
(child of [#161](https://github.com/LayeredCraft/compono/issues/161), item 4).
**Builds on:** [RESEARCH-0036](0036-issue-161-test-composition-gaps-intake.md) §2
(intake spike), [RESEARCH-0016](0016-async-composition-viability-research.md)
(the test framework's fixture lifecycle owns async setup, and composition
receives the already-running value).
**Governing process:**
[`docs/architecture/capability-admission.md`](../architecture/capability-admission.md).

## 1. Summary of findings

- **#162 needs no new mechanism. A working path already exists.** (Round 2,
  §10–§11, still recommends a small Compono-owned accessor over that path
  for correctness and DX.) On both `Compono.XunitV3` and `Compono.XunitV3.Aot` (including a
  real Native AOT publish), a profile's registration factory can reliably
  read class, collection, and assembly fixtures through xUnit's own
  `TestContext.Current.GetFixture(...)`.
- **Retrieval doesn't block in practice.** xUnit creates and initializes
  every declared fixture before any test in scope runs, and Compono
  composes after that. In every run, across all parallel modes, the
  `ValueTask` had already completed by the time a factory saw it (19/19
  calls per run). `GetAwaiter().GetResult()` returns a cached value and
  never waits on anything.
- **Blocking is still unsafe as a general habit.** Under
  `parallelAlgorithm=aggressive`, xUnit installs `MaxConcurrencySyncContext`
  around the factory call. If the `ValueTask` were ever incomplete,
  blocking there could deadlock. The documented pattern should check
  `IsCompleted` and fail fast instead of blocking (§6).
- **No better existing mechanism serves the attribute path.** The only
  ambient-free option is a class-level composer whose profile instance
  takes the fixture through its constructor (`AddProfile(new P(fixture))`).
  It works under JIT and Native AOT but gives up `[Compose<TProfile>]`
  parameter injection (§7, option B).
- **Assembly fixtures under `Compono.XunitV3.Aot` fail because of an xUnit
  bug, not Compono.** The `xunit.v3.core.aot.generators` source generator
  in 4.0.1 and 4.0.2 emits code that doesn't compile for any
  `[assembly: AssemblyFixture]`, even in a project with no Compono
  reference. Registering the same factory by hand makes the scenario pass
  under Native AOT, Compono composition included (§5).

## 2. Consumer requirement

From #161/#162: a SUT composed via `[Compose<TProfile>]` depends on values
that only exist at runtime, created by an xUnit fixture (container
connection string, broker endpoint). The consumer wants to keep
attribute-driven composition rather than calling `Composer.Create(...)`
in every test, and wants a clear failure when the fixture is missing.

## 3. Existing architecture (relevant parts)

| Concern | `Compono.XunitV3` (JIT) | `Compono.XunitV3.Aot` |
|---|---|---|
| Entry point | `ComposeAttribute.GetData` (reflection-mode `DataAttribute`) | Generator-emitted `RegisteredEngineConfig.RegisterTheoryDataRowFactory` (`AotTheoryDataRowRegistration.scriban`) |
| Discovery enumeration | `SupportsDiscoveryEnumeration() => false` | `disableDiscoveryEnumeration: true` |
| When the profile is constructed and `Configure` runs | Once per attribute instance, lazily on first `GetData` (`Lazy<Composer>`) | Every time the factory runs (a new `Composer.Create(...)` per call) |
| When registration factories run | Per composition (`CreateRow` → `Resolve`) | Same |
| Profile shape | `TProfile : new()`, or `TProfile(TConfig)` with attribute-constant config | Same, via generator-emitted `new` |

Core `Compono` exposes nothing test-framework-related.
`ICompositionContext` offers only `Resolve`, `DeriveSeed`, and
`ResolveCollectionSize`, so a profile has no Compono-provided way to reach
a test-class instance or its fixtures. That's by design: core must not
know about xUnit.

## 4. xUnit v3 fixture lifecycle (decompiled, xUnit 4.0.1)

Sources: `xunit.v3.extensibility.core` 4.0.1 (`xunit.v3.core.dll`,
netstandard2.0) and `xunit.v3.extensibility.core.aot` 4.0.1
(`xunit.v3.core.aot.dll`, net9.0), decompiled with `ilspycmd` 9.1.

1. **Creation is eager.** Before running any test in scope, the assembly,
   collection, and class runners call
   `FixtureMappingManager.InitializeAsync(types, createInstances: anyNonStaticallySkipped)`:
   `XunitTestAssemblyRunnerBase:35`, `XunitTestCollectionRunnerBase:40`,
   `XunitTestClassRunnerBase:122`, and the matching `CodeGen*RunnerBase`
   types on the AOT engine. Each fixture is constructed, its
   `IAsyncLifetime.InitializeAsync` is awaited, and the instance is cached.
2. **Lookup is hierarchical.** The managers are chained: class → collection
   → assembly (`XunitTestClassRunnerBaseContext.ClassFixtureMappings` takes
   the collection manager as parent, and the collection manager takes the
   assembly manager). `TestContext.SetForTestClass(..., ClassFixtureMappings)`
   attaches the class manager. Method-level and case-level contexts copy
   it (`MethodFixtureMappings` is the class manager instance).
3. **`GetFixture` returns.** `TestContext.GetFixture(Type)` →
   `FixtureMappingManager.TryGetFixture(type, forceCreation: true)`. It
   returns the cached instance first. If the type isn't declared at this
   level it delegates to the parent, and if no level declares it, it
   returns `null`. Matching is **exact type**: asking for an interface the
   fixture implements returns `null`. The generic `GetFixture<T>()` lookup
   is available on every supported xUnit version: 3.2.2 (the
   `Compono.XunitV3` floor, `[3.2.2, 5.0.0)`) and 4.x both expose
   `GetFixture(Type)` as the **non-generic instance method**, and both also
   ship xUnit's public **generic async extension**
   `Xunit.TestContextExtensions.GetFixture<TFixture>(this ITestContext)`,
   which wraps that call. Only the generic **instance** member
   (`ITestContext.GetFixture<T>()`) is 4.x-only. Compono's accessor uses the
   non-generic method on purpose (see the correction addendum at the end of
   this document).
4. **When it could do async work.** Only if the type is declared at a
   level but wasn't created up front: an open-generic fixture declaration,
   or a level where every test was statically skipped. Then
   `forceCreation: true` constructs the fixture and awaits
   `InitializeAsync` **inside** the returned `ValueTask`. Neither case
   arises for a composed test that actually runs and declares its fixture
   concretely. The safe pattern in §6 still handles it explicitly.
5. **Fixture failure.** If a fixture constructor or `InitializeAsync`
   throws, the class runner's aggregator records it and
   `RunTestMethods(ctxt, exception)` fails every test case without
   creating tests, so composition never runs.
6. **Data timing.** Both engines run the delayed-enumeration data factory
   in `CreateTests()` (`XunitDelayEnumeratedTheoryTestCase.CreateTests`,
   and the AOT `TheoryTestCaseFactory.GenerateDelayEnumerated` test
   factory) during test-method execution, after class setup.

## 5. Experimental findings

### Setup (reproducible)

Scratchpad only, outside the repo. Three projects share one source file:

- **JIT:** `Compono` 1.3.2, `Compono.XunitV3` 1.3.2, `xunit.v3.mtp-v2` 4.0.1.
- **AOT:** `Compono` 1.3.2, `Compono.XunitV3.Aot` 1.3.2,
  `xunit.v3.aot.mtp-v2` 4.0.1, `<PublishAot>true</PublishAot>`.
- **Assembly-fixture repro:** `xunit.v3.aot.mtp-v2` 4.0.1 and 4.0.2 alone,
  **no Compono reference**.

Environment: .NET SDK 11.0.100-rc.3.26522.148, `net10.0` target,
`global.json` rolling forward from 10.0.100, MTP runner, macOS
(Darwin 25.6.0) `osx-arm64`.

```bash
dotnet build -p:DefineConstants='JIT%3BASMFIX' && PROBE_LOG=jit.log dotnet run --no-build [-- -parallelAlgorithm aggressive -maxThreads 2 | -maxThreads 1 | -parallelMode none]
dotnet build && PROBE_LOG=aotjit.log dotnet run --no-build                            # AOT package, JIT-run
dotnet publish -c Release -r osx-arm64 -o aot-pub -p:DefineConstants='AOT%3BASMMANUAL'  # real Native AOT
PROBE_LOG=aotnative.log ./aot-pub/Aot [-parallelAlgorithm aggressive -maxThreads 2 | -maxThreads 1]
```

Each fixture used truly asynchronous initialization
(`await Task.Delay(100)` in `InitializeAsync`) and a per-instance `Id`, so
an uninitialized or wrong fixture would show up. Probes logged
`TestContext.Current.PipelineStage`, test class, test method,
`ValueTask.IsCompletedSuccessfully` before blocking, and
`SynchronizationContext.Current`. They ran in the profile constructor, in
`Configure`, and in the registration factory.

Scenarios: class fixture (two methods, lazy factory access, plus
`Configure`-time "eager" access); collection fixture; assembly fixture;
interface lookup; missing fixture; one inherited theory in an abstract
base class run by two derived classes, each with its own class-fixture
instance (profile-caching hazard); eight classes in separate parallel
collections, each checking it got *its own* fixture instance (`AsyncLocal`
isolation under parallelism); and an explicit class-level composer
(option B, §7).

### Results

| Scenario | JIT (`Compono.XunitV3`) | AOT package, JIT-run | **Native AOT publish** |
|---|---|---|---|
| Class fixture, factory access (incl. 2nd method) | Pass | Pass | Pass |
| Class fixture, `Configure`-time access | Pass | Pass | Pass |
| Collection fixture (`IAsyncLifetime`) | Pass | Pass | Pass |
| Assembly fixture via `[assembly: AssemblyFixture]` | Pass | **Doesn't compile (xUnit generator bug)** | Same |
| Assembly fixture via hand-written registration (see below) | n/a | n/a | Pass |
| Inherited theory, 2 derived classes, distinct fixtures (lazy and eager) | Pass, each sees its own | Pass | Pass |
| 8 parallel classes, each sees its own fixture | Pass | Pass | Pass |
| Interface lookup (`GetFixture<IConnInfo>`, fixture implements it) | `null` (exact type) | `null` | `null` |
| Missing fixture | `null`, so composition fails (below) | Same | Same |
| Option B: class-level composer, `AddProfile(new P(fixture))` | Pass | Pass | Pass |
| AOT/trim warnings on publish | n/a | n/a | **0** |

Totals per run: 20 tests, 2 failures (missing fixture and interface
lookup, both expected). The AOT run without the assembly fixture had 19
tests, 2 expected failures. These results held in every parallel
configuration tried.

Lifecycle observations, identical on all three engines:

- The profile constructor, `Configure`, and registration factories all ran
  at `PipelineStage = TestMethodExecution`, with `TestClass`/`TestMethod`
  set and fixtures available. None ran during discovery.
- **19/19** `GetFixture` calls per run returned an already-completed
  `ValueTask`, under default, `-maxThreads 1`, `-maxThreads unlimited`,
  `-parallelMode none`, and `-parallelAlgorithm aggressive -maxThreads 2`.
- `SynchronizationContext.Current` was `null`, except under
  `-parallelAlgorithm aggressive`, where it was `MaxConcurrencySyncContext`
  (JIT and Native AOT alike).

### Missing-fixture failure today

```text
Compono.CompositionException : The registration or configuration-rule factory for 'DbSettings' threw: <consumer's own message>

Seed: 755381082
```

That is the `Compono.XunitV3` (JIT) shape. Under `Compono.XunitV3.Aot`,
both JIT-run and native, the same failure carries **no `Seed:` line**
(see §10, unexpected finding). In both packages, Compono adds the
registered type, and everything else comes from the consumer's own
exception message. Diagnostic quality therefore depends entirely on the
helper the consumer writes (§6). The §6 helper was verified to produce
its message on both packages, including a native AOT publish with 0
warnings.

### Assembly-fixture failure under `Compono.XunitV3.Aot`: classified

Minimal repro, with only `xunit.v3.aot.mtp-v2` and no Compono:

```csharp
[assembly: AssemblyFixture(typeof(AsmFixture))]
public sealed class AsmFixture { }
```

```text
AssemblyFixtureAttribute٠….g.cs(15,112): error CS1593: Delegate 'Func<bool, ValueTask<object?>>' does not take 0 arguments
```

The generator emits
`RegisterAssemblyFixtureFactory(typeof(AsmFixture), async () => new AsmFixture())`,
but the API in `RegisteredEngineConfig` is
`RegisterAssemblyFixtureFactory(Type, Func<bool, ValueTask<object?>>)`.
This reproduces on 4.0.1 and 4.0.2, the latest on nuget.org as of
2026-11-03. The generic `[assembly: AssemblyFixture<AsmFixture>]` form
fails identically (from `AssemblyFixtureAttributeOfTGenerator`). No
matching upstream issue existed (xunit/xunit #3563 is a different, closed
AOT assembly-fixture problem), so it was filed upstream as
**[xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664)**
(2026-11-03). A hand-written
`EngineInitializationAttribute` that calls
`RegisterAssemblyFixtureFactory(typeof(AsmFixture), async _ => new AsmFixture())`
compiles, publishes with 0 warnings, and the Compono-composed
assembly-fixture test passes under Native AOT.

**Classification: an xUnit AOT generator defect, unrelated to Compono and
#162.** Compono's own path works once the fixture is registered. The
hand-written workaround depends on xUnit engine-registration surface that
the generator normally owns, so it shouldn't be documented as a
recommendation.

**Addendum (2026-11-04, Bead 1 of PLAN-0070):** upstream **reported** the
fix lands in **xUnit 4.0.3-pre.19**; it is **not yet validated by Compono**,
so the AOT assembly-fixture limitation is expected to be temporary, not
permanent — documentation should say so and point at the preview, not claim
assembly fixtures are permanently unsupported. Compono's
supported stable AOT range (`xunit.v3.extensibility.core.aot`
`[4.0.1, 5.0.0)`) is unchanged; PLAN-0070 Bead 5 adds an isolated
4.0.3-pre.19 compatibility check plus a real AOT assembly-fixture
publish/run if that preview is obtainable from its documented feed (else
record a blocker rather than substituting a version).

## 6. Lifecycle and safety analysis

**Q2. When is `TestContext.Current` usable?** Throughout profile
construction, `Configure`, and factory execution, on both engines, because
Compono defers all of it to delayed enumeration inside test-method
execution. Rows aren't composed during discovery, so no "discovery-time
fixture" hazard exists today. That guarantee depends on
`SupportsDiscoveryEnumeration() => false` /
`disableDiscoveryEnumeration: true`, so any future change to pre-enumerate
Compono rows (for example under #163) would break fixture access. Record
that as a constraint on #163.

**Where to read the fixture: in the registration factory, not in
`Configure` or the profile constructor.** `Configure`-time access works
today, but its semantics differ between engines. Under JIT, `Configure`
runs once per attribute instance and the captured value is reused for
every row that instance composes. Under AOT, it runs per row. Reading
inside the factory keeps behavior identical on both engines and in
multi-row futures (#163). It also keeps the profile usable from
`Composer.Create(...)` outside xUnit until the factory actually runs.

**Q4. Is blocking on the `ValueTask` safe?** In the supported scenario it
never blocks: the value is cached and the `ValueTask` has already
completed (§4.1, §5). Treating `.GetAwaiter().GetResult()` as safe in
general is still wrong. The edge cases in §4.4 would make the call run a
fixture's real `InitializeAsync` synchronously. Under
`MaxConcurrencySyncContext` with a small thread budget, blocking a
scheduler thread while continuations queue back to that context can
deadlock. The supportable rule is **check `IsCompleted` and fail fast if
it isn't**. That removes the sync-over-async hazard entirely instead of
arguing that it's rare.

**Q5. Variations.**

| Variation | Behavior |
|---|---|
| Class fixture | Reachable, per-class instance (verified across 8 parallel classes and 2 derived classes) |
| Collection fixture | Reachable via the parent manager |
| Assembly fixture | Reachable under JIT. Under AOT, blocked by the xUnit generator bug only (§5) |
| Parallel execution | `TestContext.Current` is `AsyncLocal`-based. Isolation verified. Cache reads after eager init are read-only (`Dictionary` concurrent reads are safe) |
| Cancellation | `GetFixture` takes no token and, on the eager path, does no work to cancel. Cancellation before class start means tests (and composition) never run |
| Native AOT | Same results as JIT, 0 trim/AOT warnings. `GetFixture` on the AOT engine resolves through generator-registered `FixtureFactories`, not reflection |
| Missing fixture | `null`, never an exception, so the consumer's helper must turn it into a clear error |
| Wrong/derived/interface type | `null` (exact-type matching), so the helper's message should say "exact type" |
| xUnit 3.2.2 (JIT package floor) | `GetFixture(Type)` and the async generic extension `Xunit.TestContextExtensions.GetFixture<TFixture>()` both exist; documentation uses the non-generic overload so the never-wait completion check works identically across versions |

**Q8. Instance vs. derived value vs. configuration input.** Retrieving the
fixture *instance* and computing a *derived value* (connection string)
are the same operation: a factory reads the instance and returns
something built from it, which is exactly what `Register<T>` is for.
Feeding runtime values into *configuration itself* is different: it means
branching on which registrations exist, or calling `WithSeed` based on a
runtime value. It isn't supported cleanly (JIT caches `Configure` per
attribute instance), and no consumer evidence asks for it. Keep it out of
scope.

**Q9. Framework-neutral?** No evidence supports it. Only xUnit fixtures
appear in #161, and TUnit/NUnit/MSTest each have a different fixture
model. The framework-neutral need ("hand an already-running value to a
profile") is already met by option B (profile instance with constructor
arguments), which works with any framework or none.

**Q10. Diagnostics for a missing fixture.** A compile-time `CMPxxxx` check
isn't feasible: the fixture request lives inside an opaque factory lambda,
and collection fixtures are declared on a separate collection-definition
class. Runtime is the right layer. The message should name the test
class, the exact fixture type, and the declarations that would satisfy it
(`IClassFixture<T>`, `ICollectionFixture<T>` on the collection definition,
or `[assembly: AssemblyFixture]`). Compono's wrapper already adds the
registered type (and, on JIT only, the seed; see §10.4). The documented helper below produces the rest
without any Compono API.

### Recommended documented pattern (consumer-owned, not a Compono API)

```csharp
internal static class XunitFixtures
{
    public static T Require<T>() where T : class
    {
        var context = TestContext.Current;
        var pending = context.GetFixture(typeof(T)); // non-generic: xUnit v3 3.2.2+ and 4.x
        if (!pending.IsCompleted)
            throw new InvalidOperationException(
                $"Fixture '{typeof(T).FullName}' is not initialized yet; it must be a concretely declared fixture.");
        return pending.GetAwaiter().GetResult() as T ?? throw new InvalidOperationException(
            $"Test class '{context.TestClass?.TestClassName}' has no class, collection, or assembly fixture of exact type " +
            $"'{typeof(T).FullName}'. Declare IClassFixture<{typeof(T).Name}>, ICollectionFixture<{typeof(T).Name}> " +
            "on its collection definition, or [assembly: AssemblyFixture(...)].");
    }
}

public sealed class DatabaseProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new DbSettings(XunitFixtures.Require<DbFixture>().ConnectionString));
}
```

`GetAwaiter().GetResult()` runs only after the `IsCompleted` check, so it
never blocks. It just unwraps the result, or rethrows if the `ValueTask`
faulted.

## 7. Approaches compared

| | Approach | Keeps `[Compose<TProfile>]` | Ambient `TestContext` | Sync-over-async | JIT + Native AOT | Verdict |
|---|---|---|---|---|---|---|
| A | Documented `Require<T>()` helper used inside registration factories | Yes | Yes (xUnit's own, supported) | No (`IsCompleted` guard) | Verified both | **Recommend** |
| B | Class-level composer: `Composer.Create(b => b.AddProfile(new P(fixture)))` field, `[Fact]` tests | No (SUT from `_composer.Create<T>()`, one line per class, not per test) | No | No | Verified both | **Document as the framework-neutral alternative** |
| C | `Compono.XunitV3`/`.Aot` helper API wrapping A (typed accessor and message) | Yes | Hidden | No | Would need parity on both packages | Round 1: defer. **Round 2: recommended, see §10–§11** |
| D | Profile constructor injection of fixtures | Yes | No | No | JIT would need runtime fixture-to-ctor mapping (reflection); AOT needs new generator emission | Reject: new public shape, breaks the `new()` contract, little gain over A |
| E | Framework-neutral "runtime value source" in core | Yes | Abstracted | Depends | Large | Reject: single-framework evidence, a generic runtime context abstraction without demonstrated value |
| F | Fixture writes to a static or `AsyncLocal` holder read by the profile | Yes | Custom | No | Yes | Reject: global mutable state, wrong under parallel class fixtures, reinvents `TestContext` |
| G | Compile-time fixture-requirement diagnostic | n/a | n/a | n/a | n/a | Reject for now (§6 Q10) |

**Capability-admission, applied in both directions.** Option C could claim
composition ergonomics (the `Share<T>()`/`Compono.Bogus` precedents): it
keeps consumers inside the composition model and standardizes a message
everyone would otherwise reinvent. In practice, the reinvented part is a
roughly 10-line helper on top of a supported xUnit API. Nothing has to be
expressed at the composition level that a factory can't already express,
and no second consumer exists yet. That's "convenience around an
already-easy call", which Gate A criterion 1 excludes. A wrapper would
also become new public surface on two packages (JIT and AOT parity) that
couples to xUnit fixture semantics Compono doesn't own.

**Re-evaluation trigger for C:** a second independent consumer reinventing
the helper, evidence that consumers get the
`Configure`-versus-factory placement wrong despite the docs, or #163
needing fixture values in multi-row composition in a way the factory
pattern can't serve.

## 8. Compatibility and AOT implications

- Nothing changes in any package. Existing public APIs are untouched, and
  Compono takes on no ownership or disposal of fixtures.
- `Compono.XunitV3` floor (xUnit 3.2.2): the pattern uses
  `GetFixture(Type)`, which exists on 3.2.2 and 4.x.
- `Compono.XunitV3.Aot` (xUnit AOT ≥ 4.0.1): verified under a real Native
  AOT publish with 0 warnings. Class and collection fixtures are fully
  supported. Assembly fixtures need an upstream xUnit fix (§5) and should
  be documented as a known limitation with a pointer to the upstream
  issue — **expected to be temporary**, reported fixed upstream in the
  4.0.3-pre.19 preview line but not yet validated by Compono (see the §5
  addendum).
- Constraint to carry into #163: fixture access depends on Compono rows
  being composed with delayed enumeration. Don't pre-enumerate composed
  rows.

## 9. Round-1 recommendation (superseded)

The first round recommended **documentation only (option A in §7)**, on
the grounds that option C was "convenience around an already-easy call".
The product owner did not accept that recommendation. They asked for a
proportionate comparison of documentation only against a small
Compono-owned accessor (§10), treating explicit product-owner interest
and the #161 consumer friction as admission evidence. §10 supersedes this
section. The evidence in §1–§8 is unchanged.

## 10. Final comparison: documentation-only (A) vs. Compono-owned accessor (B)

No new lifecycle experiments were needed. The xUnit API-surface checks
below were done by decompiling the same packages as §4, plus
`xunit.v3.extensibility.core` 3.2.2. The `Require<T>()` helper's exact
logic was already run under JIT and a Native AOT publish (§5, 0
warnings).

### A. Documentation only, consumer-owned helper

The §6 helper goes into a cookbook recipe and every consumer copies it.

- **Pros:** zero public API, zero maintenance, ships immediately.
- **Cons:** each consumer re-derives the same fiddly correctness details:
  - **Sync-over-async.** Consumers have to know not to write the natural
    `GetFixture<T>().GetAwaiter().GetResult()`. That line is what the
    #161 consumer and the intake spike both wrote first. It's the shape
    `references/coding-standards.md` forbids, and it's a latent deadlock
    under `MaxConcurrencySyncContext` (§6).
  - **Version floor.** The generic lookup is reachable on xUnit 3.2.2 only
    through xUnit's own async `GetFixture<T>()` extension method (it isn't
    an `ITestContext` instance member there), which awaits the `ValueTask`
    and so can't express a never-wait contract. (Corrected 2026-11-04 — see
    the §11 addendum.)
  - **Silent `null`.** `GetFixture` returns `null` for both a missing
    fixture and an exact-type mismatch, so a naive helper fails as a
    `NullReferenceException` or an unhelpful message.
  - **Diagnostics.** A good failure message needs the test class, the
    exact type, and how to declare the fixture.

  The documented path is discoverable only if a consumer finds the
  recipe. The discoverable path (`TestContext.Current.GetFixture<T>()` in
  IntelliSense) is the incorrect one.

### B. Compono-owned accessor in the xUnit integration packages

**Placement and naming.** An extension method on xUnit's own
`ITestContext`, in each package's existing namespace, named per the
repo's `<ExtendedType>Extensions` convention (`CompositionBuilderExtensions`,
`CompositionRowServiceProviderExtensions`, `LoggerTestingExtensions`):

```csharp
namespace Compono.XunitV3;      // and, identically, namespace Compono.XunitV3.Aot;

public static class FixtureContextExtensions
{
    /// Returns the class, collection, or assembly fixture of exactly TFixture visible to the
    /// currently running test, or throws InvalidOperationException with a diagnostic if none is.
    /// Never blocks: a fixture xUnit has not finished initializing is reported, not awaited.
    public static TFixture RequireFixture<TFixture>(this ITestContext context)
        where TFixture : class;
}
```

```csharp
public sealed class DatabaseProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new DbSettings(
            TestContext.Current.RequireFixture<DbFixture>().ConnectionString));
}
```

I chose an extension on `ITestContext` over a static `XunitFixture.Require<T>()`:

- **Native ecosystem fit (Gate A criterion 2).** It sits next to xUnit's
  own `GetFixture` and reads as xUnit API.
- **Explicit dependency.** The ambient dependency stays visible at the
  call site (`TestContext.Current`) instead of hiding inside a Compono
  static.
- **Testable.** Every failure branch, including incomplete and faulted
  `ValueTask`s, can be unit-tested against a fake `ITestContext` with no
  runner. A static over `TestContext.Current` can't be.

Rejected shapes:

- **A `CompositionBuilder` extension** (for example
  `RegisterFromFixture<TFixture, T>(…)`). It puts xUnit coupling on the
  composition API and covers only `Register`, while the accessor works
  inside any factory.
- **A `TryGetFixture`/nullable variant.** xUnit's own `GetFixture`
  already is the optional form.
- **A profile-label parameter.** Compono's `CompositionException` wrapper
  already names the registered type.

**Behavior contract (all failures are `InvalidOperationException`).**
Compono's factory wrapper turns them into the usual
`CompositionException … factory for 'X' threw: …`.

| Condition | Behavior |
|---|---|
| `context` null | `ArgumentNullException.ThrowIfNull` |
| No running test class (`context.TestClass` is null, e.g. called outside a test, or during discovery) | Throws, naming `PipelineStage` and stating fixtures are only available while a test class runs |
| Fixture not declared at class, collection, or assembly level (`GetFixture` → `null`) | Throws, naming the test class, `typeof(TFixture)`, the fact that matching is exact-type, and the three declaration forms |
| `ValueTask` not completed (declared but not yet created: open-generic or skipped-level edge cases, §4.4) | Throws immediately and **never blocks**, naming the fixture type |
| `ValueTask` completed but faulted (fixture constructor or `InitializeAsync` threw on lazy creation) | Rethrows xUnit's own exception unchanged (`GetAwaiter().GetResult()` on an already-completed `ValueTask`, so no wait) |
| Result not assignable to `TFixture` (only possible with a non-xUnit `ITestContext` implementation) | Throws, naming expected and actual types |

**No duplicated implementation.** A shared compiled assembly isn't
possible:

- `Compono.XunitV3` compiles against `xunit.v3.core.dll` and
  `Compono.XunitV3.Aot` against `xunit.v3.core.aot.dll`. They define
  `Xunit.ITestContext` as different physical types (RESEARCH-0032 §1–§2),
  so neither assembly can reference both.
- Core `Compono` must not reference xUnit.

The smallest option is **one source file compiled into both packages**
(an MSBuild `<Compile Include="…" Link="…" />` from a shared location),
with the namespace selected per package. The only per-package
difference is the namespace. The members either package needs
(`GetFixture(Type)`, `TestClass`, `PipelineStage`) are identical
signatures on xUnit 3.2.2, 4.x JIT, and 4.x AOT.

No linked shared source exists in the repo today (`src/*/*.csproj` has no
`Compile Include`/`Link`). Per `AGENTS.md` ("a new pattern is a design
question"), that choice belongs in the ADR. The alternative is two ~40-line
copies kept in sync by mirrored tests in each package's test project.

**Dependencies.** No change. `Compono.XunitV3` already references
`xunit.v3.extensibility.core` `[3.2.2, 5.0.0)` and `Compono.XunitV3.Aot`
already references `xunit.v3.extensibility.core.aot` `[4.0.1, 5.0.0)`.
Both contain `ITestContext`.

**xUnit version compatibility.** Use the non-generic `GetFixture(Type)`
overload: it's an `ITestContext` instance method identically present in
3.2.2 and 4.x (JIT and AOT), and it returns the `ValueTask` directly so the
never-wait completion check is possible. (A generic `GetFixture<T>()` is
reachable everywhere — as xUnit's async extension on 3.2.2, and as an
`ITestContext` instance member on 4.x — but it awaits internally and so
can't express never-wait. Corrected 2026-11-04 — see the §11 addendum.)
Verified present in `ITestContext` for 3.2.2 (JIT) and 4.0.1 (JIT and AOT).
The package TFMs (`net8.0`–`net11.0` JIT, `net9.0`–`net11.0` AOT) all have
`ValueTask.IsCompleted`.

**Native AOT and trimming.** No reflection, no dynamic code, and no
generic instantiation over value types. The method uses `typeof(TFixture)`
plus a type test, and `GetFixture(Type)` carries no
`DynamicallyAccessedMembers` requirement. On the AOT engine, lookup
resolves through xUnit's generator-registered `FixtureFactories`. Both
packages are already `IsAotCompatible`, so the analyzers would flag any
regression. The identical logic published with 0 warnings in §5.

### Capability admission

| Gate | A | B |
|---|---|---|
| Step 2 "already solved?" | Yes, functionally | Functionally yes. But the discoverable path is the incorrect one (`.GetAwaiter().GetResult()`, silent `null`, a generic *instance* overload absent on the 3.2.2 floor — only xUnit's async extension is available there) |
| Gate A.1 Compono-specific value | n/a | **Clears, narrowly**, on "making the obvious, discoverable Compono path also the correct one" and "consistent semantics consumers would otherwise reinvent, each slightly differently". This is a correctness and diagnostics win, not just shorter syntax |
| Gate A.2 Native ecosystem fit | n/a | Clears: an extension on xUnit's own `ITestContext` |
| Gate A.3 Meaningful abstraction | n/a | **The weakest criterion, stated plainly.** B is literally a short extension method, which is the shape A.3 is wary of. It clears only on the "leaves the consumer more correct" clause: never-blocking retrieval, fail-fast, an exact-type diagnostic, and floor-safe API choice. It doesn't create a new composition concept |
| Gate A.4 Architectural fit | n/a | Clears: public xUnit API only, no core change, no reflection, no hidden state |
| Gate A.5 Package boundary | n/a | n/a (no new package) |
| Gate B evidence | — | #161 consumer friction (helper and sync-over-async written in practice) plus explicit product-owner interest |
| Maintenance | None | Small: one source file, one test set (or two), and docs for two packages |

## 11. Recommendation

**Option B**, as specified in §10: `FixtureContextExtensions.RequireFixture<TFixture>(this ITestContext)`
in both `Compono.XunitV3` and `Compono.XunitV3.Aot`. Documentation still
ships with it: a cookbook recipe using `RequireFixture`, including the
factory-not-`Configure` rule and the explicit-profile-instance
alternative (§7 option B) for framework-neutral profiles, plus
package-page sections and the `compono` skill.

Rationale: the measurable difference between A and B isn't line count,
it's correctness. The path consumers actually write first is the
blocking, `null`-unsafe one. B makes the discoverable path the correct
one and makes the failure diagnostic uniform, with no new dependency, no
reflection, and AOT parity. A.3 is cleared narrowly, and that's recorded
honestly above.

**Risks and tradeoffs**

- **Permanent public API on two packages** for a small helper, coupled to
  xUnit's exact-type fixture semantics, which Compono doesn't own.
- **Name shadowing.** If a future xUnit adds an instance
  `ITestContext.RequireFixture`, it silently wins over the extension.
  This is low likelihood with similar semantics. Pin the behavior with
  tests.
- **Profiles bound to xUnit.** Blessing ambient `TestContext` access in
  profiles nudges profiles toward xUnit coupling. The docs must present
  explicit profile instances (§7 option B) as the framework-neutral
  choice.
- **Expectation of parity.** TUnit, NUnit, and MSTest consumers may
  expect equivalents. Each has a different fixture model, and no
  evidence exists, so the ADR should state "xUnit only, no parity
  commitment".
- **New repo pattern.** Linked shared source (or deliberate duplication)
  needs an ADR decision.
- **AOT assembly fixtures stay broken upstream** (§5, Appendix B). The
  accessor can't fix that, and the AOT docs must say so.

**ADR warranted?** Yes, a light-dive ADR. It adds new public API surface
(which `AGENTS.md` requires ADR backing for) and it settles the
shared-source question. Then a short plan and beads, per the agreed
workflow. The dogfood gate applies at implementation, because it's a
package behavior change.

**Correction addendum (2026-11-04, independent review of PLAN-0070).**
Two corrections to this research, recorded rather than silently rewritten:

1. **The generic `GetFixture<T>()` "version floor" claim above was
   wrong.** xUnit 3.2.2 does ship a public generic lookup —
   `Xunit.TestContextExtensions.GetFixture<TFixture>(this ITestContext)`,
   verified by decompiling `xunit.v3.extensibility.core` 3.2.2. It's an
   `async` extension that awaits the non-generic `ValueTask` internally, so
   it can't express the never-wait contract, but it exists. xUnit 4.x
   additionally promotes a generic *instance* member
   `ITestContext.GetFixture<TFixture>()` onto the interface. The decision
   to use `GetFixture(Type)` stands, but on semantics (never-wait, explicit
   `IsCompleted` check, identical behavior across the supported range), not
   on the generic being unavailable. §10 and the §5/§11 text were corrected
   inline; see ADR-0069's Amendment 2.
2. **The class name.** The proposal named the type `TestContextExtensions`,
   which collides by name with xUnit's own `Xunit.TestContextExtensions`.
   The shipped type is `FixtureContextExtensions` (see ADR-0069's
   Amendment). The §10/§11 code above reflects the shipped name.

## 12. Constraint recorded for #163

**Composed theory rows must keep execution-time (delayed) enumeration.**
Fixture access, whether through `GetFixture` directly or through the §10
accessor, works only because Compono composes rows during test-method
execution: `Compono.XunitV3` uses `SupportsDiscoveryEnumeration() => false`,
and `Compono.XunitV3.Aot` uses `disableDiscoveryEnumeration: true` (§3,
§6 Q2). Any #163 design that pre-enumerates or caches composed rows at
discovery time would run profile factories with no test class and no
fixtures, and break #162's supported path. #163's ADR must either keep
delayed enumeration or explicitly design and document the fixture-access
consequence. It also must not rely on JIT's once-per-attribute
`Configure` caching for per-row values (§6). This is also noted in
RESEARCH-0036 §3.

## 13. Product-owner questions (all resolved 2026-11-03)

1. ~~Approve option B, its shape, and its failure contract~~: approved,
   see [ADR-0069](../adr/0069-xunitv3-require-fixture-accessor.md).
2. ~~Linked shared source or mirrored copies~~: linked shared source,
   per ADR-0069.
3. ~~Submit the upstream xUnit report~~: submitted as
   [xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664) on
   2026-11-03, after a final duplicate check.
4. ~~AOT seed reporting~~: filed separately as
   [#166](https://github.com/LayeredCraft/compono/issues/166). It's not
   part of #162.

## 14. Proposed next step

If B is approved: a light-dive ADR (public API shape, failure contract,
shared-source decision, xUnit-only scope), then product-owner approval,
then a plan, then beads, then implementation (OpenCode), then
independent review. If A is chosen instead: a single docs-only task (cookbook recipe with the §6
helper, both package pages, the `compono` skill), with no ADR.

## Appendix A: experiment artifacts

Scratchpad only (ephemeral, not in the repo):
`exp/shared/Tests.cs`, `exp/shared/AsmManual.cs`, `exp/jit/Jit.csproj`,
`exp/aot/Aot.csproj`, and `exp/asmrepro/` in the session scratchpad
directory, plus decompiled xUnit sources under `decomp/`.

## Appendix B: upstream xUnit bug report (submitted as [xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664))

Submitted 2026-11-03 as
[xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664), after a
final duplicate search of `xunit/xunit` (only #3563, a different, closed
runtime-resolution issue, matched). The submitted text also notes that the
generic `AssemblyFixture<T>` form fails the same way. The draft below is
kept as written for reference.

> **Title:** [AOT] `[assembly: AssemblyFixture]` generates code that doesn't compile (CS1593) — 4.0.1 / 4.0.2
>
> **Repro** (`xunit.v3.aot.mtp-v2` only, no other packages):
>
> ```xml
> <Project Sdk="Microsoft.NET.Sdk">
>   <PropertyGroup>
>     <TargetFramework>net10.0</TargetFramework>
>     <OutputType>Exe</OutputType>
>     <IsTestProject>true</IsTestProject>
>     <ImplicitUsings>enable</ImplicitUsings>
>     <Nullable>enable</Nullable>
>   </PropertyGroup>
>   <ItemGroup>
>     <PackageReference Include="xunit.v3.aot.mtp-v2" Version="4.0.2" />
>   </ItemGroup>
> </Project>
> ```
>
> ```csharp
> using Xunit;
> [assembly: AssemblyFixture(typeof(AsmFixture))]
> public sealed class AsmFixture { public string Value => "asm"; }
> public sealed class Tests(AsmFixture f) { [Fact] public void Injected() => Assert.Equal("asm", f.Value); }
> ```
>
> `dotnet build` fails:
>
> ```text
> AssemblyFixtureAttribute٠<hash>.g.cs(15,112): error CS1593: Delegate 'Func<bool, ValueTask<object?>>' does not take 0 arguments
> ```
>
> **Generated code** (`Xunit.Generators.AssemblyFixtureAttributeGenerator`):
>
> ```csharp
> global::Xunit.v3.RegisteredEngineConfig.RegisterAssemblyFixtureFactory(typeof(global::AsmFixture), async () => new global::AsmFixture());
> ```
>
> **Expected:** the emitted lambda matches
> `RegisteredEngineConfig.RegisterAssemblyFixtureFactory(Type, Func<bool, ValueTask<object?>>)`,
> e.g. `async _ => new global::AsmFixture()` (presumably honoring the
> `forceCreation` flag the way class/collection fixture factories do).
> Hand-registering exactly that lambda from an `EngineInitializationAttribute`
> compiles, publishes with `PublishAot=true` with 0 warnings, and the
> fixture is injected and returned by `TestContext.Current.GetFixture(...)`
> correctly at runtime.
>
> **Versions:** `xunit.v3.aot.mtp-v2` 4.0.1 and 4.0.2 (both reproduce);
> .NET SDK 11.0.100-rc.3.26522.148, `net10.0`, macOS arm64.
