# [PLAN-0068] Compono.TestDoubles: Two Inherited Scriban Safety-Limit Defaults for Large Interfaces (Issue #142)

**Status:** Done

**Implements:** No ADR — this is a bug fix, not a new capability or architectural
change (per [RESEARCH-0034](../research/0034-testdoubles-scriban-loop-limit-issue-142-investigation.md)'s
§14 classification). Investigated in RESEARCH-0034; fixes
[GitHub issue #142](https://github.com/LayeredCraft/compono/issues/142).

## Goal

`ComponoIncrementalGenerator` currently fails to generate a test double for any
sufficiently large interface (e.g. `Amazon.S3.IAmazonS3`, measured at 182 members in
RESEARCH-0034 §7) because `TemplateHelper.Render` never explicitly configured Scriban's
`TemplateContext`, and so silently inherited **two independent Scriban safety-limit
defaults** neither of which was ever a deliberate Compono decision:

1. **`LoopLimit` (default `1_000`)** — throws `ScriptRuntimeException: Exceeding number of
   iteration limit '1000' for loop statement` once the shared per-render loop-step counter
   (RESEARCH-0034 §5) is exceeded. This is the failure issue #142 originally reported, and
   that single failure silently discards every other generated test double in the same
   compilation (RESEARCH-0034 §9).
2. **`LimitToString` (default `1_048_576` characters)** — discovered mid-implementation
   (RESEARCH-0034 §17): once total rendered output reaches this many characters, Scriban
   silently truncates everything further and appends `"..."`, with **no exception and no
   diagnostic**. Raising `LoopLimit` alone let rendering run to completion but then hit this
   second ceiling, silently producing malformed C# (unbalanced braces, missing interface
   members) — a different, quieter failure mode for the same underlying interface, not a fix.

RESEARCH-0034 (§7a, §8, §17) measured both limits directly against the real interface and
against deliberately extreme synthetic stress shapes, establishing that both are simply
Scriban's own generic library defaults — sized for arbitrary/untrusted user-facing templating
scenarios Compono doesn't have — never deliberately chosen by Compono for its own
build-time, trusted-input workload. Done when: `TemplateHelper` explicitly configures both
`LoopLimit = 20_000` and `LimitToString = 20_000_000`; a new repository-level regression test
proves a large, representative synthetic interface generates cleanly; and the real
`lightsaber-skill` consumer's `IAmazonS3`-dependent tests build and pass against freshly
packed local Compono packages. All of this is now **done** — see Notes for full evidence.

## Scope

In scope:

- `src/Compono.Generators/Emitters/TemplateHelper.cs` — explicitly construct a
  `Scriban.TemplateContext` with **both** `LoopLimit = 20_000` **and**
  `LimitToString = 20_000_000` for the `Render<TModel>` call path, replacing the current
  default, unconfigured `Template.Render(model)` overload. Both are internal,
  Compono-owned safety boundaries selected from RESEARCH-0034's direct measurement — not
  consumer-configurable, not a public API.
- One new behavior-level generator regression test in `test/Compono.Generators.Tests`
  proving a large, representative synthetic interface (≈250 members) generates cleanly,
  compiles, and produces an invocable double.
- `docs/research/0034-...md` becomes tracked/committed as part of this change (currently
  untracked) — it is the investigation record supporting this fix and belongs in the same
  change set per this repo's "update relevant docs in the same PR" convention. It now also
  documents the mid-implementation `LimitToString` discovery (§17) and is authoritative
  evidence for both configured values.
- This Plan document itself, `docs/plans/README.md`'s index row, and (if the repository's
  diagnostics/package docs reference the `1000`-iteration or `1 MiB`-output behavior
  anywhere — checked in Task list below, not assumed) any doc touch strictly necessitated by
  the value change.

Explicitly **out of scope** (per accepted product direction — do not implement even if it
looks adjacent while touching this code):

- Any `TestDouble.scriban` change aimed at reducing iteration count or output volume
  (RESEARCH-0034 §11-C/§17.9, demoted to a future, evidence-gated optimization — not pursued
  here). This includes the repeated-comment-duplication finding (RESEARCH-0034 §17.4, ≈31%
  of `IAmazonS3`'s generated output) — real and quantified, but a documented
  generated-code-hygiene observation only, not addressed as part of #142.
- Any change to `TestDoubleAnalyzer`.
- Any change to generated public API or runtime packages (`Compono`, `Compono.TestDoubles`,
  `Compono.XunitV3`, etc.) — this fix is entirely inside `Compono.Generators`.
- Per-interface exception isolation and any new diagnostic ID (RESEARCH-0034 §9/§12 —
  explicitly deferred to a separate, independently-scoped follow-up item after #142; tracked
  as [issue #143](https://github.com/LayeredCraft/compono/issues/143)).
- Multi-file generated-output emission (RESEARCH-0034 §17.6) — found architecturally
  plausible but non-trivial, and not justified by any pathological/super-linear generator
  scaling (§17.5/§17.8 found none) — not designed, prototyped, or implemented here.
- A consumer-configurable `LoopLimit`/`LimitToString` or any other new public API surface —
  both are internal generator implementation details.
- Adding `AWSSDK.S3`/`AWSSDK.Core` as a dependency of this repository, or any test that
  dynamically locates/restores AWS assemblies to build a `MetadataReference` — the synthetic
  regression test is the repository-level proof; the real `IAmazonS3` proof comes from
  dogfooding `lightsaber-skill` (see Test Plan).

## Tasks

### Production change

- [x] `TemplateHelper.Render<TModel>` (`src/Compono.Generators/Emitters/TemplateHelper.cs`):
      replace the current `template.Render(model)` call with an explicit
      `Scriban.TemplateContext` construction, mirroring exactly what Scriban's own default
      `Render(model)` overload does internally (`ScriptObject` + `Import(model)`,
      `PushGlobal`) except for setting `LoopLimit` and `LimitToString` — confirmed feasible
      during RESEARCH-0034's own measurement tooling (a temporary, worktree-only patch used
      the same shape). Kept the existing `TemplateCache` unchanged; only the render call
      itself changed. `Scriban.Runtime.ScriptObject`'s `Import(model)` uses the same
      reflection-based member import `Template.Render(model)` already used (confirmed no new
      trimming/AOT warning surface — this project has no trim analysis enabled, and the call
      was already present via the convenience overload).
- [x] Both limits are named internal constants (not bare literals), each with a doc comment
      pointing at RESEARCH-0034 for the evidence behind the number, not public/configurable:
      `ScribanLoopIterationLimit = 20_000` and `ScribanOutputCharacterLimit = 20_000_000`.
- [x] Confirmed no other call site in `src/Compono.Generators` renders a template through a
      different path that would bypass this change — `TemplateHelper.Render` is the single
      render entry point (`TestDoubleEmitter.cs` is its only caller), so the fix is
      centralized.

### Regression test

- [x] Added `test/Compono.Generators.Tests/TestDoubleLargeInterfaceGenerationTests.cs` with a
      synthetic interface generator (253 members) whose parameter/overload mix is
      *representative of RESEARCH-0034's measured `IAmazonS3` shape* (§7's histogram):
      - Majority of methods with 2-3 parameters (150 2-param + 40 3-param, plus the fixed
        `Compute`/`Lookup` members and the 2-/3-param halves of the 10 overload groups).
      - A handful of 0- and 1-parameter methods (10 and 15 respectively).
      - A handful of 5+-parameter methods (15 at 5 params).
      - 10 overload groups (2-param + 3-param pair each), plus the fixed, known-named
        `Lookup` overload pair used directly by the test — exercising the overload-safe-
        matching Configure()/Verify() extension surfaces PR #115 added (RESEARCH-0034 §4/§6),
        the code path whose added iterations caused the original regression.
      - No generic method/property added — not required to prove #142 and would have added
        unrelated complexity to the fixture (per explicit direction).
- [x] The test asserts, in one flow, all of the supported-behavior-contract properties
      RESEARCH-0034 §13 calls for — **not** an internal step-count/`LoopLimit`/`LimitToString`
      assertion:
      1. Zero diagnostics (`GeneratorTestHelpers.CompileAndExecute` asserts
         `result.Diagnostics.Should().BeEmpty()` internally).
      2. No unexpected Compono-specific diagnostic — same assertion covers this.
      3. Generated output **compiles and emits** — `CompileAndExecute` re-parses generated
         trees, adds them to the compilation, and asserts `emitResult.Success`.
      4. The generated double is **actually composable/invocable** — `CompileAndExecute`
         loads the emitted assembly and invokes a real `EntryPoint.Run()` method.
      5. A plain member's `Configure()` path works (`Compute(2, 3).Returns(42)` →
         `client.Compute(2, 3)` returns `42`).
      6. An overloaded member's discriminator-only **and** `...Matching(...)` surfaces
         coexist and dispatch correctly — the `Lookup` 2-/3-parameter overload pair,
         configuring both the 3-parameter overload's discriminator-only fallback and a
         narrower `LookupMatching(...)` override, and asserting all three call shapes
         (2-param, 3-param matched, 3-param fallback) return the expected distinct values.
- [x] The test asserts none of: Scriban's internal `LoopLimit`/`LimitToString` values, step
      counts, or the `1 + 3P` formula RESEARCH-0034 §5 used only for diagnosis — purely
      behavior-level, so it stays valid regardless of future internal template/analyzer
      changes.
- [x] **Red → partial-fix (truncation) → green evidence** captured (see Notes) — reproduced
      against the original unmodified `TemplateHelper.cs` (fails with the original
      `CS8785`/`LoopLimit=1000` exception), then against a `LoopLimit`-only intermediate fix
      (passes the diagnostics check but fails to compile — `CS1513`/`CS0535` from silent
      `LimitToString` truncation), then against the final two-limit fix (passes cleanly).

### Docs

- [x] `docs/research/0034-testdoubles-scriban-loop-limit-issue-142-investigation.md`
      (currently untracked) will be committed as part of this change set — it is the
      investigation record this fix implements against, and now includes §17's
      `LimitToString` follow-up investigation.
- [x] Added this plan's row to `docs/plans/README.md`'s index table.
- [x] Checked `docs/reference/diagnostics.md` and `docs/packages/compono-testdoubles.md` for
      any existing text describing a member-count/interface-size limitation or the `1000`
      iteration/`1 MiB` output ceiling — none found (confirmed, not assumed); no change
      needed.
- [x] No `docs/architecture.md`/`docs/public-api.md` change needed — no public API or
      architecture changes in this fix.

## Critical Files

- `src/Compono.Generators/Emitters/TemplateHelper.cs` — the entire production change.
- `test/Compono.Generators.Tests/` — new large-interface regression test file.
- `docs/research/0034-testdoubles-scriban-loop-limit-issue-142-investigation.md` — committed
  as supporting investigation record.
- `docs/plans/README.md` — index row.
- `docs/plans/0068-testdoubles-scriban-loop-limit-fix-impl-plan.md` — this file.

## Test Plan

Standard bug-fix validation gates for this repo, run in this order:

1. **Targeted generator tests**: the new large-interface regression test
   (`test/Compono.Generators.Tests`), run alone first to confirm red-before/green-after
   behavior (see Tasks above), then as part of the full `Compono.Generators.Tests` suite.
2. **Full solution build**: `dotnet build` — zero errors and no new warnings attributable to
   this change, every TFM the pinned SDK targets (per `AGENTS.md`'s "Build and test"
   section). Pre-existing warnings unrelated to this change (see Notes) don't block this
   gate; a warning newly introduced by this change's files would.
3. **Full solution test**: `dotnet test` — every test project, every TFM, fully green,
   confirming zero regressions in unrelated generator behavior (overload matching,
   sequential responses, DIM fallback, etc. — all of which share `TemplateHelper.Render`
   and are therefore all exercised by this change, even though none of their own template
   logic changes).
4. **Package validation**: this repo's standard `inspect-packed-nupkgs.sh`/package-validation
   step (per `AGENTS.md`) against the packed `Compono`/`Compono.Generators`-bearing packages,
   confirming the packed generator behaves identically to the in-repo build.
5. **AOT/trimming relevance**: no new AOT proof needed — this change is entirely
   generator-build-time behavior (a Scriban render-time configuration inside the generator
   process itself) with no runtime code-shape or trimming-surface impact on any generated
   double. Confirmed: `Compono.TestDoubles.AotSmokeTest` builds cleanly (0 warnings, 0
   errors) and every AOT smoke test project passed as part of the full solution test suite
   (step 3) — sufficient confirmation; no dedicated AOT publish-and-run re-proof was
   warranted for this specific change.
6. **Dogfood validation against `lightsaber-skill`** (this fix's real-world proof, and the
   one gate this repo's own `AGENTS.md` treats as non-negotiable for a
   substantive-generator-behavior change with an active dogfood consumer):
   - Run `scripts/dogfood-validate.sh` against `ncipollina/lightsaber-skill` (the identified
     real consumer for this issue), packing every Compono package that consumer actually
     depends on (`Compono`, `Compono.TestDoubles`, `Compono.XunitV3` — per
     `lightsaber-skill/Directory.Packages.props`) at one shared, unique local prerelease
     version, via the script's standard temporary `Directory.Packages.props` override
     mechanism — **no `ProjectReference` bypass, no ad hoc package substitution**.
   - Assert the script confirms `lightsaber-skill` actually resolved the freshly-packed
     version for all three packages (not a stale cache hit), per the script's existing
     verification behavior.
   - Run the consumer's **full test suite** through the script (not an isolated single
     test), per this repo's dogfood-validate discipline and this task's explicit
     instruction to run the full consumer validation the script defines, not a narrowed
     subset.
   - Confirm specifically, as part of that full-suite run: `LightsaberHandlerTests`' three
     `IAmazonS3`-dependent call sites (RESEARCH-0034 §2 — the two `[Compose<GeneratedTestDoublesProfile>]`
     parameter-injection tests plus the direct `composer.Create<IAmazonS3>()` call) compile,
     receive a real generated `IAmazonS3` double, no longer trigger `CS8785`, no longer lose
     the unrelated `Configure()`/`Verify()` surfaces for the mediator/logging interfaces that
     collaterally failed before this fix (RESEARCH-0034 §2's ~24 `CS1061`/`CS7036` errors),
     and pass.
   - This is a **push discipline**, not a merge gate this Plan can defer: per `AGENTS.md`'s
     "Consumer/dogfood validation gate," the exact working tree pushed for this fix must
     clear this gate freshly — not a dogfood run performed against an earlier draft of the
     `TemplateHelper` change.
7. **Final git-status hygiene**: confirm the working tree contains exactly the files listed
   under Critical Files (plus this plan) before any commit — no stray temporary
   test/measurement artifacts left over from implementation (mirroring the discipline
   RESEARCH-0034's own investigation followed when cleaning up its temporary worktrees and
   probe tests).

## Notes

**Mid-implementation discovery: `LimitToString` (this plan's biggest deviation from its
original scope).** After implementing the `LoopLimit = 20_000` fix as originally planned and
writing the ~250-member regression test, the test failed — not with the original `CS8785`,
but with `CS1513`/`CS0535` (unbalanced braces, missing interface members) from the compiled
generated code. Investigation (captured in full as RESEARCH-0034 §17) found a second,
independent Scriban safety default: `TemplateContext.LimitToString` (`1,048,576` characters),
which silently truncates total rendered output and appends `"..."` once hit — no exception,
no diagnostic. Real `IAmazonS3`'s complete, untruncated output is 2,206,258 characters (more
than double the default), so raising `LoopLimit` alone was **necessary but not sufficient**
to fix #142. A follow-up investigation (also RESEARCH-0034 §17) confirmed the generated
output's size is a reasonable consequence of `IAmazonS3`'s size and Compono's accepted
strongly-typed API (linear scaling in member/parameter/overload count, no combinatorial
blowup), with one quantified but non-blocking exception (≈31% of output is repeated
explanatory comments — a documented future hygiene candidate, not addressed here). Per
product-owner decision, this plan's scope was amended in place to configure **both** limits:
`LoopLimit = 20_000` and `LimitToString = 20_000_000`, the latter sized to give the same
demonstrated 1,800-member extreme-synthetic-stress shape (17,345,696 characters, used to
originally justify `LoopLimit`'s value) headroom under this limit too.

**Final `TemplateHelper.Render` implementation:**

```csharp
private const int ScribanLoopIterationLimit = 20_000;
private const int ScribanOutputCharacterLimit = 20_000_000;

public static string Render<TModel>(string resourceName, TModel model)
{
    var template = TemplateCache.GetOrAdd(resourceName, LoadTemplate);

    var scriptObject = new ScriptObject();
    scriptObject.Import(model);
    var context = new Scriban.TemplateContext
    {
        LoopLimit = ScribanLoopIterationLimit,
        LimitToString = ScribanOutputCharacterLimit,
    };
    context.PushGlobal(scriptObject);

    return template.Render(context);
}
```

This replicates exactly what Scriban's own `Template.Render(model)` convenience overload
does internally (`ScriptObject` + `Import` + `PushGlobal`, per Scriban's `Template.cs`
source) — the only difference is the two explicit limits, since that convenience overload
has no way to accept a caller-supplied `TemplateContext`. `Render(TemplateContext)` is
Scriban's own supported customization point for exactly this need; no smaller API exists.
Model-import/member-renaming/member-filter behavior is unchanged (Compono never passed a
custom renamer/filter, and `TemplateContext`'s own default `MemberRenamer` is already
`StandardMemberRenamer.Default`, matching what the convenience overload would have set).

**Red → partial-fix (truncation) → green evidence:**

1. **Red** (original, unmodified `TemplateHelper.cs`, `git stash` used to isolate): the new
   253-member regression test fails with:
   ```
   Expected result.Diagnostics to be empty because code should be generated without errors, but found:
     - CS8785: Generator 'ComponoIncrementalGenerator' failed to generate source. ... Exception was
       of type 'ScriptRuntimeException' with message 'TestDouble.scriban(373,24) : error :
       Exceeding number of iteration limit `1000` for loop statement.'.
   ```
2. **Partial fix** (`LoopLimit = 20_000` only, `LimitToString` intentionally omitted from the
   `TemplateContext` for this evidence capture): diagnostics are empty (no `CS8785`), but the
   compile step fails:
   ```
   Expected emitResult.Success to be True because generated code should compile and emit without
   errors, but found:
     - CS1513: } expected
     - CS1513: } expected
     - CS0535: 'TestNamespace_IBigInterface_ea2cdbc9_Double' does not implement interface member
       'IBigInterface.Filler_P2_50(int, int)'
     ... (125 more CS0535 errors for members past the truncation point)
   ```
3. **Green** (both limits configured): `LargeRepresentativeInterface_GeneratesCleanlyAndProducesAUsableDouble`
   passes — `[+1/x0/?0]`, ~2.8s.

**Full validation results:**

- `Compono.Generators.Tests` (targeted, then full suite): 724/724 passing (362 × net10.0 +
  362 × net11.0), 0 failures.
- Full solution `dotnet build`: 0 errors. 104 pre-existing warnings, all confirmed unrelated
  to this change (NU1902 package-advisory warnings + pre-existing xUnit1051/xUnit1031
  analyzer warnings in `Compono.DependencyInjection.Tests`) — grepping the build output for
  the two changed/new files (`TemplateHelper.cs`, `TestDoubleLargeInterfaceGenerationTests.cs`)
  returned zero warnings from either.
- Full solution `dotnet test`: 3,999/3,999 passing across every test project and every TFM
  (net8.0/9.0/10.0/11.0 as applicable), 0 failures.
- Package validation (`inspect-packed-nupkgs.sh` against all 13 publishable packages freshly
  packed to a temp directory, Release configuration): "All package-contents assertions
  passed."
- AOT: `Compono.TestDoubles.AotSmokeTest` builds cleanly (0/0); every AOT-related test project
  passed as part of the full solution test run above.
- Dogfood validation (`scripts/dogfood-validate.sh --consumer-repo
  /Users/ncipollina/source/repos/ncipollina/lightsaber-skill --packages "Compono
  Compono.TestDoubles Compono.XunitV3"`): **PASS**. The script confirmed "every resolved
  Compono/Compono.NSubstitute/Compono.TestDoubles/Compono.XunitV3 reference resolves to
  99.0.0-local.20260915093603-11985-31631" (the freshly-packed shared local version, not a
  stale cache hit), then ran the consumer's full test suite: 77/77 passing, including
  `LightsaberHandlerTests`' `IAmazonS3`-dependent tests (both `[Compose<GeneratedTestDoublesProfile>]`
  parameter-injection cases and the direct `composer.Create<IAmazonS3>()` call) — no
  `CS8785`, no truncation-caused compile errors, no lost `Configure()`/`Verify()` surfaces
  for unrelated interfaces. The consumer repo's `git status` was confirmed identical before
  and after the run (a pre-existing, unrelated uncommitted `Directory.Packages.props` change
  already present in that repo before this work started — not touched by the dogfood script,
  which never writes to a consumer's real `Directory.Packages.props`).
- Final git-status hygiene: working tree contains exactly `docs/plans/README.md` (modified),
  `src/Compono.Generators/Emitters/TemplateHelper.cs` (modified), and three new untracked
  files (`docs/plans/0068-...md`, `docs/research/0034-...md`,
  `test/Compono.Generators.Tests/TestDoubleLargeInterfaceGenerationTests.cs`) — no stray
  temporary/measurement artifacts. All temporary investigation tooling (a separate git
  worktree, temp test files, a scratch dump file, a temporary packed-package output
  directory) was created and removed outside this working tree during RESEARCH-0034's
  investigations.

**Deviations from the plan as originally accepted:** the single material deviation is the
`LimitToString` addition itself (discovered during implementation, not anticipated when this
plan was first written) — everything else was implemented exactly as scoped. No AWS SDK
dependency was added anywhere in this repository; no template/analyzer/diagnostic/isolation
work was performed; no multi-file generation was attempted.
