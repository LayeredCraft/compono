# [ADR-0035] Compono Agent Skill Pack

**Status:** Accepted

**Date:** 2026-08-07

**Decision Makers:** solo (Nick Cipollina), assisted by Claude

## Context

Compono's public preview shipped in Milestone 8: four packages on nuget.org,
a full documentation site, and a clean-room acceptance test proving the
learning path works without internal knowledge. The first post-MVP work
item is developer tooling, not a runtime feature: an AI-coding-agent
"skill" that teaches an agent (Claude Code, and other `npx skills`-
compatible hosts) how to write, modify, review, and troubleshoot unit
tests that use Compono in a *consumer's* test project.

This is necessary because Compono deliberately looks similar to, but
behaves differently from, AutoFixture — the library most agents already
"know" from pretraining. An agent working from general .NET knowledge
will reach for AutoFixture-shaped habits (`[Frozen]`, `ConfigureMembers`,
customization override, reflection fallback) that either don't exist in
Compono or actively conflict with its source-generated, deterministic
design. `docs/research/0001-autofixture-comparison.md` and
`docs/migrating-from-autofixture.md` already catalog this gap from real
migration evidence (Milestone 7); this ADR is about encoding that
hard-won knowledge into something an agent consults *before* writing
code, not just something a human reads.

This repo already installs skills itself via `npx skills` (see
`skills-lock.json`, `.agents/skills/`, `.claude/skills/`) and follows the
`.agents/skills/engineering-workflow` design process for exactly this
kind of decision.

## Decision Drivers

- The skill must reflect Compono's actual shipped public API — no
  invented APIs, no forward-looking/roadmap content presented as current.
- Triggering accuracy: activate for genuine Compono test-authoring work,
  never hijack ordinary non-Compono .NET test work.
- Context efficiency: an agent shouldn't have to load NSubstitute-specific
  or Bogus-specific guidance for a project that doesn't reference those
  packages.
- Maintainability: adding a future integration package (a new test
  framework, mocking library, Verify, etc.) shouldn't require restructuring
  the whole skill.
- Installability: must work via `npx skills add <owner>/compono` and the
  `skills/` subpath form, per the `npx skills`/skills.sh convention this
  repo already uses for its own tooling.
- Guardrail strength: the skill's primary value is stopping AutoFixture-
  habit mistakes before they're written, not documenting the happy path.

## Considered Options

1. **One skill** (`skills/compono/`) — single `SKILL.md` with detection,
   routing, default workflow, and guardrails in the body; deep material in
   `references/`, loaded conditionally by which packages are detected.
2. **Router + focused workflow skills** (mirroring
   [microsoft/aspire-skills](https://github.com/microsoft/aspire-skills)
   more literally) — a top-level `compono` router skill plus separate
   skills for, e.g., authoring vs. diagnostics vs. configuration.
3. **Core + per-integration skills** — `compono` (core) plus
   `compono-xunit`/`compono-nsubstitute`/`compono-bogus` as independent
   skills, split along package boundaries.

## Decision Outcome

Chosen option: **1, one skill** (`skills/compono/`), with progressive
disclosure through `references/`.

Compono is a single, coherent agent workflow — recognize the project uses
Compono → inspect the type/collaborators → decide whether/how to compose
→ act → validate — regardless of which optional packages
(`Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`) are installed.
A task like "compose a theory with a shared NSubstitute double and a
Bogus-generated email" touches all three integration surfaces *in one
decision*, not three sequential workflows. Aspire's multi-skill split is
justified there because its sub-skills are genuinely different
*operational domains* with different tools and blast radius — scaffolding
(`aspire-init`), process lifecycle/CLI safety (`aspire-orchestration`),
cloud/CI (`aspire-deployment`), observability tooling
(`aspire-monitoring`). Compono has no such domain split: everything is
"write or fix C# test code against one composition API." Splitting it
would force either constant cross-skill handoff mid-task, or duplicated
core-composition explanation copy-pasted into every sub-skill.

**Studied from [microsoft/aspire-skills](https://github.com/microsoft/aspire-skills)
and adopted, adapted to a single skill instead of six:**

- The `description` frontmatter as the actual triggering/boundary
  mechanism: a bold skill-type tag, `USE FOR:` (concrete signals/phrases),
  `DO NOT USE FOR: (use X)` redirects, and — since there's no sibling to
  hand off to — a `SCOPES TO:` note on which reference files apply given
  detected packages, replacing Aspire's `INVOKES:` (which names sibling
  skills we don't have).
- A **Detection table** (signal → how to detect → confidence) gating
  which `references/` files are relevant, adapted from the router
  skill's pattern but living in the one `SKILL.md` instead of a separate
  router file.
- **Guardrails separated by severity**: hard "never do this" rules
  (no reflection fallback, no `Activator.CreateInstance` workaround, no
  silent AutoFixture substitution) get a top-of-file refusal section like
  `aspireify`'s `.aspire/modules/` rule; softer per-topic guardrails live
  in `references/patterns-and-antipatterns.md` with the reasoning, not
  just the rule.
- **Evals with a `skill-invocation`-equivalent check** — positive
  activation (genuine Compono test work), negative activation (ordinary
  xUnit/NSubstitute/Bogus work with no Compono involvement), and
  correct-behavior scenarios (right API chosen, registration precedence
  respected, no invented APIs) — scaled down from Aspire's 167-stimulus,
  CI-gated suite to a handful of scenarios proportionate to one skill.

**Deliberately not adopted**: Aspire's self-deactivating one-time skill
pattern (`aspireify`'s SCAN→PROPOSE→EDIT→VALIDATE→DEACTIVATE) — Compono
has no one-time scaffolding phase distinct from ongoing authoring; adding
Compono to a project and writing a Compono test are the same kind of
"compose something" task, not two phases of one bigger job.

**Reference file set** (subject to renaming/consolidation during
implementation — see the escape-hatch principle below; this is a starting
shape, not a frozen list):

- `composition-model.md` — `Composer`, `Create<T>()`/`CreateMany<T>()`,
  `[Composable]`, generated-plan discovery, determinism/seeding (folded in
  rather than split out — seeding is inseparable from how a composition
  path is derived, not a separate workflow)
- `registrations-profiles-and-scopes.md` — `Register<T>()`,
  `For<T>().Use()`/`.Member()`, `ICompositionProfile`, `[Shared]`,
  recursion detection
- `diagnostics.md` — the CMP0001–CMP0012 compile-time table, the runtime
  `CompositionException`/tree-path/seed format, and the reproduce-a-
  failure workflow
- `xunit-v3.md` — `[Compose]`/`[Compose<TProfile>]`/`[Shared]` in test
  methods (only relevant if `Compono.XunitV3` is referenced)
- `nsubstitute.md` — `UseNSubstitute()`, substitutable-shape rules (only
  relevant if `Compono.NSubstitute` is referenced)
- `bogus.md` — `UseBogus()`/`UseBogus<T>()`, conventions/aliases (only
  relevant if `Compono.Bogus` is referenced)
- `patterns-and-antipatterns.md` — the guardrail/anti-pattern catalog,
  including the AutoFixture concept-mapping table (folded in here rather
  than a separate migration file — the migration guidance *is* the
  antipattern catalog, framed from the AutoFixture-habit direction)

**Escape-hatch principle for future growth** (the actual reusable
decision this ADR records, per the user's explicit direction during
design review): start with one skill because Compono today represents a
single cohesive agent workflow. Split into additional skills only when a
future capability develops **distinct activation signals, workflows,
tooling requirements, or context needs** that make the single-skill model
inefficient or ambiguous — e.g. a future `Compono.Verify` or
`TUnit`/`NUnit` integration that introduces a genuinely different
operational mode, not just another `UseX()` call inside the same
authoring loop. **The existence of a new integration package alone is not
sufficient reason to split** — the test is whether it changes *how* an
agent works, not merely *what* API surface it adds. That split, if it
ever happens, is itself a new deep-dive design decision (a new ADR), not
something this ADR pre-commits to a shape for.

### Positive Consequences

- One `SKILL.md` to keep in sync with the API surface; no duplicated
  core-composition explanation across sibling skills.
- Package-conditional loading keeps context lean without a router skill's
  indirection overhead.
- Simple installation story: `npx skills add <owner>/compono` or the
  `skills/compono` subpath, matching this repo's own tooling convention.

### Negative Consequences

- If Compono's package surface grows substantially (several new
  integrations at once), `SKILL.md`'s Detection/Routing section could
  grow unwieldy before a split is warranted — mitigated by the
  escape-hatch principle above and by `references/` absorbing the actual
  bulk of new content, not the routing table.
- A single skill can't express Aspire-style hard operational boundaries
  between sub-domains, because Compono doesn't have any today — if that
  changes, this ADR's Decision Outcome would need superseding, not
  amending.

## Pros and Cons of the Options

### Option 1 — One skill

- Good, because it matches Compono's actual single-workflow shape.
- Good, because it avoids cross-skill handoff for tasks that legitimately
  span two or three integration packages at once.
- Good, because `references/` already gives context-window efficiency
  without needing a router.
- Bad, because it doesn't scale indefinitely — mitigated by the
  escape-hatch principle.

### Option 2 — Router + focused workflow skills

- Good, because it directly mirrors the studied reference architecture.
- Bad, because Compono has no genuinely distinct operational domains to
  route between today — the split would be along API-surface lines, not
  workflow lines, which is exactly the "arbitrary API categories" split
  the design brief warned against.
- Bad, because every real task (compose a test with a shared substitute
  and semantic data) would still need multiple skills active at once,
  producing router overhead with no triggering-accuracy benefit.

### Option 3 — Core + per-integration skills

- Good, because it's easy to reason about "does this skill apply" per
  installed package.
- Bad, because it splits along package boundaries, not workflow
  boundaries — the same "compose a test" decision (which value comes from
  where: registration, rule, provider) gets fragmented across skills for
  no navigational benefit, since `references/` already achieves the same
  package-conditional loading inside one skill.
- Bad, because core composition-model knowledge (constructor selection,
  `[Composable]`, diagnostics) would need restating or cross-referencing
  in every integration skill.

## Links

- [Aspire skills repository](https://github.com/microsoft/aspire-skills) — architectural reference studied for this decision
- `docs/research/0001-autofixture-comparison.md` — source of the AutoFixture-habit gap evidence this skill encodes
- `docs/migrating-from-autofixture.md` — the human-facing counterpart this skill's `patterns-and-antipatterns.md` draws from
- `docs/mvp.md` Milestone 8 closeout — the public-preview release this skill pack follows
- `.agents/skills/engineering-workflow/references/design-decisions.md` — the process this ADR follows
