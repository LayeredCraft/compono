# [ADR-0057] Compono.MSTest Package Design

**Status:** Accepted

**Date:** 2026-08-29

**Decision Makers:** Nick Cipollina (product direction), Claude (design)

## Context

[RESEARCH-0017](../research/0017-mstest-integration-viability-research.md)
(Outcome A, accepted) establishes that a clean, maintainable `Compono.MSTest`
integration is feasible and should ship before 1.0: MSTest represents a
significant first-party, Visual Studio/Azure DevOps/enterprise .NET user base
that `Compono.XunitV3`/`Compono.TUnit` alone don't reach, and excluding it
before 1.0 would be an unnecessary adoption barrier now that a clean
integration has been proven feasible, spike-verified, not merely argued.

This ADR is the third package-design ADR in this family, after
[ADR-0022](0022-compono-xunit-package-design.md) (`Compono.XunitV3`) and
[ADR-0040](0040-compono-tunit-package-design.md) (`Compono.TUnit`). It follows
their precedent directly: reuse
[ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
`Composer.CreateRow`/`CompositionRow` unchanged, reuse
[ADR-0041](0041-aot-safe-row-binding-dispatch.md)'s `RowInvokerRegistry`
(the AOT-safe, generator-populated dispatch mechanism `Compono.TUnit`'s own
Amendments 1-2 moved both existing packages onto — `Compono.MSTest` adopts it
from the start, never the earlier `MakeGenericMethod`-per-package pattern
ADR-0022's original text described before being superseded), adapt the
established `BindingPlan`/`RowInvokers` binding pattern for MSTest (its own
package-local implementation, not a shared core type), and produce
idiomatic MSTest wearing idiomatic Compono — `[TestMethod]` + `[Compose]`, not
xUnit syntax forced onto a different framework.

The target consumer experience:

```csharp
[TestClass]
public class OrderServiceTests
{
    [TestMethod]
    [Compose]
    public void Creates_service(
        SomeService sut,
        [Shared] SomeDependency dependency)
    {
    }
}
```

with the same `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`
family, `[Shared]`, `CompositionBuilder.Share<T>()` (core, framework-independent
— no `Compono.MSTest`-specific work needed, per RESEARCH-0017 §5), exact
registrations, constructor selection, and every existing integration package
(`Compono.TestDoubles`, `Compono.NSubstitute`, `Compono.Bogus`,
`Compono.Logging`, `Compono.DependencyInjection`, `Compono.Http`) working
unmodified, exactly as they do for `Compono.XunitV3`/`Compono.TUnit` today.

RESEARCH-0017's evidence base for this ADR (cited by section throughout):
`ITestDataSource` is MSTest's current, stable, first-party custom-data-source
extension point (§2/§3, spike-confirmed); `[TestMethod]` alone is correct, not
merely sufficient — `[DataTestMethod]` is being actively removed upstream
(§4); one `CompositionRow` per `GetData` call preserves `[Shared]`/`Share<T>()`
correctly, but MSTest's discovery/execution lifecycle can invoke `GetData`
more than once for one eventual test case under some runner workflows,
unlike `Compono.XunitV3`/`Compono.TUnit`'s structural exactly-once guarantee
(§5/§20a, spike-confirmed, corrected in a follow-up pass from an
initially-overstated purity claim); `RowInvokerRegistry` is directly reusable, and the `BindingPlan`/`RowInvokers`
binding pattern is directly adaptable (§6); `MethodInfo` is framework-required
metadata, not a reflection fallback (§7); composition is synchronous, full
stop (§8, MSTest-signature-confirmed, stricter than xUnit v3's own
`ValueTask`-typed `GetData`); `[DataRow]`/`[DynamicData]` and `[Compose]`
produce independent complete rows, never a merged one (§9, spike-confirmed);
`MSTest.TestFramework` (not `MSTest`, not `MSTest.TestAdapter`) is the correct
dependency, minimum version `3.0.0` (§14/§16, evidence-driven, revised down
from an initial, uncorroborated `4.x`); no internal MSTest dogfood consumer
exists today, which is explicitly not evidence against shipping (§17).

## Decision Drivers

- `design-decisions.md` rule 3 — core `Compono` must never know
  `Compono.MSTest` exists; every mechanism this ADR uses is already public
  (`Composer.CreateRow`, `CompositionRow.Resolve`/`ResolveShared`/
  `ShareExplicit`, `RowInvokerRegistry`).
- The user's explicit framing for this research/ADR pair: the burden of
  proof is on **not** shipping `Compono.MSTest`, not on justifying shipping
  it — optimize for a high-quality native MSTest experience, not mechanical
  xUnit/TUnit symmetry.
- [ADR-0001](0001-source-generation-first.md)'s no-reflection-by-default
  posture — `MethodInfo`-based parameter inspection is framework-required
  metadata access (the same category `Compono.XunitV3` already relies on),
  never a fallback composition engine; `RowInvokerRegistry` dispatch stays
  reflection-free.
- Compono's existing non-ownership/disposal stance
  ([RESEARCH-0015](../research/0015-disposal-ownership-research.md)) and
  synchronous-only composition posture
  ([RESEARCH-0016](../research/0016-async-composition-viability-research.md))
  — neither is reopened by this ADR.
- Minimal, intentional dependency footprint, matching
  `Compono.XunitV3`'s own `xunit.v3.extensibility.core`-only reference —
  `Compono.MSTest` must not pull a runner/adapter package into a consumer's
  dependency graph merely for convenience.
- Honest documentation of genuine MSTest-specific behavioral differences
  (the discovery/execution repeat-composition risk) rather than papering
  over them with an unearned Compono-wide guarantee.

## Considered Options

**Integration seam:**
1. An `ITestDataSource`-implementing `[Compose]` attribute (chosen).
2. A custom `TestMethodAttribute`/execution-layer override.

**Consumer syntax:**
1. `[TestMethod]` + `[Compose]` (chosen).
2. `[DataTestMethod]` + `[Compose]`.

**`[DataRow]`/`[DynamicData]` + `[Compose]` mixing:**
1. Treat as independent, non-merging data sources — document the boundary
   (chosen).
2. Build custom execution-layer machinery to merge partial rows.

**MSTest dependency:**
1. `MSTest.TestFramework` only (chosen).
2. The `MSTest` umbrella package.

**Minimum supported MSTest version:**
1. `MSTest.TestFramework` `3.0.0` (chosen).
2. `MSTest.TestFramework` `4.x` only.

## Decision Outcome

**Chosen: Option 1 in every case above.** A clean `ITestDataSource`-based
integration is feasible, reuses nearly all of Compono's existing binding
architecture, and no blocker was found that meets the bar this ADR's own
product direction set (RESEARCH-0017 §19, §22).

### 1. Why `Compono.MSTest` exists before 1.0

MSTest is Microsoft's own first-party framework, ships with Visual Studio,
and is deeply tied into Azure DevOps test reporting — real enterprise-adoption
signal that raw NuGet download rank alone understates (RESEARCH-0017 §18).
Excluding it before Compono's 1.0 public-API freeze would be a durable,
self-inflicted adoption barrier for a general-purpose .NET test-composition
library, once a clean integration has been demonstrated feasible rather than
merely plausible.

### 2. Why `ITestDataSource` is the integration seam

`ITestDataSource` (`GetData(MethodInfo methodInfo) : IEnumerable<object?[]>`,
`GetDisplayName(MethodInfo methodInfo, object?[]? data) : string?`) is MSTest's
current, documented, stable, first-party extension point for a fully custom
data-source attribute — not a legacy compatibility shim, confirmed against
current Microsoft Learn documentation and `microsoft/testfx-docs` RFC-005
(RESEARCH-0017 §2). It receives the same `MethodInfo` input
`Compono.XunitV3`'s `ComposeAttribute.GetData` already receives, participates
correctly in discovery, supports a stable, custom `GetDisplayName`, and
surfaces composition failures as ordinary thrown `CompositionException`s with
no special result-wrapping shape required (§3, spike-confirmed).

A custom `TestMethodAttribute`/execution-layer override (Option 2) is
**rejected**: nothing in the target experience — composed parameters,
`[Shared]`, `Share<T>()`, profile selection, failure diagnostics, display
names — requires replacing MSTest's own test-execution mechanism. It would be
strictly more invasive, more coupled to MSTest internals, and more fragile,
for zero additional capability (§3).

### 3. Why `[TestMethod]` + `[Compose]` is the public experience

`[TestMethod]` alone is correct, not a compromise. `DataTestMethodAttribute`
"provides no additional value over `TestMethodAttribute` and will be removed
in a future version" (`microsoft/testfx` issue #4166), and analyzer
**MSTEST0044** actively flags `[DataTestMethod]` usage today with an
automatic code fixer to `[TestMethod]` (RESEARCH-0017 §4, spike-confirmed:
a `[TestMethod]`-only method with a custom `ITestDataSource` attribute
compiles and runs with zero issues). Requiring `[DataTestMethod]` alongside
`[Compose]` would be needless, actively-discouraged-by-the-framework
ceremony. `ComposeAttribute` does not derive from any MSTest attribute type —
`ITestDataSource` is an ordinary interface implemented on a plain
`Attribute` subclass, matching every current MSTest documentation example;
no inheritance trick is needed or beneficial.

### 4. Why no custom `TestMethodAttribute` is introduced

Covered in §2 above — recorded here as its own numbered answer per this
ADR's required scope. No capability in the target experience needs it, and
introducing one would trade a small, well-understood package for a large,
fragile one coupled to unstable MSTest test-execution internals for no
functional gain.

### 5. How `CompositionRow`/`RowInvokerRegistry` preserve existing architecture

```
MSTest ITestDataSource ("Compono.MSTest.ComposeAttribute")
    ->
MSTest-specific binding adaptation (Compono.MSTest.Binding.BindingPlan/RowInvokers)
    ->
CompositionRow (core, ADR-0021, unchanged)
    ->
RowInvokerRegistry (core, ADR-0041, unchanged)
    ->
source-generated composition (Compono.Generators, unchanged dispatch;
new ComposeMethodDiscovery registrations, see "Generator discovery" below)
```

There is **no second composition engine**. `RowInvokerRegistry`
(`src/Compono/RowInvokerRegistry.cs`) is core, framework-agnostic,
`Type`-keyed, populated by generator-emitted module initializers with zero
MSTest awareness — reused exactly as-is. `CompositionRow` (core, ADR-0021) is
exactly the graph-lifetime abstraction needed — reused unchanged.
`BindingPlan`/`ParameterBindingPlan`/signature validation
(`src/Compono.XunitV3/Binding/BindingPlan.cs`) operates entirely on
`System.Reflection.MethodInfo`/`ParameterInfo` — framework-agnostic inputs —
and its logic (parameter reflection, `[Shared]` detection, nullability
inference, generic-method/`ref`-parameter rejection, duplicate-`[Shared]`-type
rejection, more-than-one-Compose-family-attribute rejection) is directly
adaptable to `Compono.MSTest` (RESEARCH-0017 §6).

**One `CompositionRow` per `GetData` invocation**, exactly mirroring
`Compono.XunitV3.ComposeAttribute.GetData` and `Compono.TUnit`'s own
per-factory-invocation row. All parameters for one test method bind from
that one row, within that one `GetData` call. `[Shared]`/`Share<T>()` are
never split across calls — each `GetData` call gets its own fresh row/graph.

**Row-binding logic: duplicated, not extracted, for this release** — the
same decision ADR-0040 made for `Compono.TUnit` relative to
`Compono.XunitV3`, and for the same reason: `ITestDataSource.GetData(MethodInfo)`
and xUnit v3's `DataAttribute.GetData(MethodInfo, DisposalTracker)` share
enough of an input shape that a byte-for-byte port is tempting, but the
correct shared abstraction boundary still isn't obvious from two examples,
now three. `Compono.TUnit`'s own precedent (a `DataGeneratorMetadata`-shaped
input, genuinely different from `MethodInfo`/`ParameterInfo`) already shows
the binding *inputs* diverge more than they converge across frameworks.
Duplicating the well-understood, self-contained `BindingPlan`/`RowInvokers`
pattern (~150-200 LOC) is the lower-risk choice. `Compono.MSTest` is,
literally, the third framework-binding implementation, so an unqualified
"rule of three, not yet triggered" framing would be confusing here — the
actual reason to hold off is narrower: xUnit v3 and MSTest both receive
`MethodInfo`-shaped binding input, but `Compono.TUnit`'s own
`DataGeneratorMetadata`-shaped input is materially different, so three
packages still don't yet expose a sufficiently stable common abstraction
boundary. Extracting now would risk generalizing around superficial code
similarity between the two `MethodInfo`-shaped packages, not a proven shared
framework model. Revisit extraction if a **fourth** framework package (NUnit,
per RESEARCH-0017's own forward note) is researched — NUnit would be another
independent data point that may reveal whether a useful abstraction actually
exists, rather than a mechanical "third implementation" trigger.

**Generator discovery**: `Compono.Generators`' `ComposeMethodDiscovery`
(`src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`) is the
component that closes the "a type reached only as a `[Compose]`-attributed
method's own parameter has no textual `Resolve<T>()` call site" gap for
`Compono.XunitV3` (ADR-0022's Amendment, 2026-07-30) and `Compono.TUnit`
(ADR-0040). `Compono.MSTest` hits the identical gap — its binding is
likewise entirely runtime reflection over `MethodInfo`/`ParameterInfo`, no
textual `Resolve<T>()` call site in the consumer's own source.
`ComposeMethodDiscovery.TransformMethod`'s own logic (eligible-parameter
filtering, `ref`/`out`/`in`/`params` exclusion, generic-method exclusion) is
already attribute-family-agnostic, operating on `IMethodSymbol`/
`IParameterSymbol` alone. **Required design**: three more constants and
three more `SyntaxValueProvider.ForAttributeWithMetadataName` registrations
in `ComponoIncrementalGenerator.cs`, for
`Compono.MSTest.ComposeAttribute`/`` `1``/`` `2``, feeding the same
`ComposeMethodDiscovery.TransformMethod`. `Compono.Generators.Tests` needs a
snapshot test proving a parameter type reachable only through a
`Compono.MSTest`-attributed method receives a generated plan — mirroring the
equivalent regression coverage for the other two packages. This is
discovery-time/compile-time-only work with no new public runtime surface,
following the precedent ADR-0022's own Amendment and ADR-0040 both already
established: it belongs in this package-design ADR, not a separate
core-extension one.

### 6. Complete public API shape

Deciding explicitly, not copying RESEARCH-0017 §15's illustrative sketch
unreviewed — compared directly against `Compono.XunitV3`/`Compono.TUnit`,
diverging only where MSTest's `ITestDataSource` signature forces it:

```csharp
namespace Compono.MSTest;

/// <summary>
/// Composes an MSTest data-driven test method's parameters through Compono -
/// the default (no explicit profile) entry point. See ADR-0057 for the full
/// binding algorithm, discovery/execution behavioral contract, seed policy,
/// and diagnostics.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : Attribute, ITestDataSource
{
    public ComposeAttribute(params object?[] inlineValues);

    /// Same non-negative-only contract as Compono.XunitV3.ComposeAttribute.Seed
    /// and Compono.TUnit.ComposeAttribute.Seed - a reported seed is always
    /// pasteable back here unchanged.
    public int Seed { get; set; }

    public IEnumerable<object?[]> GetData(MethodInfo methodInfo);

    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile> : ComposeAttribute
    where TProfile : ICompositionProfile, new()
{
    public ComposeAttribute(params object?[] inlineValues) : base(inlineValues) { }
}

/// <summary>
/// Composes an MSTest data-driven test method's parameters through Compono, applying a profile built
/// from <em>profile configuration arguments</em> known at this attribute's call site - a distinct
/// concept from this attribute family's ordinary inline values, which bind to the test method's own
/// parameters instead. This constructor never binds to the test method's parameters at all; every one
/// of them is composed in full. <typeparamref name="TConfig"/> is constructed positionally from this
/// attribute's own constructor arguments, then <typeparamref name="TProfile"/> is constructed from that
/// <typeparamref name="TConfig"/> instance and applied via
/// <see cref="CompositionBuilder.AddProfile(ICompositionProfile)"/> - the same Compono-facing
/// attribute family and semantics as <c>Compono.XunitV3.ComposeAttribute{TProfile,TConfig}</c>/
/// `Compono.TUnit`'s own equivalent overload (ADR-0036); no MSTest-specific profile/configuration
/// shape is introduced.
/// </summary>
/// <typeparam name="TProfile">
/// The profile to construct and apply. Must have exactly one public constructor accepting exactly one
/// <typeparamref name="TConfig"/>-typed parameter - no <c>new()</c> constraint, unlike
/// <see cref="ComposeAttribute{TProfile}"/>, since this form is never default-constructed.
/// </typeparam>
/// <typeparam name="TConfig">
/// The type this attribute's constructor arguments bind to, positionally, against its own single
/// public constructor.
/// </typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile, TConfig> : ComposeAttribute
    where TProfile : ICompositionProfile
{
    /// <summary>
    /// Creates a <see cref="ComposeAttribute{TProfile, TConfig}"/>.
    /// </summary>
    /// <param name="configArguments">
    /// Profile configuration arguments, bound positionally to <typeparamref name="TConfig"/>'s single
    /// public constructor - an entirely separate binding target from this attribute family's ordinary
    /// inline values; every test method parameter is composed in full regardless of what's supplied
    /// here.
    /// </param>
    public ComposeAttribute(params object?[] configArguments) : base() { }

    internal override void ApplyProfile(CompositionBuilder builder);
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SharedAttribute : Attribute;
```

No public types beyond this are needed — `RowInvokerRegistry`/
`CompositionRow` are already public core types `Compono.MSTest`'s own
internal `BindingPlan` consumes, not new surface. The only structural
divergence from `Compono.XunitV3`/`Compono.TUnit` is `GetData`'s return
shape (`IEnumerable<object?[]>`, forced by `ITestDataSource`'s own
signature — synchronous, not `ValueTask`- or `Func`-wrapped) and the absence
of any `DisposalTracker`-equivalent parameter, since `ITestDataSource.GetData`
takes only `MethodInfo`. `Compono.MSTest` never derives `ComposeAttribute`
from any MSTest attribute base type; it implements `ITestDataSource` directly
on a plain `Attribute`, matching current MSTest documentation examples and
avoiding `[DataTestMethod]`'s deprecation entirely (§3 above).

**One `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`
attribute owns the entire row**, mirroring the other two packages exactly:
`AllowMultiple = false` on each, plus an explicit "more than one Compose-family
attribute" check in `BindingPlan.ValidateSignature`, reused logic from
`Compono.XunitV3`. Profiles are specified via the generic attribute type
argument, never attribute stacking or ordering — this sidesteps any MSTest
attribute-discovery-order question entirely (RESEARCH-0017 §10). Class/
assembly-level profile inheritance is not supported, matching both existing
packages' method-level-only scope.

### 7. `MSTest.TestFramework` dependency and `3.0.0` minimum

**Dependency: `MSTest.TestFramework` only.** Confirmed by binary inspection
(RESEARCH-0017 §14): `ITestDataSource`/`TestMethodAttribute` are compiled
into `MSTest.TestFramework.dll`; the umbrella `MSTest` package transitively
resolves to four separate packages (`MSTest`, `MSTest.TestFramework`,
`MSTest.TestAdapter`, `MSTest.Analyzers`), which would pull the runner
package (`MSTest.TestAdapter`) into every `Compono.MSTest` consumer's
dependency graph unnecessarily — a consumer-project/runner concern, not a
`Compono.MSTest` compile-time dependency. This mirrors how
`Compono.XunitV3` depends on `xunit.v3.extensibility.core` rather than a
full runner package.

**Minimum supported version: `MSTest.TestFramework` `3.0.0`.** Every
capability this design actually needs — `TestMethodAttribute`,
`ITestDataSource`'s two members — has been stable, unchanged, since
`MSTest.TestFramework` v1.2.1 (RESEARCH-0017 §16's capability matrix,
built from the official `testfx` changelog and current Microsoft Learn
"Applies to" version lists). Nothing in this ADR's design (§6) touches
`TestContext` constructor injection (v3.6.0, and explicitly not used —
§13), `TestDataRow<T>` (v3.8.0, not needed), the unfolding-capability
interfaces (v3.7.3, not needed), or the `[DataTestMethod]`→`[TestMethod]`
analyzer (v3.10.0, consumer-facing guidance, not a package dependency). No
breaking change to `ITestDataSource` itself was found between 3.x and 4.x.

`3.0.0` — not an even-older 1.x/2.x floor, and not `4.x` — is chosen because
it is the first modern, SDK-style, `.NET 6+`-aligned MSTest major, costs
this design literally nothing (no capability above 3.0.0 is used), and real
adoption evidence shows the 3.x and 4.x lines were maintained in parallel
for over a year past 4.0.0's release (3.11.1 shipped the same day as 4.0.2,
2025-11-11) rather than an immediate hard cutover. The 1.x/2.x range is
rejected as genuinely legacy, pre-.NET-Core-consolidation MSTest generations
with no realistic current adoption signal — supporting them would be
ancient-compatibility-for-its-own-sake, the opposite failure mode from an
arbitrary `4.x`-only floor.

**Accepted caveat, recorded rather than silently carried**: as of this
ADR's date, `MSTest.TestFramework` 3.x has received no patch release since
3.11.1 (2025-11-11), roughly nine months, while 4.x has continued to receive
releases (up to 4.3.3, 2026-07-28) — the 3.x line reads as de facto
frozen/unmaintained today, not actively patched. `3.0.0` is chosen anyway:
the floor expresses the oldest *compatible* framework version a consumer's
project may reference, not a recommendation that consumers stay there —
nothing about `Compono.MSTest`'s own design or dependency graph prevents a
consumer from using current MSTest 4.x, and a consumer choosing to stay on
an unpatched 3.x line does so under MSTest's own support posture, not
`Compono.MSTest`'s. If a future MSTest release introduces a genuine breaking
change to `ITestDataSource` itself, the floor is revisited then, on that
evidence — not preemptively today.

### 8. MTP/VSTest support policy

`Compono.MSTest` supports MSTest under **both** its currently-supported
execution platforms — the modern `Microsoft.Testing.Platform` (MTP) and the
classic VSTest adapter — because `ITestDataSource` is a first-party MSTest
extension point exercised identically by both (RESEARCH-0017 §5/§20/§20a,
spike-confirmed under both). `Compono.MSTest` imposes no runner requirement
of its own and does not depend on any MTP-specific or VSTest-specific API.
Runner selection remains entirely the consumer's project configuration
(`<UseVSTest>`/the MSTest project template default), not something
`Compono.MSTest` chooses or constrains.

MTP may be documented as MSTest's modern/preferred execution path (it's the
default for `dotnet new mstest` today), but `Compono.MSTest` must not be
built to assume it — a consumer on the classic VSTest adapter (a realistic,
common Visual Studio/Azure DevOps configuration) gets full, correct
`[Compose]` behavior, with one documented behavioral difference (§9 below),
not a degraded or unsupported experience.

### 9. Discovery/execution repeat-composition behavioral contract

**The permanent, documented behavioral contract**, stated precisely per the
corrected RESEARCH-0017 §5/§20a evidence:

> MSTest may invoke a `[Compose]` attribute's `ITestDataSource.GetData` more
> than once across separately-invoked discovery and execution sessions.
> Consequently, Compono composition — including any registration factory or
> `ICompositionValueProvider` it invokes — may also execute more than once
> for what the consumer perceives as one eventual test case.

This is **not** "VSTest always composes twice" — that claim is false and is
explicitly rejected. The evidence (RESEARCH-0017 §20/§20a, both spike-verified
with exact process-ID-tagged logging):

- A single `dotnet test` invocation under MTP (the default runner) produces
  exactly one `GetData` evaluation per method.
- A single `dotnet test` invocation under the classic VSTest adapter
  (`<UseVSTest>true</UseVSTest>`) **also** produces exactly one `GetData`
  evaluation per method — matching MTP, not doubled.
- A separate `dotnet test --list-tests` (discovery, process A) followed by a
  separately-invoked `dotnet test` (execution, process B) under the classic
  VSTest adapter produces **two** `GetData` calls per method — one per
  process. This reproduces the realistic Visual Studio Test Explorer
  workflow (discover once when the tree populates, execute separately and
  possibly repeatedly afterward), and it is specific to the classic VSTest
  adapter's lack of cross-invocation discovery-result caching, not a
  property of every VSTest-mode test run.
- A `GetData` exception during discovery causes MSTest to show a "folded"
  single test node rather than a per-row failure; the real exception (with
  its original type and message intact, confirmed through MSTest's own
  `TryExecuteFoldedDataDrivenTestsAsync` wrapping) still surfaces normally
  at execution time.

**Compono establishes no purity/repeatability contract for
`Register<T>()` factories that would excuse this.** A follow-up correction
to RESEARCH-0017 checked this directly (`src/Compono/CompositionBuilder.cs:60-92`'s
XML docs, every ADR, `docs/public-api.md`, `docs/architecture.md`) and found
no such contract exists. The one real, narrower contract that does exist —
`ICompositionValueProvider`'s own `<remarks>`
(`src/Compono/ICompositionValueProvider.cs:12-16`), "must be safe to invoke
repeatedly, including concurrently" — is a **safety** promise (won't crash
or corrupt state), not a **purity** promise (no observable side effects); a
provider satisfying it may still, say, increment a counter on every call and
remain compliant. `Register<T>()` factories carry no documented contract at
all in this dimension. This ADR does **not** modify `Register<T>()` or
`ICompositionValueProvider` semantics to accommodate MSTest — the discovery/
execution repeat-composition behavior is recorded as a genuinely new,
`Compono.MSTest`-specific consequence for consumers with side-effecting
factories, stated honestly in this package's own documentation (not implied
to be safe by an existing Compono-wide guarantee that doesn't exist).

**What stays correct regardless**: each independently-created
`CompositionRow` per `GetData` call keeps `[Shared]`/`Share<T>()` internally
correct — sharing is never split across calls, since each call gets its own
fresh row/graph. Deterministic seeding means two independently-composed rows
for the same test case are logically equivalent (same seed → same generated
values) even though they're distinct object instances — the *values* a
consumer sees stay consistent across repeat evaluations; only an
*observable side effect* a factory performs (I/O, a counter, external state
mutation) genuinely repeats. A discovery-time-created disposable value that
is discarded without disposal is a plain restatement of
[RESEARCH-0015](../research/0015-disposal-ownership-research.md)'s already-accepted
non-ownership stance (§12 below), not a new problem this ADR needs to
solve.

**No supported deferral mechanism exists.** `ITestDataSourceUnfoldingCapability`/
`TestDataSourceUnfoldingStrategy` control only how rows *display*
(collapsed vs. per-row Test Explorer nodes), not *when* `GetData` itself
runs — no supported MSTest mechanism exists for declaring a row now and
materializing its values later at execution time (RESEARCH-0017 §5). Chasing
one would mean abandoning `ITestDataSource` for the already-rejected custom-
`TestMethodAttribute` option (§2 above) or serializing composed state across
a process boundary — not reliably possible in general, since composed
values aren't required to be serializable. Documenting the limitation, per
the product direction's own stated preference for the smallest reliable
integration, is the correct and final answer for this package, not a
placeholder pending a future fix.

### 10. `[DataRow]`/`[DynamicData]` coexistence boundary

`[DataRow]`, `[DynamicData]`, and `[Compose]` are **independent, complete-row
data sources**; they do not merge into one row. Spike-verified
(RESEARCH-0017 §9): a `[DataRow(1)]` and a custom `ITestDataSource` attribute
on the same method produce two independent test cases, each attribute
supplying a complete row for every parameter — not a partial row `[Compose]`
could fill the gaps of. This is a structural property of MSTest's
independent-row-per-`ITestDataSource`-attribute model, not specific to
`[DataRow]`; any two `ITestDataSource`-family attributes on one method behave
the same way.

`Compono.MSTest` does not attempt per-parameter mixing of `[DataRow]`-supplied
and `[Compose]`-composed values on one row. This mirrors, not narrows, the
existing product boundary: `Compono.XunitV3`/`Compono.TUnit` don't support
this either — inline values are supplied through `[Compose(...)]`'s own
constructor, never through a separate `[InlineData]`/`[Arguments]`-style
attribute. Attempting to make `[Compose]` "fill in" values `[DataRow]` didn't
supply would require the already-rejected custom `TestMethodAttribute`/
execution-layer override (§2) — not worth the architectural cost for a
scenario neither existing package supports either.

### 11. Sync-only composition boundary

`ITestDataSource.GetData` returns `IEnumerable<object?[]>` — no `Task`/
`ValueTask` anywhere in its signature (RESEARCH-0017 §8, confirmed at the
API-signature level). This is a **harder** constraint than
`Compono.XunitV3`'s own `ValueTask`-returning `GetData` (which is already
`ValueTask`-typed, just synchronously completed today) — MSTest's extension
point has no async door to leave open at all.

Per [RESEARCH-0016](../research/0016-async-composition-viability-research.md)'s
already-settled principle, this is not a reason to reject `Compono.MSTest`.
`[Compose]`-supplied MSTest parameters are synchronously composed, full
stop. Async resource initialization belongs in MSTest's own lifecycle
(`[ClassInitialize]`/`[AssemblyInitialize]`/`TestContext`), with the
already-initialized resource registered into Compono synchronously — the
same boundary already established for `Compono.XunitV3`/`Compono.TUnit`.
This ADR does not invent an async composition mechanism around MSTest.

### 12. Non-ownership/disposal boundary

`Compono.MSTest` does not dispose composed argument objects, matching
`Compono.XunitV3`'s explicit stance
(`ComposeAttribute.cs:130-142`'s remarks: a composed value's provenance is
indistinguishable from Compono's own vantage point — a freshly-constructed
value from a generated plan is exactly as opaque as a shared/cached instance
from an exact registration or a configured `IServiceProvider`
([ADR-0019](0019-registrations-and-service-provider-injection.md)'s "the
caller owns the provider and its entire lifetime" contract) — so no
disposal-tracking mechanism is introduced here that would risk disposing an
externally-owned instance.

MSTest's own post-test lifecycle (`[TestCleanup]`, `IDisposable`/
`IAsyncDisposable` on the test class itself) remains available as a future
seam for a *consumer's own* disposal — the same generic seam
[RESEARCH-0015](../research/0015-disposal-ownership-research.md) §6 already
identifies for any future opt-in disposal model — but nothing here designs
or requires that now. A discovery-time-created disposable value that MSTest
discards without ever executing the test is simply a consequence of §9's
lifecycle behavior, governed by the same non-ownership contract as every
other composed value, not a new disposal problem.

No framework-owned resource (`TestContext`, any MSTest-internal type) is
ever owned or disposed by `Compono.MSTest`'s row/graph.

### 13. `TestContext`/framework-ownership boundary

`Compono.MSTest` does **not** auto-inject `TestContext` (or any other MSTest
framework value) as a composed parameter. MSTest already provides
`TestContext` idiomatically — constructor injection (MSTest 3.6+) or the
classic `public TestContext TestContext { get; set; }` property — with
ownership unambiguously MSTest's. Auto-composing it through `[Compose]`
would duplicate an existing, better-owned mechanism and blur the "who owns
this" line §12 above keeps deliberately clear. `TestContext.CancellationToken`
is reached the same existing, framework-native way. This is a conservative,
no-new-surface answer: no compelling usability gap was found to justify
crossing this boundary (RESEARCH-0017 §13).

### 14. Reflection/AOT/source-generation posture

`Compono.MSTest` preserves Compono's existing reflection-free/source-generated
composition architecture, on the same terms `Compono.XunitV3`/`Compono.TUnit`
already do:

- **MSTest hands `Compono.MSTest` a `MethodInfo`** (`ITestDataSource.GetData(MethodInfo)`)
  — framework-required metadata access, identical in kind to what
  `Compono.XunitV3` already receives and treats as AOT-safe input. Reading
  `MethodInfo.GetParameters()`, attribute presence (`[Shared]`), and
  nullability metadata off it is exactly what `BindingPlan.Build` already
  does today — not a new category of reflection.
- **No `MakeGenericType`/`Activator.CreateInstance`/dynamic generic
  instantiation.** `RowInvokerRegistry.TryGet` is a plain `Type`-keyed
  dictionary lookup (`src/Compono/RowInvokerRegistry.cs`) — every
  `Resolve<T>()`/`ResolveShared<T>()`/`ShareExplicit<T>()` call a registered
  entry makes is written directly, with a compile-time-known `T`, in
  generator-emitted source. `Compono.MSTest` reuses this dispatch unchanged
  — the same reflection-free guarantee `Compono.XunitV3`/`Compono.TUnit`
  already rely on (ADR-0041), never a fallback to a reflection-based
  composition engine.
- **MSTest's own AOT posture is orthogonal, not a constraint.**
  `MSTest.SourceGeneration` (compile-time test *discovery*, defaulting to
  `ReflectionFree` for trimmed/AOT projects starting MSTest 4.3.2) discovers
  MSTest's own `[TestMethod]`s — a different concern from how
  `Compono.MSTest` composes parameters once a test is already found. No
  conflict identified between the two generators.

### 15. Seed/display-name/diagnostics behavior

**Display name**: `ITestDataSource.GetDisplayName(MethodInfo, object?[]?)` is
the primary and only supported display-name hook, spike-confirmed working
exactly as authored (RESEARCH-0017 §11). `Compono.MSTest` produces a stable,
non-huge-object-dump name of the form:

```
{methodName} (Compono, seed: {seed})
```

This surfaces the row's seed directly in Test Explorer/`dotnet test` output
— readable, stable, and reproducible without dumping composed object values
into the name. `TestContext.Properties`/`TestProperty`-based reporting (an
execution-time-only mechanism) is **not** used for the primary seed-reporting
path; `GetDisplayName` alone is simpler, works identically under both MTP
and VSTest (§8), and requires no execution-layer coupling. This decision
diverges deliberately from `Compono.XunitV3`'s `Traits["Compono.Seed"]`
mechanism and `Compono.TUnit`'s `ITestDiscoveryEventReceiver`/`AddProperty`
mechanism — both exist because their respective frameworks have a
first-party reporter-visible property-bag concept `Compono.MSTest` doesn't
need an equivalent for, since `GetDisplayName`'s output is itself
Test-Explorer-visible and reportable without one.

**Composition-failure surfacing**: an exception thrown from `GetData` during
*execution* surfaces as an ordinary MSTest test failure with the original
`CompositionException.Message` intact — ordinary .NET exception propagation,
confirmed under both MTP and VSTest (§9). An exception thrown during
*discovery* causes the "folded" single-node Test Explorer behavior (§9) —
a real but survivable diagnostics degradation, not silent failure, since the
execution-time re-throw still carries the full message. Following
`Compono.XunitV3`/`Compono.TUnit`'s established pattern, every
`CompositionRow.Resolve`/`ResolveShared`/`ShareExplicit` call is wrapped to
catch `CompositionException` and rethrow via
`CompositionException.WithSeedInMessage(exception, row.Seed)` — the same
unconditional, pasteable-seed guarantee both existing packages already make,
applied identically here. No new exception type is introduced.

### 16. Documentation/skills/evals/dogfooding obligations

These are mandatory parts of this feature's eventual definition of done, not
optional cleanup, matching how `RESEARCH-0015`/`RESEARCH-0016` framed their
own downstream documentation work:

- **Package README** (`src/Compono.MSTest/README.md` or equivalent) — must
  state the discovery/execution repeat-composition behavioral contract (§9)
  explicitly as part of `Compono.MSTest`'s own documented behavior, not
  buried only in this ADR.
- **Root `README.md`/package matrix** — add `Compono.MSTest` alongside
  `Compono.XunitV3`/`Compono.TUnit`.
- **`docs/mvp.md`/roadmap** — add `Compono.MSTest` as a scoped pre-1.0
  package.
- **`docs/architecture.md`/`docs/public-api.md`** — wherever these enumerate
  supported test frameworks, add MSTest; verify neither currently states an
  exhaustive/closed framework list that would become stale by omission.
- **`skills/compono/SKILL.md`** — add a new `mstest.md` reference, loaded
  only when `Compono.MSTest` is referenced/requested, matching the existing
  `xunit-v3.md`/`tunit.md` scoping pattern. Must teach: `[TestMethod]` +
  `[Compose]` is the intended syntax; `[DataTestMethod]` is unnecessary;
  `[DataRow]`/`[DynamicData]` rows do not merge with `[Compose]`; composition
  is synchronous; Compono does not own/dispose composed values; MTP and
  VSTest are both supported, MTP is modern/preferred but not required;
  consumers must not rely on registration/provider factories being invoked
  exactly once across MSTest discovery/execution sessions.
- **Examples/evals** — a `Compono.MSTest`-specific discriminating eval
  (mirroring RESEARCH-0014's `Share<T>()` eval pattern) as a completion bar
  for the implementation plan.
- **Migration guidance** — a short "migrating an MSTest `[DynamicData]`-based
  test to `[Compose]`" note, mirroring `docs/migrating-from-autofixture.md`'s
  role, acknowledging no real internal MSTest consumer exists yet to
  validate it against.
- **Dogfooding**: no existing LayeredCraft/ncipollina MSTest consumer exists
  (RESEARCH-0017 §17, checked including branches, not just default
  checkouts) — this is explicitly **not** a reason to weaken or defer the
  package. The implementation plan must include a small, dedicated MSTest
  dogfood fixture (a purpose-built minimal consumer, not a synthetic
  single-file example) validating, at minimum: ordinary composition,
  profiles, `[Shared]`, `Share<T>()`, `Register<T>()`, constructor
  selection, `Compono.TestDoubles` integration, `Compono.Logging`
  integration where appropriate, deterministic seed reproduction,
  diagnostics, the `3.0.0` version floor, current MSTest 4.x, MTP
  execution, and VSTest execution where practical. Dogfood validation must
  use the repository's `scripts/dogfood-validate.sh` workflow with freshly
  packed local packages, not `ProjectReference`s or stale package
  artifacts — the same discipline every other package's dogfood validation
  already follows.

None of this is applied by this ADR — it is the implementation plan's
completion-gate checklist, recorded here so it isn't rediscovered piecemeal.

### Positive Consequences

- Reuses nearly all of Compono's existing binding architecture
  (`CompositionRow`, `RowInvokerRegistry`, the `BindingPlan`/`RowInvokers`
  pattern) — genuinely thin, framework-specific glue only.
- No new public `Compono` core API required — `CompositionRow`'s
  framework-agnostic design (ADR-0021) validated again by a third real
  consumer.
- Full scope parity with `Compono.XunitV3`/`Compono.TUnit` (profiles,
  inline values, `[Shared]`, `Share<T>()`) from the first release.
- Removes a real, defensible adoption barrier for enterprise/VS/ADO-centric
  .NET consumers.
- Every genuine limitation (discovery/execution repeat-composition risk,
  no `[DataRow]`/`[Compose]` mixing) is documented honestly, with corrected,
  evidence-based characterization rather than an overstated or understated
  claim.

### Negative Consequences

- `Compono.MSTest` must document a genuinely stricter behavioral contract
  than `Compono.XunitV3`/`Compono.TUnit` need to: composition (including any
  side-effecting registration factory/provider) may run more than once for
  one eventual test case under some classic-VSTest-adapter workflows. A
  consumer relying on exactly-once factory invocation needs to know this
  before adopting the package. Accepted: it is a real, MSTest-specific
  runner-lifecycle property, not something a redesigned `Compono.MSTest`
  binding algorithm could eliminate without abandoning `ITestDataSource`
  entirely (§9).
- `[DataRow]`/`[DynamicData]` cannot supply some parameters while `[Compose]`
  composes the rest, on one row — a real, if pre-existing-in-shape (§10),
  limitation.
- `MSTest.TestFramework` `3.0.0`'s current lack of active patch releases
  (§7) means the accepted floor is, at the time of writing, a de facto
  unmaintained line — accepted as a floor expression, not a
  recommendation, per §7's own reasoning.
- Row-binding logic is duplicated a third time rather than extracted. xUnit
  v3 and MSTest share a `MethodInfo`-shaped binding input, but `Compono.TUnit`'s
  input shape is materially different, so three packages still don't expose
  a sufficiently stable common abstraction boundary — extracting now would
  risk generalizing around superficial similarity rather than a proven
  shared model. A small, deliberate maintenance cost, revisited if a fourth
  data point (NUnit) shows the pattern actually generalizes.

## Pros and Cons of the Options

### `ITestDataSource`-implementing attribute (chosen)

- Good, because every mechanism it needs is public, documented, stable
  MSTest surface, spike-verified under both MTP and VSTest.
- Good, because it reuses `CompositionRow`/`RowInvokerRegistry`/`BindingPlan`
  almost entirely unchanged.
- Bad, because it inherits MSTest's own discovery/execution lifecycle
  quirk (possible repeat `GetData` evaluation) with no supported deferral
  mechanism to work around it.

### Custom `TestMethodAttribute`/execution-layer override

- Good, because it could, in principle, control exactly when composition
  happens, closing §9's repeat-evaluation gap.
- Bad, because it requires deep coupling to unstable/internal MSTest
  test-execution machinery for a benefit no target scenario actually needs.
- Bad, because it is strictly more invasive than `ITestDataSource` for zero
  additional required capability (composed parameters, `[Shared]`,
  `Share<T>()`, profiles, diagnostics, display names are all already
  reachable through `ITestDataSource` alone).

### `[DataTestMethod]` + `[Compose]`

- Bad, because `[DataTestMethod]` is being actively removed from MSTest
  (tracked upstream, `MSTEST0044`) — requiring it would be needless,
  discouraged-by-the-framework ceremony with no compensating benefit.

### `MSTest` umbrella package dependency

- Bad, because it transitively pulls `MSTest.TestAdapter` (a consumer
  test-project/runner concern) into every `Compono.MSTest` consumer's
  dependency graph unnecessarily — confirmed via a real spike project's
  `project.assets.json`.

### `MSTest.TestFramework` `4.x`-only floor

- Bad, because no capability this design actually uses postdates
  `MSTest.TestFramework` v1.2.1 — an arbitrary floor that would exclude
  real, recently-parallel-maintained 3.x consumers for no technical reason.

## Deferred Decisions and Non-goals

- **Per-parameter mixing of `[DataRow]`/`[DynamicData]` values with
  `[Compose]`-composed parameters** — not supported; would require the
  rejected custom-`TestMethodAttribute` option. Mirrors
  `Compono.XunitV3`/`Compono.TUnit`'s existing scope for the same boundary.
- **Class/assembly-level `[Compose<TProfile>]`** — method-level only,
  matching both existing packages.
- **Automatic `TestContext`/framework-value injection through `[Compose]`**
  — deliberately not built (§13); a consumer uses MSTest's own
  constructor/property injection.
- **A supported deferred/lazy `ITestDataSource` evaluation mechanism** —
  none exists in current MSTest; not designed around, documented as a
  limitation instead (§9).
- **Async composition** — out of scope, per RESEARCH-0016's already-settled
  principle; `ITestDataSource.GetData` has no async door regardless.
- **Extracting a shared `BindingPlan`/`RowInvokers` base across all three
  framework packages** — deferred; xUnit v3 and MSTest share a
  `MethodInfo`-shaped binding input but `Compono.TUnit`'s input shape is
  materially different, so three packages don't yet expose a sufficiently
  stable common abstraction boundary. Revisit if NUnit (a fourth,
  independent data point) is researched and shows the pattern actually
  generalizes.
- **A `Compono.MSTest`-owned disposal mechanism** — not built; the existing
  non-ownership stance (RESEARCH-0015) is unchanged and unaffected by
  MSTest's discovery-time-composition possibility.

## Amendment 1 (2026-09-02): `MSTest.TestFramework` minimum raised to `4.0.0` — the `3.0.0` floor is binary-incompatible, not just an untested range

**What changed**: §7's accepted minimum supported version is raised from
`MSTest.TestFramework` `3.0.0` to `MSTest.TestFramework` `4.0.0`. Every
other decision in this ADR (§1-§6, §8-§16) is unaffected and stands as
originally written.

**The evidence, found during implementation, not anticipated by §7's
original text**: `MSTest.TestFramework`'s 3.x line and 4.x line ship under
**two different assembly identities**, not just two different version
numbers of the same assembly:

- Every `3.x` release, including the latest (`3.11.1`), compiles its
  framework types into `Microsoft.VisualStudio.TestPlatform.TestFramework.dll`
  (assembly name `Microsoft.VisualStudio.TestPlatform.TestFramework`) —
  confirmed by direct inspection of the packed `.nupkg` for both `3.0.0`
  and `3.11.1`.
- Every `4.x` release (`4.0.0` through the current `4.3.3`) compiles the
  same framework types into a renamed `MSTest.TestFramework.dll` (assembly
  name `MSTest.TestFramework`) — confirmed by direct inspection of the
  packed `4.3.3` `.nupkg`.
- **No type-forwarder/facade assembly bridges the two.** Neither package
  ships a compatibility shim; `4.3.3`'s `.nupkg` contains exactly one
  physical assembly per target framework, under the new name only.

**Why this invalidates §7's original reasoning, not just its evidence
base**: §7 stated "No breaking change to `ITestDataSource` itself was
found between 3.x and 4.x" and treated that as sufficient to support both
lines from one compiled `Compono.MSTest.dll`. That claim is true at the
C#-signature level and false at the level that actually determines whether
one compiled binary works against both: `Compono.MSTest.ComposeAttribute`
compiled against `3.x`'s `ITestDataSource` implements a **different,
assembly-identity-scoped interface** than the one a `4.x` test host checks
for at runtime — not a version-skew warning or a reflection fallback
opportunity, a hard `FileNotFoundException`/type-identity mismatch.
Reproduced directly during implementation: building `Compono.MSTest`
against the CPM-resolved `3.0.0` floor and running a real MSTest test
project pulling in the `MSTest` `4.3.3` meta-package failed immediately
with `FileNotFoundException: Microsoft.VisualStudio.TestPlatform.TestFramework,
Version=14.0.0.0`; forcing both projects onto `4.3.3` made the identical
test suite pass with zero other changes. One compiled `Compono.MSTest.dll`
genuinely cannot serve a `3.x` consumer and a `4.x` consumer at once —
§7's original "a tested range, not a bare unbounded floor" framing assumed
ordinary NuGet/API backward compatibility across the range, which does not
hold here.

**Why the fix is raising the floor, not shipping dual binaries**: the
two lines being binary-incompatible means true "both-floors" support would
require either multiple compiled variants (a `3.x`-targeting build and a
`4.x`-targeting build, published and selected somehow) or equivalent
TFM/asset-conditional packaging complexity — real, ongoing packaging cost
for `Compono.MSTest`'s *first* release. Weighed against that: `MSTest`
`3.x` has meaningful existing real-world usage (not being dismissed here)
and §7's own already-accepted caveat that its latest release, `3.11.1`,
has had no patch since 2025-11-11 (roughly nine months) while `4.x`
continues active development — `4.x` is the currently-maintained line and
the one `dotnet new mstest`/MTP default to today. For a new, pre-1.0
integration package with no existing `Compono.MSTest` consumers to
preserve compatibility for, the dual-binary/conditional-packaging cost is
not justified by preserving a floor whose own package line is already
accepted as de facto unmaintained. `Compono.MSTest` therefore establishes
`MSTest.TestFramework` `4.0.0` as its support boundary; a consumer on
`MSTest` `3.x` must upgrade to `4.x` to use `Compono.MSTest` — this is a
deliberate product decision, not a claim that `3.x` is irrelevant or
unused.

**What is unaffected**: §8's MTP/VSTest support policy is untouched — this
finding is about the `MSTest.TestFramework` *major version* a consumer
references, not which execution platform they use; `Compono.MSTest`
continues to support both MTP and the classic VSTest adapter, under
`4.0.0`+, exactly as §8 already decided. §7's own "no breaking change to
`TestMethodAttribute`/`ITestDataSource`'s two members since `v1.2.1`"
capability-matrix claim stands unchanged for the `4.x` line itself — it
was never about the `3.x`→`4.x` boundary this amendment addresses. The
"Accepted caveat" paragraph in §7 (3.x de facto unmaintained) is now the
amendment's own rationale rather than a caveat carried alongside a lower
floor.

**Revised minimum supported version statement (supersedes §7's own
"`3.0.0` — not an even-older 1.x/2.x floor, and not `4.x`" framing for
this one point only)**: `MSTest.TestFramework` `4.0.0` is the accepted
floor. The `1.x`/`2.x`/`3.x` ranges are all rejected — `1.x`/`2.x` for the
reason §7 already gave (legacy, pre-.NET-Core-consolidation generations
with no realistic current adoption signal), `3.x` for this amendment's own
binary-incompatibility finding.

## Amendment 2 (2026-09-02): `GetDisplayName` is a discovery-time surface, not visible in ordinary execution output

**What changed**: a precision correction to §15's "This surfaces the row's
seed directly in Test Explorer/`dotnet test` output" sentence. That
sentence is true for Test Explorer/discovery-mode listing but overstates
`dotnet test`'s own ordinary *execution* output — every other decision in
§15 (display-name format, no `TestContext.Properties` usage, parity under
MTP/VSTest) stands unchanged.

**The evidence, found during implementation**: `GetDisplayName` is called
by MSTest during *discovery*/listing — `--list-tests` (MTP), `dotnet
vstest -lt` (classic VSTest adapter), and Visual Studio Test Explorer's own
tree population — confirmed directly under both runners, e.g.
`ComposesTwoStrings_RealRun (Compono, seed: 1913922119)` appearing in
`--list-tests` output. It is **not** called during an ordinary `dotnet
test`/`dotnet vstest` *execution* run, under either MTP or the classic
VSTest adapter — confirmed by direct instrumentation (a hit/miss counter
on the `GetDisplayName`→`SeedByRow` lookup, reset per test class, read
back after a real run): zero hits during ordinary execution, only during
an explicit discovery/listing invocation.

**Why this doesn't change §15's actual decision**: `GetDisplayName`
remains the correct, and only, seed-reporting hook — MSTest's own
`ITestDataSource` contract simply doesn't call it at execution time, the
same way it doesn't hand `Compono.MSTest` any other execution-time
metadata-reporting surface. This is a fact about *when MSTest itself
invokes the hook*, not a design gap in `Compono.MSTest`'s own
implementation of it. A composition *failure*'s seed is unaffected by this
finding — `CompositionException.WithSeedInMessage` still puts the seed
directly in the thrown exception's message on every pre-composition and
composition-pipeline failure, independent of `GetDisplayName`, and that
path *is* visible in ordinary execution output.

**Correction to §15's own text**: read "This surfaces the row's seed
directly in Test Explorer/`dotnet test` output" as "This surfaces the
row's seed in Test Explorer/discovery-mode listing output (`--list-tests`,
`dotnet vstest -lt`) — not in an ordinary `dotnet test`/`dotnet vstest`
execution run's own console output, which never calls `GetDisplayName` at
all." Downstream documentation (`docs/packages/compono-mstest.md`,
`skills/compono/references/mstest.md`) states the corrected version
directly.

## Amendment 3 (2026-09-02): §9's repeat-composition contract corrected — the VSTest-only attribution was false, and the "reproducible values" claim overstated the unpinned case

**What changed**: two precision corrections to §9's original text, both
raised by an automated PR review (Codex) against PLAN-0057's own
implementation PR and confirmed correct against this repo's own recorded
evidence. §9's core contract statement (the blockquote — "MSTest may
invoke `GetData` more than once across separately-invoked discovery and
execution sessions") is unaffected and stands unchanged; only two of its
supporting claims are corrected.

**Correction 1 — the doubling behavior is not VSTest-specific.** §9's
original text says the separate-discovery-then-execution doubling "is
specific to the classic VSTest adapter's lack of cross-invocation
discovery-result caching, not a property of every VSTest-mode test run."
PLAN-0057's own real, PID-tagged implementation evidence (recorded in its
Notes, and already reflected correctly in `docs/packages/compono-mstest.md`
and `skills/compono/references/mstest.md` — only this ADR's own text
lagged) found the opposite: running a separate discovery process
(`--list-tests`) followed by a separate execution process produces **two**
`GetData` calls per method under **both** MTP and the classic VSTest
adapter, confirmed via distinct OS process IDs in each case. The doubling
is a consequence of *separate discovery and execution being separate
process invocations at all* — nothing MTP-or-VSTest-specific about it, and
specifically not a classic-VSTest-only caching gap as originally stated.
**Corrected statement**: Compono.MSTest does not guarantee exactly-once
`ITestDataSource.GetData` invocation across separately-invoked discovery
and execution sessions. Either supported runner (MTP or the classic VSTest
adapter) may evaluate the data source independently in those sessions;
each invocation creates a fresh composition graph. Every other consequence
§9 already lists stands unchanged: no cross-session caching is introduced;
no static graph state is introduced; `Register<T>()` factories may run
more than once; providers may run more than once; `[Shared]`/`Share<T>()`
identity applies within one graph only; both runners remain fully
supported, and neither receives special-cased Compono execution
architecture.

**Correction 2 — "logically equivalent values" overstated the unpinned
case.** §9's "What stays correct regardless" paragraph states "Deterministic
seeding means two independently-composed rows for the same test case are
logically equivalent (same seed → same generated values) ... the *values*
a consumer sees stay consistent across repeat evaluations." This is true
**only when `Seed` is explicitly pinned** (`[Compose(Seed = N)]`) — every
independent call then genuinely uses the identical configured seed and
produces the same deterministic values. It is **not** true for an
unpinned, plain `[Compose]`: `ComposeAttribute` generates a fresh,
independent, non-negative random seed on every `GetData` call (no
`CompositionBuilder.WithSeed` call), so a discovery-time row and a later,
separately-invoked execution-time row generally hold **different**
composed values, not "logically equivalent" ones — and a seed displayed
for a discovery-time row (per Amendment 2, discovery/listing is the only
context `GetDisplayName` is even called in) must not be presented as
sufficient to reproduce a later, independently-composed execution row.
**Corrected statement**: reproducibility of composed *values* across
separate `GetData` invocations requires an explicitly pinned `Seed`; it is
not a property of Compono's seeding mechanism in general. What *does* hold
unconditionally, pinned or not: each call's own `CompositionRow` keeps
`[Shared]`/`Share<T>()` internally correct within itself, and a
composition *failure*'s own `CompositionException` always carries the
seed that specific execution actually used
(`CompositionException.WithSeedInMessage`), independent of any
discovery-time display name — that execution-time exception message,
not a discovery-time seed, is the correct reproduction value to act on
when `Seed` isn't pinned.

**Downstream documentation already correct, now made explicit here**:
`docs/packages/compono-mstest.md`'s "Discovery/execution repeat-composition
behavior" and "Seed and display name" sections, and
`skills/compono/references/mstest.md`'s equivalent passage, were updated
in the same pass as this amendment to state both corrected claims
directly, distinguishing the unpinned and pinned cases and pointing at
`CompositionException`'s own seed for execution-time reproduction.

## Links

- [RESEARCH-0017](../research/0017-mstest-integration-viability-research.md)
  — the accepted evidence base this ADR's every decision cites; not
  re-litigated, only converted into a durable architectural record.
- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)
  — `CompositionRow`/`Composer.CreateRow`, reused entirely unmodified; its
  own Positive Consequences anticipated exactly this kind of third-consumer
  reuse.
- [ADR-0022](0022-compono-xunit-package-design.md) — `Compono.XunitV3`'s
  package design, the template this ADR's scope, attribute family, seed
  policy, and diagnostics approach mirror.
- [ADR-0040](0040-compono-tunit-package-design.md) — `Compono.TUnit`'s
  package design; the "row-binding duplicated, not extracted" and
  "no automatic disposal ownership" precedents this ADR follows directly.
- [ADR-0041](0041-aot-safe-row-binding-dispatch.md) — `RowInvokerRegistry`,
  the AOT-safe dispatch mechanism this ADR adopts from the start (not the
  earlier per-package `MakeGenericMethod` pattern ADR-0022's original text
  used before being superseded).
- [ADR-0056](0056-composition-builder-share-graph-wide-sharing.md) —
  `CompositionBuilder.Share<T>()`, confirmed to need zero MSTest-specific
  work (RESEARCH-0017 §5); not reopened by this ADR.
- [RESEARCH-0015](../research/0015-disposal-ownership-research.md) — the
  non-ownership/disposal stance carried forward unchanged (§12).
- [RESEARCH-0016](../research/0016-async-composition-viability-research.md)
  — the synchronous-composition boundary carried forward unchanged (§11).
- [ADR-0019](0019-registrations-and-service-provider-injection.md) — the
  "caller owns the provider and its entire lifetime" contract §12's
  disposal reasoning depends on.
- `docs/mvp.md` — gets `Compono.MSTest` added as a scoped pre-1.0 package
  once implementation begins (§16).
