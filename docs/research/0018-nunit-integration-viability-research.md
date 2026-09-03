# [RESEARCH-0018] NUnit Integration Admission and Viability Research for Compono

**Status:** Research complete. No ADR yet — scoped to research/discovery only, per
the request that produced it.

**Framing:** ADR-0039 already ran Gate A once and recorded `Compono.NUnit`
as an **admitted candidate** (architecturally legitimate), citing
`IParameterDataSource` (per-parameter granularity) and
`ITestBuilder`/`IFixtureBuilder` (row/fixture-constructor cases). Gate B
(evidence, ADR-0029) has never been evaluated for NUnit — no dogfooding or
demand evidence is recorded anywhere. This document re-runs Gate A with
current (2026) primary-source evidence, closes Gate B honestly (no
evidence found), and does the deep technical discovery the requester asked
for: current NUnit version landscape, the one-package-vs-many binary-
compatibility question (proven empirically, not asserted), the correct
extension seam, complete-row/data-coexistence behavior, generator/binding
fit, AOT posture, and runner support. The stated product bias is
`Compono.NUnit` (one package), not an AutoFixture-style
`Compono.NUnit3`/`Compono.NUnit4`/`Compono.NUnit5` split, and the burden
was on finding real evidence a split is *required*, not on justifying a
single package.

## 1. Desired consumer experience

Same target shape as every other Compono test-framework package, adapted
to NUnit's own idiom:

```csharp
[TestFixture]
public class OrderServiceTests
{
    [Compose]
    public void Creates_service(
        SomeService sut,
        [Shared] SomeDependency dependency)
    {
    }
}
```

`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`,
`[Shared]`, `CompositionBuilder.Share<T>()`, exact registrations,
constructor selection, and every other integration package
(`Compono.TestDoubles`, `Compono.NSubstitute`, `Compono.Bogus`,
`Compono.Logging`, `Compono.DependencyInjection`, `Compono.Http`) working
unmodified — confirmed by the same reasoning RESEARCH-0017 §1 already
established (none of those packages reference `Compono.XunitV3`/
`Compono.TUnit`/`Compono.MSTest`; they register providers and generator
hooks against core `Compono` only, so a fourth framework package adds no
new coupling).

**One real NUnit-specific divergence found and confirmed necessary by
spike (§20):** unlike `[Test]`, a method whose only test-identifying
attribute is a custom `ITestBuilder`-implementing attribute is **not**
automatically recognized as making its containing class a test fixture —
the class needs an explicit `[TestFixture]` attribute. `Compono.NUnit`'s
own documentation must state this plainly (`[TestFixture] [Compose]`, not
`[Compose]` alone), because a consumer who drops `[TestFixture]` (a
reasonable move-fast guess, since `Compono.XunitV3`/`Compono.MSTest` need
no fixture-level attribute at all) gets a silent zero-tests result, not an
error — spike-confirmed below.

## 2. Re-run Gate A (ADR-0039) against current (2026) evidence

Evaluated against ADR-0039's five required legs, all cleared:

- **Compono-specific value.** Confirmed, not just asserted: NUnit's
  `IParameterDataSource` (per-parameter) and `ITestBuilder`/`ISimpleTestBuilder`
  (whole-method) are genuinely different extensibility shapes from
  anything `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest` sit on — see
  §4. A hand-rolled `Composer.Create<T>()` call inside a test body doesn't
  get NUnit's own discovery-time enumeration, custom display names, or
  per-row `[Shared]` semantics for free the way a real extension does.
- **Native ecosystem fit.** `[TestFixture] [Compose]` (§1) is idiomatic
  NUnit (a `TestBuilder`-family attribute on an ordinary test method,
  NUnit's own documented extension shape, §4) — not an xUnit-shaped clone
  forced onto NUnit's model.
- **Meaningful abstraction.** Confirmed by spike (§9/§20): a hand-written
  `IParameterDataSource`/`ITestBuilder` consumer would need to reimplement
  row/graph lifetime, `[Shared]` scoping, and seed reporting themselves —
  materially more than an afternoon's extension method.
- **Architectural fit.** `Compono.NUnit` builds entirely on
  `CompositionRow`/`RowInvokerRegistry`/`BindingPlan` (ADR-0021), a
  pre-existing public core extension point — no core change required
  (§10/§11).
- **Package-boundary justification.** NUnit is a real, separate ecosystem
  dependency; belongs outside core, matching every other integration
  package's own shape.

**Maintenance-cost weighing factor:** small and linear, matching
ADR-0039's own finding for the other candidates — one more
detection-table row and reference file in `skills/compono`, one more
package/test-project pair following an already-proven template
(`Compono.MSTest` is the most recent, most similar precedent: a
synchronous, `MethodInfo`-shaped extension point).

**Gate A verdict: reconfirmed ADMIT (architectural admission).** Nothing
in this research's primary-source re-check overturns ADR-0039's original
disposition.

### Gate B (evidence, ADR-0029) — checked, not found

Repo-wide search (same methodology RESEARCH-0017 §17 used) for any NUnit
usage, branch, or discussion across
`/Users/ncipollina/source/repos/layered-craft/` and
`/Users/ncipollina/source/repos/ncipollina/` (default checkouts and all
local/remote branch names) found **nothing** — no existing NUnit consumer,
no dogfooding friction, no repeated request recorded anywhere in this
repo's own history (`docs/roadmap/post-mvp.md`'s two real dogfooding
passes recorded zero candidates in this space, matching ADR-0039's own
finding). **This is not evidence against admission** (same principle
RESEARCH-0017 §17 applied to MSTest) — it means NUnit remains an
**admitted candidate**, not yet a **roadmap item**. This research supplies
Gate A/technical-viability evidence; it does not and cannot manufacture
Gate B evidence.

**Overall admission verdict for this research: ADMIT PRE-1.0 conditional
on Gate B** — i.e., the correct terminology per ADR-0039 is that NUnit
clears Gate A (confirmed, stronger evidence than before) and remains
blocked on Gate B exactly as before. If the user's intent in commissioning
this research is to *also* supply the Gate B trigger via explicit
product-owner request (the same mechanism that gated `Compono.TUnit` and
`Compono.TestDoubles` into roadmap items per `future-packages.md`), that
is a decision for the user to make explicitly, not one this research can
infer.

## 3. Current NUnit version landscape (primary sources, fetched directly, not recalled)

Fetched/searched directly against nunit.org, NuGet, and GitHub — current
as of this research (2026-09-02):

- **NUnit 3.x:** current released version **3.14.0**. Framework assets
  ship for `net35`/`net40`/`net45`/`netstandard2.0` — **no
  net6.0/net8.0-specific asset** (confirmed directly, §17 — `lib/` only
  contains `net35`, `net40`, `net45`, `netstandard2.0`); a modern
  `net8.0` consumer resolves the `netstandard2.0` asset via NuGet's
  standard TFM-compatibility fallback (the same fallback mechanism
  ADR-0037 already documents for Moq).
- **NUnit 4.x:** current released version **4.6.1** (NuGet, last updated
  2026-05-19; 4.6.0 itself released 2026-05-03). Ships `net8.0` and
  `net462` assets (confirmed directly from the installed package's
  `lib/` directory — no `netstandard2.0` fallback needed for a modern
  consumer). [Towards NUnit 4](https://docs.nunit.org/articles/nunit/Towards-NUnit4.html)
  (fetched directly) states NUnit 4's minimum platform floor is **.NET
  Framework 4.6.2** and **.NET 6.0**, and documents `Assert.That`
  message-format and `ClassicAssert`-relocation breaking changes — none
  of which touch `ITestBuilder`/`IParameterDataSource`/`IMethodInfo` (the
  surface `Compono.NUnit` actually needs, §4).
- **NUnit 5.x:** **prerelease only** — `5.0.0-beta.1` on NuGet (last
  updated 2026-07-04). Ships `net8.0`, `net10.0`, and `net462` assets
  (confirmed directly). No stable 5.0.0 release exists at the time of
  this research; nunit.org's own release-notes/roadmap pages (fetched)
  confirm the NUnit team does not currently publish a long-term roadmap
  and works issue-by-issue rather than against a committed schedule — so
  no reliable public date exists for 5.0.0 GA.
- **NUnit3TestAdapter:** current version **6.2.0**. Per NUnit's own docs
  (fetched, `NUnit-And-Microsoft-Test-Platform.html`): adapter **5.0+**
  supports Microsoft.Testing.Platform (MTP) 1.x; adapter **6.0+** supports
  MTP 2.0 and drops .NET Core 3 support. The same adapter package
  supports both classic VSTest and MTP simultaneously — opt into MTP via
  `<EnableNUnitRunner>true</EnableNUnitRunner>` + `<OutputType>Exe</OutputType>`
  in the consumer's own test project, not a different adapter package.
- **Microsoft Testing Platform support:** confirmed current via the .NET
  blog's "Microsoft.Testing.Platform: Now Supported by All Major .NET Test
  Frameworks" post and Microsoft Learn's own
  "Microsoft.Testing.Platform (MTP) support in NUnit" page — NUnit is a
  first-class MTP-supported framework today, on equal footing with
  xUnit v3/MSTest/TUnit.

**Framing conclusion, directly answering the requester's stated bias:**
NUnit 5 is real but pre-GA (beta), not yet a version any consumer should
be asked to target as a floor. NUnit 3 is still current-released (3.14.0,
2026), not legacy in the way MSTest's frozen 3.x line turned out to be
(RESEARCH-0017 §16) — no evidence found that NUnit 3.x has stopped
receiving releases. This changes the version-floor calculus from the
requester's own framing ("if NUnit 3 creates significant compatibility
complexity, evaluate a 4+-only floor") because §5 finds **no
compatibility complexity was found at all** between 3.x and 4.x/5.0-beta.

## 4. NUnit extension seam — verified against current source/docs, and by spike

NUnit's documented (`docs.nunit.org/articles/nunit/extending-nunit/Custom-Attributes.html`,
fetched) custom-attribute extension interfaces, all living in
`NUnit.Framework.Interfaces`/`NUnit.Framework.Internal`
(`nunit.framework.dll` itself — no separate extensibility assembly, unlike
xUnit v3's `xunit.v3.extensibility.core`):

- **`ITestBuilder`** — `IEnumerable<TestMethod> BuildFrom(IMethodInfo method, Test? suite)`.
  An attribute implementing this owns building **one or more** complete
  `TestMethod` instances for the whole method — the exact shape
  `[Compose]` needs to own one complete composed row per method, verified
  working end-to-end by spike (§20).
- **`ISimpleTestBuilder`** — `TestMethod BuildFrom(IMethodInfo method, Test suite)`,
  a single non-parameterized case; NUnit's docs state attributes
  implementing this are **ignored** if any other attribute on the same
  method implements `ITestBuilder` — i.e., `ITestBuilder` takes priority,
  confirming `ITestBuilder` (not `ISimpleTestBuilder`) is the correct base
  for `[Compose]`, which always needs to supply real composed arguments,
  not zero.
- **`IParameterDataSource`** — `IEnumerable GetData(IParameterInfo parameter)`,
  per-parameter, combinatorially combined by NUnit's own
  `ParameterDataProvider` across every parameter that carries one. This is
  the extension point ADR-0039 originally cited for "per-parameter
  granularity `Compono.XunitV3`'s row model doesn't have" — real, but
  **not the right seam for `[Compose]` itself**, because per-parameter,
  independently-combined data sources are structurally the opposite of
  "one Compose source owns one complete row" (the product's own stated
  bias, and the same principle RESEARCH-0017 §10 already established for
  MSTest). `IParameterDataSource` remains available as a possible
  *future*, narrower `[Shared]`-adjacent building block if a per-parameter
  scenario is ever motivated by real evidence — not needed for the
  primary `[Compose]` row-owning scenario this research is scoped to.
- **`IFixtureBuilder`** — analogous to `ITestBuilder` but for
  `[TestFixture]`-class-level construction (parameterized fixture
  constructors). Out of scope for the primary `[Compose]`-on-a-method
  target shape (§1); worth recording as available if a future
  `Compono.NUnit` fixture-constructor-composition feature is ever
  motivated, not needed now.

**`IMethodInfo`/`IParameterInfo` are NUnit's own metadata-wrapper types**
(confirmed directly, `ITestBuilder-Interface.html`: "`IMethodInfo` is an
NUnit internal class used to wrap a `MethodInfo`"), not raw
`System.Reflection.MethodInfo`/`ParameterInfo` — a genuine, real
divergence from xUnit v3 (`MethodInfo` directly) and MSTest
(`MethodInfo` directly). `IMethodInfo` exposes `.MethodInfo` (the
underlying real `System.Reflection.MethodInfo`) and `.GetParameters()`
(returning `IParameterInfo[]`, each wrapping a real `ParameterInfo` via
`.ParameterInfo`) — confirmed directly by the spike (§20) successfully
calling `method.GetParameters()` and using the wrapped `.ParameterType`.
**Practical consequence for `BindingPlan`:** `Compono.NUnit`'s own
`ComposeAttribute.BuildFrom` must first unwrap `IMethodInfo.MethodInfo` to
get a real `System.Reflection.MethodInfo`, then hand that unwrapped value
to the same `BindingPlan.Build(MethodInfo)` signature
`Compono.XunitV3`/`Compono.MSTest` already use unchanged (§10) — one extra
unwrap call, not a new binding architecture.

**Building the actual `TestMethod` result requires NUnit's internal
`NUnit.Framework.Internal.Builders.NUnitTestCaseBuilder` and
`NUnit.Framework.Internal.TestCaseParameters`** (both technically in the
`Internal` namespace, not a stable-contract-guaranteed public surface, but
the same pattern NUnit's *own* built-in `[TestCase]`/`[Values]`/
`[Combinatorial]` attributes use internally, and the same pattern this
research found no publicly-documented alternative to for producing a
fully-formed multi-parameter `TestMethod` from an `ITestBuilder`). No
purely-public-API path to build a complete parameterized `TestMethod`
without touching `NUnit.Framework.Internal.Builders` was found during this
research — a real, honestly-stated dependency on an "Internal"-namespaced
but empirically-stable (§5) NUnit type, the same class NUnit's own
framework attributes are built on, not an unsupported hack.

**Conclusion: `ITestBuilder`, unwrapping `IMethodInfo`→`MethodInfo` and
delegating to `NUnitTestCaseBuilder`/`TestCaseParameters` for the actual
`TestMethod` construction, is the correct and smallest seam.** Confirmed
working end-to-end, across three NUnit generations, by spike (§5/§20).

## 5. Binary/assembly-identity compatibility — proven empirically, the central question

**This is the single most important result of this research, and it
contradicts the MSTest precedent's caution: no binary incompatibility was
found across NUnit 3.x → 4.x → 5.0-beta.**

### Assembly identity, inspected directly

`System.Reflection.AssemblyName.GetAssemblyName(...)` run directly against
each installed NuGet package's `nunit.framework.dll`:

| Package version | Resolved assembly identity |
| --- | --- |
| `NUnit` 3.14.0 (`netstandard2.0` asset) | `nunit.framework, Version=3.14.0.0, Culture=neutral, PublicKeyToken=2638cd05610744eb` |
| `NUnit` 4.6.1 (`net8.0` asset) | `nunit.framework, Version=4.6.0.0, Culture=neutral, PublicKeyToken=2638cd05610744eb` |
| `NUnit` 5.0.0-beta.1 (`net8.0` asset) | `nunit.framework, Version=5.0.0.0, Culture=neutral, PublicKeyToken=2638cd05610744eb` |

**Same strong-name public key token across all three majors** — NUnit has
kept `nunit.framework` a single, continuously strong-named assembly
identity across 3.x/4.x/5.0-beta, unlike the MSTest precedent
(RESEARCH-0017's `MSTest.TestFramework` 3.x/4.x pair, which ADR-0057
Amendment 1 found to be **binary-incompatible** despite an unchanged
public API surface). This is exactly the category of fact the requester's
prompt warned against inferring from source-level API similarity — so it
was proven by loading real compiled binaries, not read off a changelog.

### The real spike: compile once, run against all three

A throwaway `Compono.NUnit`-shaped extension (`ext3`, a class library
targeting `net8.0`) was compiled **once**, referencing only `NUnit`
**3.14.0**, implementing `ComposeAttribute : NUnitAttribute, ITestBuilder`
per §4's chosen seam (unwrapping `IMethodInfo`, calling
`NUnitTestCaseBuilder().BuildTestMethod(method, suite, new TestCaseParameters(args))`).
The resulting `ext3.dll` — **never recompiled** — was then referenced via
a raw `<Reference HintPath>` (not a `PackageReference`, to guarantee no
silent recompilation) from three separate consumer test projects, each
pinned to a different NUnit generation:

| Consumer | NUnit package | Adapter | Result |
| --- | --- | --- | --- |
| `consumer3` | `NUnit` 3.14.0 (same version compiled against) | `NUnit3TestAdapter` 4.6.0, classic VSTest | `dotnet test --list-tests` discovers `ComposedMethod(Compono:v3)`; `dotnet test` — **1 passed** |
| `consumer4` | `NUnit` 4.6.1 | `NUnit3TestAdapter` 6.2.0, classic `dotnet test` **and** MTP-executable mode (`EnableNUnitRunner=true`) | Both modes discover and run the identical `ext3.dll` correctly — classic: **2 passed** (plus a sanity `[Test]`); MTP exe: **4 passed** (extended fixture set, §9) |
| `consumer5` | `NUnit` 5.0.0-beta.1 | `NUnit3TestAdapter` 6.2.0, classic `dotnet test` | Discovers and runs — **1 passed** |

**The exact same `ext3.dll`, compiled against 3.14.0 only, correctly
discovered and executed a real parameterized test case against 4.6.1 and
5.0.0-beta.1 at runtime, with no recompilation, no shim, no facade.**
This directly answers the requester's central question: *if
`Compono.NUnit` compiles against the oldest selected NUnit version, the
same `Compono.NUnit.dll` loads and operates correctly against the newest
selected version* — proven, not inferred from NuGet range syntax or
API-surface reading.

**One real caveat, stated honestly:** the `NUnitTestCaseBuilder`/
`TestCaseParameters` types this depends on (§4) live in the `Internal`
namespace, which NUnit does not contractually guarantee stable across
majors the way a `Public`-namespaced type is. This research's empirical
result (unchanged behavior across three majors, including a prerelease
5.0.0-beta.1) is strong evidence, not a permanent guarantee — the same
honest caveat RESEARCH-0017 §16 applied to MSTest's own unmaintained-3.x
finding. A version-matrix regression check (§18) belongs in CI once
`Compono.NUnit` exists, exactly as `Compono.MSTest`/`Compono.TUnit`
already do for their own floors.

### Why this differs from the MSTest precedent

MSTest's binary break (ADR-0057 Amendment 1) was a real, framework-internal
architectural change between its 3.x and 4.x lines. No equivalent event
was found for NUnit: `Towards-NUnit4.html`'s own documented breaking
changes (§3) are all about `Assert`/`ClassicAssert` call-site behavior,
never about `ITestBuilder`/`IMethodInfo`/the extension-point assembly
surface `Compono.NUnit` actually touches. The AutoFixture comparison (§17)
independently corroborates this: AutoFixture's own NUnit-line split is
NUnit**2**→NUnit**3** (a genuine architectural rewrite,
`NUnit.Core`-based → attribute-based), not NUnit3→NUnit4 — AutoFixture
never needed a NUnit4-specific package at all.

## 6. Complete-row / data-coexistence semantics — spike-verified, not assumed

**`[Compose]` (`ITestBuilder`) and NUnit's own `[TestCase]` on the same
method produce two independent test cases, not a merged row** — confirmed
directly by spike (§20):

```
Mixed(Compono:v3)
Mixed(1,"x")
```

Exactly the same "independent complete rows, not partial-row merging"
model RESEARCH-0017 §9 found for MSTest's `ITestDataSource` — and for the
same underlying reason: every `ITestBuilder`-implementing attribute (and
`[TestCase]` is itself backed by NUnit's own internal `ITestBuilder`
implementation) independently contributes its own complete `TestMethod`(s)
to the method's test-case list; nothing in NUnit's model merges two
builders' outputs into one row. **This settles the product's own stated
preference cleanly and for free** — "one Compose source owns one complete
row" is not a Compono-imposed restriction fighting NUnit's grain, it's
what NUnit's own `ITestBuilder` model already does by construction.

`IParameterDataSource`-based sources (`[Values]`, `[Range]`, `[Random]`,
a custom per-parameter source) behave differently and were **not**
spiked directly (out of `[Compose]`'s own seam, §4) — NUnit's own
`ParameterDataProvider` combinatorially cross-products every
`IParameterDataSource` found across a method's parameters when **no**
`ITestBuilder`-family attribute is present. Because `[Compose]` itself is
an `ITestBuilder`, and `ITestBuilder`'s own presence is what produces the
method's test cases in the first place, a method carrying both
`[Compose]` and a per-parameter `[Values]`/custom `IParameterDataSource`
attribute would have `[Compose]` solely responsible for producing test
cases — the per-parameter sources would go unused unless `[Compose]`
itself chose to read and honor them (it does not, by the same "no
partial-row merging" design bias). This is a reasoned extrapolation from
the documented interaction rule (`ISimpleTestBuilder` is ignored when
`ITestBuilder` is present, §4), not independently spiked — flagged here as
a real, if minor, gap for a future implementation pass to verify directly
before shipping, not settled by this research.

## 7. Generic Compose family shape — no NUnit-specific obstacle found

No NUnit-specific restriction was found against generic attribute types.
`ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile, TConfig>` are
ordinary C# generic `Attribute` subclasses (the same shape
`Compono.TUnit`/`Compono.MSTest` already ship) — nothing about NUnit's
attribute-discovery mechanism (`ITestBuilder`-implementation detection via
ordinary `.GetCustomAttributes()`/interface-check reflection, confirmed
implicitly by the spike's own working `[Compose]`) inspects an attribute's
open-generic identity in a way that would reject a closed generic
attribute instance. `Compono.NUnit` can reuse the exact
`ComposeAttribute`/`ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile,TConfig>`
inheritance shape `Compono.MSTest`'s own design (RESEARCH-0017 §15)
already established as workable, adapted only for `ITestBuilder` instead
of `ITestDataSource`. Not independently spiked with a generic attribute
(the throwaway `ext3` spike used a non-generic attribute for minimalism) —
a reasoned extension of already-proven precedent (`Compono.TUnit`/
`Compono.MSTest` both already ship working generic Compose attributes in
this exact family), not a new risk area specific to NUnit.

## 8. Source-generator discovery — no NUnit-specific generator work needed

`Compono.Generators`' existing `ComposeMethodDiscovery` pipeline discovers
Compose-family methods by attribute *name*/metadata registration, feeding
the same composition-plan/`RowInvokerRegistry`-entry generation already
proven for `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest` (confirmed
by reading the pattern those three packages already follow, per
RESEARCH-0017 §6's identical finding — not re-derived from scratch here,
since `Compono.NUnit`'s method shape, `[Compose]` on an ordinary method
with composed parameters, is structurally identical to the other three at
the generator's level of abstraction: a method, its parameters, and which
ones carry `[Shared]`). No NUnit-specific composition generator is
required; only a metadata-name registration entry for
`Compono.NUnit.ComposeAttribute`, matching the existing per-package
registration pattern.

## 9. Runtime binding/dispatch — package-local, thin adapter over IMethodInfo

`RowInvokerRegistry`/`CompositionRow`/`BindingPlan`/`RowInvokers.Build` are
all core or already-proven-portable (RESEARCH-0017 §6), operating on plain
`System.Reflection.MethodInfo`/`ParameterInfo` — framework-agnostic. The
only genuinely new, NUnit-specific code is:

- `ComposeAttribute : NUnitAttribute, ITestBuilder` itself (§4).
- The `IMethodInfo.MethodInfo`/`IParameterInfo.ParameterInfo` unwrap step
  (§4) — a few lines, not a new binding layer.
- The `NUnitTestCaseBuilder`/`TestCaseParameters` call to actually produce
  the `TestMethod` NUnit expects back (§4/§5) — NUnit-specific, with no
  equivalent needed in `Compono.XunitV3`/`Compono.MSTest` (both hand back
  a plain data row, not a fully-constructed `TestMethod` object; this is
  a real, if small, NUnit-specific cost of `ITestBuilder`'s
  richer-but-more-invasive shape relative to `ITestDataSource`/`DataAttribute`).

**Answering the requester's explicit question directly: `Compono.NUnit`
should reuse the existing package-local `BindingPlan` pattern unchanged**
(same as `Compono.MSTest`'s own conclusion, RESEARCH-0017 §6) — NUnit does
**not** require a materially different binding layer, only a thin,
NUnit-specific adapter (`IMethodInfo` unwrap + `NUnitTestCaseBuilder`
construction) in front of the same unchanged core machinery. No shared
extraction is warranted by this evidence alone — three packages
(`Compono.XunitV3`, `Compono.TUnit`, `Compono.MSTest`) already prove the
package-local-adaptation pattern works, and NUnit's own `IMethodInfo`
wrapper is a genuine enough surface difference (not superficial) that
forcing it into a shared abstraction now would be premature, exactly per
the request's own instruction not to extract shared infrastructure merely
because four frameworks now exist.

## 10. Reflection and Native AOT/trimming

- **Framework-mandated metadata access:** `ITestBuilder.BuildFrom` hands
  `Compono.NUnit` an `IMethodInfo` wrapping a real `MethodInfo` — the same
  category of framework-required reflection input `Compono.XunitV3`/
  `Compono.MSTest` already treat as AOT-safe (unavoidable, not
  Compono-controlled). Reading `.GetParameters()`/attribute presence off
  it is exactly what `BindingPlan.Build` already does.
- **No `MakeGenericType`/`Activator.CreateInstance`/dynamic generic
  activation needed** — `RowInvokerRegistry.TryGet` is unchanged,
  reflection-free dispatch, reused as-is (same conclusion RESEARCH-0017
  §7 reached for MSTest).
- **The one NUnit-specific reflection-adjacent risk:**
  `NUnitTestCaseBuilder`/`TestCaseParameters` (§4/§5) are ordinary public
  (if `Internal`-namespaced) constructor/method calls with concrete,
  non-generic parameter types — not reflection themselves, but their
  trim-safety was **not independently verified against a trimmed/published
  binary** in this research (the spike ran under an ordinary `dotnet test`
  process, not a trimmed or Native-AOT-published executable). This is a
  real gap for a future implementation pass to close with an
  `AotSmokeTest`-style project (the same pattern `Compono.XunitV3`/
  `Compono.TUnit`/`Compono.MSTest` already use), not something this
  research can honestly claim proven.
- **NUnit's own Native AOT posture: not confirmed runnable, and this
  research found no evidence it is.** No NUnit documentation, release
  note, or blog post found during this research states that
  `nunit.framework`/the NUnit console runner/the VSTest or MTP adapters
  are published or validated for Native AOT execution — in contrast to
  xUnit v3 (confirmed via this research's own web search: xUnit.net v3
  4.0, shipped 2026-08-14, added real Native AOT support with explicit,
  documented tradeoffs) and MSTest (`MSTestSourceGenMode=ReflectionFree`,
  RESEARCH-0017 §7). **This is a real, honestly-stated distinction to
  preserve, not overclaim:** *`Compono.NUnit`'s own integration code can
  be written trim/AOT-safely* (framework-mandated `MethodInfo` access
  only, no Compono-controlled reflection) is a materially different claim
  from *NUnit test execution itself is Native-AOT-runnable* — this
  research did not find evidence for the latter, and a permanent
  `Compono.NUnit.AotSmokeTest` project (matching the other three
  packages' own pattern) may or may not be achievable depending on
  whether NUnit's own runner/adapter chain supports AOT publishing at
  all — an open question for the implementation phase to resolve
  directly, not answered here.

## 11. Runner support: MTP vs classic VSTest — both work, spike-confirmed

Both runner paths were exercised directly against the same `ext3.dll`
integration (§5/§20):

- **Classic VSTest / `dotnet test`** (via `NUnit3TestAdapter`,
  `Microsoft.NET.Test.Sdk`): worked cleanly against NUnit 3.14.0, 4.6.1,
  and 5.0.0-beta.1 alike.
- **Microsoft Testing Platform (MTP), executable mode**
  (`<EnableNUnitRunner>true</EnableNUnitRunner>` + `<OutputType>Exe</OutputType>`,
  `NUnit3TestAdapter` 6.2.0): running the built executable directly
  discovered and ran all 4 spike test cases correctly under MTP's own
  `Microsoft.Testing.Platform` host (`v2.1.0`), including telemetry/host
  banner output confirming MTP, not VSTest, actually executed the run.

**One real current-ecosystem friction point, not a `Compono.NUnit`-specific
problem:** on the newest available preview SDK
(`11.0.100-preview.7.26381.103`), a plain `dotnet test` against a project
with `<EnableNUnitRunner>true</EnableNUnitRunner>` **fails outright**
("Testing with VSTest target is no longer supported by Microsoft.Testing.Platform
on .NET 10 SDK and later... opt-in to the new dotnet test experience") —
the classic `dotnet test` CLI verb itself is being phased toward MTP-only
semantics industry-wide, independent of NUnit. Running the built
executable directly (bypassing `dotnet test`'s own VSTest-bridge
assumptions) worked without issue. **Recommendation, matching the
requester's own MSTest-derived default bias:** `Compono.NUnit` should
support both runner modes (cost is zero at the package level — nothing
`Compono.NUnit` does differs between them, the difference is entirely in
the consumer's own `.csproj`/adapter choice) and its docs should note the
current SDK-driven `dotnet test`/MTP transition honestly rather than
silently assuming classic `dotnet test` remains the default indefinitely.

## 12. Discovery/execution lifecycle — same double-evaluation risk as MSTest, spike-confirmed

Applying RESEARCH-0017's exact methodology (a file-backed, cross-process
call-count log in the `BuildFrom` body) to `consumer3` (classic VSTest
adapter):

- **A single `dotnet test` invocation** (discovery and execution inside
  one process): **exactly 1** `BuildFrom` call.
- **A separate `dotnet test --list-tests` followed by a separate, later
  `dotnet test`** (two genuinely different OS processes, confirmed via
  distinct PIDs in the log): **2 total `BuildFrom` calls, one per
  process** — the same "each separately-invoked discovery-then-execution
  session re-evaluates the data source" pattern RESEARCH-0017 §20a found
  for MSTest's classic VSTest adapter, not a per-run guarantee-breaking
  property. A CI pipeline running `dotnet test` once gets exactly one
  `BuildFrom` call per method; a Visual Studio Test Explorer session
  (discover once, execute later/repeatedly) can produce more than one.

**Practical consequence, stated with the same honesty RESEARCH-0017 §5
required for MSTest:** `Compono.NUnit`'s own documentation must state that
composition — including any registration factory/provider side effect —
may run more than once for what appears to be one eventual test case,
under the same separately-invoked-discovery-then-execution conditions
already documented for `Compono.MSTest`. `[Shared]`/`Share<T>()` remain
correct within each independently-built row (deterministic seeding, per
ADR-0009/ADR-0012, keeps repeated composition logically equivalent even
though it's a different object graph each time) — this is not a
`[Shared]`-correctness problem, only an observable-side-effect-repetition
one, exactly matching the MSTest finding.

MTP-executable mode was not independently re-tested for the
same double-invocation question (the `consumer4` MTP spike ran the
executable directly, once, and observed correct results, §11) — a
reasonable extrapolation from the MSTest precedent (MTP's own default
template showed no additional evaluation beyond discovery, RESEARCH-0017
§20) but not independently re-proven here for NUnit specifically; a future
implementation pass should close this exact gap the same way
RESEARCH-0017's own follow-up closed its VSTest gap.

## 13. Seed and display-name semantics

`TestMethod.Name` (set directly in the spike via
`testCase.Name = "..."`) is the mechanism `Compono.NUnit` would use for
seed-bearing display names — confirmed working in `dotnet test --list-tests`
output (`ComposedMethod(Compono:v3)` appeared exactly as authored) and
during actual execution/reporting (`Mixed(Compono:v3)` appeared correctly
in the MTP host's own test listing, §9/§20). This is set once, inside
`BuildFrom`, at whatever point NUnit calls it (§12 — potentially more than
once across separate discovery/execution sessions under classic VSTest) —
the same seed-generation-timing caveat RESEARCH-0017 §12 already
established for MSTest (`[Compose(Seed = N)]` as the deterministic
reproduction mechanism; an unpinned seed may legitimately read differently
across two separately-invoked discovery/execution sessions, a real but
documentable consequence of §12's finding, not a defect). Not
independently re-derived beyond adapting RESEARCH-0017's already-settled
reasoning — no NUnit-specific seed/display-name behavior was found to
diverge from that established pattern.

## 14. Framework-owned special values

No NUnit-owned parameter injection mechanism was found that `[Compose]`
would need to special-case or defer to (matching RESEARCH-0017 §13's
identical MSTest finding). NUnit's own framework-context access
(`TestContext.CurrentContext`, `CancellationToken` via
`TestContext.CurrentContext.CancellationToken`) is a static/ambient
accessor, not a constructor- or parameter-injected value the way MSTest's
`TestContext` can be — so there is no equivalent "should `[Compose]` avoid
auto-injecting this" question to answer for NUnit at all; NUnit's own
idiomatic path (the static `TestContext.CurrentContext` accessor) already
exists independently of any test-method parameter, and `Compono.NUnit`
introduces no new special-parameter-rejection logic beyond what
`BindingPlan.ValidateSignature` already does (ref/out parameters, generic
methods — inherited, unchanged, per §9).

## 15. Async composition boundary — synchronous only, same as every other integration

`ITestBuilder.BuildFrom` returns `IEnumerable<TestMethod>` — a plain
synchronous iterator, no `Task`/`ValueTask` anywhere in the signature
(confirmed directly from the interface declaration, §4). This is the same
hard synchronous constraint RESEARCH-0017 §8 found for MSTest's
`ITestDataSource.GetData`, and per RESEARCH-0016's already-settled
principle, not a reason to reject the integration: async host/resource
setup belongs in NUnit's own lifecycle
(`[OneTimeSetUp]`/`[SetUp]`/`TestContext`), with the already-initialized
resource registered into Compono synchronously, matching every other
integration's documented boundary.

## 16. Disposal/lifetime ownership — no new ownership question

Applying RESEARCH-0015's already-settled non-owning stance directly:
`Compono.NUnit` must not, and by design would not, dispose composed
argument objects. NUnit exposes its own post-test lifecycle
(`[TearDown]`/`[OneTimeTearDown]`, `IDisposable` on the fixture class
itself) as the consumer's own available disposal seam — the same
boundary RESEARCH-0015/RESEARCH-0017 §12 already established generically
and for MSTest specifically. No NUnit-specific lifetime concern was found
during this research; NUnit's `TestMethod`/`Test` result objects
(built by `NUnitTestCaseBuilder`, §4) are NUnit's own, never composed
values themselves, and Compono's `CompositionRow`/composed graph is
entirely separate from anything NUnit owns or disposes.

## 17. Package dependency design

- **Minimum dependency: `NUnit` (the framework package) only** — the
  same package `NUnit3TestAdapter`/`Microsoft.NET.Test.Sdk` themselves
  depend on for the framework surface, and the only one containing
  `ITestBuilder`/`IMethodInfo`/`NUnitTestCaseBuilder` (confirmed directly:
  every type `Compono.NUnit` needs, per §4, is compiled into
  `nunit.framework.dll`, itself shipped by the `NUnit` NuGet package —
  unlike MSTest, there is no separate "umbrella vs. framework-only"
  package split to navigate here; NUnit ships one framework package,
  named simply `NUnit`, not an `NUnit.TestFramework`/`NUnit`-umbrella
  pair). No dependency on `NUnit3TestAdapter`, `Microsoft.Testing.Platform`
  packages, or `Microsoft.NET.Test.Sdk` — all three remain consumer/
  test-project runner concerns, exactly matching every other Compono
  integration package's existing minimal-dependency posture.
- **Version range:** per §5's proof, no upper-bound compatibility concern
  was found up to and including a 5.0.0 **prerelease**. A deliberately
  conservative but evidence-driven range is `NUnit >= 3.14.0, < 6.0.0` —
  floor justified below (§18), upper bound conservative (excludes an
  unreleased, still-beta major) rather than open-ended, consistent with
  "no accidental next-major previews" from the requester's own stated
  policy. This should be revisited once NUnit 5.0.0 actually ships stable
  (a trivial version-matrix re-run, not a redesign, per §5's own binary-
  compatibility proof already covering the beta).

## 18. Version matrix spike — summary (full detail in §5/§20)

| NUnit version | Assembly identity | Compiled against | Ran against | Result |
| --- | --- | --- | --- | --- |
| 3.14.0 | `3.14.0.0`, PKT `2638cd05610744eb` | ✓ (compile target) | ✓ | Pass (classic VSTest) |
| 4.6.1 | `4.6.0.0`, same PKT | — | ✓ (unmodified `ext3.dll`) | Pass (classic VSTest **and** MTP-exe) |
| 5.0.0-beta.1 | `5.0.0.0`, same PKT | — | ✓ (unmodified `ext3.dll`) | Pass (classic VSTest) |

No transitive package silently upgraded the framework version in any
consumer project (each consumer's own `project.assets.json`/build output
was checked directly against its declared `PackageReference` version, and
each `nunit.framework.dll` copied to `bin/` was independently re-verified
via `AssemblyName.GetAssemblyName` per the table in §5) — the same
discipline RESEARCH-0017 §17 required after the MSTest silent-upgrade
near-miss. All spike projects/DLLs were built entirely under the session's
scratch directory, never inside the `compono` repo tree, and were not
committed.

## 19. AutoFixture comparison — historical evidence, not a specification

Checked directly (GitHub issues, NuGet package pages, not recalled):

- AutoFixture ships **`AutoFixture.NUnit2`** and **`AutoFixture.NUnit3`**
  — **no `AutoFixture.NUnit4`/`AutoFixture.NUnit5`package exists**. The
  split is NUnit**2**→NUnit**3**, a genuine architectural rewrite
  (`NUnit.Core`/`nunit.core.interfaces`-based extensibility in NUnit 2 →
  attribute-based `ITestBuilder`/`IParameterDataSource` extensibility in
  NUnit 3, confirmed via `AutoFixture/AutoFixture` issue #246's own
  description of the `nunit.core.interfaces` dependency) — not a
  compatibility boundary anywhere near the NUnit3→NUnit4 (or 4→5)
  transition this research evaluated.
- `AutoFixture.NUnit3`'s own NuGet dependency range caps at `< 4.0.0`
  (confirmed directly from the package page) — but this is a **defensive
  NuGet range**, not proof of an actual incompatibility; nothing found in
  AutoFixture's own issue tracker documents a real NUnit4 binary-break
  discovery the way `Compono.MSTest`'s own research uncovered a real
  MSTest 3/4 break. §5's own binary spike (3.14.0-compiled code running
  correctly against 4.6.1/5.0.0-beta.1) directly supersedes this NuGet
  range as evidence — AutoFixture's cap reads as an unreviewed/unbumped
  range, not a documented compatibility finding, exactly the distinction
  the requester's prompt asked this research to draw.

**Direct answer to the requester's own question: no, the reason
AutoFixture split its NUnit integrations (a NUnit2→NUnit3 architectural
rewrite) does not apply to a new Compono package starting in 2026** —
Compono has no NUnit2-generation legacy to carry, and this research found
no NUnit3→4→5(-beta) equivalent of that rewrite. AutoFixture's package
history is evidence that *a* split was once warranted, for a specific,
non-recurring reason that has no counterpart in the version range
`Compono.NUnit` would actually target.

## 20. Spike performed and exact results (consolidated)

Two throwaway spike trees were built (scratch directory only, `nunit-spike/`,
never inside the `compono` repo tree; not committed, not left in the
repo):

**Tree 1 — `ext3` + three consumers (§5/§18):** a class library (`ext3`)
compiled once against `NUnit` 3.14.0, implementing
`ComposeAttribute : NUnitAttribute, ITestBuilder` per §4's chosen seam
(unwrap `IMethodInfo` → real `MethodInfo`, hand composed argument values
to `new NUnitTestCaseBuilder().BuildTestMethod(method, suite, new TestCaseParameters(args))`,
set a custom `TestMethod.Name`). Referenced via raw `HintPath` (never
recompiled) from `consumer3` (NUnit 3.14.0 + `NUnit3TestAdapter` 4.6.0,
classic VSTest), `consumer4` (NUnit 4.6.1 + `NUnit3TestAdapter` 6.2.0,
both classic `dotnet test` and MTP-executable mode), and `consumer5`
(NUnit 5.0.0-beta.1 + `NUnit3TestAdapter` 6.2.0, classic `dotnet test`).

- One real, non-obvious discovery-mechanics finding along the way:
  the `[Compose]`-only method was **not** discovered as a test at all
  (`dotnet test --list-tests` reported zero tests, matching a genuine
  "no tests found" adapter message) until an explicit `[TestFixture]`
  attribute was added to the containing class — confirmed by first
  reproducing the failure with `[TestFixture]` omitted, then fixing it by
  adding the attribute alone, no other change (§1).
- Complete-row coexistence (§6): a `[Compose] [TestCase(1, "x")]`-decorated
  method produced two independent listed test cases
  (`Mixed(Compono:v3)` and `Mixed(1,"x")`), not a merged row.
- Discovery/execution call-count (§12): a file-backed,
  `Environment.ProcessId`-tagged log in `BuildFrom` showed 1 call for a
  single combined `dotnet test`, and 2 calls (one per process) across a
  separately-invoked `--list-tests` then `test` pair — reproducing
  RESEARCH-0017's exact MSTest finding for NUnit's own classic-VSTest
  path.
- MTP-executable spike (`consumer4`, `EnableNUnitRunner=true`,
  `OutputType=Exe`): running the built executable directly produced
  `Microsoft.Testing.Platform v2.1.0` host output and **4 passed, 0
  failed** — confirming NUnit3TestAdapter 6.2.0's MTP path also discovers
  and executes the `[Compose]`/`ITestBuilder` extension correctly.

**Tree 2 — `asmcheck`:** a small console utility calling
`System.Reflection.AssemblyName.GetAssemblyName(path)` directly against
each installed NuGet package's `nunit.framework.dll`, producing the exact
version/public-key-token table in §5.

All spike trees were built entirely under the scratchpad directory; `git
status` in the `compono` repo shows no changes from this research beyond
this document itself.

## 21. Documentation/skill implications (not applied — description only)

If the Gate B evidence trigger this research cannot itself supply (§2) is
later satisfied and `Compono.NUnit` becomes a real roadmap item, the
following would need updating, per the "definition of done, not cleanup"
framing RESEARCH-0015/0016/0017 already established:

- `docs/roadmap/future-packages.md` — move `Compono.NUnit` from "admitted
  candidate" to a real roadmap item once Gate B is satisfied, citing this
  research.
- `docs/mvp.md`/root `README.md` package matrix — add `Compono.NUnit`
  alongside `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`.
- `skills/compono/SKILL.md` — a new `nunit.md` reference file, loaded only
  when `Compono.NUnit` is referenced/requested, matching the existing
  per-package scoping rule.
- A future `docs/adr/00NN-compono-nunit-package-design.md` — would need to
  record: the `ITestBuilder`+`IMethodInfo`-unwrap+`NUnitTestCaseBuilder`
  binding algorithm (§4/§9); the `[TestFixture]`-required-alongside-`[Compose]`
  documentation requirement (§1/§20); the double-`BuildFrom`-evaluation
  guidance under classic-VSTest discover-then-execute sessions (§12); the
  `NUnit >= 3.14.0, < 6.0.0` version range and its evidence (§17/§18); and
  the `Internal`-namespace dependency caveat (§4/§5) as an explicit,
  monitored risk (a version-matrix CI check, not just a one-time proof).
- Examples/evals — a `Compono.NUnit`-specific discriminating eval,
  mirroring the pattern RESEARCH-0014 established for `Share<T>()`.
- No existing documentation was found to be factually wrong about current
  NUnit support (there is no NUnit documentation yet to be wrong) — no
  out-of-band doc corrections were needed or made.

## 22. Options considered

**Option A — single broad package, `Compono.NUnit`, supporting NUnit 3+4+5.**
Directly supported by this research's own binary-compatibility proof
(§5/§18) — the same compiled binary already runs correctly across all
three majors, including the 5.0.0 beta. **This is the option the evidence
actually supports**, not merely the product's stated preference.

**Option B — single modern package, `Compono.NUnit` supporting NUnit 4+
only.** Would be the fallback if §5's binary-compat proof had failed for
3.x specifically — it did not. Choosing this anyway would forgo real,
currently-supported NUnit 3.14.0 adoption reach for no technical reason,
the same mistake RESEARCH-0017 warned against for MSTest's own original
"4.x only" recommendation before its own evidence-driven correction.
**Not recommended** — no evidence supports narrowing to 4+ specifically.

**Option C — version-specific packages
(`Compono.NUnit3`/`Compono.NUnit4`/`Compono.NUnit5`).** Explicitly not
supported by any evidence found in this research — §5's spike is a direct,
empirical refutation of the premise that would justify this option (real
binary/assembly incompatibility). **Rejected** — the AutoFixture
precedent this option would be modeled on doesn't even apply to this
version range (§19).

**Option D — defer NUnit.** Would require a genuine feasibility,
compatibility, runner, or AOT blocker. **None was found** (§2's Gate A
reconfirmation, §5's compatibility proof, §11's dual-runner spike
success) — the only real open item is Gate B evidence (§2), which is an
evidence-timing question, not a technical deferral reason. Not
recommended on technical grounds; if deferred, it should be for an
explicit product-sequencing reason (ADR-0039's own "no committed
sequence" heuristics), not a technical one.

## 23. Recommendation

**1. Should NUnit be admitted pre-1.0?** Gate A: yes, reconfirmed with
stronger, empirically-verified evidence than ADR-0039's original pass.
Gate B: no evidence found in this repo (§2) — remains an **admitted
candidate**, not a roadmap item, unless the user separately supplies an
explicit product-owner request as the Gate B trigger, the same mechanism
already used for `Compono.TUnit`/`Compono.TestDoubles`.

**2. Package name:** `Compono.NUnit` — one package, matching the product's
stated bias, directly supported (not merely permitted) by the evidence.

**3. Minimum supported NUnit version:** `3.14.0` — the current released
3.x version at the time of this research, proven binary-compatible with
newer majors (§5), and NUnit 3.x shows no equivalent of MSTest's
frozen-line finding (no evidence found that NUnit 3.x has stopped
receiving releases). A narrower floor would forgo real, current adoption
reach for no evidence-based reason.

**4. Maximum/major range:** `< 6.0.0` — conservative (excludes NUnit 5 by
default while it remains beta, per NuGet's own prerelease-exclusion
default), not because of any found incompatibility with 5.0.0-beta.1
(§5 found none) but because committing to an unreleased major's stable
API before it ships is the "no accidental next-major previews" policy the
requester named explicitly. Revisit trivially once 5.0.0 ships stable.

**5. Can ONE compiled package support the selected range?** Yes — proven,
not inferred (§5/§18): the identical compiled binary ran correctly against
3.14.0, 4.6.1, and 5.0.0-beta.1.

**6. Support MTP?** Yes — spike-confirmed working (§11), zero
`Compono.NUnit`-side cost (the difference is entirely in the consumer's
own project configuration).

**7. Support classic VSTest?** Yes — spike-confirmed working (§11), same
zero-cost reasoning; both runner modes share the identical
`Compono.NUnit` binary and behavior.

**8. Exact NUnit extension seam:**
`ComposeAttribute : NUnitAttribute, ITestBuilder`, unwrapping
`IMethodInfo`→`MethodInfo` for `BindingPlan.Build`, and delegating final
`TestMethod` construction to NUnit's own `NUnitTestCaseBuilder`/
`TestCaseParameters` (§4) — the smallest and only seam found capable of
producing one complete composed row per method, matching NUnit's own
`[TestCase]`/`[Combinatorial]` implementation pattern.

**9. Complete-row/coexistence semantics:** one `[Compose]`-family
attribute owns the entire row, exactly matching
`Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest` — confirmed by spike to
be NUnit's own natural `ITestBuilder` behavior (§6), not a restriction
Compono has to impose against NUnit's grain.

**10. Compono's source-generation/AOT posture:** `Compono.NUnit`'s own
integration code can be written reflection-free/trim-safe on the same
terms as the other three packages (§10) — framework-mandated `MethodInfo`
access only, no Compono-controlled reflection. **Whether NUnit test
execution itself is genuinely Native-AOT-runnable was not established by
this research** (§10) — an honest gap for the implementation phase to
close, not a blocker for admission (the same standard RESEARCH-0017 §7
applied to MSTest's own, better-documented AOT story).

**11. Is an ADR warranted next?** **Only once Gate B is satisfied.** Per
ADR-0039's own two-stage model (§2), an admitted candidate does not get
its own design ADR until real evidence exists — this research strengthens
the Gate A record (making a future ADR's technical sections easier to
write, should Gate B ever clear) but does not itself satisfy Gate B. If
the user wants to supply that trigger explicitly now (as was done for
`Compono.TUnit`/`Compono.TestDoubles`), that is the next decision, stated
directly to the user, not inferred by this research.

## Evidence index

- `docs/adr/0039-future-extension-package-admission-gate-and-release-sequence.md` —
  read directly for §2 (Gate A re-run baseline, original NUnit
  disposition).
- `docs/roadmap/future-packages.md`, `docs/roadmap/post-mvp.md` — read
  directly for §2 (current admitted-candidate/roadmap-item state, Gate B
  evidence record).
- `docs/research/0017-mstest-integration-viability-research.md` — read in
  full; structural/rigor template and the direct precedent for §1/§5/§6/§9/§12/§16/§17
  reasoning, explicitly re-verified against NUnit rather than assumed to
  transfer.
- [Towards NUnit 4](https://docs.nunit.org/articles/nunit/Towards-NUnit4.html)
  (fetched directly) — primary evidence for §3/§5's breaking-change scope.
- [NUnit and Microsoft.Testing.Platform](https://docs.nunit.org/articles/vs-test-adapter/NUnit-And-Microsoft-Test-Platform.html)
  (fetched directly) — primary evidence for §3/§11.
- [Custom Attributes](https://docs.nunit.org/articles/nunit/extending-nunit/Custom-Attributes.html),
  [ITestBuilder Interface](https://docs.nunit.org/articles/nunit/extending-nunit/ITestBuilder-Interface.html)
  (fetched directly) — primary evidence for §4.
- NuGet package pages for `NUnit` (3.14.0, 4.6.1, 5.0.0-beta.1),
  `NUnit3TestAdapter` 6.2.0, `AutoFixture.NUnit3` 4.18.1 — searched/fetched
  directly — primary evidence for §3/§17/§19.
- `AutoFixture/AutoFixture` GitHub issue #246 — primary evidence for §19's
  NUnit2→NUnit3 architectural-rewrite finding.
- Direct binary inspection: `AssemblyName.GetAssemblyName` run against
  each installed `nunit.framework.dll` (3.14.0/4.6.1/5.0.0-beta.1) —
  primary evidence for §5's assembly-identity table.
- A throwaway two-tree spike (`ext3` + three consumers; `asmcheck`) built,
  run, and left only in the scratchpad directory — primary evidence for
  §1/§4/§5/§6/§9/§11/§12/§18/§20. Never touched the `compono` repo's
  `src/`/`test/` trees; not committed.
- Repo-wide search across `/Users/ncipollina/source/repos/layered-craft/`
  and `/Users/ncipollina/source/repos/ncipollina/` (default checkouts and
  all local/remote branch names) — primary evidence for §2's Gate B
  "no evidence found" finding.

## Links

- Reconfirms and strengthens ADR-0039's original `Compono.NUnit` Gate A
  disposition with primary-source, spike-verified evidence — does not
  supersede it.
- Carries forward RESEARCH-0015 (§16) and RESEARCH-0016 (§15) without
  reopening either.
- Directly extends RESEARCH-0017's own closing note ("should inform a
  later NUnit-integration-viability research pass... §6's binding-reuse
  finding and §9's independent-row model are both plausibly NUnit-relevant
  questions worth re-verifying independently") — both were re-verified
  here (§6/§9) and confirmed to hold for NUnit's own `ITestBuilder` model,
  for NUnit-specific reasons, not by assumption.
- Would feed a future `Compono.NUnit` package-design ADR **once Gate B is
  independently satisfied** (§2/§23.11) — no ADR drafted by this research.
