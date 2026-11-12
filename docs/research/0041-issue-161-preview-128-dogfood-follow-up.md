# RESEARCH-0041: Issue #161 preview.128 Dogfood Follow-Up — Four Findings

**Status:** Complete. Every finding is triaged. The maintainer decided all
four on 2026-11-12: each is resolved by documentation, with no code, public
API, or ADR change. Execution is tracked by
[PLAN-0076](../plans/0076-issue-161-preview-128-dogfood-follow-up-impl-plan.md).

**Trigger:** the consumer's follow-up comment on
[#161](https://github.com/LayeredCraft/compono/issues/161#issuecomment-6832495571).
The consumer upgraded to `1.4.0-preview.128` (xUnit v3 JIT, with Compono,
Logging, Metrics, Options, TestDoubles, and XunitV3). Every PR #181 item
behaved as documented: 231/231 tests passed, with 0 warnings and no `CMP`
diagnostics. Four follow-up observations remain. None is a regression.

**Release state when researched:** `main` at `546af41` (PR #181 merged).
`v1.4.0` hasn't shipped stable.

## Summary

| # | Finding | Kind | Disposition |
|---|---|---|---|
| 1 | Missing `UseMetrics()` gives a generic error | Documentation gap (diagnostic enhancement declined) | **DOCUMENT** |
| 2 | Exposing the row's `TConfig` takes boilerplate | Convenience over a supported pattern | **DOCUMENT** (opt-in API **REJECTED** for now) |
| 3 | `UseMetrics()` plus `Share<IMeterFactory>()` | Documentation defect (misleading lead example) | **DOCUMENT** |
| 4 | A failed `ComposeData` row collapses the theory count | Expected behavior (ADR-0070 item 9) | **DOCUMENT**; per-row isolation **DEFERRED** |

No runtime defect was found. No provider precedence, sharing, or ownership
contract changes.

## Reproduction harness

A throwaway xUnit v3 project lives outside the repository, in the session
scratchpad. It targets `net11.0` and uses `xunit.v3.mtp-v2` 4.0.1. It takes
`ProjectReference`s to `src/Compono`, `src/Compono.Metrics`, and
`src/Compono.XunitV3`, plus `src/Compono.Generators` as an analyzer, with
`ComponoMetricsOwnsMeterFactory=true`. The harness changed no tracked file.
Results are quoted below.

## Finding 1: missing `UseMetrics()` gives a generic error

**Consumer report.** If `UseMetrics()` is left out, composition throws:

```
Compono.CompositionException : No registration, configuration rule, semantic provider,
test-double provider, built-in provider, or generated plan could satisfy 'IMeterFactory'.
```

The consumer asks for a hint naming `UseMetrics()`, and the same for
`ILogger` → `UseLogging()`.

**Reproduced.** `Composer.Create(_ => { }).Create<OrderService>()`, where
`OrderService(IMeterFactory)`, gives exactly that message.

**Where it comes from.** The message is built once, in
`CompositionContext.ResolveCore` (`src/Compono/CompositionContext.cs:679-681`),
after stages 3 to 8 have all returned `NotHandled`. Core knows only the
requested `Type`. It has no notion of which package owns which type, and the
modularity rule (AGENTS.md: core never references or knows about an
integration package) forbids adding one.

**What the generator knows.** `ComponoMetricsOwnsMeterFactory` reaches the
generator as `GeneratorFeatureFlags.MetricsOwnsMeterFactory`
(`ComponoIncrementalGenerator.cs:44-48`). `TransitiveClosureWalker` uses it
only to skip generating an `IMeterFactory` double (`TransitiveClosureWalker.cs:212-217`).
`ComponoGeneratedLogging` works the same way for `ILogger`/`ILogger<T>`
(ADR-0055 Amendment 4). The generator could therefore produce ownership
facts. Today nothing carries them to the runtime.

**Options considered.**

1. **Status quo plus docs.** Add a troubleshooting entry keyed on the exact
   message: `IMeterFactory` → `UseMetrics()`, `ILogger`/`ILogger<T>` →
   `UseLogging()`. No code.
2. **Generator-emitted hint.** Add a generator-facing core hook (an
   `EditorBrowsable(Never)` type-to-hint registry). The generator emits a
   `[ModuleInitializer]` that registers "IMeterFactory is supplied by
   Compono.Metrics; call `UseMetrics()`" when the flag is on and the type is
   reached. Core appends the hint only on a final no-provider failure for
   that exact type. Core stays package-agnostic, because the text comes from
   the generator. Costs: new core surface used only for messages, a generator
   emitter, an ADR amendment, and JIT and AOT tests.
3. **Generic core wording.** For an interface, abstract type, or delegate
   that nothing handled, add "register it, or add the test-double provider
   that supplies it". There's no coupling and it helps every interface, but
   it can't name `UseMetrics()`.
4. **Compile-time diagnostic.** Rejected. Whether `UseMetrics()` was called
   is a runtime builder fact. A profile can live in another assembly, and a
   syntactic scan would raise false positives.

**Avoiding misleading guidance.** Every runtime option (2 and 3) fires only
after the whole pipeline has failed. A consumer who supplies `IMeterFactory`
through `Register<T>()`, `UseServiceProvider`, or another stage-6 provider
never reaches that point, so they never see the hint. The docs entry carries
the same "if you meant Compono.Metrics to supply it" wording.

**Decision (maintainer, 2026-11-12): option 1, documentation only.** The
error is accurate, appears at the right moment, and names the type. A
troubleshooting entry fixes the lookup cost without new core surface. Option
2 stays recorded as the smallest code path if diagnostic evidence grows. If
it's ever built, it applies to both Logging and Metrics.

**Incidental drift.** The sample message in
`docs/troubleshooting/common-errors.md` ("No registration, semantic
provider, …") is missing "configuration rule", which the real message
includes. The maintainer confirmed on 2026-11-12 that correcting it is part
of the finding 1 docs change (PLAN-0076 D1).

**Public API / compatibility / AOT / performance.** None.

## Finding 2: reading the row's `TConfig` takes boilerplate

**Consumer report.** Each profile adds `builder.Register(() => config)`, and
the test declares an extra parameter. The consumer calls this minor.

**How `TConfig` flows.** For `ComposeData<TProfile, TConfig>`, row element 0
is the `TConfig`. It's passed to the `TProfile` constructor
(`ConfigProfileBinder`, ADR-0036, ADR-0070 item 4) and isn't bound to any
test parameter. `[Compose<TProfile, TConfig>(args)]` builds `TConfig` from
attribute constants the same way. The profile is the only code that sees
the instance.

**Already supported.** RESEARCH-0040 finding 2 and PR #181 documented the
recipe: the profile registers `config`, and a composed `TConfig` parameter
resolves at stage 3 to that row's own instance. The consumer confirmed it
works. It's in `docs/cookbook/vary-composed-configuration-per-theory-row.md`,
`docs/packages/compono-xunitv3.md`, and `skills/compono/references/xunit-v3.md`.

**Why automatic registration conflicts.**

- **Duplicate registration.** `Register<T>()` is strict, so a duplicate is a
  build-time error. Auto-registering `TConfig` would break every consumer
  who already wrote the documented recipe, this consumer included.
- **Graph-wide side effect.** A stage-3 registration answers every
  `TConfig` request in the graph, not just a test parameter. A `TConfig`
  that is also a SUT dependency (an options record, for example) would
  silently change resolution. That violates "predictability over magic".
- **Surface.** An explicit opt-in (for example `ExposeConfig = true`) means
  new public properties on `Compose<,>` and `ComposeData<,>`, in every
  adapter that has them (XunitV3, XunitV3.Aot, TUnit, NUnit, MSTest). It
  also means generator emission for AOT and parity tests. That's a lot of
  surface to remove one line per profile.

**Assessment.** This is convenience around a supported pattern, not a
missing capability. The test parameter is inherent: it's how a test reads
any composed value.

**Decision (maintainer, 2026-11-12): no new API.**
`builder.Register(() => config)` in the profile, plus a composed `TConfig`
parameter, stays the **primary documented recommendation**. For consumers
who repeat that line across many profiles, the docs may also show an
**optional consumer-side convenience**. It's the consumer's own base
profile, not a Compono type, and Compono won't ship it as API:

```csharp
public abstract class ConfigProfile<TConfig>(TConfig config) : ICompositionProfile
    where TConfig : class
{
    protected TConfig Config => config;

    public void Configure(CompositionBuilder builder)
    {
        builder.Register(() => config);
        ConfigureCore(builder);
    }

    protected abstract void ConfigureCore(CompositionBuilder builder);
}
```

The harness ran this base profile with a 2-row
`ComposeData<WinProfile, Win>` theory (JIT). Each row's composed `Win`
parameter was that row's own config, and both rows passed.

The decline of an opt-in API is recorded here. Revisit only if several
consumers show the explicit line is a real burden.

**Public API / compatibility / AOT / performance.** None. The recipe is an
ordinary registration. It already works on JIT and AOT.

## Finding 3: is `Share<IMeterFactory>()` still needed with `UseMetrics()`?

**Consumer report.** The consumer calls both `builder.UseMetrics()` and
`builder.Share<IMeterFactory>()`, and asks whether both are needed.

**Existing semantics.**

- `MetricsProvider` returns a **new** `CapturingMeterFactory` per request
  (`src/Compono.Metrics/MetricsProvider.cs`). ADR-0072 "Lifecycle and
  ownership" makes this deliberate: composition is non-owning, and sharing
  is opt-in.
- `[Shared]` parameters are composed **first**, before any other
  parameter, whatever their declaration order (ADR-0022 binding step 6).
  The result is stored in the row's shared scope, so every later request
  for that type in the row gets the same instance.
- `Share<T>()` (ADR-0056) makes the first request for `T` in a graph the
  shared one, from any source and in any order, with no `[Shared]` needed.
  It also applies to `Composer.Create<T>()` graphs, which have no
  parameters.

**Reproduced (harness, all passing):**

| Setup | Result |
|---|---|
| `UseMetrics()` only; `(OrderService sut, [Shared] IMeterFactory meters)` | `meters` is the SUT's factory, even when declared after the SUT |
| `UseMetrics()` only; `([Shared] IMeterFactory meters, Coordinator c)` with two nested consumers | Both consumers get `meters` |
| `UseMetrics().Share<IMeterFactory>()`; undecorated `(IMeterFactory meters, Coordinator c)` | Both consumers get `meters` |
| `UseMetrics()` only; undecorated `IMeterFactory` plus SUT | Different instances |
| `Composer.Create(b => b.UseMetrics()).Create<Coordinator>()` | The two nested consumers get different factories |

**Multiple factories and measurement collection.** Each
`CapturingMeterFactory` captures only the meters it created. If two
components get different factories, a test that asserts against one of them
misses the other's measurements. That's why sharing matters. It doesn't
matter which mechanism provides the sharing.

**Generated doubles and provider ordering.** These aren't involved.
`ComponoMetricsOwnsMeterFactory` already guarantees that no generated
`IMeterFactory` double exists (ADR-0072 Amendment 5). Sharing caches
whatever stage 6 produced. It doesn't change which provider wins.

**Answer to the consumer.** Use one or the other:

- In a `[Compose]`-family test that takes `[Shared] IMeterFactory`, the
  attribute alone is enough. `Share<IMeterFactory>()` is redundant but
  harmless.
- Use `Share<IMeterFactory>()` in a profile for profile-wide intent: tests
  that take an undecorated `IMeterFactory`, or `Composer.Create` graphs with
  several consumers.

**Root cause of the confusion.** The lead example in
`docs/packages/compono-metrics.md` (lines 54-70) uses both together without
saying why. `skills/compono/references/metrics.md` ("`Share<T>()` and
`[Shared]`") says "Prefer `Share<IMeterFactory>()`", while its own examples
use `[Shared]`. Those are documentation defects.

**Decision (maintainer, 2026-11-12): fix the docs only.** Test placement:
adapter-level `[Shared]` sharing coverage, if any is needed, goes in
`Compono.XunitV3.Tests` with a stand-in type. `Compono.Metrics.Tests` gets
no `Compono.XunitV3` dependency and keeps only metrics lifetime tests that
don't involve an adapter. `UseMetrics()`
keeps composing a new factory per request. Making it share by default would
reverse ADR-0072's non-owning lifecycle and change `Composer.Create`
behavior, so it was declined. An opt-in overload was declined because it
would duplicate `Share<T>()`.

**Public API / compatibility / AOT / performance.** None.

## Finding 4: a failed `ComposeData` row collapses the theory count

**Consumer report.** When a row fails to compose (for example because
`UseMetrics()` is missing), the theory is reported once instead of once per
row. The Application project's count fell from 49 to 42.

**Reproduced (xUnit 4.0.1, JIT).** A 3-row
`ComposeData<CfgProfile, Cfg>` theory where only row 1 omits `UseMetrics()`:

```
Probe.F4.ComposeDataRows [FAIL]
  Compono.CompositionException : ComposeData row 1 from 'Probe.F4.Rows' for
  'Probe.F4.ComposeDataRows' failed: No registration, ... could satisfy 'IMeterFactory'.
  Seed: 1556484067
```

One result is reported. Rows 0 and 2, which would pass, **never run**. The
same theory with every row valid reports three results.

**Where the failure happens.** At execution time, during row
materialization, not during discovery or the test body:

- `ComposeDataAttribute.SupportsDiscoveryEnumeration()` returns `false`
  (`src/Compono.XunitV3/ComposeDataAttribute.cs:100`). Discovery therefore
  creates **one** delay-enumerated test case for the theory (RESEARCH-0038
  §2.2).
- At execution, xUnit calls `GetData`. `GetDataCore` composes every row in a
  loop (`ComposeDataAttribute.cs:149-156`). The first `CompositionException`
  aborts the loop, so xUnit gets no rows and reports the single test case as
  failed.

**Is xUnit in control of the count?** Yes. A delay-enumerated theory's result
count is however many rows `GetData` returns. When it throws, xUnit reports
the test case itself. That's xUnit's model, not a Compono choice.

**Scope.**

- Every data attribute on the method is affected. One non-discoverable
  source collapses the whole method into one test case, so `[InlineData]`
  rows on the same method fail with it (RESEARCH-0038 §2.2).
- `[Compose]` and `[Compose<TProfile>]` produce one row, so there's no
  count to collapse. The failure behavior is otherwise the same.
- `Compono.XunitV3.Aot` follows the same contract (ADR-0070 item 9 applies
  to both engines).

**Status: expected behavior.** ADR-0070 item 9 ("the theory test case fails
as a unit. No placeholder rows, no custom invokers") and its Negative
Consequences ("Whole-theory failure granularity") accept this explicitly.
The maintainer rejected per-row alternatives in RESEARCH-0038 Q12. It's
already documented in `docs/packages/compono-xunitv3.md` ("Failures, seeds,
and row labels") and the skill. Neither says plainly that the **reported
count drops** or that other data attributes' rows go down too.

**New evidence: deferred per-row failure prototype (JIT only).** Two
mechanisms were tried in the harness:

1. **A row whose `ITheoryDataRow.GetData()` throws lazily.** This still
   collapses to one failure. xUnit reads every row's data before running
   any. **Not viable.**
2. **A placeholder row plus a before-test hook.** `GetData` catches the row
   failure, stores the exception in a static dictionary keyed by a GUID, and
   returns a placeholder row with typed default arguments and a trait
   `compono.rowFailure = <id>`. The same attribute implements
   `IBeforeAfterTestAttribute`. `Before` looks up the trait and rethrows the
   stored exception. Result: **3 results, 2 passed and 1 failed with the
   real `CompositionException`.** The test body didn't run for the failing
   row.

**Why the prototype doesn't justify reopening ADR-0070 now.**

- **Display identity.** The failing row shows placeholder arguments
  (`DeferredRows(n: 0, s: null)`) unless a synthetic `TestDisplayName` is
  set. Either way the name is invented, not the row's real identity.
- **Typed placeholders.** Every parameter needs a value its type accepts.
  For a value type that means creating a default instance through
  reflection on JIT and generated code on AOT. A `null` passed for a value
  type fails conversion with a misleading error.
- **Out-of-band state.** A process-wide static dictionary that leaks if the
  row is filtered out or never runs. A synthetic trait also becomes visible
  in Test Explorer and filter output.
- **Hook ordering.** Other `BeforeAfterTest` attributes on the class or
  assembly run around Compono's hook. Their side effects would run for a
  row that can't execute.
- **AOT parity unknown.** It hasn't been checked whether xUnit's AOT core
  honors `IBeforeAfterTestAttribute` on a data attribute through the
  generated registration path. ADR-0070 item 13 requires engine parity.
- **It reverses a recorded decision (Q12)** on one report where the
  consumer calls the effect "harmless but surprising".

**Other option considered.** Composing every row and throwing one exception
that lists every failing row keeps the xUnit model, but the count still
collapses and valid rows still don't run. It wasn't chosen.

**Decision (maintainer, 2026-11-12): document, and keep ADR-0070
unchanged.** The user docs to update are the xUnit v3 guide, the AOT
guidance, troubleshooting, and the skill. They must state that a composition
failure stops the remaining rows from running and can lower the reported
test count. They must describe whole-theory failure: the theory fails once,
with the first failing row's exception. They must not imply that each row
failed on its own. Record the
prototype and its limits here. Per-row isolation is a **possible future
investigation**, to reopen only if more real-world evidence appears (for
example several consumers, or CI tooling that depends on stable per-row
counts). That investigation would start from mechanism 2 above, and would
first settle AOT parity, display identity, and state lifetime.

**Public API / compatibility / AOT / performance.** None.

## Test coverage if the deferred options are ever implemented

- **F1 option 2:** generator tests (flag on emits the hint registration,
  flag off or type not reached emits nothing); runtime tests (hint present
  only on a no-provider failure for that exact type; absent when
  `Register<T>`, `UseServiceProvider`, or another provider supplies it);
  Logging parity; an AOT smoke test.
- **F2 opt-in:** each adapter and engine (JIT, AOT, TUnit, NUnit, MSTest);
  a duplicate-registration conflict with an explicit `Register`; a
  `TConfig` that is also a SUT dependency.
- **F4 per-row isolation:** result counts with mixed good and bad rows;
  display names; value-type placeholders; `[InlineData]` plus `ComposeData`
  on one method; filtered and explicit rows (state cleanup); other
  `BeforeAfterTest` attributes present; AOT parity.

## Links

- [#161 preview.128 comment](https://github.com/LayeredCraft/compono/issues/161#issuecomment-6832495571)
- [RESEARCH-0040](0040-issue-161-1-4-dogfood-follow-up.md) (findings 1-3 precedents)
- [RESEARCH-0038](0038-issue-163-composed-theory-data-rows.md) §2.2 and Q12
- [ADR-0022](../adr/0022-compono-xunit-package-design.md) (binding step 6, `[Shared]` first)
- [ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md) (`Share<T>()`)
- [ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md) item 9
- [ADR-0072](../adr/0072-compono-metrics-testing-support-package.md) "Lifecycle and ownership", Amendment 5
- [PLAN-0076](../plans/0076-issue-161-preview-128-dogfood-follow-up-impl-plan.md)
