# RESEARCH-0036: Issue #161 Feature Intake — Test-Composition Gaps

**Status:** Intake complete, awaiting product-owner review. No ADR or Plan
has been created. This document is an intake summary, not a design.

**Governing process:**
[`docs/architecture/capability-admission.md`](../architecture/capability-admission.md).
**Trigger:** [#161](https://github.com/LayeredCraft/compono/issues/161),
real consumer evidence from an xUnit v3 / .NET 11 suite migrating to
Compono 1.3.2 (`Compono`, `Compono.XunitV3`, `Compono.TestDoubles`,
`Compono.Logging`, `Compono.Options`). Gate B (evidence) is met for all
four items by that migration. Gate A is assessed per item below.

The full per-item detail (scenario, acceptance criteria, research
questions, non-goals) lives in the GitHub sub-issues and is not repeated
here.

## 1. Validated gaps

| #161 item | Sub-issue | Confirmed? | Classification | Likely home |
|---|---|---|---|---|
| 4. Runtime fixture values in profiles | [#162](https://github.com/LayeredCraft/compono/issues/162) | Partly. A working path exists (§2) | Docs/discoverability gap. Whether an API is also needed is open | Docs + `compono` skill; `Compono.XunitV3`/`.Aot` only if an API is added |
| 3. Composed theory data rows | [#163](https://github.com/LayeredCraft/compono/issues/163) | Yes | Missing capability (intentional restriction, ADR-0022 Amendment 7) | `Compono.XunitV3`, `Compono.XunitV3.Aot`, `Compono.Generators` |
| 1. Cross-category log capture | [#164](https://github.com/LayeredCraft/compono/issues/164) | Yes | Missing capability / unsupported integration (ADR-0055 `ILoggerFactory` non-goal) | `Compono.Logging`, no new dependency |
| 2. Metrics testing | [#165](https://github.com/LayeredCraft/compono/issues/165) | Yes | Missing capability. Gate A is uncertain | Undecided: new package, existing package, or recipe |

Key repository facts behind these verdicts:

- **Logging.** Each `CapturingLogger`/`CapturingLogger<T>` owns its own
  internal `LogEntryCollector`, so no collector is shared across loggers.
  `CapturedLogEntry` has no category field. `ILoggerProvider`/`ILoggingBuilder`
  live in `Microsoft.Extensions.Logging.Abstractions` (already referenced),
  which already depends on `Microsoft.Extensions.DependencyInjection.Abstractions`.
  A provider-based integration therefore needs no new package or dependency.
- **Theory rows.** ADR-0022 rejected stacking Compose-family attributes
  and mixing them with `[InlineData]`/`[MemberData]`. Amendment 7 kept
  that rule only because no real call site existed, and #161 now
  supplies one. The AOT path's `RegisterTheoryDataRowFactory` factory
  already returns a row *collection*, so multi-row support can extend
  existing emission instead of adding a parallel mechanism. AOT inline
  values are still a deferred phase (PLAN-0066/0067).
- **Metrics.** Compono has no metrics surface. `IMeterFactory`/`Meter`/
  `MeterListener` are in-box on every target TFM. `MetricCollector<T>`
  (`Microsoft.Extensions.Diagnostics.Testing`) brings a large dependency
  set with it. RESEARCH-0015 §3 records a second consumer
  (`dynamodb-distributed-lock`, 13 `[Shared] Meter` usages) with the
  same need.
- **Ownership.** Composition is non-owning: ADR-0022 Amendment 4
  reverted auto-disposal, and RESEARCH-0015 recommended ownership as a
  post-1.0 additive feature with no ADR yet. #161's "dispose with the
  composition scope" proposal for metrics conflicts with the current
  contract.

## 2. Fixture-access spike (#162)

This was a throwaway project in the session scratchpad, not in the repo.
It used published `Compono`/`Compono.XunitV3`/`Compono.XunitV3.Aot` 1.3.2
and `xunit.v3.mtp-v2`/`xunit.v3.aot.mtp-v2` 4.0.1 on net10.0. A profile
whose `Register<DbSettings>(...)` factory calls
`TestContext.Current.GetFixture<DbFixture>()`:

| Package | Class fixture | Collection fixture (`IAsyncLifetime`) | Assembly fixture | Missing fixture |
|---|---|---|---|---|
| `Compono.XunitV3` | Pass | Pass | Pass | `CompositionException` wrapping the factory's own exception |
| `Compono.XunitV3.Aot` (JIT run, not native-published) | Pass | Pass | Not exercised (xUnit AOT generator compile error in the spike, not investigated) | Same |

Friction observed: in xUnit v3 4.x, `GetFixture<T>()` returns
`ValueTask<T?>`, so the synchronous factory needs
`.GetAwaiter().GetResult()`. The profile is also coupled to xUnit's
ambient `TestContext`, and Compono offers no typed diagnostic. Native
AOT publish was not verified.

## 3. Dependencies

- No hard dependencies between the four items.
- #162 ↔ #163 (soft): both feed non-constant values into profile
  configuration. Decide #163's non-constant-config question with #162's
  outcome in view.
  **Hard constraint from RESEARCH-0037 §12:** composed rows must keep
  execution-time (delayed) enumeration, otherwise fixture access in
  profiles (#162) breaks.
- #164 ↔ #165 (soft): similar collector and verification shape, and the
  consumer can drop `Microsoft.Extensions.Diagnostics.Testing` only once
  both are addressed.
- #165 → disposal ownership (conditional): only if the chosen lifetime
  model needs Compono-owned disposal (RESEARCH-0015, no ADR).
- #163 → AOT inline values (conditional): only if rows vary inline
  values on the AOT path.

## 4. Complexity, risk, and process

| Item | Complexity | Architectural risk | Process recommended |
|---|---|---|---|
| #162 | Low | Low | Docs first (cookbook + skill). Light dive + ADR only if an API is wanted |
| #164 | Low–medium | Low | Light/medium dive. ADR extending ADR-0055 |
| #163 | High | High (public attribute contract, JIT + AOT, cross-framework consistency) | Deep dive + ADR. Short empirical note on xUnit multi-source row semantics |
| #165 | Medium–high | Medium–high (package boundary, ownership) | Admission research doc first. ADR only if admitted |

## 5. Recommended research order

1. **#162**: cheapest, most frequent (container-backed integration
   tests), and largely closable with documentation. Its outcome also
   shapes #163.
2. **#163**: largest boilerplate reduction, but also the riskiest, so
   start the deep dive early.
3. **#164**: small and contained in one package. It touches packages
   disjoint from #163, so it can run in parallel with #163 as a light
   track.
4. **#165**: admission research first. Gate A and the ownership model
   are genuinely open.

This matches the product owner's preliminary order. The only adjustments
are #162's narrowed, docs-first scope and allowing #164 to run in
parallel with #163.

## 6. Open product-owner decisions

1. #162: is documentation of the existing `GetFixture` path sufficient,
   or is a typed / framework-neutral Compono API wanted?
2. #163: is reversing ADR-0022's single-Compose-family rule acceptable in
   principle, and is cross-framework parity (TUnit/NUnit/MSTest) required
   in the same effort?
3. #164: should host-created and composed loggers share one collector by
   default, opt-in, or never?
4. #165: is a recipe-only outcome acceptable if Gate A's
   "meaningful abstraction" isn't cleared? Is any Compono-owned disposal
   acceptable, or must the design stay strictly non-owning?

## Links

- Issues: [#161](https://github.com/LayeredCraft/compono/issues/161) (umbrella),
  [#162](https://github.com/LayeredCraft/compono/issues/162),
  [#163](https://github.com/LayeredCraft/compono/issues/163),
  [#164](https://github.com/LayeredCraft/compono/issues/164),
  [#165](https://github.com/LayeredCraft/compono/issues/165)
- [ADR-0022](../adr/0022-compono-xunit-package-design.md) (Amendments 4, 7)
- [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md)
- [ADR-0055](../adr/0055-compono-logging-testing-support-package.md)
- [ADR-0066](../adr/0066-compono-xunitv3-aot-package-architecture.md),
  [ADR-0067](../adr/0067-compono-xunitv3-aot-profile-support.md)
- [RESEARCH-0015](0015-disposal-ownership-research.md),
  [RESEARCH-0016](0016-async-composition-viability-research.md)
