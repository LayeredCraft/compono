# [ADR-0059] Compono.NUnit Package Design

**Status:** Accepted

**Date:** 2026-09-02 (proposed); accepted 2026-09-03

**Decision Makers:** Nick Cipollina (Gate A/Gate B admission and roadmap
commitment decision, 2026-09-02; product-owner acceptance of the full
package design, 2026-09-03), Claude (research synthesis and proposed
package design, 2026-09-02; pre-acceptance corrections from two
adversarial review rounds — the `TestAttribute`-based seam replacing
`NUnitAttribute`, the narrowed `[3.14.0, 5.0.0)` range, and the `new`
`BuildFrom` declaration — 2026-09-03)

## Context

[RESEARCH-0018](../research/0018-nunit-integration-viability-research.md)
reconfirmed ADR-0039's Gate A disposition for `Compono.NUnit` with
stronger, empirically-verified evidence (§2) and closed Gate B honestly:
no dogfooding/demand evidence exists anywhere in this repo's history
(RESEARCH-0018 §2). The user has now supplied an explicit,
product-owner-level request to add `Compono.NUnit` before 1.0 — the same
Gate B mechanism that already gated `Compono.TUnit` and Compono-owned
source-generated test doubles into roadmap items (`docs/roadmap/future-packages.md`'s
"No committed sequence" section). **Gate B is satisfied by explicit
product-owner request (2026-09-02), per RESEARCH-0018** — not by
dogfooding evidence, which RESEARCH-0018 §2 explicitly found does not
exist and this ADR does not pretend otherwise.

**Result: `Compono.NUnit` is a committed pre-1.0 roadmap item.**

This is the fourth package-design ADR in this family, after
[ADR-0022](0022-compono-xunit-package-design.md) (`Compono.XunitV3`),
[ADR-0040](0040-compono-tunit-package-design.md) (`Compono.TUnit`), and
[ADR-0057](0057-compono-mstest-package-design.md) (`Compono.MSTest`,
including its Amendment 1 binary-incompatibility finding — a precedent
this ADR treats as a live methodological warning, not a foregone
conclusion for NUnit). It follows their pattern directly: reuse
[ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
`Composer.CreateRow`/`CompositionRow` unchanged, reuse
[ADR-0041](0041-aot-safe-row-binding-dispatch.md)'s `RowInvokerRegistry`,
adapt the established `BindingPlan`/`RowInvokers` binding pattern
package-locally (not a shared core type), and produce idiomatic NUnit
wearing idiomatic Compono.

The target consumer experience (revised — see the Amendment note below):

```csharp
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

with the same `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`
family, `[Shared]`, `CompositionBuilder.Share<T>()` (core, no
`Compono.NUnit`-specific work needed), exact registrations, constructor
selection, and every existing integration package
(`Compono.TestDoubles`, `Compono.NSubstitute`, `Compono.Bogus`,
`Compono.Logging`, `Compono.DependencyInjection`, `Compono.Http`) working
unmodified, exactly as they do for the other three framework packages
today.

**Amendment (pre-acceptance, 2026-09-03): `[TestFixture]` is not
required.** A pre-acceptance adversarial review
(`.review/adr-0058-plan-0059-pi-codex-review.md`) found that
`ComposeAttribute : TestAttribute, ITestBuilder` (not `NUnitAttribute,
ITestBuilder`, the seam this ADR originally proposed) discovers and runs
`[Compose]`-only methods with no `[TestFixture]` needed at all. This was
independently reproduced in a second, fresh scratch spike (not merely
copied from the review) across NUnit 3.14.0/4.6.1/5.0.0-beta.1, classic
VSTest, and MTP-executable mode — see the revised §4/§5/§7 below for the
seam, the evidence, and why this changes the consumer contract. This is
a real design change, not a cosmetic one: the original `NUnitAttribute`-
based seam and its permanent `[TestFixture]` requirement are now a
**rejected alternative** (§18), not the chosen design.

RESEARCH-0018's evidence base for this ADR (cited by section throughout,
amended in §4/§5/§7/§8 per the pre-acceptance review above):
`ITestBuilder` is NUnit's correct, current, first-party whole-row
extension point (§4, spike-confirmed); the single most important
finding of this research is that one compiled `Compono.NUnit.dll`,
built against NUnit 3.14.0, ran correctly and unmodified against
NUnit 4.6.1 and NUnit 5.0.0-beta.1 alike — no binary/assembly-identity
break, the opposite of the MSTest 3.x/4.x precedent (§5/§18, proven by
spike, not inferred); `[Compose]` and NUnit's own `[TestCase]` produce
independent test cases, never a merged row (§8, spike-confirmed, and
re-confirmed under the revised seam); `[Compose]` alongside `[Values]`/
`[Range]` produces additional, independently-executing rows of its own
— genuinely additional, not "unused" as an earlier pre-acceptance draft
assumed — never merged into the Compose row (§8, corrected by the
pre-acceptance spike); no NUnit-specific obstacle to the generic
`Compose<TProfile>`/`Compose<TProfile, TConfig>` family exists (§7,
reasoned from precedent, not independently spiked); the existing
`ComposeMethodDiscovery` generator pipeline needs only a metadata-name
registration, no NUnit-specific generator (§10); `BindingPlan` is reused
package-locally with a thin `IMethodInfo`→`MethodInfo` unwrap, no shared
cross-framework binding layer (§9); both MTP and classic VSTest work,
spike-confirmed under the revised seam too, zero package-level cost
either way (§11); composition may run more than once across
separately-invoked discovery/execution sessions under classic VSTest,
matching the MSTest precedent exactly (§12, spike-confirmed; MTP not
independently re-verified — an explicit implementation-phase gap, not an
assumption); `Compono.NUnit`'s own code can be reflection-free/trim-safe
on the same terms as the other three packages, but NUnit's own
Native-AOT runnability was not established (§17) — a real, narrower
claim this ADR preserves precisely.

## Decision Drivers

- `design-decisions.md` rule 3 — core `Compono` must never know
  `Compono.NUnit` exists; every mechanism this ADR uses is already public
  (`Composer.CreateRow`, `CompositionRow.Resolve`/`ResolveShared`/
  `ShareExplicit`, `RowInvokerRegistry`).
- The MSTest precedent's exact methodological lesson (ADR-0057 Amendment
  1): apparent API-surface compatibility across a framework's own major
  versions does not guarantee binary/assembly-identity compatibility —
  every version-floor and one-package claim in this ADR is grounded in
  RESEARCH-0018's real compiled-binary spike (§5/§18), not source-level
  reasoning alone.
- ADR-0001's no-reflection-by-default posture — `IMethodInfo`/`MethodInfo`
  access is framework-required metadata, never a fallback composition
  engine; `RowInvokerRegistry` dispatch stays reflection-free.
- Compono's existing non-ownership/disposal stance
  ([RESEARCH-0015](../research/0015-disposal-ownership-research.md)) and
  synchronous-only composition posture
  ([RESEARCH-0016](../research/0016-async-composition-viability-research.md))
  — neither is reopened by this ADR.
- Minimal, intentional dependency footprint — `Compono.NUnit` must not
  pull a runner/adapter package into a consumer's dependency graph merely
  for convenience.
- The product's explicit stated bias: one `Compono.NUnit` package, not an
  AutoFixture-style `Compono.NUnit3`/`Compono.NUnit4`/`Compono.NUnit5`
  split — but only insofar as the evidence actually supports it (it does,
  §5/§18; RESEARCH-0018's own framing put the burden of proof on finding
  a real reason to split, not on justifying a single package).
- Honest, precise scoping of what was actually proven vs. reasoned by
  extension or left open — the same discipline RESEARCH-0017/ADR-0057
  established and RESEARCH-0018 explicitly carried forward.

## Considered Options

**Integration seam:**
1. `TestAttribute`-based `ComposeAttribute : TestAttribute, ITestBuilder`
   (chosen — revised pre-acceptance, see the Amendment note above).
2. `NUnitAttribute`-based `ComposeAttribute : NUnitAttribute, ITestBuilder`
   (this ADR's original proposal; rejected — requires `[TestFixture]` and
   creates a silent-zero-tests footgun, §18).
3. `IParameterDataSource`-implementing `[Compose]` (per-parameter).
4. `ISimpleTestBuilder`-implementing `[Compose]`.
5. `IFixtureBuilder`-based fixture-level composition.

**Package/version-range shape:**
A. One `Compono.NUnit` package, `NUnit >= 3.14.0, < 5.0.0` (chosen —
   narrowed pre-acceptance from this ADR's original `< 6.0.0`, §3).
B. One `Compono.NUnit` package, NUnit 4+ floor only.
C. Version-specific packages (`Compono.NUnit3`/`Compono.NUnit4`/`Compono.NUnit5`).

**`[TestFixture]` handling:**
1. No `[TestFixture]` requirement at all — `[Compose]` alone makes the
   method discoverable via the `TestAttribute`-based seam (chosen —
   revised pre-acceptance; a consumer MAY still add `[TestFixture]` for
   their own unrelated NUnit reasons, but `Compono.NUnit` does not
   require it, §7).
2. Document `[TestFixture]` as a required consumer-supplied attribute
   (this ADR's original proposal, tied to option 2 above; rejected).
3. Have `Compono.NUnit` attempt to auto-register the containing class as
   a fixture.

**`[Compose]` + `[TestCase]`/`[Values]`/etc. coexistence:**
1. Independent, non-merging rows — document the boundary (chosen; the
   pre-acceptance spike additionally found `[Values]`/`[Range]` produce
   their *own* independent rows too, not merged and not unused, §8).
2. Build custom machinery to merge partial rows.

## Decision Outcome

**Chosen: Option 1 in every case above** (the `TestAttribute`-based seam,
the narrowed `< 5.0.0` range, no `[TestFixture]` requirement, and
independent-row coexistence).

### 1. Admission — Gate A and Gate B

**Gate A: satisfied**, reconfirmed with stronger evidence than ADR-0039's
original pass (RESEARCH-0018 §2) — `Compono-specific value`, `native
ecosystem fit`, `meaningful abstraction`, `architectural fit`, and
`package-boundary justification` all independently re-verified against
current (2026) NUnit extension-point evidence, not merely re-asserted
from ADR-0039's own text.

**Gate B: satisfied by explicit product-owner request (2026-09-02), per
RESEARCH-0018.** RESEARCH-0018 §2 found zero dogfooding evidence and zero
existing NUnit consumer anywhere in this repo's history — that finding is
carried forward unedited, not manufactured or backdated. The trigger for
this ADR is the user's explicit, current instruction to add
`Compono.NUnit` before 1.0, the same mechanism (not dogfooding) that
already gated `Compono.TUnit` and Compono-owned source-generated test
doubles into roadmap items.

**Result: `Compono.NUnit` is a committed pre-1.0 roadmap item.** That
roadmap commitment (Gate A + Gate B) was settled by the product-owner
decision above. The detailed package design below — the seam, dependency
range, `[TestFixture]` handling, and every other §1–§18 decision —
underwent two rounds of pre-acceptance adversarial review (2026-09-03,
recorded inline throughout) before the product owner accepted the full
design on 2026-09-03, per this repo's normal ADR lifecycle
(`design-decisions.md`).

### 2. Package: one `Compono.NUnit`, not a version-specific split

**Chosen because the evidence proves it, not merely because it matches
product preference.** RESEARCH-0018 §5/§18's central empirical result: a
throwaway `Compono.NUnit`-shaped extension assembly, compiled once
against NUnit 3.14.0 and never recompiled, was loaded and ran correctly
— across real `dotnet test`/MTP executions — against NUnit 4.6.1 and
NUnit 5.0.0-beta.1 unmodified. All three majors share the same strong-name
public key token (`2638cd05610744eb`); only the `AssemblyVersion`
differs (`3.14.0.0`/`4.6.0.0`/`5.0.0.0`). This is the **opposite** of the
MSTest 3.x/4.x finding (ADR-0057 Amendment 1: different assembly names,
no forwarder, a hard `FileNotFoundException`) — no equivalent break was
found for NUnit at any of the three tested majors.

**Options B (4+-floor-only) and C (version-specific packages) are
rejected**: no evidence supports narrowing away from 3.14.0 (Option B),
and Option C's premise — real binary/assembly incompatibility — is
directly disproven by the spike (RESEARCH-0018 §5/§22). The AutoFixture
precedent this option would be modeled on (`AutoFixture.NUnit2`/
`AutoFixture.NUnit3`) doesn't even apply to this version range — that
split was a genuine NUnit2→NUnit3 architectural rewrite
(`nunit.core.interfaces`-based → attribute-based extensibility),
independently confirmed via AutoFixture's own issue tracker
(RESEARCH-0018 §19), with no NUnit3→4→5 counterpart.

### 3. Dependency and version range

**Dependency: `NUnit` (the framework package) only** — confirmed
directly (RESEARCH-0018 §17): every type `Compono.NUnit` needs
(`ITestBuilder`, `IMethodInfo`, `NUnitTestCaseBuilder`) compiles into
`nunit.framework.dll`, itself shipped by the single `NUnit` package.
Unlike MSTest, there is no umbrella-vs-framework-only split to navigate —
NUnit ships one framework package. No dependency on `NUnit3TestAdapter`,
any `Microsoft.Testing.Platform` package, or `Microsoft.NET.Test.Sdk` —
all three remain consumer/test-project runner concerns.

**Version range: `NUnit >= 3.14.0, < 5.0.0`** (narrowed pre-acceptance
from this ADR's original `< 6.0.0` — see the pre-acceptance review's HIGH
finding).

- **Floor `3.14.0`**: the current released 3.x version at the time of
  this research (RESEARCH-0018 §3), proven binary-compatible with newer
  majors (§5), with no evidence found that the NUnit 3.x line has stopped
  receiving releases — unlike MSTest's frozen-3.x finding. A narrower
  floor would forgo real, current adoption reach for no evidence-based
  reason.
- **Upper bound `< 5.0.0`**: **NUnit 5 is prerelease only
  (`5.0.0-beta.1`) as of this ADR** (RESEARCH-0018 §3) — the product does
  not promise a support contract for a stable major that does not exist
  yet. `Compono.NUnit` accepts an `Internal`-namespace dependency (§6);
  because of that, the declared NuGet range is a real support promise,
  not just a restore hint, and a range that already included stable
  NUnit 5 the moment it ships would extend that promise to a major this
  ADR has only tested as a beta. RESEARCH-0018's and the pre-acceptance
  spike's `5.0.0-beta.1` compatibility results are recorded as valuable
  **forward-compatibility surveillance evidence** — strong signal that a
  future `< 6.0.0` widening is likely safe — not as a current support
  claim. NUnit 3.14.0 through current stable 4.x are genuinely released
  and are what this range actually promises. **Once NUnit 5.0.0 ships
  stable, rerun the compatibility matrix (PLAN-0059) and, if it passes,
  amend this ADR's range to `< 6.0.0`** — a version-matrix re-run, not a
  redesign.

### 4. Public API shape

Same family as every other Compono framework package, no NUnit-specific
profile abstraction:

```csharp
namespace Compono.NUnit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : TestAttribute, ITestBuilder
{
    public ComposeAttribute(params object?[] inlineValues);

    public int Seed { get; set; }

    public new IEnumerable<TestMethod> BuildFrom(IMethodInfo method, Test? suite);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile> : ComposeAttribute
    where TProfile : ICompositionProfile, new()
{
    public ComposeAttribute(params object?[] inlineValues) : base(inlineValues) { }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile, TConfig> : ComposeAttribute
    where TProfile : ICompositionProfile
{
    public ComposeAttribute(params object?[] configArguments) : base();

    internal override void ApplyProfile(CompositionBuilder builder);
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SharedAttribute : Attribute;
```

`ComposeAttribute` derives from `TestAttribute` (NUnit's own native
test-identifying attribute — the same base `[Test]` itself derives from)
and implements `ITestBuilder` directly — the smallest seam found capable
of both making `[Compose]`-decorated methods independently discoverable
by NUnit *and* owning one complete composed row per method (§5/§7
below). `TestAttribute` itself carries a public (implicit-interface)
`ITestBuilder.BuildFrom` implementation, so `ComposeAttribute.BuildFrom`
is declared **`public new`** — an explicit, intentional member hiding,
not an accidental one the compiler happens to tolerate. A pre-acceptance
spike (§5) built and compared both declarations (with and without `new`)
against a real NUnit runner: `new` changes nothing observable — the
`ITestBuilder` interface map resolves to `ComposeAttribute.BuildFrom` in
both cases (interface dispatch is governed by the most-derived type's
matching member regardless of `new`), and real NUnit discovery/execution
produced identical results either way (exactly one row per `[Compose]`
method, no `[TestFixture]` needed, no leftover empty/default test case
from `TestAttribute`'s own base behavior). The only difference is that
`new` eliminates `CS0108` at compile time; the declaration without `new`
compiles with that warning present. `Compono.NUnit`'s production source
uses `new` so the intentional hiding is explicit and the package builds
warning-free — see §5 for the full spike evidence. One `[Compose]`-family
attribute owns the entire row: `AllowMultiple = false` on each, plus an
explicit "more than one Compose-family attribute" check in
`BindingPlan.ValidateSignature`, matching every other package. Profiles
are specified via the generic attribute type argument only — no
attribute stacking or ordering question.

### 5. Extension seam and binding algorithm

**`ComposeAttribute : TestAttribute, ITestBuilder`** (revised
pre-acceptance from `NUnitAttribute, ITestBuilder`; RESEARCH-0018 §4/§9
plus the pre-acceptance spike, §4 above). Algorithm:

1. NUnit calls `BuildFrom(IMethodInfo method, Test? suite)`.
2. `Compono.NUnit` unwraps `IMethodInfo.MethodInfo` to the underlying
   real `System.Reflection.MethodInfo` — `IMethodInfo`/`IParameterInfo`
   are NUnit's own metadata-wrapper types, a genuine divergence from
   xUnit v3/MSTest's direct `MethodInfo` handoff, confirmed by spike
   (§4).
3. The existing package-local `BindingPlan`/`ParameterBindingPlan`/
   `RowInvokers` machinery — reused unchanged from the established
   pattern, not a new binding architecture — builds one `CompositionRow`
   per `BuildFrom` invocation via `composer.CreateRow(...)`, resolving
   every parameter through `RowInvokerRegistry`-backed dispatch.
4. `NUnit.Framework.Internal.Builders.NUnitTestCaseBuilder`/
   `TestCaseParameters` (NUnit's own `Internal` namespace — §6 below)
   construct the final `TestMethod` NUnit expects back, with `Name` set
   to the seed-bearing display string.

**No shared cross-framework binding layer is extracted.** RESEARCH-0018
§9 explicitly found the `IMethodInfo` wrapper a real, non-superficial
surface difference from `MethodInfo`-shaped packages (xUnit v3/MSTest)
and `DataGeneratorMetadata`-shaped input (TUnit) — three packages already
show binding *inputs* diverge more than converge across frameworks, and
NUnit is a fourth independent data point showing the same. Row-binding
logic (`BindingPlan`/`ParameterBindingPlan`/`PositionalArgumentBinder`/
`ConfigProfileBinder`) is duplicated a fourth time, package-locally, per
the same reasoning ADR-0040/ADR-0057 already applied.

### 6. `Internal`-namespace dependency — accepted, monitored architectural risk

**Precise statement, corrected pre-acceptance:** `NUnitTestCaseBuilder`
and `TestCaseParameters` are **public CLR types** — accessible,
constructible, and callable at compile time, confirmed directly by the
spike successfully referencing and calling both — living in NUnit's
`NUnit.Framework.Internal.Builders`/`NUnit.Framework.Internal`
*namespaces*. The risk is not compile-time accessibility (an earlier
pre-acceptance draft of this ADR incorrectly stated "`NUnitTestCaseBuilder`/
`TestCaseParameters` are not [public]," conflating namespace naming with
CLR visibility — corrected here). **The real risk is that NUnit does not
present these `Internal`-namespaced types as a supported, stable
extensibility contract**, unlike `ITestBuilder` itself, which is public
API in every sense NUnit documents. No publicly-documented alternative
was found for producing a fully-formed, multi-parameter `TestMethod` from
an `ITestBuilder` (RESEARCH-0018 §4) — this is the same pattern NUnit's
own built-in `[TestCase]`/`[Values]`/`[Combinatorial]` attributes are
implemented on internally, not an unsupported hack, but genuinely not a
stable-contract-guaranteed public surface either.

**Accepted explicitly, not engineered around**: no reflection, dynamic
invocation, or wrapper abstraction is introduced to hide this dependency
— that would trade a small, well-understood risk for real complexity
with no compatibility benefit (an `Internal`-namespace type can change
regardless of how indirectly it's called). The dependency is empirically
stable across three real NUnit majors, including a prerelease
(RESEARCH-0018 §5/§18), which is strong evidence, not a permanent
guarantee. **This requires permanent CI compatibility-matrix coverage
(§8 below), not a one-time proof** — the implementation plan must wire
this in from the start so a future NUnit change that breaks
`NUnitTestCaseBuilder`/`TestCaseParameters` is caught by CI, not by a
consumer.

### 7. `[TestFixture]` — not required, corrected pre-acceptance

**This ADR originally required an explicit `[TestFixture]` attribute
alongside `[Compose]`, because the `NUnitAttribute, ITestBuilder`-based
seam it proposed does not itself make the containing class discoverable
— NUnit silently discovered zero tests without one (RESEARCH-0018
§1/§20).** A pre-acceptance adversarial review found a smaller, better
seam (§4/§5): deriving `ComposeAttribute` from NUnit's own `TestAttribute`
instead. Because `TestAttribute` is NUnit's native test-identifying
marker, `[Compose]` alone makes its method discoverable — no
`[TestFixture]` needed, and no silent-zero-tests trap at all.

**Independently spike-verified** (scratchpad, `nunit-spike-2/`, not the
review's own spike — a second, fresh reproduction), across NUnit
3.14.0/4.6.1/5.0.0-beta.1 and both classic VSTest and MTP-executable
mode:

- A class with **no** `[TestFixture]` and a `[Compose]`-only method is
  discovered and runs correctly (`ComposedMethod(Compono:TestAttribute)`
  appears and passes).
- **No duplicate test case** results from `TestAttribute`'s own default
  `ITestBuilder` behavior — exactly one row per `[Compose]` method, not
  an extra empty/default case alongside it (verified by exact discovered-
  test-count and name, §4/§5's C# interface-resolution explanation for
  why).
- `[Compose]` + `[TestCase]` still produces two independent rows.
- Identical behavior confirmed at all three NUnit majors and both
  runners — no version- or runner-specific divergence found.

**`Compono.NUnit` does NOT auto-convert a non-fixture class into an
NUnit fixture** — this is now moot as a design question, since no
fixture-level marker is needed at all. A consumer **may** still add
`[TestFixture]` to a class for their own unrelated NUnit reasons (e.g. a
class that mixes `[Compose]` methods with ordinary parameterized fixture
construction) — `Compono.NUnit` neither requires nor forbids it.

**Documentation implication**: the target consumer shape (Context above)
no longer needs `[TestFixture]` ceremony at all — `Compono.NUnit` now has
**no** NUnit-specific attribute-count divergence from
`Compono.XunitV3`/`Compono.TUnit`. The implementation plan must include
a regression-locked test proving `[Compose]` alone works without
`[TestFixture]`, plus a duplicate-test-case regression guard (protecting
the finding above, not re-discovering it).

### 8. Complete-row semantics and coexistence

**One `[Compose]`-family attribute owns the entire row — every
coexisting NUnit data source stays independent, never merged.**
Spike-confirmed (RESEARCH-0018 §6, re-confirmed under the revised
`TestAttribute`-based seam by the pre-acceptance spike): `[Compose]` +
NUnit's own `[TestCase]` on one method produce two independent test
cases, never a merged row — a structural property of NUnit's
`ITestBuilder` model (every `ITestBuilder`-implementing attribute
independently contributes its own complete `TestMethod`(s)), not a
restriction Compono imposes against NUnit's grain. This settles the
product's own "one Compose source owns one complete row" bias for free,
exactly matching `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`.

**`[Compose]` + `[Values]`/`[Range]` — now settled by direct spike,
correcting an earlier assumption.** This ADR's original text (and the
pre-acceptance review's own claim) expected `[Compose]`'s presence alone
to suppress parameter-level `IParameterDataSource` sources entirely (the
sources "go unused"). **An independent pre-acceptance spike found this
expectation wrong**: under the `TestAttribute`-based seam, `[Values(7, 8,
9)]`/`[Range(1, 3)]` on a `[Compose]` method's parameter produce their
**own additional, independently-executing test cases** — real rows,
individually run, individually pass/fail on their own literal parameter
values (`WithValues(7)`/`WithValues(8)`/`WithValues(9)`,
`WithRange(1)`/`WithRange(2)`/`WithRange(3)`, verified alongside a single
`WithValues(Compono:TestAttribute)`/`WithRange(Compono:TestAttribute)`
Compose row) — confirmed identically at NUnit 3.14.0, 4.6.1, and
5.0.0-beta.1, and under both classic VSTest and MTP. This is NUnit's own
default per-method, per-parameter-data-source expansion (a `TestAttribute`
behavior; not triggered by the rejected `NUnitAttribute`-based seam,
which is why the two seams were never expected to behave identically
here). **The product invariant still holds — no row is ever merged**:
the Compose row remains fully composed and independent; `[Values]`/
`[Range]` rows are NUnit's own ordinary parameterized cases, entirely
separate from and untouched by `[Compose]`. What changes is only that
the earlier "the sources go unused" framing was inaccurate — they are
used, by NUnit itself, independently. `Compono.NUnit`'s documentation
must state this plainly: combining `[Compose]` with `[Values]`/`[Range]`/
other parameter-level sources on the same method yields *more* test
cases than just the Compose row, not fewer. A custom
`IParameterDataSource` was not independently spiked (only the two
built-in attributes were) — a narrow, explicitly-flagged remaining gap
for the implementation plan's regression suite to close, not expected to
differ in kind from `[Values]`/`[Range]`'s own confirmed behavior.

### 9. Binding architecture — package-local, no shared extraction

Covered in §5 above; restated here per this ADR's required scope. The
only genuinely new, NUnit-specific code is the attribute itself, the
`IMethodInfo`/`IParameterInfo` unwrap step, and the
`NUnitTestCaseBuilder`/`TestCaseParameters` call. `RowInvokerRegistry`/
`CompositionRow`/`BindingPlan.Build`'s core logic are reused unchanged.

### 10. Generator discovery

`Compono.Generators`' existing `ComposeMethodDiscovery` pipeline
discovers Compose-family methods by attribute name/metadata registration
— the same mechanism already proven for `Compono.XunitV3`/`Compono.TUnit`/
`Compono.MSTest` (RESEARCH-0018 §8). **Required design**: three new
metadata-name constants and three new `SyntaxValueProvider
.ForAttributeWithMetadataName` registrations in
`ComponoIncrementalGenerator.cs`, for `Compono.NUnit.ComposeAttribute`/
`` `1``/`` `2``, feeding the same, unforked
`ComposeMethodDiscovery.TransformMethod`. No NUnit-specific composition
generator is introduced.

### 11. Runner support: both MTP and classic VSTest

`Compono.NUnit` supports NUnit under **both** currently-supported
execution platforms — spike-confirmed working correctly under both
classic `dotnet test`/VSTest (via `NUnit3TestAdapter`) and MTP-executable
mode (`<EnableNUnitRunner>true</EnableNUnitRunner>` +
`<OutputType>Exe</OutputType>`) against all three tested NUnit majors
(RESEARCH-0018 §11). Zero package-level cost either way — the difference
is entirely in the consumer's own project configuration.
`Compono.NUnit` stays runner-neutral: no dependency on
`NUnit3TestAdapter`, any MTP package, or `Microsoft.NET.Test.Sdk`.

### 12. Discovery/execution lifecycle

**Composition may run more than once for what appears to be one eventual
test case.** Spike-confirmed under classic VSTest (RESEARCH-0018 §12,
matching the MSTest precedent exactly): a single combined `dotnet test`
invocation produces exactly one `BuildFrom` call per method; a
separately-invoked `dotnet test --list-tests` followed by a later,
separate `dotnet test` produces two `BuildFrom` calls, one per process.
Same non-guarantee established for `Compono.MSTest` applies here: no
cross-session caching, no static graph state, `Register<T>()` factories/
`ICompositionValueProvider`s may run more than once, `[Shared]`/
`Share<T>()` remain correct *within* each independently-built row.

**MTP-specific re-verification of this exact question is an explicit
open item, not assumed to transfer** — RESEARCH-0018's MTP spike ran the
executable directly, once, and observed correct results, but did not
independently re-test the discovery-then-separate-execution double-
invocation question under MTP the way it did for classic VSTest. The
implementation plan (§14 below) must close this gap directly, the same
way RESEARCH-0017's own follow-up closed its equivalent VSTest gap for
MSTest.

### 13. Seed and display-name semantics

`TestMethod.Name` is set inside `BuildFrom` to a seed-bearing display
string (matching `Compono.MSTest`'s `GetDisplayName` pattern in effect,
though NUnit's own mechanism is different — the name is set directly on
the constructed `TestMethod`, not returned through a separate hook).
`[Compose(Seed = N)]` is the deterministic reproduction mechanism; an
unpinned `[Compose]` generates a fresh seed per `BuildFrom` invocation,
so a discovery-time row and a later, separately-invoked execution-time
row may legitimately hold different composed values — the same
unpinned/pinned distinction ADR-0057 Amendment 3 already established for
MSTest, carried forward here rather than re-derived, since RESEARCH-0018
§13 found no NUnit-specific divergence from that already-settled
reasoning.

### 14. Async composition — synchronous only

`ITestBuilder.BuildFrom` returns a plain `IEnumerable<TestMethod>` — no
`Task`/`ValueTask` anywhere in the signature (RESEARCH-0018 §15). Per
[RESEARCH-0016](../research/0016-async-composition-viability-research.md)'s
already-settled principle, this is not a reason to reject the
integration: async resource setup belongs in NUnit's own lifecycle
(`[OneTimeSetUp]`/`[SetUp]`, `TestContext`), with the already-initialized
resource registered into Compono synchronously. This ADR does not invent
an async composition mechanism.

### 15. Disposal/lifetime — non-owning, no new ownership question

Applying [RESEARCH-0015](../research/0015-disposal-ownership-research.md)'s
already-settled non-owning stance directly: `Compono.NUnit` does not, and
by design would not, dispose composed argument objects. NUnit's own
post-test lifecycle (`[TearDown]`/`[OneTimeTearDown]`, `IDisposable` on
the fixture class) remains the consumer's own available disposal seam.
No NUnit-specific lifetime concern was found (RESEARCH-0018 §16).

### 16. Framework-owned context

`Compono.NUnit` does not auto-inject `TestContext.CurrentContext` or any
other NUnit-owned value as a composed parameter. NUnit's own context
access is a static/ambient accessor, not a constructor- or parameter-
injected value the way MSTest's `TestContext` can be — there is no
equivalent "should `[Compose]` special-case this" question to answer at
all (RESEARCH-0018 §14). `BindingPlan.ValidateSignature`'s existing
signature-rejection rules (generic methods, `ref`/`out`/`in`/`params`)
apply unchanged.

### 17. Reflection/AOT/source-generation posture

`Compono.NUnit` preserves Compono's reflection-free/source-generated
composition architecture on the same terms as the other three packages
— a **precise, two-part claim**, not a single blanket one:

- **`Compono.NUnit`'s own integration code can be written reflection-
  free/trim-safe.** NUnit hands `Compono.NUnit` an `IMethodInfo`
  wrapping a real `MethodInfo` — framework-mandated metadata access,
  identical in kind to what `Compono.XunitV3`/`Compono.MSTest` already
  treat as AOT-safe input. No `MakeGenericType`/`Activator.CreateInstance`/
  dynamic generic instantiation is needed; `RowInvokerRegistry.TryGet`
  is reused unchanged. `NUnitTestCaseBuilder`/`TestCaseParameters` calls
  are ordinary, non-generic constructor/method calls — not reflection
  themselves, though their trim-safety under a real trimmed/published
  binary was **not** independently verified by RESEARCH-0018 (§10) and
  must be proven by the implementation plan's `AotSmokeTest`, not
  assumed. **`ComposeAttribute<TProfile, TConfig>`'s constructor-
  reflection flow requires `[DynamicallyAccessedMembers(PublicConstructors)]`
  (or the equivalent established pattern) from the first implementation,
  not as something discovered only if the `AotSmokeTest` fails.**
  `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`'s own
  `ConfigProfileBinder` `Type`-flow already requires the identical
  annotation (ADR-0041 Amendments 4-5) — `Compono.NUnit` repeats the same
  known-correct constructor-reflection shape, so the plan must implement
  it correctly on day one and *then* verify with a real trim/Native-AOT
  proof, not implement unannotated and wait for the proof to rediscover
  a gap this repo has already found three times.
- **NUnit's own Native-AOT runnability was NOT established by
  RESEARCH-0018, and this ADR does not claim it.** No NUnit
  documentation, release note, or blog post found during that research
  states `nunit.framework`/the NUnit console runner/the VSTest or MTP
  adapters are published or validated for Native AOT execution — in
  contrast to xUnit v3 4.0's documented Native AOT support and MSTest's
  `MSTestSourceGenMode=ReflectionFree`. The implementation plan must
  attempt a real `Compono.NUnit.AotSmokeTest`, matching the other three
  packages' own pattern, and **record honestly** whether NUnit's own
  runner/adapter chain permits true Native-AOT publishing at all —
  distinct from, and not to be conflated with, `Compono.NUnit`'s own
  code being trim-safe.

### 18. Alternatives considered and rejected

- **`NUnitAttribute`-based `ComposeAttribute : NUnitAttribute,
  ITestBuilder`.** This ADR's own original proposal; rejected
  pre-acceptance. Requires an explicit `[TestFixture]` attribute on the
  containing class (`NUnitAttribute` carries no `ITestBuilder`-driven
  discovery of its own) or NUnit silently discovers zero tests — a real
  consumer footgun with no equivalent in `Compono.XunitV3`/
  `Compono.TUnit`/`Compono.MSTest`. The `TestAttribute`-based seam (§4/§5)
  achieves the same one-complete-row-per-method result with no such
  requirement, confirmed by independent spike (§7) at no cost found.
- **Option B — NUnit 4+ floor only.** Rejected: no evidence supports
  narrowing away from 3.14.0; the binary-compatibility spike (§2/§5)
  found no reason to exclude it.
- **Option C — version-specific packages
  (`Compono.NUnit3`/`Compono.NUnit4`/`Compono.NUnit5`).** Rejected:
  empirically disproven by the binary-compatibility spike — the premise
  that would justify this option (real binary/assembly incompatibility)
  does not hold.
- **`IParameterDataSource` as the primary `[Compose]` seam.** Rejected:
  wrong shape — per-parameter, independently-combined data sources are
  structurally the opposite of "one Compose source owns one complete
  row," the product's own stated bias and the same principle already
  established for MSTest's `ITestDataSource`.
- **`ISimpleTestBuilder`.** Rejected: NUnit's own docs state
  `ISimpleTestBuilder`-implementing attributes are ignored when any
  other attribute on the same method implements `ITestBuilder` — i.e.
  `ITestBuilder` takes priority, and `[Compose]` always needs to supply
  real composed arguments, never zero, so `ITestBuilder` is the only
  correct choice.
- **`IFixtureBuilder`-based fixture-level composition.** Out of scope —
  not motivated by any evidence found in RESEARCH-0018; available as a
  future extension point if a real fixture-constructor-composition need
  is ever motivated, not needed for this ADR's target experience.

### Positive Consequences

- Reuses nearly all of Compono's existing binding architecture
  (`CompositionRow`, `RowInvokerRegistry`, the `BindingPlan`/`RowInvokers`
  pattern) — genuinely thin, framework-specific glue only.
- No new public `Compono` core API required — `CompositionRow`'s
  framework-agnostic design validated again by a fourth real consumer.
- One compiled package, empirically proven (not merely argued) to work
  across NUnit 3.x/4.x/5.0-beta — no version-specific package split, no
  AutoFixture-style fragmentation.
- Full scope parity with `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`
  (profiles, inline values, `[Shared]`, `Share<T>()`) from the first
  release.
- **No `[TestFixture]` requirement at all** — the `TestAttribute`-based
  seam means `Compono.NUnit` has *no* NUnit-specific attribute-count
  divergence from `Compono.XunitV3`/`Compono.TUnit`, better than this
  ADR's own original proposal.
- Every genuine limitation (Internal-namespace dependency, possible
  repeat composition, the still-narrow custom-`IParameterDataSource`
  regression gap) is documented honestly, with the evidence that grounds
  it, rather than an overstated or understated claim.

### Negative Consequences

- `Compono.NUnit` depends on NUnit's `Internal`-namespaced
  `NUnitTestCaseBuilder`/`TestCaseParameters` types (public CLR types, no
  publicly-guaranteed *stability* contract, §6) — accepted as the
  smallest viable seam, monitored via permanent CI compatibility-matrix
  coverage rather than engineered around with reflection/wrapper
  machinery.
- `Compono.NUnit` must document that combining `[Compose]` with
  `[Values]`/`[Range]`/other parameter-level sources on the same method
  produces additional independent test cases, not just the Compose row —
  a real, non-obvious behavior consumers should understand, though not a
  correctness risk (§8).
- Composition (including any side-effecting registration factory/
  provider) may run more than once for one eventual test case under
  separately-invoked discovery-then-execution sessions — the same
  MSTest-precedent risk, now confirmed for NUnit's classic-VSTest path
  and still open for MTP.
- A custom `IParameterDataSource`'s coexistence behavior was not
  independently spiked (only `[Values]`/`[Range]` were) — a narrow,
  explicitly-flagged gap for the implementation plan's regression suite,
  not expected to differ from the confirmed built-in-attribute behavior.
- Row-binding logic is duplicated a fourth time rather than extracted —
  a small, deliberate maintenance cost, consistent with ADR-0040/
  ADR-0057's own reasoning that binding *inputs* diverge more than
  converge across the four frameworks evaluated so far.

## Pros and Cons of the Options

### `TestAttribute`-based `ITestBuilder`-implementing attribute (chosen)

- Good, because every mechanism it needs is public NUnit surface
  (`TestAttribute`, `ITestBuilder`), empirically proven binary-compatible
  across three real NUnit majors under both supported runners.
- Good, because it owns one complete row per method by construction,
  matching the product's stated bias for free.
- Good, because `[Compose]` alone makes the method discoverable — no
  `[TestFixture]` ceremony, no silent-zero-tests footgun.
- Bad, because producing the final `TestMethod` requires NUnit's
  `Internal`-namespaced `NUnitTestCaseBuilder`/`TestCaseParameters`, an
  accepted, monitored risk (§6).

### `NUnitAttribute`-based `ITestBuilder`-implementing attribute (this ADR's original proposal, rejected)

- Good, because it needs no interaction with `TestAttribute`'s own
  default `ITestBuilder` behavior at all.
- Bad, because it requires an explicit `[TestFixture]` attribute on the
  containing class, or NUnit silently discovers zero tests — a real,
  non-obvious consumer trap with no equivalent in
  `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`.

### `IParameterDataSource`-implementing attribute

- Bad, because per-parameter, combinatorially-combined data sources are
  the structural opposite of one-Compose-owns-one-row.

### `ISimpleTestBuilder`-implementing attribute

- Bad, because NUnit's own docs state it is ignored whenever an
  `ITestBuilder`-implementing attribute is also present on the method,
  and `[Compose]` always needs real arguments — never the
  zero-arguments case `ISimpleTestBuilder` is meant for.

### `IFixtureBuilder`-based fixture-level composition

- Bad (for this ADR's scope), because no evidence motivates it; a
  possible, separately-motivated future extension, not part of this
  design.

### One package, NUnit 4+ floor only (Option B)

- Bad, because no evidence supports narrowing away from the current
  3.14.0 release — the binary-compatibility spike found no reason to
  exclude it.

### Version-specific packages (Option C)

- Bad, because the binary-compatibility spike directly disproves the
  premise (real binary/assembly incompatibility) that would justify
  this option.

## Deferred Decisions and Non-goals

- **`Compono.NUnit` auto-registering a class as an NUnit fixture** — moot;
  the `TestAttribute`-based seam needs no fixture-level marker at all
  (§7).
- **Merging `[Compose]` with parameter-level `IParameterDataSource`
  sources** — not designed around; settled by spike as independent,
  non-merging (§8) — `[Values]`/`[Range]` produce their own additional
  rows, not a merged one, and not unused either.
- **`IFixtureBuilder`-based fixture-constructor composition** — out of
  scope, not motivated by evidence (§18).
- **Extracting a shared `BindingPlan`/`RowInvokers` base across all four
  framework packages** — deferred; NUnit's `IMethodInfo`-wrapped input is
  a fourth independent data point showing binding inputs diverge more
  than converge, not a trigger to extract now (§9).
- **Automatic `TestContext`/framework-value injection through
  `[Compose]`** — deliberately not built (§16).
- **A `Compono.NUnit`-owned disposal mechanism** — not built; the
  existing non-ownership stance is unchanged (§15).
- **Async composition** — out of scope, per RESEARCH-0016's already-
  settled principle; `ITestBuilder.BuildFrom` has no async door
  regardless.
- **Claiming NUnit's own Native-AOT runnability** — not claimed; only
  `Compono.NUnit`'s own code's trim-safety is claimed, and only once the
  implementation plan's `AotSmokeTest` proves it (§17).

## Links

- [RESEARCH-0018](../research/0018-nunit-integration-viability-research.md)
  — the accepted evidence base this ADR's every decision cites; not
  re-litigated, only converted into a durable architectural record.
- [ADR-0039](0039-future-extension-package-admission-gate-and-release-sequence.md)
  — Gate A's original disposition, reconfirmed by RESEARCH-0018 §2 and
  by this ADR.
- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)
  — `CompositionRow`/`Composer.CreateRow`, reused entirely unmodified.
- [ADR-0022](0022-compono-xunit-package-design.md),
  [ADR-0040](0040-compono-tunit-package-design.md),
  [ADR-0057](0057-compono-mstest-package-design.md) — the three prior
  package-design ADRs this one follows in shape; ADR-0057 Amendment 1 in
  particular is the direct methodological precedent for why this ADR's
  one-package claim is grounded in a real binary spike, not source-level
  reasoning.
- [ADR-0041](0041-aot-safe-row-binding-dispatch.md) — `RowInvokerRegistry`,
  reused unchanged.
- [RESEARCH-0015](../research/0015-disposal-ownership-research.md) — the
  non-ownership/disposal stance carried forward unchanged (§15).
- [RESEARCH-0016](../research/0016-async-composition-viability-research.md)
  — the synchronous-composition boundary carried forward unchanged (§14).
- `docs/roadmap/future-packages.md` — updated alongside this ADR to
  record `Compono.NUnit`'s Gate B satisfaction and move it into the
  "Roadmap items" section.
- `.review/adr-0058-plan-0059-pi-codex-review.md` — the pre-acceptance
  adversarial review that found the `TestAttribute`-based seam, the
  premature `< 6.0.0` range, the imprecise Internal-namespace wording,
  and the deferred DAM-annotation timing this ADR's §3/§4/§5/§6/§7/§8/§17
  now correct; its central `[TestFixture]`-elimination finding was
  independently reproduced (not merely trusted) in a second scratch
  spike before this ADR was amended.
