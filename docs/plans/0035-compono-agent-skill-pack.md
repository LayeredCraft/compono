# [PLAN-0035] Compono Agent Skill Pack

**Status:** In Progress

**Implements:** ADR-0035

## Goal

A `skills/compono/` skill, installable via `npx skills add <owner>/compono`,
that makes an AI coding agent noticeably better at writing, modifying,
reviewing, and troubleshooting Compono-based unit tests than one relying
only on pretrained knowledge — verified by evals that the skill correctly
activates on genuine Compono work, stays silent on ordinary non-Compono
.NET test work, and every API/example it cites is real and current.

## Scope

Per ADR-0035's Decision Outcome: one skill (`skills/compono/SKILL.md`)
with package-conditional `references/`. In scope:

- `skills/` root structure installable via `npx skills`
- `SKILL.md` — detection, routing, default workflow, guardrails
- `references/` — composition model, registrations/profiles/scopes,
  diagnostics, xunit-v3, nsubstitute, bogus, patterns-and-antipatterns
  (file boundaries may be renamed/consolidated during Group 1 based on
  actual content density, per ADR-0035's explicit non-freeze on the list)
- `skills/compono-evals/` (was `skills/compono/evals/` — moved out so it never ships via `npx skills add`, see Notes) — positive/negative activation + correct-behavior scenarios
- Root `README.md` update (Compono packages table area) documenting the
  skill's existence and install command
- A `docs/*.md` page (or section) explaining what the skill is, how to
  install/update it, and that it's agent guidance, not runtime behavior

Explicitly deferred (not this plan):

- Cross-agent packaging beyond `npx skills` compatibility (Copilot/Codex/
  Cursor-specific marketplace files) — ADR-0035 doesn't require it; revisit
  only if it becomes low-cost and clearly wanted
- Any Compono runtime/API change — if implementation surfaces a real doc
  or API defect, it's called out and scoped as its own fix, not folded in
  here
- A second skill for any future integration package — the escape-hatch
  principle in ADR-0035, not work to do now

**One atomic PR, not phase-per-PR**: the sections below are grouped by
concern (scaffold, reference content, evals, docs, verification) for
readability, not as independent phase boundaries each shipping its own
PR. `design-decisions.md`'s "each phase ships as its own PR" rule applies
to a large or multi-milestone effort where one PR for the whole thing
would be unreviewable — this plan's total diff (one new skill directory
plus a handful of doc-nav updates) doesn't meet that bar, and splitting
it into five artificially-sequenced PRs would have been fragmentation for
its own sake, not genuine independent reviewability. Below, "Task groups"
replaces the earlier "Phases" framing to avoid implying a shipping
promise this plan never intended to keep.

## Task groups

### Group 0 — Skill scaffold and detection/routing

- [x] `skills/compono/SKILL.md` frontmatter (`name`, pushy `description`
      with `USE FOR`/`DO NOT USE FOR`/`SCOPES TO`), Detection table
      (package refs, attribute/API grep signals, confidence), default
      workflow (recognize → inspect → decide → act → validate), hard
      guardrail section (no reflection fallback, no `Activator
      .CreateInstance`, no silent AutoFixture substitution)
- [x] Skeleton `references/` files created (empty sections, filled in
      Group 1)

### Group 1 — Reference content

- [x] `references/composition-model.md` — `Composer`, `Create<T>()`/
      `CreateMany<T>()`, `[Composable]`, discovery, determinism/seeding
- [x] `references/registrations-profiles-and-scopes.md` — `Register<T>()`,
      `For<T>().Use()`/`.Member()`, `ICompositionProfile`, `[Shared]`,
      recursion
- [x] `references/diagnostics.md` — CMP0001–CMP0012 table, runtime
      `CompositionException` tree-path/seed format, reproduce-a-failure
      workflow
- [x] `references/xunit-v3.md`, `references/nsubstitute.md`,
      `references/bogus.md` — package-conditional integration guidance
- [x] `references/patterns-and-antipatterns.md` — guardrail catalog +
      AutoFixture concept-mapping table
- [x] Consolidate/rename any reference file whose content turned out too
      thin to justify a standalone file (per ADR-0035's non-freeze note)
      — all 7 files carry enough distinct content to stand alone; no
      further consolidation needed

### Group 2 — Evals

Evals must prove three independent things, not just "does it trigger":
**activation** (fires on genuine Compono work, stays silent otherwise),
**routing/reference selection** (loads only the reference files the
detected packages warrant), and **behavioral correctness** (the guidance
it gives is actually right). Each scenario in `skills/compono-evals/evals.json` is
tagged with which of the three it targets.

- [x] Activation scenarios — agent activates for genuine Compono work;
      agent does *not* activate for ordinary xUnit/NSubstitute/Bogus
      usage with no Compono involvement; agent does not unilaterally
      introduce Compono into a project that doesn't reference it
- [x] Routing scenarios — agent only recommends `Compono.NSubstitute`
      guidance when that package is referenced; agent only recommends
      `Compono.Bogus` guidance when that package is referenced
- [x] Behavioral-correctness scenarios — agent never invents a Compono
      API; agent does not introduce AutoFixture as a substitute when
      Compono is already in use; agent does not "fix" a composition
      failure with reflection or `Activator.CreateInstance`; agent
      respects registration/rule precedence (duplicate `Register<T>()`
      is a conflict, not an override); agent understands `[Shared]`
      correctly (type-keyed, `Compono.XunitV3`-only, resolves first);
      agent knows when *not* to use Compono (a hand-built value is
      clearer than composing one, even in a Compono-using project)
- [x] 18 scenarios total in `skills/compono-evals/evals.json`, each tagged
      `activation` / `routing` / `behavioral-correctness`
- [x] Manual spot-check pass — 6 of 18 scenarios (covering all three
      categories, including the AutoFixture-introduction,
      reflection-workaround, and when-not-to-use-Compono scenarios) run
      as one-off subagent prompts, self-graded by the subagent against
      the eval's `expectations`, with no independent grader pass. All 6
      read as passing on inspection. Real signal, but explicitly *not*
      `/skill-creator`'s documented eval workflow — no
      `<skill-name>-workspace/` run directories, no with-skill/baseline
      pairing, no `grading.json`/`timing.json` artifacts, no aggregated
      `benchmark.json`.
- [x] Run the actual `/skill-creator` eval workflow across all 18
      scenarios — with-skill + baseline pairs (1 run each, not
      `/skill-creator`'s default 3, per the honest scope note in
      `skills/compono-evals/benchmarks/2026-08-07/README.md`), independent grading
      (separate grader subagent per scenario, not self-graded),
      `benchmark.json`/`benchmark.md` aggregated via
      `scripts.aggregate_benchmark`. **Result: 97.4% pass rate with the
      skill (38/39 assertions) vs. 56.4% without it (22/39)** — summary
      artifacts and per-scenario grading committed at
      `skills/compono-evals/benchmarks/2026-08-07/`. See that directory's README for
      known limitations (single run per config, baseline wasn't
      repo-isolated, no timing data) and eval-quality feedback the
      graders surfaced for a future `evals.json` revision.

### Group 3 — Installation UX and docs

- [x] Layout matches the convention microsoft/aspire-skills uses
      successfully (a top-level `skills/<name>/SKILL.md`, no separate
      manifest file required — see Notes). This is evidence the *shape*
      is right, not evidence the install path actually works end to end.
- [ ] Run a real `npx skills add LayeredCraft/compono` (and/or the
      `skills/` subpath form) against a merge-ready ref and record the
      command and its output. Not yet done — the layout-convention match
      above was previously written up in a way that could read as
      "verified"; it wasn't. This is genuinely outstanding, not merely
      deferred, and should happen before or immediately after this lands
      on `main`.
- [x] Update root `README.md`
- [x] Add/update a `docs/*.md` page: what the skill is, install/update
      instructions, supported agents, relationship to the NuGet packages
      — `docs/getting-started/ai-agent-skill.md`, linked from nav,
      Next Steps, and README
- [x] Cross-link from this plan's ADR and from the doc page back to each
      other

### Group 4 — Verification and closeout

- [x] Every API/attribute/type named in the skill grepped against `src/`
      to confirm it's real and current — full sweep of every code
      example and every named symbol across `SKILL.md` and all 7
      `references/*.md` files (144 unique backtick-quoted identifiers
      enumerated and checked), not a spot-check
- [x] Every code example verified against current public API signatures
      (parameter order, overloads, defaults) — found and fixed one real
      defect: `xunit-v3.md` cited a non-existent `BindingPlan
      .ValidateSignature` method (the actual type is `internal sealed
      class BindingPlan` with a `SignatureError` property, no such
      method) — rewritten to describe the observable behavior without
      naming the internal type or an invented member
- [x] Confirm the skill never references internal implementation types,
      generator internals, test-only helpers, or any API that's visible
      in the repository but not intended for consumers — swept for this
      specifically; `PlanCache<T>`, `NSubstituteProvider`,
      `BogusMemberNameProvider`, `ProfileCycle`, `UniqueValueResolver`,
      `ICompositionContext.Resolve<T>()` (descriptor-less overload) are
      all confirmed `public` and already part of the published API
      reference site, so describing them is fine; added one clarifying
      note in `composition-model.md` that the descriptor-taking
      `Resolve<T>(...)` overload is generated-code-only, not something
      to hand-write; confirmed `CMP0003`'s "historical/rare, not reached
      via ordinary composition" claim against `LeafTypeClassifier
      .IsProviderResolved` (interfaces/abstract/delegate types are
      classified provider-resolved before ever reaching
      `ConstructorSelector`, so its `CMP0003` checks for those shapes are
      unreachable via the normal discovery path)
- [x] Links resolve — `mkdocs build --strict` clean, no warnings/errors
- [x] Confirm ordinary non-Compono test work doesn't trigger the skill —
      eval scenarios 8/9/10/14 (activation category), all spot-checked
      clean
- [x] Confirm optional-integration guidance only fires when that package
      is referenced — eval scenarios 3/5/18 (routing category); 3 and 5
      spot-checked clean, 18 documented not run live (same pattern as 3)
- [x] `dotnet build`/`dotnet test` — `dotnet build Compono.slnx` clean (0
      warnings, 0 errors). `dotnet test Compono.slnx` (both Debug and the
      documented `-c Release`) fails with a Microsoft Testing Platform
      handshake error across every test project, including ones this
      plan never touches — confirmed to be a local `dotnet test` CLI
      orchestration issue, not a real test failure, by running each
      compiled test executable directly instead of through the `dotnet
      test` driver: `Compono.Tests` (213/213), `Compono.Generators.Tests`
      (84/84), `Compono.XunitV3.Tests` (47/47),
      `Compono.NSubstitute.Tests` (23/23), `Compono.Bogus.Tests` (63/63)
      — 430/430 passing. No `.cs`/`test/` files are touched by this plan,
      consistent with a pre-existing local-environment issue rather than
      a regression from this change; worth a separate look (CI almost
      certainly isn't affected, since it presumably isn't hitting this
      handshake failure on every PR, but that's an assumption, not
      verified here).
- [ ] Set `Status: Done`, closeout note — not yet; two real items remain
      open in Group 2 and Group 3 above (the actual `/skill-creator` eval
      workflow, and a real `npx skills add` run). `Status` reverted from
      `Done` to `In Progress` during the PR #63 review round below rather
      than leave a completion record two of its own checked items
      contradicted.

## Critical Files

- `skills/compono/SKILL.md` — new
- `skills/compono/references/*.md` — new (7 files, subject to renaming)
- `skills/compono-evals/*` — new
- `README.md` — updated (skill install mention)
- `docs/*.md` — new or updated page documenting the skill pack
- `docs/adr/0035-compono-agent-skill-pack.md`, `docs/adr/README.md`,
  `docs/plans/README.md` — already updated

## Test Plan

No `.cs`/runtime test changes expected — this is documentation/tooling
content, not code. Verification is: skill-creator eval scenarios tagged
activation/routing/behavioral-correctness (Group 2), a full (not
spot-checked) manual API-signature and public-vs-internal accuracy sweep
of every code example (Group 4), link resolution, and confirming the
existing `dotnet build`/`dotnet test` suite is unaffected (sanity check
only, no new automated coverage needed since nothing in `src/`/`test/`
changes).

## Notes

**Design-review round (before implementation proceeded far)**: the user
reviewed the ADR/plan and asked for five refinements, all incorporated
before/during implementation:

1. Evals must prove activation, routing, and behavioral correctness
   independently, not just "does it trigger" — `skills/compono-evals/evals.json`'s 18
   scenarios are now tagged by category, with explicit coverage for
   registration precedence, `[Shared]` semantics, never inventing an API,
   never introducing AutoFixture as a silent substitute, and never
   "fixing" a failure with reflection/`Activator.CreateInstance`.
2. Every code example verified against current public API, not
   spot-checked — done in Group 4; found and fixed one real defect (see
   Group 4).
3. ADR-0035's escape-hatch principle reworded so a new integration
   package alone is explicitly *not* sufficient reason to split into a
   second skill — the test is whether it changes how an agent works, not
   just what API surface it adds.
4. Added an explicit Group 4 verification step confirming the skill never
   teaches internal/generator-internal/non-consumer-facing API as
   something to use.
5. Added eval scenario 15 (age-boundary test) proving the skill
   recommends literal values over composition when that's genuinely
   clearer, even in a Compono-adopting project — directly exercises the
   "When not to use Compono" section.

**Eval execution**: 18 scenarios authored across all three categories,
6 spot-checked live via subagents (one per category from the original
set, plus all three of the new critical guardrail scenarios — reflection
refusal, AutoFixture-swap refusal, when-not-to-use-Compono). All 6 passed
clean on first run — no skill revision needed. Full with/without-skill
benchmark matrix (all 18 × 2 configurations × N runs, per `/skill-creator`'s
complete workflow) deliberately deferred as disproportionate for a v0.1
skill pack; revisit if real-world usage surfaces triggering or accuracy
problems the spot-checks didn't catch.

**PR #63 Copilot review (post-merge-request)**: 5 inline findings, all
confirmed real and fixed (commit `f0a368b`). Four were the same class of
defect — `Composer.Create<T>()` written as if `Create<T>()`/`CreateMany<T>()`
were static generics on `Composer`, when they're instance methods on the
`Composer` the static, non-generic `Composer.Create(...)` returns
(`SKILL.md`, `composition-model.md`, `registrations-profiles-and-scopes.md`,
`skills/compono-evals/evals.json`) — notable for landing in a skill whose explicit point
is teaching agents not to invent Compono APIs. The fifth was a real
seed-type gap in `diagnostics.md`'s reproduce-a-failure step:
`CompositionDiagnostic.Seed` is `ulong` (an unseeded composer draws a full
random 64-bit value) and doesn't always fit the `int`-typed
`WithSeed(int)`/`[Compose(Seed = ...)]` reproduction APIs the way a
`Compono.XunitV3` row failure's seed always does.

**Real defect found and fixed during Group 4**: `references/xunit-v3.md`
originally cited `BindingPlan.ValidateSignature` as the mechanism behind
a runtime `CompositionException` for stacked Compose-family attributes.
`BindingPlan` is `internal sealed class BindingPlan` with a
`SignatureError` property — no `ValidateSignature` method exists at all.
Rewritten to describe the observable behavior (fails at data-binding
time, not compile time) without naming the internal type.

**PR #63 human review (Jonas / `j-d-ha`, `🛑 Request changes`)**: 11
inline findings (5 🐛, 6 ⚠️ per this repo's review-emoji convention), all
confirmed real against source and fixed:
- `SKILL.md`'s frontmatter `description` was 1523 chars folded, over
  `skill-creator`'s 1024-char validator limit — trimmed to 914.
- `diagnostics.md`'s seed-reproduction step (already partly rewritten for
  the Copilot round above) still implied a "supported reproduction path"
  for an out-of-`int`-range `ulong` diagnostic seed that doesn't actually
  exist — rewritten to say so plainly instead of hand-waving an
  alternative.
- This plan's own "Phases" framing implied phase-per-PR shipping per
  `design-decisions.md`'s rule, but all five landed in one PR — reframed
  as "Task groups" with an explicit note on why one atomic PR was the
  right call here (small, tightly-coupled scope, not a large/
  multi-milestone effort).
- The eval-workflow-completion and `npx skills` install-verification
  claims both overstated what was actually done — split each into a
  checked item for the real, narrower thing that happened and an
  unchecked item for the genuinely outstanding work; `Status` reverted
  from `Done` to `In Progress` accordingly.
- The `dotnet test` claim was self-contradictory (claimed green, then
  admitted not independently run) — actually run; `dotnet test`'s CLI
  driver hits a local Microsoft Testing Platform handshake error on
  every project (including ones this plan never touches), but every
  compiled test executable run directly passes clean (430/430 across the
  5 core test projects) — recorded as a local-environment issue to look
  at separately, not a regression from this change.
- `SKILL.md`'s hardcoded `0.x.y-preview.N`/`--prerelease` version claim
  was stale — the repo's actual published version policy has moved on;
  removed the hardcoded claim and pointed at `installation.md` instead of
  duplicating a fact that changes independently of this skill.
- The no-retry `CompositionException` guardrail over-generalized —
  scoped to Compono's own deterministic generated/built-in path, with an
  explicit call-out that consumer-supplied factories/providers/
  `IServiceProvider` can be genuinely non-deterministic.
- `composition-model.md`'s "rebuild throws away seed/config" rationale
  was inaccurate — corrected to distinguish a seeded rebuild (stays
  reproducible) from an unseeded one (draws a fresh random seed each
  time).
- `docs/getting-started/ai-agent-skill.md`'s Update section claimed
  re-running `add` overwrites an install, unverified — replaced with the
  real, documented `npx skills update compono` command.
- `docs/documentation-architecture.md` still declared 5 Getting Started
  pages and omitted the new `ai-agent-skill.md` from its canonical tree —
  added an entry with audience/purpose/handoff, consistent with every
  other page's treatment.

**Full `/skill-creator` benchmark run (2026-08-07, closing the eval-workflow
gap Jonas flagged)**: ran the real workflow — 36 subagent runs (18 evals
× with-skill/baseline), 18 independent grader subagents (one per eval,
grading both variants against the eval's own `expectations`), aggregated
via `scripts.aggregate_benchmark`. **97.4% pass rate with the skill
(38/39) vs. 56.4% without (22/39)** — a real, evidence-backed gap.
Artifacts committed at `skills/compono-evals/benchmarks/2026-08-07/`
(summary + per-scenario grading, not raw transcripts, per the chosen
scope). Honest limitations recorded in that directory's own README: one
run per configuration rather than three, no timing/token capture, and a
methodology gap multiple graders independently flagged — the baseline
subagents kept full repo filesystem access even though told not to read
the skill, and at least one (eval 9) still produced accurate
Compono-specific terminology, likely by exploring the repo directly. That
means the true skill-driven gap is probably *larger* than 97.4/56.4
against a genuinely repo-isolated baseline, not smaller. Graders also
surfaced concrete eval-quality feedback (several assertions pass
regardless of skill use) — recorded as a follow-up, not acted on in this
pass. The one remaining Group 3 item (a real `npx skills add` run against
a merge-ready ref) is still outstanding, so `Status` stays `In Progress`.

**Real defect: `evals/` was inside the installable skill directory**
(caught by the user after reading `benchmark.md`, not by any review
round). Checked `npx skills`' actual install behavior against its real
source (`vercel-labs/skills`, `src/add.ts`): a disk-based install does a
recursive `copyDirectory` of the whole skill folder, excluding only
`.git` — no `.skillignore`/manifest mechanism exists to exclude files.
`skills/compono/evals/` (18KB `evals.json` plus the 40-file
`benchmarks/2026-08-07/` directory) would therefore have shipped into
every consumer's `.claude/skills/compono/evals/` on `npx skills add` —
pure internal-QA dead weight with no value to a consumer. Fixed by moving
the whole directory to `skills/compono-evals/` (sibling to
`skills/compono/`, no `SKILL.md` so `npx skills`' discovery never offers
it as an installable skill, and it sits outside `skills/compono/`'s own
copy scope). All path references in this plan updated accordingly. This
is exactly the kind of installation-payload question Group 3's still-open
real `npx skills add` run (above) would also have needed to catch —
another reason that item stays open rather than being treated as
optional polish.
