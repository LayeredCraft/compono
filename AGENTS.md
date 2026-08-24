# AGENTS.md

Instructions for any coding agent working in this repository.

## What this repo is

Compono is a source-generated test composition framework for .NET (see
`README.md` for the product pitch). It's pre-1.0 and still building out
Milestone 1 — see `docs/mvp.md` for the full roadmap and
`docs/plans/0001-milestone-1-source-generation-foundation.md` for exactly
how far Milestone 1 itself has gotten (it's a phased plan, not
all-or-nothing).

## Core philosophy

These shape countless small decisions, not just the big ones. Full detail
in `docs/manifesto.md` and `docs/design-principles.md`; the load-bearing
points:

- **Composition over generation.** Compono coordinates every contributor
  a test needs (constructor dependencies, shared instances, test doubles,
  semantic data), not just an object factory.
- **Source-generated first, no reflection fallback by default.**
  Generated composition plans are the primary execution mechanism
  (`docs/adr/0001-source-generation-first.md`) — don't reach for runtime
  reflection to unblock a feature; that's either a sign the generator
  needs to cover a new shape, or a design question worth raising, not a
  default.
- **Predictability over magic.** Resolution order must be deterministic
  and documented. A useful compile-time diagnostic is better than a
  clever runtime fallback.
- **Performance is a feature, not a later optimization** — this is test
  infrastructure that runs on every test, so allocations and generated-
  plan execution cost matter from the start.
- **Modular by design.** The core `Compono` package must never reference
  or know about an integration package (`Compono.XunitV3`,
  `Compono.NSubstitute`, `Compono.Bogus`, or any future one).

## Don't build ahead of the milestone

Prefer the simplest implementation that satisfies the current ADR and the
milestone it belongs to. Don't implement a future milestone's scope early
just because you're nearby in the code — e.g. don't build provider-
pipeline machinery while Milestone 1 is still just direct constructor
invocation, don't add a reflection fallback path preemptively, don't
stand up a package (`Compono.XunitV3`, etc.) before its own milestone
starts it. `docs/mvp.md` scopes each milestone explicitly; if something
looks missing, check whether it's deferred there before assuming it's a
gap to fill.

## When uncertain

- Prefer asking over silently making an architectural call — especially
  when there's a genuine fork between two plausible shapes (see the
  "Design dives" section of `references/design-decisions.md`: that's
  exactly what the light/deep-dive process and its "talk it through with
  the user" step are for).
- Don't invent public API surface that isn't backed by an ADR or already
  described in `docs/public-api.md`/`docs/architecture.md`.
- Follow the existing convention for a given concern instead of
  introducing a new one (a new DI registration style, a new error-
  handling shape, a new test-helper pattern) — this repo intentionally
  has one way to do each thing; a new pattern is a design question, not a
  default.

## Start here: the engineering-workflow skill

**Before designing, implementing, reviewing, or documenting anything
non-trivial, read `.agents/skills/engineering-workflow/SKILL.md`.** It is
the source of truth for how work gets done in this repo — process and
standards, not architecture (that's `docs/`). It routes to:

- `tasks/design.md` — designing a feature/architecture decision (ADR +
  plan)
- `tasks/implement.md` — implementing against an already-`Accepted` ADR
- `tasks/pr-review.md` — reviewing a PR/diff in this repo
- `tasks/respond-to-pr-feedback.md` — addressing feedback left on a PR
- `tasks/explain.md` — a detailed code walkthrough
- `references/*.md` — coding standards, testing, security, documentation,
  design-decision mechanics (ADRs/plans), code of conduct

Agents should read this skill before making any non-trivial change, not
just once at session start — each task type routes to a different file.

## Repo layout

- `src/Compono` — the core composition engine (no runtime provider
  pipeline yet — that's Milestone 2).
- `src/Compono.Generators` — the incremental source generator
  (Milestone 1's actual scope right now).
- `test/` — one test project per `src` project (same name + `.Tests`
  suffix), xUnit v3 on Microsoft Testing Platform.
- `docs/adr/` — numbered, immutable architecture decision records.
- `docs/plans/` — living execution trackers for a non-trivial ADR (task
  checklists, critical files, verification notes).
- `docs/*.md` (`architecture.md`, `public-api.md`, `mvp.md`,
  `manifesto.md`, `design-principles.md`) — current/intended state,
  linking back to the ADR that decided it.
- `Compono.XunitV3`, `Compono.NSubstitute`, `Compono.Bogus` are named in
  `docs/mvp.md` but don't exist as projects yet — forward-looking, not
  current fact.

## Build and test

```bash
dotnet build   # full solution, every TFM the SDK pinned in global.json targets
dotnet test    # full solution, every test project and TFM
```

A change isn't done until `dotnet build` is 0 warnings/0 errors and
`dotnet test` is fully green — see `references/testing.md` and
`tasks/implement.md`'s procedure.

For anything source-generator-facing (`src/Compono.Generators`), unit
tests alone aren't enough — do a real manual verification: `dotnet pack`
`src/Compono/Compono.csproj`, reference it from a throwaway console
project via a local NuGet feed, and confirm the generated code actually
compiles and runs in a real consuming project. `tasks/implement.md` and
recent plan Notes entries (e.g. Phase 2/Phase 3 in
`docs/plans/0001-...md`) show this pattern in detail.

### Consumer/dogfood validation gate

**For any substantive Compono package/generator behavior change that has
an active dogfood consumer, the exact working tree being pushed must first
pass that consumer through freshly packed local packages — Compono's own
green test suite is necessary but not sufficient when an active consumer
acceptance gate exists.** This governs pushing, not just merging: don't
push a working tree whose most recent substantive change hasn't cleared
this gate. A consumer run performed before the latest substantive change
is stale and does not authorize a push, no matter how recently it ran —
running the gate once at PR-open time and treating that as standing proof
for later revisions is not sufficient; repeat it after every substantive
PR feedback change. This is a development/PR-validation discipline, not a
GitHub Actions requirement.

Known dogfood consumers today: `ncipollina/trivia-platform` (the
`scripts/dogfood-validate.sh` default) and, for `Compono.Http`-touching
changes per PLAN-0051, `alexa-vox-craft` (pass `--consumer-repo`). A
package/generator change only needs to clear the gate against the
consumer(s) it's actually relevant to, not every known consumer
unconditionally.

Verification requires all of:

1. `dotnet build`/`dotnet test` green in this repo.
2. `scripts/dogfood-validate.sh` green, against the relevant consumer,
   packing every Compono package that consumer actually depends on (its
   `--packages`/`DOGFOOD_PACKAGES` option — not always just the original
   four-package default) — packs the current working tree under one
   shared unique local prerelease version per package, restores the
   consumer against it (via a temporary `Directory.Packages.props`
   override, never editing the consumer's real file), asserts the
   consumer actually resolved that exact version for every one of those
   packages (not a stale cache hit, and not a mix of freshly-packed and
   previously-published versions), and runs the consumer's full test
   suite.

See `scripts/dogfood-validate.sh --help` for usage and
`docs/research/0008-trivia-platform-multi-entry-testdoubles-dogfood.md` for
the dogfood pass that established this gate.

## Generated code

What `Compono.Generators` emits into a consumer's compilation follows its
own rules, distinct from the generator's own hand-written source
(`references/coding-standards.md`'s "Generated code" section has the full
list). The load-bearing ones:

- Every emitted type is `file`-scoped — generated plans are reached
  through `PlanCache<T>.Instance`, never by name, so there's no reason to
  risk a collision. **Exception: ADR-0043's generated test doubles** - the
  double, its configuration extensions, and its `Configure()` bridge
  reference each other in public signatures, which `file`-scoping any of
  them breaks with `CS9051` (proven by two failed drafts during design
  review). They're `internal` + hash-suffixed names instead; only the
  `[ModuleInitializer]` registration class stays `file`-scoped. See
  `references/coding-standards.md`'s "Generated code" section for the
  full account before "fixing" this back.
- Every type reference is `global::`-qualified
  (`SymbolDisplayFormat.FullyQualifiedFormat`) — collision-proof
  regardless of a consumer's `using`s or shadowed namespace segments.
- A compile-time diagnostic beats emitting code that might not compile —
  reject an unsupported shape with a clear `CMPxxxx` diagnostic rather
  than generating something and hoping.
- Generated code should be low-allocation by construction (pre-sized
  collections, no LINQ, expression-bodied where there's no branching) —
  this is on the hot path of every composed test.

## The non-negotiables

- Architecture is ADR-driven: every non-trivial design decision gets an
  ADR in `docs/adr/` before code is written against it. An ADR's original
  Decision/Rationale/Consequences text is immutable once `Accepted` —
  never rewritten in place — but a correction or extension found later is
  recorded as a dated Amendment appended to that same ADR (see ADR-0022's
  Amendments for real examples); reserve superseding with a whole new ADR
  for an actual reversal of the core decision. Don't silently evolve
  architecture in code — see `references/design-decisions.md`.
- Keep PRs scoped to one decision or one feature — no bundling an
  unrelated fix or refactor. If a plan is phased, that's one PR per
  phase, and the prior phase's status/PR-merge state should be current
  before the next phase starts.
- Update the relevant `docs/*.md` and the plan's task checklist in the
  same PR that changes the behavior they describe — not a follow-up.
- Full detail on naming, nullable handling, async patterns, and file
  layout lives in `references/coding-standards.md` — read it before
  writing C#, don't guess from surrounding code alone.
