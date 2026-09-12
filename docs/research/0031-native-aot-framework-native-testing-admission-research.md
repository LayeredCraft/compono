# [RESEARCH-0031] Native AOT Framework-Native Testing: Capability & Package Admission Research

**Status:** Research complete for MSTest and TUnit — no ADR needed there;
the capability does not clear Gate A/B as a package or public-API addition
for those two frameworks, a narrow, real bug was found in `Compono.TUnit`
and is recorded as a required follow-up (see §6/§18). **xUnit v3 is a
separate, unresolved, more significant open question** (see §4/§5/§12) —
not blocked by an upstream issue as originally reported (that
characterization was based on testing the wrong, reflection-mode xUnit
package and has been retracted, see the correction note below), but by a
real gap in `Compono.XunitV3` itself that would need its own Gate A/B pass
before any admission verdict can be given. This document flags that
question for the product owner rather than resolving it.

**Correction (this revision):** the original version of this document
concluded xUnit v3 was "AOT-compatible as-is, blocked only by an upstream
bug (xunit/xunit#3335)." That conclusion was based on a spike built against
`xunit.v3.mtp-v2` — xUnit v3's **ordinary reflection-mode** package — with
`PublishAot=true` forced onto it, not xUnit's actual documented Native AOT
package set (`xunit.v3.aot.mtp-v2` + AOT-specific extension packages). A
corrected spike, built from xUnit's own official Native AOT documentation
and sample projects, found a different and more significant problem: see
§4/§5 below. The upstream issue #3335 does **not** reproduce under the
correctly-configured official AOT path — the plain-xUnit AOT baseline
(no Compono) runs cleanly. That original framing is retracted throughout
this document; every section below reflects the corrected finding.

**Governing process:** [`docs/architecture/capability-admission.md`](../architecture/capability-admission.md),
applied end to end (Gate A, then Gate B).

**Trigger:** an explicit product-owner request to investigate whether
Compono could provide first-class Native AOT test composition/execution —
real framework-native tests (`[Fact]`/`[Test]`/`[TestMethod]` +
`[Compose]`), published as Native AOT, replacing the bespoke
console-validator pattern used in several LayeredCraft library repos
(`decoweaver`, `alexa-vox-craft`, `dynamodb-efcore-provider`,
`minimal-lambda`).

**Candidate framing, as instructed:** not "should we build
`Compono.Aot`" — that's explicitly rejected as a premise per the trigger's
own instructions. The actual question: *can a consumer publish a real,
Compono-composed xUnit v3/TUnit/MSTest test project as Native AOT and run
the resulting native binary as a permanent validation gate, and if so,
does Compono need to do anything new to make that work?*

**Scope note (user decision, this session):** NUnit is excluded from this
investigation. Neither this session's fresh research nor Compono's own
prior [RESEARCH-0018](0018-nunit-integration-viability-research.md) §10
found any evidence that NUnit's own runner/adapter chain can execute a
published Native AOT test assembly at all — there is nothing for Compono
to plug into yet. Recorded as blocked on the framework (§14), not
researched further here.

---

## 1. What problem are we actually solving?

At least four sibling LayeredCraft repos hand-roll a Native AOT validation
console application to get *runtime* proof that a library works under
`PublishAot=true` (a clean `dotnet publish` alone is explicitly treated as
insufficient by this pattern's own authors — see `decoweaver`'s
`research/2026-09-11-native-aot-compatibility.md` §7). The shape, confirmed
by direct inspection this session:

- **`decoweaver`** (`test/LayeredCraft.DecoWeaver.AotValidation`) — a
  top-level-statements `Program.cs` with a hand-rolled
  `Check(scenario, actual, expected)` helper accumulating a `List<string>`
  of failures, printing `[PASS]`/`[FAIL]`, returning `1` on any failure.
  Seven DI-decoration scenarios exercised. CI greps the publish log for
  `IL[23][0-9]{3}` and fails on any hit (zero-tolerance), then runs the
  native binary and trusts its exit code.
- **`alexa-vox-craft`** (`test/AlexaVoxCraft.NativeAot.ValidationApp`) —
  the same `Check(name, bool)` pattern, one iteration earlier
  (bool-based, not actual/expected), deliberately stripped of any test
  framework or test-double reference "so this binary's failures must be
  attributable unambiguously to AlexaVoxCraft's own code."
- **`dynamodb-efcore-provider`** (`testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke`) —
  raw `if (...) throw` instead of a `Check()` helper, run against a real
  `dynamodb-local` Docker container, with a maintained IL-warning
  *baseline* diff (not zero-tolerance). Notably, this repo **also** has a
  real xUnit v3 test project (`tests/EntityFrameworkCore.DynamoDb.AotTests`)
  — but it's a plain JIT project, never AOT-published. The actual Native
  AOT gate is still the bespoke console app.
- **`minimal-lambda`** — a thinner, earlier-stage variant: one project
  (`AotCompatibility.TestApp`) whose `Program.cs` is literally `return;`,
  existing only to root assemblies for trim analysis; no runtime
  assertions at all.

**What this pattern gives library authors that plain `dotnet test` doesn't
today:** a binary that is genuinely AOT-published *and executed* (proving
the real native-compiled path, not just a clean analyzer pass), tight
control over exactly what's in the compiled graph (so a failure is
attributable to the library, not test-framework/test-double reflection
noise), and IL2xxx/IL3xxx warning-gating built into the publish step.

**What it costs them, every one of these repos, independently:** no named
or isolated tests (one failure just appends to a shared list and the whole
run still reports pass/fail as a unit), no lifecycle, no filtering, no IDE
test-explorer integration, no parallelization, and hand-rolled
assertion/reporting code reinvented per repo with zero consistency between
them (`Check(string, bool)` vs. `Check(string, string, string)` vs. raw
`if/throw` — three different shapes for the same underlying need, found in
three different repos by the same author). This is a real, concrete,
repeated problem — Step 1 of the admission process is satisfied.

## 2. Is this already solved?

No. Confirmed directly (this session, sibling-repo survey): none of the
four repos surveyed has ever used a real framework-native test project,
AOT-published, as its Native AOT gate. `dynamodb-efcore-provider`'s split
(real xUnit v3 project exists, but only runs JIT) shows this was never
even attempted, not that it was tried and rejected. `decoweaver`'s own
research doc, which explicitly designed the console-app approach, contains
no discussion of a test-framework alternative at all.

## 3. What do xUnit v3, TUnit, and MSTest's current Native AOT capabilities provide?

All three (current, authoritative sources, cited):

| | xUnit v3 | TUnit | MSTest |
|---|---|---|---|
| Native AOT officially supported | Yes, since **4.0.0** (shipped 2026-08-14) — [xunit.net/docs/getting-started/v3/native-aot](https://xunit.net/docs/getting-started/v3/native-aot) | Yes, by design since early releases — source-generated discovery/execution always, no reflection mode to opt out of — [tunit.dev/docs/execution/engine-modes](https://tunit.dev/docs/execution/engine-modes/) | Yes, since **MSTest 4.4** (`MSTestSourceGenMode=ReflectionFree`, default since 4.3.2) — [devblogs.microsoft.com/dotnet/mstest-source-generation](https://devblogs.microsoft.com/dotnet/mstest-source-generation/) |
| Minimum .NET | **.NET 9** (explicit in xUnit's own doc — extensive `[OverloadResolutionPriority]` use) | .NET 8+ (TUnit.Core also targets netstandard2.0) | net10.0 shown in Microsoft's own AOT example; MSTest.TestFramework itself has no AOT-specific floor beyond the source-gen version |
| Discovery/execution | Source-generated in AOT mode; reflection-mode still exists for JIT | Always source-generated, no runtime reflection for discovery/execution in any mode | Source-generated (`MSTestSourceGenMode=ReflectionFree`, default), with a documented reflective fallback for shapes the generator can't materialize statically |
| How AOT binary is produced | `dotnet publish -p:PublishAot=true` on the test project | Same | Same, via `MSTest.Sdk` |
| Run mechanism | Native binary run directly (self-hosting, via xUnit's own in-process AOT runner — **verified working**, see §4); xUnit's first-party runners (`xunit.v3.runner.console`/`.msbuild`) can also point at it — **`dotnet test` not documented as supported for a published AOT binary** | Native `.exe`/binary invoked directly, any flags passed straight through | Native binary run directly, **not** via `dotnet test` (Microsoft's own doc: "The native binary runs directly—not through `dotnet test`") |
| Required AOT package set | **Not** the ordinary `xunit.v3`/`xunit.v3.mtp-v2`/`xunit.v3.extensibility.core` packages — a **separate, parallel AOT-specific package family**: `xunit.v3.aot.mtp-v2` (meta-package), `xunit.v3.core.aot`, `xunit.v3.extensibility.core.aot`, `xunit.v3.assert.aot`, etc. Confirmed by building from xUnit's own official sample projects (§4) — verified directly, not assumed from prose | N/A — no parallel package family; one package set for both modes | N/A — one package set (`MSTest.Sdk`), mode selected via `MSTestSourceGenMode` |
| Custom extensibility under AOT | **Requires a companion Roslyn source generator project**, referenced as an `Analyzer`-only `ProjectReference`, that calls `RegisteredEngineConfig.RegisterTheoryDataRowFactory` (or the discovery/trait equivalent) at build time for every attributed method — confirmed by reading xUnit's own official `AotCsvDataSource`/`AotRetryFact`/`AotTraitExtensibility` samples (all three, independently, use this exact three-project Extension/Generator/Sample shape; no exceptions found). A plain hand-written `DataAttribute` subclass with runtime logic (`Compono.XunitV3.ComposeAttribute`'s actual shape) is **not** xUnit's supported AOT extensibility pattern — see §4/§5 | N/A — TUnit's own extension model is source-gen-based throughout | Not separately documented; MSTest's generator targets its own `[TestMethod]`/`[TestClass]` discovery, orthogonal to a data-attribute's own internals |
| Known AOT limitations | No generic test methods, no interface-based attributes (`IFactAttribute`), no `EventSource` reporting, no serialization, reduced stack traces, argument-formatting differences | Not separately catalogued in what was found; framework is AOT-first, so no reflection-mode/AOT-mode split to reconcile | No generic test methods, no inheritance-only `[TestClass]` (must declare directly), no open-generic/inaccessible/`static`/file-local test classes, no `ref`/`out`/`in` test-method parameters |
| Fixtures/lifecycle under AOT | Supported via source-generated `FixtureMappingManager` registration | Supported (source-generated throughout) | Supported (ordinary `[TestClass]`/`[TestMethod]` programming model unchanged per Microsoft's own framing: "the generator changes the build and execution path, not the programming model") |
| Theory/data-driven under AOT | Supported; only *dynamic operator-conversion* theory-data conversions are unsupported | Supported (TUnit's `DataSourceGeneratorAttribute` family is itself source-gen-based) | Supported via `ITestDataSource`, unchanged from JIT |

## 4. How does each framework's AOT execution model actually work — verified directly, not just from docs

Documentation claims were verified against real, throwaway `[Theory]`/`[Test]`/`[TestMethod]` + `[Compose]` projects, packed against locally-built `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest` 1.0.0 and each framework's current AOT-capable version, published `-p:PublishAot=true -r osx-arm64 --self-contained true`, and **executed as native binaries** (all spikes discarded after use, not committed):

- **MSTest 4.4 (`MSTest.Sdk/4.4.0`) + `Compono.MSTest`: works cleanly.** JIT run passed; `dotnet publish -p:PublishAot=true` produced **zero warnings attributable to Compono.MSTest or MSTest itself**; the native binary, run directly (no `dotnet test`), executed the real `[TestMethod]`+`[Compose]` test and reported `Test run summary: Passed! ... total: 1, succeeded: 1`. This is the cleanest result of the three.
- **TUnit 1.65.68 + `Compono.TUnit`: works, but with one real, new trim gap.** JIT run passed; `dotnet publish -p:PublishAot=true` succeeded and the native binary ran the real `[Test]`+`[Compose]` test and passed (`Test run summary: Passed!`, HTML report artifact produced) — **but the publish emitted a real IL2075 trim-analysis warning**, not previously known:
  ```
  Compono.TUnit.Binding.BindingPlan.ResolveMethodInfo(MethodMetadata,ParameterMetadata[]):
  'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.NonPublicMethods' in call to
  'System.Type.GetMethods(BindingFlags)'. The return value of method 'TUnit.Core.ClassMetadata.Type.get'
  does not have matching annotations.
  ```
  `src/Compono.TUnit/Binding/BindingPlan.cs:159` calls `Type.GetMethods(BindingFlags)` on a `Type`
  obtained from TUnit's own `ClassMetadata.Type`, without a matching
  `[DynamicallyAccessedMembers]` annotation on the receiving parameter. This did **not** fail at
  runtime for this trivial case, but it is a real correctness risk under more aggressive trimming —
  see §7/§18 for why none of the eight existing `*.AotSmokeTest` projects ever caught it.
- **xUnit v3 4.0.0, corrected spike (superseding the original finding below the line).** Built from
  scratch from xUnit's own official Native AOT documentation and, critically, its official
  `v3/AotCsvDataSource` sample project (the real, working reference implementation for a custom
  `DataAttribute`-style theory data source under AOT) — not adapted from the original,
  wrong-package spike.

  **Step 1 — plain xUnit AOT baseline, no Compono.** A trivial `[Fact]`/`[Theory]` project
  referencing only `xunit.v3.aot.mtp-v2` 4.0.0, `PublishAot=true`, net10.0. `dotnet publish` produced
  **zero warnings**. The published native binary, run directly with no arguments (no `dotnet test`,
  no separate runner process), **self-hosted, discovered, and executed all 3 tests successfully**:
  ```
  xUnit.net v3 In-Process Runner v4.0.0+8bf043c053 [native/osx-arm64] (.NET 10.0.12)
    Discovering: BaselineAot
    Discovered:  BaselineAot
    Starting:    BaselineAot
    Finished:    BaselineAot (ID = '297ae526...')
  === TEST EXECUTION SUMMARY ===
     BaselineAot  Total: 3, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.001s
  ```
  Exit code 0. **The framework baseline is fully proven, independent of Compono** — xUnit v3's own
  documented Native AOT path works cleanly when configured as xUnit actually documents it. This also
  directly disproves the original document's central claim: xunit/xunit#3335 does **not** reproduce
  under the correctly-configured official AOT path. That issue was an artifact of forcing
  `PublishAot=true` onto the wrong (reflection-mode) package in the original spike, not a real
  blocker on xUnit v3's supported AOT path.

  **Step 2 — add `Compono.XunitV3` + a real `[Theory]` + `[Compose]` test to the same project.**
  Adding `PackageReference Include="Compono.XunitV3" Version="1.0.0"` (the locally packed build)
  alongside `xunit.v3.aot.mtp-v2` **fails at compile time**, before publish, before run, before any
  `[Compose]` dispatch code executes:
  ```
  error CS0433: The type 'TheoryAttribute' exists in both 'xunit.v3.core.aot, Version=4.0.0.0, ...'
  and 'xunit.v3.core, Version=3.2.2.0, ...'
  ```
  **Root cause, confirmed directly, not inferred:** the packaged `Compono.XunitV3` nupkg's `.nuspec`
  declares a hard NuGet dependency on `xunit.v3.extensibility.core` (the **reflection-mode** package,
  version range `[3.2.2, 5.0.0)`, matching `Directory.Packages.props`'s own current pin) for every
  target framework. Referencing `Compono.XunitV3` in *any* project therefore transitively pulls in
  `xunit.v3.extensibility.core` regardless of what other xUnit package the consumer references.
  `xunit.v3.aot.mtp-v2` transitively pulls in the **separate, parallel** `xunit.v3.extensibility.core.aot`
  assembly. Both assemblies define their own `TheoryAttribute`/`DataAttribute` types — genuinely
  different physical types, not just different versions of the same one — so any project referencing
  both `Compono.XunitV3` and xUnit's real AOT package set gets an unresolvable ambiguous-reference
  error. Confirmed by inspecting the actual restored package set
  (`xunit.v3.extensibility.core` **and** `xunit.v3.extensibility.core.aot` both present side by side
  in `obj/.nuget-packages`) and the packed `Compono.XunitV3.nuspec`'s dependency group directly —
  this is not a local-feed artifact.

  **This is a materially different and more significant finding than "blocked by an upstream bug."**
  `Compono.XunitV3`, as currently built and shipped, **cannot even be referenced** alongside xUnit
  v3's real, documented Native AOT package set — a compile-time break for every consumer, not a
  runtime discovery nuance. The run/execute steps of this investigation's own procedure (steps 5-8)
  could not proceed past this point, because there is no buildable project to publish. Whether
  `[Compose]`/`ComposeAttribute` would even be *discovered* by xUnit's AOT source-generated pipeline
  if the compile-time conflict were somehow resolved is a second, independent open question — xUnit's
  own `AotCsvDataSource`/`AotRetryFact`/`AotTraitExtensibility` samples all show that a **custom
  data-attribute source generator is xUnit's actual supported extensibility mechanism** under AOT
  (a companion Roslyn generator project, referenced as `OutputItemType="Analyzer"
  ReferenceOutputAssembly="false"`, emitting a `RegisteredEngineConfig.RegisterTheoryDataRowFactory`
  call per attributed test method) — and `Compono.Generators` has **no emission of any kind matching
  that API** (confirmed: no `RegisteredEngineConfig`/`RegisterTheoryDataRowFactory`/`.aot` references
  anywhere in `src/Compono.Generators`). `ComposeAttribute`'s current shape (a hand-written
  `DataAttribute` subclass whose `GetData` runs ordinary managed code, not source-generated
  registration) does not follow xUnit's documented AOT extensibility pattern at all. **Both
  problems would need to be solved — the package/assembly-identity conflict and the missing
  generator-side AOT registration — before a real `Compono.XunitV3` AOT test could even be attempted**,
  and neither was solvable within this research session's scope (see §5/§12 for why this is now an
  open question for the product owner rather than a resolved finding).

  <details><summary>Original (retracted) finding, kept for record only — do not cite</summary>

  The original spike used `xunit.v3.mtp-v2` (reflection-mode) with `PublishAot=true` forced onto it
  and hit `xunit/xunit#3335` ("xUnit v3 in a single file executable," an `Assembly.Location`-related
  crash) in both runner modes tried. That spike was built against the wrong package family — see the
  correction note at the top of this document. The corrected baseline (above) proves #3335 does not
  reproduce under xUnit's actual documented AOT configuration.

  </details>

## 5. Is `Compono.XunitV3` AOT-compatible as-is?

**No — not for xUnit's real, documented Native AOT path, and this is now an open question, not a
closed one.** Two distinct problems, both confirmed directly in §4:

1. **Compile-time package/assembly-identity conflict.** `Compono.XunitV3`'s shipped dependency on the
   reflection-mode `xunit.v3.extensibility.core` package makes it impossible to reference alongside
   xUnit's actual AOT package set (`xunit.v3.aot.mtp-v2` and its `.aot`-suffixed dependencies) at all
   — `CS0433` on `TheoryAttribute` before any other code runs.
2. **No generator-side AOT registration.** Even if problem 1 were solved (e.g. a parallel
   AOT-targeted build of `Compono.XunitV3` referencing the `.aot` package family), xUnit's own sample
   projects show custom data-attribute extensibility requires a companion source generator emitting
   `RegisteredEngineConfig.RegisterTheoryDataRowFactory`-style registration at build time.
   `Compono.Generators` emits nothing of this kind today, and whether `[Compose]`'s existing
   generator-emitted `PlanCache<T>`/`RowInvokerRegistry` machinery ([ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md))
   could be extended to also emit this xUnit-AOT-specific registration is a genuine, undesigned
   architecture question, not something this research session resolved or should resolve.

This reverses the original version of this document, which claimed `Compono.XunitV3` was
"AOT-compatible as-is" based on source-level reflection auditing alone (no live
`Activator.CreateInstance`/`MakeGenericMethod`/etc., `ConfigProfileBinder`'s `ConstructorInfo.Invoke`
already annotated per ADR-0041 Amendments 1/4/5 — all still true and still relevant to *JIT* and to
the existing eight `*.AotSmokeTest` throwaway-console-app legs) **without ever attempting to combine
`Compono.XunitV3` with xUnit's real AOT package set**. The reflection audit and the eight existing
smoke tests remain accurate for what they actually test (Compono's own dispatch mechanism, driven
directly); they say nothing about whether xUnit v3's own AOT pipeline will discover and invoke
`Compono.XunitV3`'s attribute at all, because none of them exercise that pipeline. This is exactly
the class of gap ADR-0041 Amendment 7 already flagged as out of scope for the existing smoke-test
design (§4/§6 of this document).

## 6. Is `Compono.TUnit` AOT-compatible as-is?

**No — one real, previously-undiscovered trim gap exists**, found only by driving TUnit's actual
test-discovery engine (§4). `BindingPlan.ResolveMethodInfo` (`src/Compono.TUnit/Binding/BindingPlan.cs:159`)
calls `Type.GetMethods(BindingFlags)` on a `Type` sourced from TUnit's own `ClassMetadata.Type`, with
no `[DynamicallyAccessedMembers]` annotation carried through. `Compono.TUnit.AotSmokeTest` never
caught this because — like all eight existing `*.AotSmokeTest` projects, by ADR-0041 Amendment 7's own
explicit design — it drives Compono's dispatch mechanism directly with a hand-built
`DataGeneratorMetadata`/stand-in, never TUnit's real `ClassMetadata`-based discovery. This is a real
gap against ADR-0041's own AOT-safety claim for `Compono.TUnit`, not a new capability question — see
§18 for the recommended fix.

## 7. What is the state of MSTest support (excluding NUnit per scope)?

Clean. `Compono.MSTest` produced zero AOT/trim warnings in this session's spike and its real
`[TestMethod]`+`[Compose]` test executed correctly as a native binary. Consistent with
[RESEARCH-0018](0018-nunit-integration-viability-research.md) §7's prior finding that MSTest's own
source generator and `Compono.MSTest`'s dispatch are orthogonal, non-conflicting concerns.

## 8. Which Compono features, if any, conflict with Native AOT?

None found that are Native-AOT-inherent. The one real gap found (§6) is a missed annotation, fixable
the same way ADR-0041 Amendments 1/4/5 already fixed the identical class of gap in
`ConfigProfileBinder` — not a fundamental architectural conflict. No existing, documented Compono
capability needs to be dropped or scoped away for Native AOT.

## 9. Can ordinary Compono tests serve as both JIT tests and Native AOT runtime validation?

**Yes, for MSTest and TUnit, demonstrated directly this session** — the exact same `[TestMethod]`/`[Test]`
+ `[Compose]` source ran unmodified under JIT and, after `dotnet publish -p:PublishAot=true`, under
Native AOT. **Unresolved for xUnit v3** — not because of a framework-side blocker (the plain-xUnit AOT
baseline works cleanly, §4), but because `Compono.XunitV3` itself cannot currently be combined with
xUnit's real AOT package set at all (§5). No `[Compose]`-attributed xUnit v3 test has been shown to
run under Native AOT, successfully or otherwise — the question is genuinely open, not "not yet
demonstrated but expected to work."

## 10. Can this replace bespoke AOT console validator apps without reducing confidence?

**Conditionally yes, for MSTest and TUnit, with one caveat.** A real test project (MSTest or TUnit,
using Compono composition), published `-p:PublishAot=true` and executed natively, proves the same
thing the console-app pattern proves (real native compilation, real production-shaped scenarios,
actual execution not just a clean publish) while adding named/isolated tests, real assertions,
filtering, and IDE-integratable reporting the console-app pattern doesn't have. **The caveat:** none
of the sibling repos' current console validators do IL2xxx/IL3xxx zero-tolerance/baseline gating
*through* a real test framework's own publish step yet — `decoweaver`'s zero-tolerance grep and
`dynamodb-efcore-provider`'s baseline diff both operate on the console app's own publish log; whether
the same discipline transfers cleanly onto a full MSTest/TUnit test-project publish log (which is
noisier — carries the framework's and runner's own warnings alongside the library's) needs to be
proven by an actual conversion, not assumed (see §16/§18).

## 11. What should Compono own vs. the test framework's own responsibility?

Unchanged from ADR-0041's existing framing, confirmed again by this session's audit:
Compono owns composition (constructing/sharing/configuring test parameters); the framework owns
everything about discovery, execution, lifecycle, Native AOT publish-and-run mechanics, and
reporting. Nothing found in this investigation suggests Compono should take on any of the latter.

## 12. Should this be admitted as a Compono capability?

**Split verdict.** For MSTest and TUnit: **yes, but narrowly — and mostly as documentation, not new
API**, per Gate A/B below (unchanged from the original conclusion). **For xUnit v3: this section does
not give a verdict** — §4/§5 found that making `Compono.XunitV3` work under xUnit's real AOT path
would require genuinely new Compono-side work (a package/build reorganization to resolve the
assembly-identity conflict, plus new generator-emitted AOT registration code, per §5) whose shape was
not designed in this research session. That is a materially different, more significant kind of
change than "a bug fix plus documentation" — it would need its own pass through
`capability-admission.md`'s Gate A (is this an architecturally legitimate thing for Compono to build —
a parallel AOT-targeted package/build? a `Compono.Generators` extension emitting xUnit-AOT-specific
registration code?) and Gate B (is there real demand specifically for xUnit v3 AOT support, given
MSTest and TUnit already work and could satisfy the same underlying need for a consumer not
attached to xUnit specifically). **This research flags the question for the product owner rather than
running that process itself, per this session's explicit instructions not to.**

### Gate A (architectural admission) — MSTest and TUnit only; xUnit is out of scope for this pass, see above

1. **Compono-specific value** — marginal but real. The value isn't a new runtime capability
   (Compono's dispatch already works, per §5); it's *proving and documenting* that the existing,
   already-shipped composition mechanism survives a consumer's own real-world usage pattern
   (framework-native AOT test execution) that Compono never explicitly validated before. That's
   closer to a documentation/CI-coverage gap than a design gap.
2. **Native ecosystem fit** — N/A; no new integration surface is proposed.
3. **Meaningful abstraction** — fails, in the sense that matters here: there is no new abstraction to
   build. A consumer who wants this today can already do it (MSTest/TUnit) with zero Compono changes,
   once §6's bug is fixed.
4. **Architectural fit** — clean; nothing here needs a new extension point.
5. **Package-boundary justification** — moot; no new package is being proposed.

Gate A doesn't clear as "a new capability" because there **is no new capability to build** — the
thing under investigation already works today for two of three in-scope frameworks. What Gate A does
support is a narrower claim: **fixing §6's real bug, adding a permanent CI proof (an "AOT test
project" leg alongside the existing eight `*.AotSmokeTest` legs), and writing consumer-facing
documentation** all clear admission on their own, smaller merits — a bug fix, a CI-coverage
extension, and a Cookbook recipe, not a new capability, package, or public API.

### Gate B (evidence)

Real: an explicit product-owner request (this investigation itself), plus four real sibling repos
with the exact bespoke-console-validator friction this would relieve — dogfooding-grade evidence,
not hypothetical.

## 13. If admitted, does it require new packages or public APIs?

**For MSTest and TUnit: no.** Everything needed is: (a) a bug fix in already-shipped `Compono.TUnit`
(§6/§18); (b) new, permanent CI coverage exercising each framework's *real* test-discovery-and-execution
path under `PublishAot=true` (distinct from the existing eight `*.AotSmokeTest` legs, which ADR-0041
Amendment 7 explicitly scopes away from ever doing this); (c) Cookbook/package-guide documentation. No
new public API surface.

**For xUnit v3: undesigned.** §5 found two problems that, if solved, would plausibly require new
package/build architecture and new generator responsibility: a parallel AOT-targeted
`Compono.XunitV3` build or package variant (to resolve the `xunit.v3.extensibility.core` vs.
`.core.aot` assembly-identity conflict), and new `Compono.Generators` emission logic producing
xUnit-AOT-specific `RegisteredEngineConfig` registration code. That is not the same claim as new
*public API* — a package/build split or an internal/generated registration mechanism does not by
itself add public surface, and whether any actually would is exactly one of the open questions a
dedicated Gate A/B pass (§12) would need to resolve, not something to assume here. Whether the
resulting shape lands as a new TFM-conditional build of the existing package, a new package, or
something else entirely was not evaluated here either — same deferral.

## 14. Smallest viable architecture

**For xUnit v3, this section does not apply** — per §12, the smallest viable architecture can't be
picked until Gate A/B evaluates what §5's two problems actually require, which is future work, not a
conclusion of this research.

Per capability-admission.md §Step 6, work down the list for MSTest/TUnit — this lands at the last,
smallest option:

1. Core `Compono` — not needed.
2. Existing extension package — the one real change needed (§6's fix) belongs in
   `Compono.TUnit`'s existing `Binding/BindingPlan.cs`, an ordinary bug fix against ADR-0041's
   already-`Accepted` decision, not a new design.
3. New package — not justified; explicitly rejected as a premise by the investigation's own framing,
   and nothing found here changes that.
4. **Documentation/CI-only — where this actually lands.** A Cookbook recipe ("publish your MSTest/TUnit
   Compono-composed tests as a Native AOT validation gate"), a permanent CI leg proving it continues to
   work, and closing §6's gap. No ADR is needed for this — it's implementation against
   already-`Accepted` ADR-0041's own AOT-safety claim (the bug fix) plus ordinary documentation/CI work,
   not a new architectural decision.

## 15. Framework/version/TFM support matrix

| Framework | Minimum version for AOT test execution | Status |
|---|---|---|
| MSTest | `MSTest.Sdk` / `MSTest.TestFramework` **4.4.0+**, `MSTestSourceGenMode=ReflectionFree` (default) | Works today, verified |
| TUnit | `TUnit`/`TUnit.Core` **1.65.68** (repo's current pin) or later | Works today, verified, once §6's fix ships |
| xUnit v3 | `xunit.v3.aot.mtp-v2` **4.0.0+** (the AOT-specific package family, **not** `xunit.v3.mtp-v2`), min **.NET 9** | **Framework itself works** (verified — plain-xUnit AOT baseline runs cleanly, §4). **`Compono.XunitV3` does not work with it today** — compile-time package conflict plus missing generator-side AOT registration (§5). Open question for the product owner (§12), not resolved here; no re-evaluation trigger yet since no upstream fix is needed |
| NUnit | N/A | Out of scope this session — no confirmed AOT runner support at all |

## 16. Permanent CI validation, if pursued

Extend the existing `aot-validation.yaml` pattern (nine legs today) with new legs that publish and run
a **real** MSTest/TUnit test project (not a hand-written `Program.cs` stand-in) under
`PublishAot=true`, matching ADR-0041 Amendment 7's own trigger-selectivity design (no `paths:`
trigger-level filter; an inexpensive `changes` job computes applicability; a final `if: always()`
aggregation job is the required check). One RID (`linux-x64`, matching the existing legs) is
sufficient — this isn't validating platform-specific behavior, it's validating that the framework's
AOT execution model accepts Compono's dispatch, which is platform-independent.

## 17. Dogfooding recommendation

The user selected **both `decoweaver` and `alexa-vox-craft`** as target consumers for an eventual real
conversion (not performed in this research phase, per its explicit boundary). Both currently use the
`Check()`-style hand-rolled harness and neither currently depends on a specific test framework for
their validator, so either is a clean conversion target once §6's fix ships and a documented pattern
exists. `decoweaver` is the richer example (actual-vs-expected `Check()`, seven DI scenarios,
zero-tolerance IL gating, its own research doc reasoning) and should be attempted first if only one is
converted initially; `alexa-vox-craft`'s conversion would additionally prove the pattern holds for a
consumer with real Alexa-request-shaped fixtures pulled from its existing JIT suite, a different shape
of production scenario than DI decoration.

## 18. Documentation/skill changes and other required follow-ups

**Required regardless of any further admission decision (bug fix, not a capability question):**
`Compono.TUnit`'s `BindingPlan.ResolveMethodInfo` (`src/Compono.TUnit/Binding/BindingPlan.cs:159`)
needs a `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)]` annotation
(or equivalent) on the `Type` parameter/property chain reaching `Type.GetMethods(BindingFlags)`,
mirroring the exact fix shape ADR-0041 Amendments 1/4/5 already established for
`ConfigProfileBinder`. This should be recorded as a new dated Amendment to ADR-0041 (its own
AOT-safety claim for `Compono.TUnit` is incomplete without it) once fixed and proven with a real
publish-and-run — the existing `Compono.TUnit.AotSmokeTest` project does not exercise this path and
should be extended (or a new project added) to drive real TUnit `ClassMetadata`-based discovery, not
just the hand-built stand-in it uses today.

**If the documentation/CI-only outcome (§14) is pursued:**

- A new Cookbook recipe: "Publish Compono-composed MSTest/TUnit tests as a Native AOT validation
  gate," including the exact caveat from §10 (IL-warning-gating discipline needs to be re-proven
  against a full test-project publish log, not assumed to transfer from the console-app pattern).
- `docs/packages/compono-tunit.md` and `docs/packages/compono-mstest.md` gain a short Native AOT note
  once §18's fix and CI coverage land.
- No action needed on `xunit/xunit#3335` specifically — it does not reproduce under xUnit's real AOT
  configuration (§4) and is not a blocker for anything. The actual open item, if `Compono.XunitV3`
  AOT support is pursued later, is the product-owner-facing question recorded just above this bullet
  list, not an upstream issue to track.
- No skill (`compono` agent skill) changes are required unless/until the Cookbook recipe ships — the
  skill's package-reference-file pattern only needs an update alongside real new documented behavior,
  not for this research artifact itself. No `evals.json` changes needed for the same reason.

**Not required (MSTest/TUnit only):** no ADR. This finding closes as implementation against an
already-`Accepted` ADR (the bug fix) plus ordinary Cookbook/CI work — there is no new architectural
decision here for an ADR to record.

**Open question flagged for the product owner (xUnit v3 — not resolved, not implemented, not run
through Gate A/B in this session per explicit instruction):** whether Compono should pursue Native AOT
support for `Compono.XunitV3` at all is now a real, undecided question, distinct in kind from the
MSTest/TUnit outcome above. If pursued, it would need:

- A decision on how to resolve the `xunit.v3.extensibility.core` (reflection) vs.
  `xunit.v3.extensibility.core.aot` package conflict — most plausibly a parallel,
  AOT-targeted build or package variant of `Compono.XunitV3`, but that's a real architectural choice
  (naming, TFM/RID conditioning, whether it's the same package with a conditional dependency or a
  distinct package) that a dedicated design pass should make, not this document.
- New `Compono.Generators` emission logic producing xUnit-AOT-specific
  `RegisteredEngineConfig.RegisterTheoryDataRowFactory`-style registration for `[Compose]`-attributed
  methods — genuinely new generator responsibility, modeled on xUnit's own
  `AotCsvDataSource`/`AotRetryFact`/`AotTraitExtensibility` sample pattern, not a small extension of
  existing `PlanCache<T>`/`RowInvokerRegistry` emission.
- Real Gate B evidence specific to xUnit v3 (not just "AOT testing in general is wanted" — MSTest and
  TUnit already satisfy that for a consumer not already committed to xUnit specifically).

Given `Compono.XunitV3` is this repo's oldest and most widely dogfooded integration package, and given
MSTest/TUnit already provide a working path to the same underlying goal, this may or may not clear the
bar — that determination is explicitly left to the product owner and a future dedicated research/design
pass, not decided here.

## Provenance

- [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md) and its Amendments — the existing AOT-safety
  mechanism and claim this research audited and found one real gap in.
- [RESEARCH-0018](0018-nunit-integration-viability-research.md) §7/§10/§11 — prior findings on MSTest's
  and NUnit's AOT/runner posture, confirmed and extended here.
- `docs/architecture/capability-admission.md` — the admission process applied throughout §12-14.
- Sibling-repo survey (this session): `decoweaver`, `alexa-vox-craft`, `dynamodb-efcore-provider`,
  `minimal-lambda`, plus six repos confirmed to have no AOT validation pattern at all
  (`dynamodb-distributed-lock`, `dynamo-mapper`, `aws-secrets-manager-provider`,
  `lambda-aspnetcore-hosting-extensions`, `optimized-enums`, `structured-logging`,
  `source-generator-tools`).
- External: [xUnit.net Native AOT docs](https://xunit.net/docs/getting-started/v3/native-aot),
  [xunit/samples.xunit `v3/AotCsvDataSource`](https://github.com/xunit/samples.xunit/tree/main/v3/AotCsvDataSource)
  (and its `AotRetryFact`/`AotTraitExtensibility` siblings) — the official reference implementation
  for custom data-attribute extensibility under xUnit v3 Native AOT, used to correct this document's
  xUnit findings,
  [xunit/xunit#3335](https://github.com/xunit/xunit/issues/3335) (does **not** reproduce under the
  correct AOT package set — see the correction note; kept as a citation only to explain the original
  document's retracted error),
  [TUnit Engine Modes](https://tunit.dev/docs/execution/engine-modes/),
  [.NET Blog: Test what you ship — MSTest and Native AOT](https://devblogs.microsoft.com/dotnet/mstest-source-generation/).
