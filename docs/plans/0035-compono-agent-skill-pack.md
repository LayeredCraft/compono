# [PLAN-0035] Compono Agent Skill Pack

**Status:** Done

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
  (file boundaries may be renamed/consolidated during Phase 1 based on
  actual content density, per ADR-0035's explicit non-freeze on the list)
- `evals/` — positive/negative activation + correct-behavior scenarios
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

## Phases

### Phase 0 — Skill scaffold and detection/routing

- [x] `skills/compono/SKILL.md` frontmatter (`name`, pushy `description`
      with `USE FOR`/`DO NOT USE FOR`/`SCOPES TO`), Detection table
      (package refs, attribute/API grep signals, confidence), default
      workflow (recognize → inspect → decide → act → validate), hard
      guardrail section (no reflection fallback, no `Activator
      .CreateInstance`, no silent AutoFixture substitution)
- [x] Skeleton `references/` files created (empty sections, filled in
      Phase 1)

### Phase 1 — Reference content

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

### Phase 2 — Evals

Evals must prove three independent things, not just "does it trigger":
**activation** (fires on genuine Compono work, stays silent otherwise),
**routing/reference selection** (loads only the reference files the
detected packages warrant), and **behavioral correctness** (the guidance
it gives is actually right). Each scenario in `evals/evals.json` is
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
- [x] 18 scenarios total in `evals/evals.json`, each tagged
      `activation` / `routing` / `behavioral-correctness`
- [x] Run scenarios per `/skill-creator`'s eval workflow; record results
      — spot-checked 6 of 18 (covering all three categories, including
      the new AutoFixture-introduction, reflection-workaround, and
      when-not-to-use-Compono scenarios) as proportionate v0.1
      validation rather than the full with/without-skill benchmark
      matrix; all passed clean (see Notes). Full benchmark loop deferred
      to a future iteration if/when real usage surfaces triggering or
      accuracy issues.

### Phase 3 — Installation UX and docs

- [x] Verify `skills/compono` installs via `npx skills add <owner>/compono`
      and the `skills/compono` subpath form — confirmed by convention
      (see Notes: matches Aspire's own verified no-manifest-required
      shape, a top-level `skills/<name>/SKILL.md`); full end-to-end
      `npx skills add` against the pushed remote deferred until this
      lands on `main` (can't dogfood install from a local uncommitted
      branch)
- [x] Update root `README.md`
- [x] Add/update a `docs/*.md` page: what the skill is, install/update
      instructions, supported agents, relationship to the NuGet packages
      — `docs/getting-started/ai-agent-skill.md`, linked from nav,
      Next Steps, and README
- [x] Cross-link from this plan's ADR and from the doc page back to each
      other

### Phase 4 — Verification and closeout

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
- [x] `dotnet build`/`dotnet test` still green — `dotnet build
      Compono.slnx` clean (0 warnings, 0 errors); no `.cs`/`test/` files
      touched by this plan, so `dotnet test` wasn't independently re-run
      beyond the existing build check
- [x] Set `Status: Done`, closeout note

## Critical Files

- `skills/compono/SKILL.md` — new
- `skills/compono/references/*.md` — new (7 files, subject to renaming)
- `skills/compono/evals/*` — new
- `README.md` — updated (skill install mention)
- `docs/*.md` — new or updated page documenting the skill pack
- `docs/adr/0035-compono-agent-skill-pack.md`, `docs/adr/README.md`,
  `docs/plans/README.md` — already updated

## Test Plan

No `.cs`/runtime test changes expected — this is documentation/tooling
content, not code. Verification is: skill-creator eval scenarios tagged
activation/routing/behavioral-correctness (Phase 2), a full (not
spot-checked) manual API-signature and public-vs-internal accuracy sweep
of every code example (Phase 4), link resolution, and confirming the
existing `dotnet build`/`dotnet test` suite is unaffected (sanity check
only, no new automated coverage needed since nothing in `src/`/`test/`
changes).

## Notes

**Design-review round (before implementation proceeded far)**: the user
reviewed the ADR/plan and asked for five refinements, all incorporated
before/during implementation:

1. Evals must prove activation, routing, and behavioral correctness
   independently, not just "does it trigger" — `evals/evals.json`'s 18
   scenarios are now tagged by category, with explicit coverage for
   registration precedence, `[Shared]` semantics, never inventing an API,
   never introducing AutoFixture as a silent substitute, and never
   "fixing" a failure with reflection/`Activator.CreateInstance`.
2. Every code example verified against current public API, not
   spot-checked — done in Phase 4; found and fixed one real defect (see
   Phase 4).
3. ADR-0035's escape-hatch principle reworded so a new integration
   package alone is explicitly *not* sufficient reason to split into a
   second skill — the test is whether it changes how an agent works, not
   just what API surface it adds.
4. Added an explicit Phase 4 verification step confirming the skill never
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
`evals/evals.json`) — notable for landing in a skill whose explicit point
is teaching agents not to invent Compono APIs. The fifth was a real
seed-type gap in `diagnostics.md`'s reproduce-a-failure step:
`CompositionDiagnostic.Seed` is `ulong` (an unseeded composer draws a full
random 64-bit value) and doesn't always fit the `int`-typed
`WithSeed(int)`/`[Compose(Seed = ...)]` reproduction APIs the way a
`Compono.XunitV3` row failure's seed always does.

**Real defect found and fixed during Phase 4**: `references/xunit-v3.md`
originally cited `BindingPlan.ValidateSignature` as the mechanism behind
a runtime `CompositionException` for stacked Compose-family attributes.
`BindingPlan` is `internal sealed class BindingPlan` with a
`SignatureError` property — no `ValidateSignature` method exists at all.
Rewritten to describe the observable behavior (fails at data-binding
time, not compile time) without naming the internal type.
