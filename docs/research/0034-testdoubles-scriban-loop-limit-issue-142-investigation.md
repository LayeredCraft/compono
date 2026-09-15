# [RESEARCH-0034] Compono.TestDoubles Scriban Loop-Limit Failure (Issue #142) Investigation

**Status:** Done

**Date:** 2026-09-15

## Scope

Issue [#142](https://github.com/LayeredCraft/compono/issues/142) reports that generating a
test double for a large interface (`Amazon.S3.IAmazonS3`, ~171 members) crashes
`ComponoIncrementalGenerator` with:

```
CSC : warning CS8785:
Generator 'ComponoIncrementalGenerator' failed to generate source.
Exception was ScriptRuntimeException:
TestDouble.scriban(...):
Exceeding number of iteration limit `1000` for loop statement.
```

and that the crash silently discards **every** other generated test double in the same
compilation, producing a wave of unrelated `CS1061`/`CS7036` errors. This is a
research-only investigation (per the governing request): reproduce, bisect the regression,
quantify the exact iteration mechanism, and evaluate candidate fixes. No ADR, no plan, no
production-code change, no commit/push.

## 1. Executive summary

- **Reproduced** directly against the real consumer
  (`ncipollina/lightsaber-skill`, current `main`, Compono 1.3.0) — identical failure text
  and identical secondary fallout (unrelated `Configure()`/`Verify()` extensions vanish
  across the whole compilation, not just `IAmazonS3`'s own).
- **Root cause**: `TemplateHelper.Render` calls Scriban's `Template.Render(model)` with a
  **default, unconfigured `TemplateContext`** (`LoopLimit = 1000`). Scriban's loop-step
  counter (`TemplateContext._loopStep`) is **not scoped per `{{ for }}` tag** — it resets to
  zero only when loop nesting depth returns to `0`. `TestDouble.scriban`'s single largest
  block is one `{{~ for member in members ~}}` loop (currently lines 218-506) that emits
  every interface member's explicit-implementation dispatch body, and that block itself
  contains multiple nested `{{ for p in member.parameters }}` loops per member (signature,
  matcher-condition build, callback-argument forwarding, etc.). Because none of those
  nested loops is itself top-level, **every iteration of every nested per-member loop, for
  every member processed so far in that single outer loop, shares one Scriban-wide
  counter**. A wide interface simply accumulates enough nested-loop steps before the outer
  loop finishes.
- **This is not a new 1.3.0 regression.** Empirically bisected: the defect was introduced
  in **PR #115** (`feat(testdoubles): sequential responses and overload-safe argument
  matching`, merged 2026-08-28, the `0.8.0` → `0.9.0` boundary), which materially expanded
  `TestDouble.scriban` (ADR-0054 sequential responses + ADR-0044 Amendment 21 overload-safe
  matching) — adding several new per-member (and, for overloaded members, per-overload)
  nested loops without ever revisiting the template's total Scriban step budget. Every
  version from `0.9.0` through current `1.3.0` merely preserves this defect; none of them
  changed the affected loop structure again.
- **Quantified**: a synthetic interface of plain, non-overloaded, 2-parameter, int-returning
  methods reproduces the exact same failure at **exactly 143 members** (142 passes, 143
  fails) against the current template — matching a per-member cost model of `1 + 3P` Scriban
  loop steps (`P` = parameter count) inside the single 218-506 block, i.e. `142 × 7 = 994 ≤
  1000` but `143 × 7 = 1001 > 1000`. The same model, cross-checked with a single
  member whose parameter count varies (fails between 300 and 400 params for one member:
  `1 + 3×400 = 1201 > 1000`), is internally consistent. This iteration mechanism is real
  evidence of *why* the crash happens, but (per revised direction below) it is not itself
  the thing being fixed — the underlying defect is that Compono never chose a step budget
  appropriate to its own workload in the first place.
- **Real `IAmazonS3` measured directly** (not estimated from the issue report): analyzing
  the actual `Amazon.S3.IAmazonS3` symbol from `AWSSDK.S3 4.0.103.3` (the exact version the
  real consumer depends on) through Compono's own `TestDoubleAnalyzer` gives **182**
  analyzed members (not ~171 — see §7 for the corrected figure and full shape), and the
  interface requires a Scriban `LoopLimit` of **exactly 1448** to render successfully (1447
  fails, 1448 succeeds) against the current template — no literal-thousand-member interface
  is required, exactly as the issue's working hypothesis anticipated.
- **Revised primary recommendation**: the defect is that `TemplateHelper.Render` silently
  inherited Scriban's generic library default (`LoopLimit = 1000`) as Compono's own
  supported-interface complexity ceiling — a number Compono never chose and that has no
  relationship to what its own template actually needs. The primary correctness fix is to
  **explicitly configure a deliberately generous, evidence-based, finite `LoopLimit`** on
  `TemplateHelper`'s `TemplateContext` (§8, §11-A) — this directly restores `IAmazonS3` (and
  every interface of comparable or larger size) to working order without changing what the
  template produces. Reducing the template's own iteration cost (previously proposed as the
  primary fix, `§11-C`) remains a valid *future* generator-performance idea but is no longer
  the primary recommendation — see §11 and §12 for the revised ordering and rationale.
- **Failure isolation does not exist today** — confirmed empirically, not just by code
  reading: an unhandled exception thrown while rendering *any one* discovered test double
  causes Roslyn's `IIncrementalGenerator` host to discard **the entire generator run's**
  output (not just that one interface's), even though the emit call site
  (`RegisterSourceOutput(discoveredTestDoubles, ...)`) is invoked once per discovered
  interface. This exactly matches the issue's "unrelated interfaces disappear" symptom and
  is a separate hardening decision from the `LoopLimit` fix itself (§9, §12) — not required
  to fix issue #142, but recommended as an independent follow-up.
- `IAmazonS3` is not inherently unsupported — nothing in its shape (overloads, async
  methods, inherited-interface members) is outside what `Compono.TestDoubles` documents as
  supported. The crash is a resource-limit defect in generation, not a correctness rejection
  of the interface.

## 2. Exact real-consumer reproduction

Consumer: `/Users/ncipollina/source/repos/ncipollina/lightsaber-skill`, current working
tree (`AWSSDK.S3` `4.0.103.3`, Compono/`Compono.TestDoubles`/`Compono.XunitV3` all pinned to
`1.3.0` in `Directory.Packages.props`).

`IAmazonS3` becomes reachable through two paths in
`test/Lightsaber.Skill.Tests/Handlers/LightsaberHandlerTests.cs`:

- `[Compose<GeneratedTestDoublesProfile>]` test methods taking `IAmazonS3 mockAmazonS3(Client)`
  as a parameter (lines ~30, ~82).
- A direct `composer.Create<IAmazonS3>()` call off a `Composer.Create(builder =>
  builder.UseGeneratedTestDoubles())` instance (line ~110).

`dotnet build test/Lightsaber.Skill.Tests` on the current tree reproduces:

```
CSC : warning CS8785: Generator 'ComponoIncrementalGenerator' failed to generate source.
Exception was of type 'ScriptRuntimeException' with message
'TestDouble.scriban(373,24) : error : Exceeding number of iteration limit `1000` for loop
statement.'.
```

followed immediately by ~24 unrelated `CS1061`/`CS7036` errors against
`IResponseBuilder.Configure`, `IHandlerInput.Configure`, `ILoggingBuilder.Configure` (a real
BCL extension whose lookup now resolves ambiguously without the generated one available),
and `ISkillMediator.Verify` — none of which have anything to do with `IAmazonS3`. This is
materially identical to issue #142: same exception type, same message shape, same
"unrelated generated members vanish" fallout. No changes were made to the consumer to avoid
the interface.

## 3. Version/regression matrix

| Version | Behavior against a synthetic 2-param-per-method interface |
|---|---|
| `v0.8.0` | Passes through **171** members; first fails at **300** members (fails between 171 and 300) |
| `v0.9.0` template (post-PR #115) | Materially the same failing structure as current — not independently re-measured member-for-member (template line numbers moved further in `1.3.0` due to later, unrelated additions), but the causal loop shape introduced in PR #115 is unchanged since |
| `current` (`1.3.0`, same commit as the real consumer's dependency) | Fails at **143** members (2 params each); real `IAmazonS3` (~171 members, real per-member param counts) fails identically to the issue report |

**Regression boundary: `v0.8.0` → `v0.9.0`, introduced by PR #115.** `v0.8.0` tolerates
roughly double the member count of current for the same per-member shape (171 passes vs.
143 failing on current), consistent with PR #115 roughly doubling/tripling the number of
per-member nested `{{ for p in member.parameters }}` loops that share the same Scriban
step budget. Every version after `0.9.0` (including `1.3.0`) preserves rather than
introduces the defect — `git log v0.8.0..v0.9.0 -- src/Compono.Generators/Templates/TestDouble.scriban`
shows exactly **one** commit touching the template between the two tags.

## 4. Causal commit/PR

**PR #115** — `feat(testdoubles): sequential responses and overload-safe argument matching`
(commit `6f58dee`, merged 2026-08-28), implementing:

- **ADR-0054** (sequential/call-count-based responses — `ReturnsSequence`/`SequenceOutcome`),
  wired through every dispatch shape in the template.
- **ADR-0044 Amendment 21** (overload-safe argument matching) — every matching-eligible
  *overload* gets an additional generated `<Member>Matching(Match<T1>, ...)` surface,
  doubling the Configure()/Verify() extension-method loops for overloaded members.

`diff` of `TestDouble.scriban` between `v0.8.0` and `v0.9.0` (540 → 612 lines; current is
818 lines after later, independently-shipped features) shows the loop-relevant additions:
new `HasConfiguredSequence`/`NextSequenceOutcome()` branches inside the existing per-member
dispatch loop (more branches evaluated per member, though branches themselves don't add
loop iterations), and, more importantly, **entirely new discriminator-only + matching-named
Configure()/Verify() extension method pairs per overload-matching-eligible member**, each
with its own `{{ for p in member.parameters }}` loops for parameter lists and matcher
assignment — this is the structural change that raises the per-member Scriban step count.

## 5. Exact template loop and quantified iteration mechanism

`TemplateHelper.Render` (`src/Compono.Generators/Emitters/TemplateHelper.cs`) calls
`Template.Render(model)` with **no custom `TemplateContext`** — Scriban's own default
(`LoopLimit = 1000`, unconfigured `LoopLimitQueryable`) applies.

Scriban's loop-limit enforcement (`TemplateContext.cs`, `StepLoop`/`EnterLoopScopeCore`/
`ExitLoopScopeCore`) works like this:

```csharp
private void EnterLoopScopeCore() { if (_loopDepth == 0) _loopStep = 0; _loopDepth++; }
private void ExitLoopScopeCore()  { _loopDepth--; if (_loopDepth == 0) _loopStep = 0; }
internal bool StepLoop(...) { _loopStep++; if (_loopStep > LoopLimit) throw ...; }
```

**`_loopStep` only resets when loop nesting returns to zero** — i.e., once per *outermost*
loop's full execution, not once per `{{ for }}` tag. A `{{ for }}` nested inside another
`{{ for }}`'s body accumulates into the *same* counter as its parent and every sibling
nested loop that ran before it, across every iteration of the outer loop.

`TestDouble.scriban` has 7 sibling top-level `{{~ for member in members ~}}` blocks (each
gets its own fresh budget). The largest — the one that emits every member's real dispatch
body (explicit interface implementation, argument-matcher reverse-scan check, callback
dispatch, DIM-fallback forwarding) — spans **lines 218-506** in the current template.
Within that single block, a member that is both matching-eligible and callback-eligible
(the common case — most methods with no `ref`/`out`/generic parameters qualify for both)
triggers three separate `{{ for p in member.parameters }}` loops per iteration of the outer
member loop: the explicit-implementation signature, the reverse-scan matcher-condition
build, and the callback-delegate argument list.

**Empirical confirmation** (temporary probe test against `GeneratorTestHelpers
.GenerateFromSource`, deleted after use — not committed):

| Members (`M`) | Params/method (`P`) | Overloads | Result |
|---|---|---|---|
| 142 | 2 | 1 | passes |
| **143** | 2 | 1 | **fails, `TestDouble.scriban(398,64)`** |
| 1 | 300 | 1 | passes |
| 1 | 400 | 1 | fails, same line |
| 171 | 2 | 2 (i.e. 342 members) | fails, `TestDouble.scriban(18,135)` (a different loop — the Configure() extension block — dominates once overloads double the member count) |

Fitting `M × (1 + 3P) ≈ 1000`: `142 × (1 + 3×2) = 142 × 7 = 994 ≤ 1000`;
`143 × 7 = 1001 > 1000`. Cross-checked with the single-member/large-`P` case: `1 × (1 +
3×400) = 1201 > 1000` (fails) vs. `1 × (1 + 3×300) = 901 ≤ 1000` (passes). Both data points
agree with the same `1 + 3P` per-member step model for this loop, where the `1` is the
outer loop's own step and the `3P` is the sum of the three nested per-parameter loops
described above.

**Concretely, for `IAmazonS3`** (~171 members, average parameter count above 2 once
`AmazonS3Config`/`CancellationToken`/request-object parameters and heavy overload sets are
counted): `171 × (1 + 3×2) = 1197` already exceeds 1000 well before the real (higher)
per-member cost is even applied — no literal thousand-member interface is required, exactly
as the issue's working hypothesis anticipated. The discrepancy between the reported
`(373,24)`/`(398,64)` line numbers reflects which nested loop happens to be the one whose
step pushes the shared counter over 1000 for a given member shape — not a different root
cause.

## 6. v0.8.0 vs v0.9.0 code-path/template difference

- `v0.8.0`: one Configure() extension per member (no overload-matching-specific surface),
  no `ReturnsSequence`/`SequenceOutcome` branches, no per-overload matching entries list.
  Per-member step cost inside the shared budget is lower — empirically tolerates up to
  ~250-300 members of the same simple shape used above (171 passes, 300 fails).
- `v0.9.0`+ (PR #115 onward, including current): adds, per overload-matching-eligible
  member, a **second** Configure() extension and a **second** Verify() extension (each with
  their own parameter-list loops), plus new sequential-response dispatch checks in the
  existing per-member body. Net effect: materially more Scriban loop iterations consumed
  per member before the shared 1000-step budget within any one of the 7 top-level blocks is
  exhausted — the ~2x member-count-tolerance drop measured above (171→300 boundary in
  `v0.8.0` vs. 142→143 in current, for the *same* synthetic shape) is consistent with this.

## 7. IAmazonS3 shape analysis (corrected — actual measurement, not the issue's estimate)

`AWSSDK.S3` `4.0.103.3` / `AWSSDK.Core` `4.0.102.6` are the exact versions the real consumer
resolves (`ncipollina/lightsaber-skill`'s restored `project.assets.json`). Rather than
accepting the issue's "roughly 171 declared members" figure, `Amazon.S3.IAmazonS3` was
loaded as a real Roslyn `INamedTypeSymbol` from these exact assemblies and run directly
through Compono's own `TestDoubleAnalyzer.Analyze(...)` (internal, but reachable from
`Compono.Generators.Tests` via the existing `InternalsVisibleTo`) — the same analysis path
the real generator uses, not an independent hand-count. Measured via a temporary probe test
(not committed, deleted after use):

| Metric | Measured value |
|---|---|
| Total analyzed members (`DiscoveredTestDoubleInfo.Members.Count`) | **182** (not ~171 — the issue's figure undercounts by ~6%, likely a slightly different SDK version or a manual/approximate count) |
| Distinct member names | 132 |
| Overload groups (name used more than once) | 38 groups, covering 88 of the 182 members |
| Parameter count: min / max / mean | 0 / 8 / 2.32 |
| Parameter count histogram | 0p:3, 1p:7, **2p:135**, 3p:17, 4p:12, 5p:5, 6p:1, 7p:1, 8p:1 |
| Total parameters across all members | 422 |
| `is_eligible_for_matching` | 91 of 182 |
| `is_callback_eligible` | 179 of 182 |
| Non-blocking diagnostics from analysis | 1 × `CMP0032` (a member requires explicit `Configure()` before use — the normal, pre-existing "configuration-required member" informational diagnostic, unrelated to this bug) |
| Blocking diagnostics from analysis | none — `IAmazonS3` analyzes cleanly as a supported shape |

This confirms and sharpens §5's finding: the interface's overwhelming majority shape is
2-parameter methods (135 of 182), which is exactly the shape the earlier synthetic
bisection (§5) used to find the 142/143-member threshold — the real interface isn't an edge
case relative to the synthetic reproduction, it's a near-exact real-world match for it, just
~40 members larger (182 vs. the 143-member synthetic failure point), which is why it fails
by a comfortable margin rather than marginally.

Nothing about `IAmazonS3`'s shape (interface inheritance, async methods, generic-free
members, heavy overloading, 0 blocking diagnostics) falls outside what `Compono.TestDoubles`'
existing ADRs (0042-0054) document as supported — the crash is a resource-exhaustion bug in
generation, not a correctness rejection.

### 7a. Actual LoopLimit requirement (real IAmazonS3 and synthetic stress shapes)

Using the same real `IAmazonS3` symbol, `TemplateHelper.Render` was temporarily patched
(worktree-only, reverted, never committed) to accept an overridable `LoopLimit` on a custom
`TemplateContext` (replicating exactly what Scriban's own default `Render(model)` overload
does internally, per §8), and bisected:

| Interface | Members | Result |
|---|---|---|
| Real `IAmazonS3` | 182 | **fails at `LoopLimit=1447`, succeeds at `LoopLimit=1448`** (exact threshold) |
| Synthetic, 200 methods × 3 params | 200 | succeeds at `LoopLimit=2000` |
| Synthetic, 500 methods × 3 params | 500 | fails at `LoopLimit=3000`, succeeds at `LoopLimit=5000` |
| Synthetic, 1000 methods × 3 params | 1,000 | fails at `LoopLimit=8000`, succeeds at `LoopLimit=10000` |
| Synthetic, overload-heavy: 600 method groups × 3 overloads, 5 params/method | 1,800 | fails at `LoopLimit=5000`, succeeds at `LoopLimit=12000` (comfortably clears well before 20000) |

The real `IAmazonS3` (182 members, realistic parameter distribution, 38 overload groups)
needs **~1,450** — consistent with, and only modestly above, the earlier synthetic
143-member/2-param estimate scaled up (182/143 × ~1000 ≈ 1270, in the right range once real
per-member variation in parameter count and overload-doubling is accounted for). A
1,000-member interface with realistic parameters needs on the order of **8,000-10,000**; an
extreme 1,800-member, heavily-overloaded synthetic interface needs on the order of
**5,000-12,000**. No interface tested, up to and including a synthetic 1,800-member
overload-heavy shape well beyond any real-world SDK interface's size, required more than the
low tens of thousands.

## 8. Scriban iteration-limit semantics/configuration findings

- `LoopLimit = 1000` is **Scriban's own library default** (`TemplateContext`'s constructor),
  not something Compono set. Compono's generator source has zero references to `LoopLimit`
  or a custom `TemplateContext` anywhere in `src/Compono.Generators` — `TemplateHelper.Render`
  uses the one-argument `Template.Render(model)` overload, which constructs a default
  context internally.
- Compono **can** configure it: `Template.Render` has an overload taking a
  `Func<TemplateContext>` (or a caller can build a `TemplateContext`, set `LoopLimit`, and
  call `template.Render(context)` after pushing the model). This is a config-surface change
  confined entirely to `TemplateHelper.cs` — no public API impact.
- Raising it is safe for this specific use: this is a **compile-time-only, build-machine-
  local, non-attacker-controlled** execution — the "template" is Compono's own shipped
  `.scriban` file (not consumer-supplied), and the "data" driving loop counts is a Roslyn
  symbol model derived from the consumer's own referenced assemblies. There is no untrusted
  input driving the loop count in a way that resembles the DoS scenario Scriban's limit
  exists to guard against (a template author accidentally or maliciously supplying runaway
  data to a *user-facing* templating engine). Removing/raising the limit does **not** create
  a new attack surface for Compono; the worst case of an unbounded run is a slow or hung
  `dotnet build`, not memory/security exposure, and even that is already bounded by the
  interface being a finite, already-compiled Roslyn symbol.
- **Evidence-based recommended value**: §7a's measurements give a concrete basis rather
  than an intuition-picked number. Real `IAmazonS3` (182 members, the largest known
  real-world trigger) needs 1,448; a synthetic 1,000-member interface with realistic
  parameters needs ~8,000-10,000; a deliberately extreme, overload-heavy 1,800-member
  synthetic interface (well beyond any real SDK interface encountered) needs at most
  ~12,000-20,000. A `LoopLimit` in the **20,000-50,000** range clears every measured case,
  including the extreme synthetic stress shape, by a wide margin, while remaining a small,
  genuinely finite number relative to what an *actual* runaway (an infinite Scriban loop
  from a future template bug) would produce — Scriban's step counter increments once per
  loop iteration, so a real infinite loop hits even a 50,000 ceiling in well under a second
  and still gets caught. This is an internal generator implementation detail, not a
  consumer-facing configuration surface — it belongs entirely inside
  `TemplateHelper.Render`/its `TemplateContext` construction, with no new public API.
- This is explicitly **not** an argument for disabling the limit or setting it to
  `int.MaxValue` — `LoopLimit` still meaningfully guards against a genuine runaway (e.g., a
  future template bug that introduces an actual infinite loop, or a pathological future
  interface shape nobody has stress-tested), and a large-but-finite ceiling catches that
  class of bug just as effectively as a small one, while clearing every measured legitimate
  shape.
- **Reducing the template's own per-member iteration cost** (precomputing more of the
  per-member parameter-list text in C# rather than building it via nested Scriban `{{ for
  }}` tags) remains available as a **future, evidence-gated generator-performance
  optimization** — see the revised §11-C and §12. It is not needed to restore correctness
  once Compono explicitly owns an appropriate `LoopLimit`, and should only be pursued if
  generator-time profiling demonstrates a worthwhile improvement, not as a substitute for
  choosing the right ceiling.

## 9. Generator exception-isolation findings

- The exception escapes from `TestDoubleEmitter`'s call to `TemplateHelper.Render(...)`
  (`TestDoubleEmitter.cs:232`), inside the lambda passed to
  `context.RegisterSourceOutput(discoveredTestDoubles, static (productionContext, testDouble)
  => ...)` (`ComponoIncrementalGenerator.cs:442`).
- Generation **is** structured per-interface at the `RegisterSourceOutput` registration
  level — Roslyn invokes that callback once per item in the `discoveredTestDoubles`
  incremental value collection, not once for an aggregated batch.
- **Empirically confirmed** (temporary probe, not committed): a compilation with one
  interface deliberately sized to exceed the loop limit *and* one small, otherwise-healthy,
  unrelated interface produces **zero** generated trees for either interface — not just the
  failing one. `GeneratorDriverRunResult.GeneratedTrees` was empty and only the single
  `CS8785` diagnostic was present. This means Roslyn's `IIncrementalGenerator` host discards
  the *entire generator's* output for that compilation once any unhandled exception escapes
  generator execution, regardless of how many separate `RegisterSourceOutput` invocations
  would otherwise have succeeded independently — matching issue #142's "unrelated
  Configure()/Verify() extensions disappear" symptom exactly.
- Catching the render failure per interface is straightforward: wrap the
  `TemplateHelper.Render(...)` call (or the whole per-item lambda body) in
  `TestDoubleEmitter`'s `RegisterSourceOutput` callback in a `try`/`catch`, and on failure
  report a `CMPxxxx` diagnostic (e.g. "test double for 'IAmazonS3' could not be generated:
  exceeded internal template rendering limit") via `productionContext.ReportDiagnostic(...)`
  instead of letting the exception propagate, then simply not calling `AddSource` for that
  one interface.
- **Continuing to generate unrelated interfaces would be safe** in isolation — each
  `RegisterSourceOutput` invocation for a different `testDouble` item operates on an
  independent render model with no shared mutable state; nothing about one interface's
  failure taints another's correctness.
- **Important caveat the issue itself anticipates, and the revised product direction makes
  explicit**: silent fallback is not automatically correct. If `IAmazonS3` is within
  Compono's supported contract (§7 concludes it is), an isolation fix alone would convert a
  *loud* compile failure into a *quiet* missing double — the consumer's test would then fail
  at runtime ("no test double was generated for `IAmazonS3`") rather than at compile time,
  without fixing the underlying defect. Per the product-owner direction driving this
  revision: **any dedicated Compono diagnostic for a failed-to-generate interface should be
  Error severity, not Info** — a consumer must learn about a missing double at compile time,
  the same moment they'd otherwise see the crash today, not discover it later when a test
  using that double fails for an unrelated-looking reason. This is a deliberate departure
  from the existing `CMP0020`-`CMP0032` informational-diagnostic family's severity
  convention (§8 of `docs/reference/diagnostics.md`), which is appropriate here because those
  diagnostics describe *reduced* surface on an otherwise-successfully-generated double
  (e.g. "this member needs manual configuration"), whereas a loop-limit (or other internal
  render) failure means **no double was generated at all** — a materially different, more
  severe outcome that existing Info-severity precedent doesn't cover.
- Isolation remains a robustness improvement for *other, unrelated* interfaces in the same
  compilation — with an Error-severity diagnostic, `IAmazonS3` failing still surfaces loudly
  (as it does today), but healthy sibling interfaces (e.g. the mediator/logging interfaces
  seen failing collaterally in §2) would still get their doubles generated rather than being
  swept away as well. It is a hardening decision *in addition to* an appropriate `LoopLimit`
  fix, not a substitute for one.
- **Testing isolation without depending on the `LoopLimit` implementation**: the loop-limit
  crash is one way to make a per-interface render fail, but coupling the isolation
  regression test to "an interface large enough to exceed whatever `LoopLimit` happens to be
  configured to" is fragile — it silently stops testing isolation at all if a future
  `LoopLimit` change (or the eventual §11-C optimization) pushes that specific interface back
  under the ceiling. A **controlled, deterministic emitter failure** decouples the two: e.g.
  a test-only render-model shape that's valid enough to pass analysis but deliberately
  crashes `TemplateHelper.Render` for a reason unrelated to iteration count (for instance, a
  test seam/hook analogous to `TemplateHelper.LoopLimitOverrideForMeasurement` used for this
  investigation's own measurements — a way to force `TestDoubleEmitter`'s render call to
  throw for exactly one named interface without needing that interface to be enormous), or,
  more simply, asserting isolation behavior against a mocked/injected render step in a
  narrower unit test rather than a full generator-driver integration test. This is
  implementation work for whoever picks up the isolation follow-up, not something this
  research resolves, but the underlying finding — that isolation *can* be tested
  independently of the loop-limit specifics — holds because the exception-handling
  boundary (§9's `try`/`catch` around the per-interface `RegisterSourceOutput` callback) is
  agnostic to *why* the render threw.
- **Cohesion vs. separate follow-up**: per the product-owner direction, per-interface
  isolation is *not* the fix for issue #142 and should not be presented as one PR's single
  scope alongside the `LoopLimit` change. Recommended: **ship as a separate follow-up item**
  (Option B), not folded into the same bug-fix PR (Option A) — despite the two changes
  touching adjacent code (`TestDoubleEmitter`'s `RegisterSourceOutput` callback and
  `TemplateHelper`, respectively), they are two independently-motivated decisions with
  independently-reviewable scope: the `LoopLimit` change is a pure bug fix restoring
  previously-working behavior (§14), while isolation is new generator behavior (a new
  diagnostic ID, a new severity-and-scope policy for render failures) that itself needs the
  design-decision treatment §14 already calls for — bundling a policy decision into a bug-fix
  PR either slows down the bug fix waiting for that decision, or rushes the policy decision
  to keep pace with the bug fix. This repository's own non-negotiables ("Keep PRs scoped to
  one decision or one feature") support the same conclusion independent of PR-count
  considerations.

## 10. Minimal synthetic reproduction

The exact reproducer used throughout this investigation (not committed, deleted after use):
a temporary xUnit test in `test/Compono.Generators.Tests` building a synthetic
`public interface IBig { int Method0(int p0, int p1); ... }` with a configurable member
count/parameter count/overload count, run through
`GeneratorTestHelpers.GenerateFromSource` with `ComponoGeneratedTestDoubles=true`, asserting
on `driver.GetRunResult().Diagnostics`. This is fully representative: it triggers the exact
same `ScriptRuntimeException` message and (for equivalent per-member shapes) the exact same
failing template line as the real `IAmazonS3` consumer build, and requires no AWS SDK
reference. Per §7's corrected measurement (182 real members, not ~171), a permanent
regression fixture should be sized above 182 with margin — see §13 for the recommended
concrete shape, which is a behavior-level fixture, not a fixture pinned to the `1 + 3P`
formula used only for this investigation's own diagnosis.

## 11. Candidate fixes with tradeoffs

**A. Explicitly configure Scriban's loop limit to a deliberately generous, evidence-based,
finite value** (via a custom `TemplateContext` in `TemplateHelper.Render`) — **primary
recommendation, see §12**
- Correctness: this is the actual defect fix, not a workaround. The real defect isn't "the
  template is too expensive" — it's that Compono accidentally adopted Scriban's generic
  library default (`1000`, tuned for arbitrary/untrusted user-facing templating scenarios)
  as its own supported-interface complexity boundary, without ever deciding that number was
  appropriate for its own, entirely different workload (a Compono-owned template rendering
  finite, already-compiled Roslyn metadata at build time — see §8's DoS-surface analysis).
  Explicitly choosing a value sized to Compono's real measured needs (§7a, §8) restores
  supported behavior at the actual resource boundary, exactly as intended once Compono
  *owns* the decision instead of inheriting an arbitrary one.
- Performance/memory: negligible — this only affects Scriban's own per-render step counter,
  not actual work done.
- Determinism: unaffected — same generated output, just without the artificial early abort.
- Maintainability: smallest possible change (one file, `TemplateHelper.cs`), and — unlike
  §11-C — doesn't touch the template or the render-model layer at all.
- AOT/trimming: no relevance (build-time only).
- Public API: none — an internal generator implementation detail, not a consumer-facing
  configuration option (per explicit product direction).
- Behavior change for existing consumers: none for anyone not currently hitting the limit;
  fixes it for anyone who is.
- Fixes `IAmazonS3` outright: yes — confirmed by direct measurement (§7a: real `IAmazonS3`
  needs exactly 1,448; any evidence-based ceiling per §8 clears this with wide margin).
- Residual risk: an even-larger future interface could in principle still exceed a
  raised-but-finite ceiling — §7a's synthetic stress measurements (up to a deliberately
  extreme 1,800-member overload-heavy shape, needing at most ~12,000-20,000) inform how much
  margin is actually reasonable to build in, so this residual risk is now evidence-bounded
  rather than an open unknown.

**B. Remove/reduce the template's iteration amplification** (restructure
`TestDouble.scriban`'s nested per-member loops so fewer Scriban-interpreted iterations are
needed per member — e.g. splitting the one 218-506 mega-loop into narrower per-concern
loops that each reset their own budget, though per §5 this only helps if the *split* loops
individually stay under 1000, which doesn't scale better than raising the limit for a
genuinely huge interface)
- Correctness: fixes the crash for interfaces under whatever the new effective ceiling
  becomes; more fragile than A because it depends on where the split lands, and any future
  per-member feature addition (another sequential-response-style feature) re-erodes the
  headroom the same way PR #115 did.
- Performance: template restructuring itself doesn't inherently speed up generation but
  doesn't slow it down either.
- Maintainability: raises the template's structural complexity (splitting a cohesive
  per-member dispatch-body loop into pieces purely to reset a step counter is not a
  design-motivated split) — this is the option most likely to read as "gaming the counter"
  rather than fixing the underlying issue, and needs care not to violate ADR-0005's
  templating conventions.
- Fixes `IAmazonS3`: yes, if sized correctly, but doesn't durably fix the *class* of bug the
  way A does.

**C. Precompute more render text/model information in C# so Scriban performs less looping**
(e.g., `TestDoubleMemberInfo`/`TestDoubleParameterInfo` precompute the joined parameter-list
strings — signature text, matcher-condition text, call-site-forwarding text — once, in C#,
during model construction, so the template does `{{ member.signature_text }}` instead of a
`{{ for p in member.parameters }}` loop) — **demoted: a possible future optimization, not
part of the #142 fix (revised per product direction)**
- **This is not the "real fix" and should not be characterized as one.** The actual defect
  (§11-A) is that Compono never deliberately chose an appropriate `LoopLimit` for its own
  workload — it inherited Scriban's generic default. Once Compono explicitly owns a
  evidence-based ceiling (§11-A), correctness is fully restored for `IAmazonS3` and every
  measured stress shape (§7a) without touching the template's iteration structure at all.
  Reducing template iteration count would *also* incidentally raise the effective ceiling
  further, but doing so specifically to stay under Scriban's arbitrary default would be
  solving the wrong problem.
- Performance: *may* reduce generator rendering cost — string-joining in compiled C# is
  plausibly faster than Scriban's interpreted loop-and-concatenate per render — but this is
  an unverified hypothesis, not a measured result. No generator-time profiling has been done
  to confirm the actual cost of the current template's looping is a real bottleneck worth
  optimizing, as opposed to a cost that's already negligible relative to Roslyn's own
  compilation work.
- Maintainability/design cost: moving presentation/formatting logic out of the template and
  into the C# render model blurs the existing model-then-template separation this repo's
  generator conventions establish (`docs/adr/0005-generator-implementation-conventions.md`)
  — the template is meant to own "how the generated code looks," and the render model "what
  data describes it." Precomputing joined signature/condition/call-site text in C# starts to
  duplicate the template's own formatting responsibility into the model layer, which is a
  real design cost to weigh against any performance gain, not a free improvement.
- Recommendation: **keep as a documented candidate for a future, separately-scoped
  generator-performance investigation**, pursued only if profiling of real generator
  invocations (not this research's synthetic stress tests, which exist to characterize
  `LoopLimit` needs, not generator wall-clock time) demonstrates a meaningful, worthwhile
  improvement. Do not pursue it as part of resolving issue #142.
- Public API / AOT / determinism: unchanged from the original analysis — none of these
  would be affected either way, since this remains purely a generator-internal change if
  ever pursued.

**D. Per-interface exception isolation + Error-severity diagnostic** (§9) — **independent
hardening decision, not part of the #142 fix**
- This does **not** fix the `LoopLimit` crash for the interface that's actually failing — it
  only stops that failure from taking down *unrelated* interfaces' doubles in the same
  compilation, and (per revised direction) surfaces it as a loud, Error-severity compile
  failure rather than a silent/informational one. `IAmazonS3` still wouldn't get a usable
  double from isolation alone — the consumer would see a clear compile-time error naming the
  interface and reason, instead of today's confusing wave of unrelated `CS1061`/`CS7036`
  errors, but would still need the actual `LoopLimit` fix (A) to get a working double.
- Correctness: strictly additive robustness; doesn't change output for compilations that
  don't hit any render failure.
- Public API: adds one new **Error-severity** diagnostic ID (`CMPxxxx`) — a deliberate
  departure from the existing `CMP0020`-`CMP0032` Info-severity family (§9) — needs
  `docs/reference/diagnostics.md` updated in the same PR per this repo's non-negotiables.
- Determinism/AOT/maintainability: no concerns; a straightforward `try`/`catch` +
  `ReportDiagnostic` at one call site (`TestDoubleEmitter`'s `RegisterSourceOutput` callback).
- Testability: can be tested independently of the `LoopLimit` implementation specifics via a
  controlled/deterministic render-failure seam (§9) rather than by deliberately constructing
  an interface large enough to exceed whatever ceiling happens to be configured.
- Scoping: per §9's cohesion analysis, recommended as a **separate follow-up item**, not
  bundled into the same PR as the `LoopLimit` fix (A) — it's an independent policy decision
  (new diagnostic, new severity-and-scope convention for render failures) that deserves its
  own design-decision treatment (§14) rather than riding along with a straightforward bug fix.

**E. Recommended combination** (revised — see §12): **A** (explicitly configure an
evidence-based, finite `LoopLimit`) as the **sole correctness fix** for issue #142 — it is
sufficient on its own, confirmed by §7a's direct measurement of the real interface. **D**
(per-interface isolation with an Error-severity diagnostic) is recommended as a **separate,
independently-scoped follow-up** for generator robustness, not required to close #142. **C**
(reducing template iteration cost) is retained only as a documented, evidence-gated future
optimization (§11-C) — not part of this fix.

## 12. Recommended direction (research recommendation, not an accepted product decision)

**Revised per product-owner direction.** The defect is not "the template does too much
work" — it's that `TemplateHelper.Render` never made a deliberate decision about Scriban's
`LoopLimit` at all, and silently inherited Scriban's own generic default (`1000`, sized for
arbitrary/untrusted user-facing templating use cases Compono doesn't have — see §8). Compono
owns the template; the input is finite, already-compiled Roslyn metadata; and `IAmazonS3`
is otherwise within Compono's documented supported contract (§7). The correct fix is for
Compono to explicitly choose a `LoopLimit` appropriate to its own workload, sized from real
measurement rather than intuition.

Recommendation, ordered by priority:

1. **A** — explicitly configure `TemplateHelper.Render`'s `TemplateContext` with a finite
   `LoopLimit` chosen from §7a/§8's measurements (real `IAmazonS3` needs 1,448; a
   deliberately extreme 1,800-member overload-heavy synthetic shape needs at most
   ~12,000-20,000). This is the **complete, sufficient fix for issue #142** — confirmed by
   direct measurement, not projection. No template restructuring, no render-model changes,
   no public API surface.
2. **D** (separate follow-up, not gated on or bundled with #1) — per-interface exception
   isolation with a new **Error-severity** diagnostic, so that if some future interface
   (larger still, or failing for an unrelated internal-render reason) exhausts even the
   revised budget, it fails loudly and only for itself, without taking down unrelated
   interfaces in the same compilation. Recommended as its own PR/design item per §9's
   cohesion analysis — it is a policy decision (new diagnostic, new severity convention for
   render failures), not a mechanical extension of the `LoopLimit` fix.
3. **C** (not scheduled; documented only) — precomputing more per-member render text in C#
   remains available as a future generator-performance optimization, pursued only if
   profiling of real generator invocations demonstrates it's worth the model/template
   maintainability tradeoff (§11-C). It is not required by, and should not be bundled with,
   the #142 fix.

This ordering directly reflects the product-owner correction: the earlier draft of this
research treated reducing Scriban's interpreted workload as the "real" fix and configuring
`LoopLimit` as a stopgap. That got the framing backwards — `LoopLimit` is Compono's own
resource-boundary decision to make, and issue #142 exists because that decision was never
actually made.

## 13. Proposed regression-test strategy

Per product direction: the permanent regression test should prove the **supported-behavior
contract** ("Compono can successfully generate a test double for a large, valid,
representative interface"), not assert anything about Scriban's internal step-counting
mechanics. The `M × (1 + 3P)` formula in §5 is diagnostic evidence for *this investigation*
— useful for explaining why the bug happened, not something a test should encode or depend
on, since it's tied to the current template's exact loop structure and would need silent
"formula maintenance" every time an unrelated future template change shifted per-member
loop counts.

- **Primary regression test**: a `Compono.Generators.Tests` test using a synthetic
  interface with **250 methods**, each with a representative mix of parameter counts (a
  handful of 0/1-parameter methods, a majority of 2-3-parameter methods matching
  `IAmazonS3`'s measured distribution — §7's histogram — plus a few 5+-parameter methods),
  and a representative overload mix (a handful of method-name groups with 2-3 overloads
  each, mirroring §7's 38-overload-group finding). 250 was chosen with real margin above
  `IAmazonS3`'s measured 182 members (§7) and above the largest realistic near-term SDK
  interface Compono is likely to be asked to double, without being so large that the test
  encodes an arbitrary "must handle N members" number disconnected from any real shape.
  Assert the generator produces **zero diagnostics** and the resulting double compiles and
  is invocable — a pure behavior-level assertion, agnostic to whatever `LoopLimit` value or
  template structure is in place at the time.
- **Real-shape corroboration**: keep (or add) a `Compono.Generators.Tests` test that
  references the real `Amazon.S3.IAmazonS3` type directly (as this investigation's own
  measurement tests did, via `MetadataReference.CreateFromFile` against the pinned
  `AWSSDK.S3`/`AWSSDK.Core` versions) and asserts the same zero-diagnostics/compiles-cleanly
  behavior — this is the test most directly tied to issue #142 itself and doubles as living
  proof the originally-reported interface now works, without needing `AWSSDK.S3` as a
  *project* dependency of `Compono.Generators.Tests` (a test-local `MetadataReference` is
  enough).
- **Isolation regression test** (once the separate §9/§12 follow-up ships): assert that a
  compilation containing one interface whose render is deliberately forced to fail (via a
  controlled/deterministic failure seam, §9 — not by exceeding `LoopLimit`) still produces a
  working, compiled double for an unrelated, healthy sibling interface in the same source,
  and that the failing interface reports the new Error-severity diagnostic. This lives in
  its own test(s), separate from the `LoopLimit`-fix regression tests above, matching the
  separate-PR scoping recommended in §9/§12.
- Optionally, a **dogfood-level** confirmation against `lightsaber-skill` itself (already an
  identified real consumer hitting this) once the `LoopLimit` fix lands, per this repo's
  consumer validation gate discipline — not required for the unit-level regression tests,
  but a strong real-world confirmation given this issue was reported against that exact
  consumer.

## 14. Bug fix vs. architectural/product decision

**The `LoopLimit` fix itself (A) is a bug fix**, not a new capability or architectural
change — no ADR needed for it. `IAmazonS3` is already within Compono.TestDoubles'
documented supported shape (§7); explicitly configuring an evidence-based `LoopLimit`
restores previously-working behavior (a `v0.8.0`-era interface of comparable size worked)
rather than extending scope, and touches one internal file with no public API surface.

**Per-interface isolation with an Error-severity diagnostic (D) is the one genuinely
product-adjacent decision** here, and remains so under the revised direction — it's new
generator behavior (a new diagnostic ID, a new scope-and-severity policy for what happens
when one interface's render fails) and should get the same design-decision treatment
(light-dive ADR amendment or new ADR, per `references/design-decisions.md`) any new
diagnostic ID gets, even though it's a small, low-risk change. This is exactly why §9/§12
recommend it ship as a separate, independently-scoped follow-up rather than inside the bug
fix — it has a different classification (policy decision vs. bug fix) and a different
approval bar.

## 15. Contradictions with existing ADRs/docs

None found. `docs/adr/0005-generator-implementation-conventions.md` establishes Scriban as
the templating engine and describes template caching, but says nothing about Scriban's
`LoopLimit` or per-interface failure isolation — this is a genuine gap rather than a
contradiction. ADR-0044/0050/0054 (overload matching, dispatch ordering, sequential
responses) are all still correctly implemented by the current template; none of their
decisions are in question here, only the *volume* of Scriban interpretation their combined
implementation now costs per member.

## 16. Open questions requiring product-owner input

Several previously-open questions are now resolved by this revision and the new
measurements: the `LoopLimit` value is evidence-based (§8, §7a) rather than an open
"generous enough?" question, and the isolation diagnostic's severity is settled as Error per
explicit product direction (§9). What remains open:

- **Exact numeric `LoopLimit` value**: §8 recommends a value in the 20,000-50,000 range as
  clearing every measured case with wide margin, but the precise number within that range
  (and whether to round to something legible like `25,000` vs. `50,000`) is a small,
  low-stakes implementation choice for whoever picks up the fix — not something this
  research needs to pin further given the measured headroom is already comfortable across
  that whole range.
- **Isolation follow-up sequencing**: this research recommends isolation (D) ship as a
  separate item from the `LoopLimit` fix (§9, §12), but doesn't determine *when* — same
  milestone, immediately after, or queued behind other roadmap work — that's a planning/
  prioritization call, not a research question.
- **Whether §11-C (template iteration reduction) is ever worth doing**: this now explicitly
  depends on generator-time profiling data this research did not collect (it measured
  `LoopLimit` requirements, not wall-clock generator performance) — if/when someone profiles
  real generator invocations and finds the current template's looping is a measurable cost
  center, that would be new evidence justifying revisiting C; absent that evidence, C stays
  parked.
- **Regression-test interface shape maintenance**: §13 recommends a fixed, evidence-sized
  synthetic interface (250 members, representative parameter/overload mix) rather than a
  test that tracks internal step-cost formulas — whether this fixture's size should be
  periodically revisited as Compono's own supported-interface bar changes (e.g. if a future
  consumer needs an even larger interface doubled) is a standing maintenance question, not
  one this research resolves definitively.

## 17. Follow-up investigation: Scriban's second limit, `LimitToString` (2026-09-15)

During PLAN-0068 implementation (the `LoopLimit = 20_000` fix, §8/§12), the new large-
interface regression test (§13) and a direct re-check against real `IAmazonS3` both revealed
that raising `LoopLimit` alone is **not sufficient** to fix issue #142. This section
documents a follow-up investigation into a second, independent Scriban safety ceiling this
research did not previously know about, and answers the question the product owner posed
before deciding how to address it: *is the amount of source Compono generates for a
182-member interface reasonable, or is it inflated by avoidable duplication?*

### 17.1 Discovery: `TemplateContext.LimitToString`

Scriban's `TemplateContext` has a **second**, independent limit from `LoopLimit`:
`LimitToString`, defaulting to **1,048,576** (1 MiB) — "the buffer limit in characters for a
ToString in a list/string" per its own doc comment, but in practice (confirmed by reading
`TemplateContext.Helpers.cs`'s `GetAllowedOutputCount`/`WriteOutputChunk`) it caps **total
rendered output length for the whole render call**, not just list-to-string conversions.
Once cumulative output reaches this limit, every further `Write` call is silently truncated
(`GetAllowedOutputCount` returns `0` or a partial count) and a literal `"..."` is appended
once (`WriteOutputLimitEllipsis`) — **no exception, no diagnostic, nothing observable except
a shorter-than-expected string**. This is why raising `LoopLimit` alone produced a
`ScriptRuntimeException`-free, diagnostics-empty generator run that nonetheless emitted
invalid C# (`CS1513`/`CS0535`/missing `Configure()` extensions) once the interface's true
output exceeded 1,048,576 characters — the render "succeeded" by Scriban's own accounting,
having quietly discarded everything past the 1 MiB mark.

Empirically confirmed: with `LoopLimit = 20_000` applied and no other change, both the
~250-member synthetic regression fixture and the real `Amazon.S3.IAmazonS3` (`AWSSDK.S3
4.0.103.3`) produced a generated file truncated at **exactly 1,048,579 characters**
(`1,048,576 + "..."`).

### 17.2 Complete, untruncated `IAmazonS3` generated output

Measured directly (temporary investigation worktree, `LoopLimit` and `LimitToString` both
disabled purely for measurement, discarded after use — see §17.7):

| Metric | Value |
|---|---|
| Total generated characters | **2,206,258** |
| Total generated bytes (UTF-8) | 2,206,258 (pure ASCII output — no multi-byte characters) |
| Total generated lines | **29,690** |
| Interface members (from §7, reused not recomputed) | 182 |
| Overload groups (from §7) | 38, covering 88 members |
| Average parameters/member (from §7) | 2.32 (max 8) |
| Characters per member (2,206,258 / 182) | **≈12,122** |

`IAmazonS3`'s true output is **more than double** Scriban's 1,048,576-character default —
not a marginal overrun.

### 17.3 Breakdown by generated concern

The generated file's top-level types fall into clean, non-overlapping, contiguous line
ranges (there is no interleaving between concerns at the top level — each concern is a
distinct generated type):

| Section (generated type) | Lines | Chars | % of total |
|---|---|---|---|
| `..._Double` (member dispatch bodies, matcher checks, callback/sequential-response logic, entry state) | 14-18,393 (18,380 lines) | 1,355,384 | 61.4% |
| `..._DoubleConfiguration` (`Configure()` extension surface) | 18,394-23,043 (4,650 lines) | 429,990 | 19.5% |
| `..._VerifyExtension` + `..._DoubleVerification` (`Verify()` surface) | 23,044-27,907 (4,864 lines) | 305,367 | 13.8% |
| `..._ReceivedCallsExtension` + `..._DoubleReceivedCallsAccess` | 27,908-29,475 (1,568 lines) | 91,226 | 4.1% |
| `..._ClearCallsExtension` | 29,476-29,671 (196 lines) | 22,560 | 1.0% |
| `..._ConfigureExtension` (small bridge type) + file header | remainder | 1,731 | 0.1% |

The dispatch-body implementation dominates (61%), followed by `Configure()` (19.5%) and
`Verify()` (14%) — matching the shape of Compono.TestDoubles' documented, accepted API
surface (ADR-0043/ADR-0044/ADR-0048/ADR-0050/ADR-0054: every member gets a real dispatch
body, a `Configure()` extension, and a `Verify()` extension; overload-matching-eligible
members get a second pair of each per ADR-0044 Amendment 21/PR #115). No section is
unexpectedly large relative to what those accepted decisions call for.

### 17.4 A real, quantified duplication finding: repeated explanatory comments

Within the dominant `..._Double` section, comment lines (`grep -c '^\s*//'`) account for
**678,925 of the file's 2,206,258 characters — 30.8% of total generated output**. Three
specific multi-line comment blocks explaining *why* the dispatch logic is shaped the way it
is (an `ADR-0050` reverse-scan rationale, a "no `break` here" rationale, an `ADR-0054`
sequence-ordering rationale) each appear **179 times**, once per matching-eligible member,
byte-identical every time.

This is real, quantifiable, and matches the "large-scale repetition providing no consumer
value" pattern the investigation was asked to look for: a consumer inspecting `IAmazonS3`'s
generated double gains nothing from reading the same three design-rationale paragraphs 179
times — the rationale is for someone reading `TestDouble.scriban`'s own source (already
served by the comments living in the template file itself), not for a consumer reading
generated output. **However, removing these comments entirely would only reduce the file to
≈1,527,333 characters (2,206,258 − 678,925) — still 46% over Scriban's 1,048,576 default.**
This finding does not eliminate the need to raise `LimitToString`; it identifies a real,
narrow, separately-addressable secondary inefficiency layered on top of otherwise
reasonably-scaled output. It is also worth noting these comments are a long-established,
deliberate pattern in this codebase's generated code (recorded across multiple prior PRs'
review history, e.g. the "Codex review, PR #108 round 5/6" attributions baked into the
comment text itself) — removing them is a legitimate future generated-code-hygiene
candidate, not an obvious oversight to reflexively strip.

No other duplication of comparable scale was found. Code-pattern occurrence counts (e.g.
`HasConfiguredException` at 182 — exactly one per member; `public static global::Compono`
extension-method signatures at 364 — exactly two per member, one `Configure()` + one
`Verify()`) show the *functional* code scales cleanly at one unit of work per member, not
duplicated redundantly.

### 17.5 Scaling characteristics: linear, not combinatorial

Controlled synthetic experiments (same measurement worktree, `LoopLimit`/`LimitToString`
both disabled), varying one dimension at a time:

| Member count (2 params, no overloads) | Chars | Chars/member |
|---|---|---|
| 50 | 513,956 | 10,279 |
| 100 | 1,022,656 | 10,227 |
| 200 | 2,045,056 | 10,225 |
| 400 | 4,089,856 | 10,225 |

Perfectly linear — doubling member count exactly doubles output, with a stable ≈10,225
chars/member constant across a 8x range.

| Parameters/member (100 members, no overloads) | Chars/member |
|---|---|
| 1 | 9,920 |
| 2 | 10,227 |
| 4 | 10,809 |
| 8 | 11,973 |

Linear, ≈291 chars/member per additional parameter — no acceleration.

| Overloads/group (100 groups, base 2 params, each overload adds 1 param) | Total members | Chars/member |
|---|---|---|
| 1 | 100 | 10,227 |
| 2 | 200 | 8,619 |
| 3 | 300 | 8,753 |

Per-member cost *decreases slightly* as overload count rises, because shared per-group
scaffolding (the entries list, its lock field) is emitted once per overload *group*, not
once per overload — the opposite of combinatorial growth. **No evidence of accidental
combinatorial or exponential behavior in any dimension tested.**

Real `IAmazonS3`'s measured ≈12,122 chars/member sits squarely on this linear model once its
higher average parameter count (up to 8, vs. this experiment's baseline of 2) is accounted
for — the 100-member/8-param synthetic case above measured 11,973 chars/member, essentially
matching. **`IAmazonS3` is not an outlier or a pathological case; it is an ordinary, large
interface landing exactly where the linear scaling model predicts.**

An intentionally extreme synthetic stress shape (600 method-name groups × 3 overloads × 5
base parameters = 1,800 members, mirroring RESEARCH-0034 §7a's most extreme `LoopLimit`
stress case) required **17,345,696 characters** fully rendered — useful context for how much
larger an evidence-based `LimitToString` would need to be to cover that same deliberately
extreme case, versus covering only real known consumers with comfortable margin (see §17.9).

### 17.6 Multi-file feasibility (brief observation only, no design performed)

The six top-level generated types (§17.3) are **already cleanly, non-overlappingly
separable** at the source level — they don't interleave, and (per
`references/coding-standards.md`'s "Generated code" section, ADR-0043) test-double types are
already `internal` + hash-suffixed rather than `file`-scoped, specifically because they
reference each other across type boundaries in public/internal signatures. That existing
design choice means `internal` visibility already works correctly regardless of which
physical file a type is emitted into — Roslyn doesn't distinguish generated-file origin for
ordinary assembly-visibility rules.

This suggests multi-file emission (one `AddSource` call per concern, e.g. separate hint
names for `_Double`, `_DoubleConfiguration`, `_VerifyExtension`/`_DoubleVerification`, etc.)
is **not incompatible** with the current type-scoping model. It is, however, **non-trivial**
to actually build: today's single `TestDouble.scriban` template and single
`TemplateHelper.Render` call produce one concatenated string for one `AddSource` call: doing
this properly would mean splitting the template into independently-renderable sections (or
introducing several smaller templates) that each still need the same member/overload model,
plus new per-section hint-name generation alongside the existing
`GeneratedFileNaming.HintNameFor`. This is a real, bounded engineering effort, not a
one-line change and not an architectural dead end — but it was not prototyped, scoped, or
designed further, per the investigation's instructions.

### 17.7 Temporary investigation artifacts (all removed)

- A separate `git worktree` at `/tmp/compono-investigate` (checked out from the same commit
  PLAN-0068's uncommitted work sits on top of) — `TemplateHelper.Render` there was patched to
  set `LoopLimit = 1_000_000` and `LimitToString = 0` (disabled) purely to obtain complete,
  untruncated output for measurement. Removed via `git worktree remove --force` after use.
- A temporary test file (`ZZZSizeInvestigation.cs`) in that worktree's
  `test/Compono.Generators.Tests`, measuring real `IAmazonS3` (via `MetadataReference`s to
  the same locally-cached `AWSSDK.S3 4.0.103.3`/`AWSSDK.Core 4.0.102.6` DLLs RESEARCH-0034 §7
  already used) and the controlled synthetic scaling shapes in §17.5. Never committed; the
  worktree's removal deleted it along with everything else in that checkout.
- A scratch file (`/tmp/iamazons3_full_generated.cs`) holding the full untruncated real
  `IAmazonS3` output for the §17.3/§17.4 breakdown analysis (`grep`/`sed`/`wc` against it).
  Deleted after analysis.
- **No AWS package reference was added to this repository** — the `MetadataReference`s
  pointed directly at the same locally-cached NuGet package DLLs RESEARCH-0034's original §7
  measurement used; nothing was added to any `.csproj`/`Directory.Packages.props` in this
  repo, in the investigation worktree, or otherwise retained.
- The repository's actual working tree (PLAN-0068's in-progress `TemplateHelper.cs`
  `LoopLimit` fix, the new regression test, this research document, and the plan itself) was
  never touched by this investigation — confirmed via `git status` before and after.

### 17.8 Outcome assessment

**This is Outcome A (generated size is a reasonable consequence of interface size and
Compono's accepted API), with one quantified but non-blocking Outcome B observation layered
on top — not Outcome C.**

- No evidence of combinatorial/exponential scaling (§17.5) — growth is linear in member
  count, parameter count, and overload count, with a large but constant per-member
  multiplier explained entirely by Compono's deliberately rich, strongly-typed
  Configure()/Verify()/matching/callback/sequential-response API surface per member (§17.3),
  exactly as already accepted by ADR-0043/ADR-0044/ADR-0048/ADR-0050/ADR-0054.
  `IAmazonS3`'s size is not anomalous; it sits exactly where the linear model predicts for
  its measured shape (§7, §17.5).
- One real, quantified, narrow inefficiency was found (§17.4: ≈31% of output is
  byte-identical repeated design-rationale comments) — worth documenting as a candidate
  future generated-code-hygiene improvement, but insufficient on its own to bring `IAmazonS3`
  under Scriban's 1,048,576-character default even if fully removed, and is a long-standing,
  deliberate pattern in this codebase rather than an accidental defect.
- Multi-file emission is architecturally plausible given the existing `internal` +
  hash-suffixed (non-`file`-scoped) type design (§17.6), but non-trivial, unscoped, and not
  something the evidence here demands as a prerequisite to fixing #142.

### 17.9 Recommendation for PLAN-0068 (research recommendation only — not yet applied to the plan)

**Configure an evidence-based, finite `LimitToString` as part of PLAN-0068**, using the same
reasoning already accepted for `LoopLimit` (§8): Compono owns this template, the input is
finite already-compiled Roslyn metadata, and the 1,048,576 default is Scriban's own generic
ceiling, not a value Compono ever deliberately chose for its own workload.

Evidence for an appropriate value:

| Case | Required characters |
|---|---|
| Real `IAmazonS3` (largest known real-world trigger) | 2,206,258 |
| Synthetic, 400 members / 2 params | 4,089,856 |
| Synthetic, deliberately extreme: 1,800 members, heavy overloads, 5+ params | 17,345,696 |

Unlike `LoopLimit` (where 20,000 comfortably covered even the most extreme stress shape with
wide margin), covering that same most-extreme synthetic stress case for `LimitToString`
would require a value in the high tens of millions of characters — a much larger number,
because output size scales directly with total generated *text volume*, not with a bounded
iteration count. Two defensible framings, left for the product owner to choose between
(this research does not pick one):

- **Cover real `IAmazonS3` with generous margin, not the most extreme synthetic case**: a
  value in the **8,000,000-16,000,000** character range clears the real interface with
  3.6x-7x headroom and comfortably covers realistic future growth (e.g. a 400-member
  interface at 4,089,856 chars), while remaining a bounded, finite ceiling that still catches
  a genuine runaway (a future template bug producing actually-unbounded output would still
  hit even a 16,000,000-character ceiling in well under a second).
- **Cover the same extreme synthetic ceiling `LoopLimit` was sized against**, for
  internal consistency with §8's stated methodology: would require a much larger value
  (≥20,000,000), which is still a small, cheap, finite number for a build-time-only,
  trusted-input render, but is a materially different-feeling number to commit to than
  `LoopLimit`'s 20,000.

This research recommends the first framing (real-consumer-plus-margin, not
worst-case-synthetic-plus-margin) as more consistent with how `LimitToString`'s risk profile
actually differs from `LoopLimit`'s (a template bug that loops forever is caught quickly
either way; a template bug that produces gradually-more output is far more likely to be
caught by ordinary build-time/CI slowness long before tens of millions of characters
accumulate) — but this is offered as a recommendation, not a decision, per the instruction
not to finalize PLAN-0068's implementation tasks yet.

**Not recommending as part of #142**: the comment-duplication finding (§17.4) — real, but a
separate, narrow, non-blocking generated-code-hygiene candidate, not required to restore
correct generation for `IAmazonS3` or any other supported interface size measured here.
Whether it's worth a future look is a product-owner call, not a blocker for this fix.
