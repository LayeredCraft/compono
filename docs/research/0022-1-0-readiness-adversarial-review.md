# [RESEARCH-0022] 1.0 Readiness Adversarial Review

**Status:** Complete. No product/API-design blocker found. Five
release-mechanics/hardening items accepted as required pre-1.0 work
(tracked below, resolved and verified across two follow-up rounds); five
further items explicitly considered and rejected as unnecessary gates.

## Why this exists

Final release-readiness gate for Compono 1.0, run as an adversarial
review whose job was to actively try to prove Compono should **not** ship
1.0 yet — not to summarize prior plans' conclusions. Covered: public API
freeze, deferred pre/post-1.0 decisions (disposal, async composition,
framework-binder duplication), source-generation/runtime contracts,
Native AOT/trimming, package correctness (all 11 publishable packages),
framework integrations, canonical samples, documentation sync, CI/package
validation gates, release/versioning readiness, and dogfooding.

## Method

Direct repository investigation plus four parallel research forks
(public-API/generator-contract audit; package-correctness/release
audit; framework-integration/canonical-sample audit; AOT/CI-gate audit),
cross-checked against a fresh clean build, full test run, and two real
dogfood-consumer runs (a live external consumer, `trivia-platform`,
restored against freshly-packed current-`HEAD` Compono packages via
`scripts/dogfood-validate.sh`). Three of the four forks were lost
mid-run to a session rate limit; their load-bearing findings were
recovered by direct inspection rather than re-run (see the original
review's own "checks not executed" section for the exact gaps).

## Original findings (as reported, unmodified)

**No BLOCKER found under the review's own criteria** (a finding blocks
1.0 only if fixing it after 1.0 likely requires a breaking change, is a
correctness defect in a supported scenario, is an unstable
generated-code/runtime contract, misstates a package's compatibility
contract, or is a genuine trimming/AOT gap where Compono claims
compatibility). One process-integrity finding was flagged at
blocker-equivalent severity because it undermines confidence in every
other gate the review relied on:

- **[F0] Required status checks on `main` covered only `build / build`**
  (blocker-equivalent, not a numbered Release Task) — `Package
  Validation` and `AOT Validation` (the gates PLAN-0061/0062 built
  specifically for the 1.0 boundary) were not required checks, so either
  could fail red and a PR could still merge.

Six items were originally classified as RELEASE TASKS, numbered F1-F6
below (this numbering is this record's own stable identifier scheme, not
part of the original review's prose — introduced to remove the ambiguity
a reviewer flagged in an earlier draft that referred back to items only
as "Item N above"):

1. **[F1]** `breaking-change` label auto-resolves to `v1.0.0` with no
   separate decision point (ADR-0031 Amendment 5, already deliberately
   wired).
2. **[F2]** Packed-consumer smoke-test coverage in
   `package-validation.yaml` covers only 5 of 11 packages
   (XunitV3/TUnit/MSTest/NUnit/TestDoubles); `Compono`, `Http`, `Logging`,
   `DependencyInjection`, `NSubstitute`, `Bogus` rely on static nuspec
   inspection + AOT-smoke packing only.
3. **[F3]** Install docs universally instruct `--prerelease`.
4. **[F4]** ADR-0031's "rolling two-TFM window" prose appeared to
   disagree with the actual four-TFM (`net8.0;net9.0;net10.0;net11.0`)
   build matrix.
5. **[F5]** `scripts/dogfood-validate.sh` has an undocumented Docker
   prerequisite (its default consumer's repository tests use
   Testcontainers).
6. **[F6]** 20 xUnit-analyzer warnings (`xUnit1031`/`xUnit1051`) in
   `Compono.DependencyInjection.Tests` aren't gated as errors.

Deferred decisions re-verified safely post-1.0, unchanged from prior
research: disposal/lifetime ownership ([RESEARCH-0015](0015-disposal-ownership-research.md),
Outcome C), async composition ([RESEARCH-0016](0016-async-composition-viability-research.md),
Outcome C), framework-binder duplication ([RESEARCH-0019](0019-framework-binder-duplication-spike-scope.md),
still scoped-not-executed, no second drift bug found on independent
re-check of the negative-seed guard across all four binders).

## Disposition after product-owner review

The product owner accepted the original review's core conclusion (no
product/API-design blocker) without reopening it, then narrowed the six
Release Task items:

**Accepted as required pre-1.0 work (4):**

- **[F0]** (blocker-equivalent) — required checks. **Resolved and
  verified live**: `LayeredCraft/.github` PR #5 (merged) opts Compono out
  of safe-settings' `required_status_checks.contexts` management via the
  same `{{EXTERNALLY_DEFINED}}` marker `dynamodb-efcore-provider`/
  `devops-templates` already use — confirmed via
  `gh api repos/LayeredCraft/compono/branches/main/protection`, before
  (`["build / build"]` only, unaffected by the merge) and after applying
  a narrow `PATCH .../branches/main/protection/required_status_checks`
  (not a full protection-object replace) setting
  `["build / build", "package-validation", "aot-gate"]` with
  `strict: true`. Re-read after the change: exactly those three contexts
  present, no literal `{{EXTERNALLY_DEFINED}}` check registered, and every
  other branch-protection field (reviews, `enforce_admins`, linear
  history, force-push/delete, conversation resolution, signatures)
  byte-identical to before.
- **[F3]** — stale `--prerelease` install guidance. Resolved across three
  passes:
  1. First pass: audited and corrected `docs/getting-started/installation.md`,
     all 11 `docs/packages/*.md` guides, `docs/packages/index.md`,
     `docs/migrating-from-autofixture.md`, and `docs/troubleshooting/faq.md`.
     Correction: **most packages already have a stable `0.9.0` release**
     on nuget.org (verified live) — `--prerelease` was already unnecessary
     and wrong for `Compono`/`Compono.XunitV3`/`Compono.TUnit`/
     `Compono.NSubstitute`/`Compono.Bogus`/`Compono.DependencyInjection`/
     `Compono.Http`/`Compono.Logging`/`Compono.TestDoubles` *today*, not
     merely "once 1.0 ships" as the original review assumed. This pass
     kept `Compono.MSTest`/`Compono.NUnit` on `--prerelease`, since
     neither had a stable release (verified live against nuget.org).
  2. A Codex review of PR #131 correctly flagged a bug this introduced:
     `Compono.MSTest`/`Compono.NUnit` each pin their `Compono` dependency
     to their own **exact** package version (`[%(ProjectVersion)]` bracket
     syntax in each `.csproj`'s `PinProjectReferenceVersionsExact`
     target) — verified directly against the packed `.nuspec` on
     nuget.org: `Compono.MSTest`/`Compono.NUnit` `0.10.0-preview.101` both
     declare `<dependency id="Compono" version="[0.10.0-preview.101]" />`.
     The docs' shared "`dotnet add package Compono`" line (resolving
     stable `0.9.0`) followed by `Compono.MSTest`/`Compono.NUnit
     --prerelease` (resolving `0.10.0-preview.101`) taught an invalid
     pairing — NuGet cannot satisfy an exact `[0.10.0-preview.101]`
     dependency with an already-resolved `0.9.0` core.
  3. Before that fix shipped, the product owner directed a different,
     simpler resolution: `Compono.MSTest`/`Compono.NUnit` are about to go
     stable together with every other package immediately after this PR
     merges, so the docs now install all six packages
     (`Compono`/`XunitV3`/`TUnit`/`MSTest`/`NUnit` plus the four
     independent add-ons) the same way, with no `--prerelease` anywhere
     and no stable/preview distinction — correct once that release lands,
     and this record notes plainly that the docs are written slightly
     ahead of the actual publish rather than pretending otherwise.
- **[F4]** — the ADR-0031 TFM discrepancy. **Investigated and found to be a
  false positive**, not a real drift: ADR-0031's own **Amendment 3**
  (2026-08-10) already records, in the same file, that ADR-0038
  superseded the original two-TFM framing and that
  `net8.0;net9.0;net10.0;net11.0` are all actively-tracked TFMs. The
  original review's finding came from reading only the ADR's original
  "Decision Outcome" prose (2026-08-04) without reading its own later
  amendments in the same document. No ADR/documentation change was
  needed — the decision record was already accurate. Nothing in
  current-facing docs (README, package guides) states a TFM count that
  would need correcting either.
- **[F5]** — dogfood Docker prerequisite. Resolved: `scripts/dogfood-validate.sh`'s
  usage text now states the prerequisite and its failure mode explicitly.
- The README's "Status" section ("APIs are experimental until the first
  public preview") was also already stale *today* — Compono has stable
  `0.9.0` packages, not merely "preview." Corrected to state plainly:
  publicly released, stable `0.x` packages exist, still pre-`1.0` so the
  API can still change.
- **New in this round: Compono skill eval/workspace structure** — brought
  into alignment with the current [Agent Skills eval-workspace
  convention](https://agentskills.io/skill-creation/evaluating-skills).
  Authored `evals.json` moved from the sibling `skills/compono-evals/`
  directory (a 2026-08-07 fix, at the time correct for the ecosystem
  convention as it existed then — see PLAN-0035's own "Real defect"
  note) into `skills/compono/evals/evals.json`, matching the doc's
  canonical `<skill>/evals/evals.json` layout; the generated-workspace
  half (`benchmarks/`, all historical, preserved as-is) moved to a new
  sibling `skills/compono-workspace/`, matching the doc's
  `<skill>-workspace/` sibling convention, with a new
  `skills/compono-workspace/README.md` documenting the current
  `iteration-N/`/`with_skill`+`old_skill`/`timing.json`/`grading.json`/
  `benchmark.json`/`feedback.json` shape for future runs. **Trade-off
  reintroduced, flagged rather than silently accepted:** this move puts
  `evals.json` (51KB, 46 scenarios) back inside the directory `npx skills
  add` copies verbatim to every consumer installing the `compono` skill —
  exactly the bloat PLAN-0035's 2026-08-07 move was built to avoid. The
  current agentskills.io specification and eval-workflow doc were both
  checked directly and neither documents any installer-side exclusion
  mechanism for `evals/`; the doc's own canonical example ships
  `evals/evals.json` (and any `evals/files/`) as part of the installed
  skill. This is a genuine, upstream-level tension between "evals belong
  in the skill directory" (current convention) and "evals shouldn't ship
  to every consumer" (this repo's original, still-valid concern) — not
  something this pass invented or silently resolved. Implemented as
  directed because the instruction was explicit and precisely specified,
  not exploratory; flagged here as a decision the product owner may want
  to revisit if consumer-side skill-install size becomes a real
  complaint (e.g. by asking upstream for a `.skillsignore`-equivalent, or
  reconsidering the trade-off).

**Explicitly rejected as unnecessary pre-1.0 gates (5):**

- **[F2]** (additional packed-consumer smoke projects for the remaining 6
  packages) — the product owner determined the original Logging finding
  was specifically a ProjectReference-vs-packed-MSBuild-assets
  development concern (already correctly scoped and handled — see
  [PLAN-0061](../plans/0061-pre-1-0-cleanup-and-consolidation.md) Phase
  2 and the packed asset's own inline documentation), and that the
  packed nupkg's assets for those 6 packages are already inspected by
  `inspect-packed-nupkgs.sh`. No new concrete evidence of an actual
  uncovered packaging failure mode was presented, so six more
  packed-consumer scenarios were not added.
- **[F6]** (20 xUnit-analyzer warnings) — no shipped-product correctness
  impact; left for separate follow-up.
- Additional release-drafter machinery around `breaking-change → major`
  — that mapping (ADR-0031 Amendment 5) is intentional, already-decided
  SemVer behavior for leaving `0.x`; publishing itself stays
  human-controlled regardless. No new gate added.
- Re-litigating disposal/lifetime ownership, async composition, or
  framework-binder consolidation — the adversarial review found no
  reason for any of these to block 1.0; not reopened.
- Additional AOT projects without a contradicted support claim (e.g. for
  `Compono.Bogus`/`Compono.DependencyInjection`, which document no AOT
  claim either way) — not added; nothing regresses by adding this later.

## Final disposition

**READY FOR 1.0**, pending only the release-execution act itself. The
product owner has stated intent to cut `1.0.0` for every package
immediately once PR #131 merges — F3's final documentation state (§
above) is written for that near-term stable world, not the transient
state that existed while `Compono.MSTest`/`Compono.NUnit` were still
preview-only. All five accepted pre-1.0/hardening items above are
resolved and verified, including the required-status-check change now
confirmed live on `main`. No item originally listed as a Release Task,
and later rejected, should be read as a mandatory gate — each rejection
above is a deliberate product-owner decision, not an oversight.

## Links

- Original adversarial review — conducted in-session; no separate
  research record existed for it prior to this one, which supersedes it
  as the authoritative write-up of both the original findings and their
  disposition.
- [PLAN-0061](../plans/0061-pre-1-0-cleanup-and-consolidation.md),
  [PLAN-0062](../plans/0062-package-validation-gap-fixes.md) — the
  pre-1.0 work this review verified.
- [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md) —
  release/versioning policy, including Amendment 5 (`0.x` graduation)
  referenced above.
- [ADR-0038](../adr/0038-net8-net9-explicit-multi-target.md) — the
  four-TFM decision ADR-0031 Amendment 3 already reconciles against.
- `LayeredCraft/.github` PR #5 — safe-settings repo-managed
  required-checks opt-out for `compono`.
