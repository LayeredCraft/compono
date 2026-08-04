# [PLAN-0008] Milestone 8: Public Preview

**Status:** Not Started

**Implements:** [ADR-0030](../adr/0030-compono-documentation-architecture.md)
(including Amendments 1-2), [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)
(release/versioning policy, package-readiness bar),
[ADR-0032](../adr/0032-api-reference-documentation-toolchain.md) (API
reference toolchain), [ADR-0033](../adr/0033-public-preview-samples-strategy.md)
(samples strategy) — a `docs/mvp.md` roadmap milestone drawing on four
`Accepted` ADRs, per `docs/plans/README.md`'s multi-ADR plan convention.

## Goal

An external developer can discover, understand, install, use
successfully, troubleshoot, evaluate, and safely contribute to Compono
using only public artifacts (nuget.org packages, the deployed docs site,
the GitHub repository) — verified by a clean-room acceptance pass
(Phase 8) that follows only public instructions and consumes only
published/local-feed packages. See "Exit criteria" below for the full,
checkable bar.

## Scope

Per `docs/mvp.md`'s Milestone 8 section and the four ADRs above. This
plan supersedes the earlier flat, unphased PLAN-0008 draft (recorded in
Notes below) — same total scope, now split into independently shippable
phases, each its own PR, per `design-decisions.md`'s "each phase ships as
its own PR" rule.

Explicitly deferred (unchanged from the original draft): any change to
the documentation *architecture* itself (ADR-0030's hierarchy, section
purposes, audiences) — friction discovered while executing this plan
routes to a new `tasks/design.md` pass (likely an ADR-0030 Amendment), not
a silent deviation absorbed here. Any post-MVP product capability
discovered while writing docs or running the acceptance test is recorded
in `docs/roadmap/post-mvp.md`, not built in this milestone — per the
milestone brief's own "don't introduce unrelated post-MVP features"
constraint. A blocking bug found along the way may be fixed in its own
scoped PR, same precedent as ADR-0029's "Bug handling."

## Phase ordering rationale

The milestone brief's suggested shape (decisions → toolchain → package
hardening → core docs → package-specific docs → samples/cookbook →
contributor readiness → release pipeline → acceptance → closeout) is
adopted with two changes, both driven by real dependencies this design
pass surfaced:

- **Release pipeline moves out of its own phase.** The user's own
  decision (this plan's design conversation) settled that the pipeline
  itself needs no redesign — only the `alpha`→`preview` identifier rename
  ([ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)).
  That change is mechanically trivial and has no dependency on anything
  else in this plan, so it's folded into Phase 0 rather than getting a
  whole phase to itself. It belongs in Phase 0, not a later checkpoint —
  every version `publish-preview.yaml` produces is a SemVer prerelease
  regardless of the identifier string (`-alpha.N` and `-preview.N` are
  both still prereleases, both still excluded from a plain `dotnet add
  package` install), so renaming it carries no risk of a still-in-progress
  milestone looking publicly "done." The actual "does this look done"
  gate is Phase 8's manual GitHub Release publish
  (`publish-release.yaml`), which this rename has no effect on either
  way — see ADR-0031's "Preview publishing identifier" Decision Outcome
  for the full mechanics.
- **Package-readiness hardening moves before documentation writing, not
  after.** Package Guides, Samples, and the acceptance test all need real,
  installable packages to write accurate content against and verify
  examples with — writing Package Guides against packages that haven't
  yet been hardened (symbols, validation, packed-consumer verification)
  risks documenting a shape that changes underneath the docs before
  launch.

## Phase 0: Package readiness hardening

**Status:** Not Started

**Checkpoint: Package Quality Complete** — every package meets ADR-0031's
readiness bar and can be safely built against by every later phase.

Executes [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)'s
package-readiness bar against all five packages — four independently
published (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`,
`Compono.Bogus`), plus `Compono.Generators`, which never gets its own
`.nupkg` (`IsPackable=false`, embedded inside `Compono.nupkg`'s
`analyzers/dotnet/cs`, per [ADR-0003](../adr/0003-generator-package-distribution.md))
and is verified by inspecting that embedded content, not by packing it
independently. No documentation content depends on this phase's *outcome*
being novel — it hardens what already ships — but Phases 3/4/8 depend on
it being *done* before writing package-specific content or running the
acceptance test.

- [ ] Rename `publish-preview.yaml`'s `prereleaseIdentifier` input from
      `alpha` to `preview` (ADR-0031) — `main` starts publishing
      `0.x.y-preview.N` instead of `0.x.y-alpha.N`. Still a real SemVer
      prerelease either way, so this carries no sequencing risk and
      belongs here, not at a later checkpoint.
- [ ] Convert `Directory.Packages.props`'s `PackageVersion` entries to
      exact-pin bracket syntax (`[3.2.2]`, not bare `3.2.2`) — a bare
      version is a NuGet minimum-inclusive floor, not a hard pin, so it
      doesn't actually enforce ADR-0031's "install the version we tested
      against" policy as written today; bracket syntax closes that gap.
- [ ] Add `PackageTags` and `PackageReleaseNotes` to `Directory.Build.props`,
      not per-project — one shared, uniform value for all five packages
      (`PackageTags`: `testing;test-data;source-generator;dotnet`;
      `PackageReleaseNotes`: `$(PackageProjectUrl)/releases`, the repo's
      stable releases index rather than a specific version tag — a
      per-version `.../releases/tag/v$(Version)` link would 404 for every
      preview build `publish-preview.yaml` pushes on a plain `main` push,
      since no GitHub Release or tag exists for those, only for versions
      published through `publish-release.yaml`). Matches how
      `PackageLicenseExpression`/`RepositoryUrl`/`PackageIcon`/
      `PackageReadmeFile` are already centralized — the five packages
      distribute as one coherent set, so their discovery metadata comes
      from one place, not five copies that can drift. No per-package tag
      differentiation (e.g. `xunit`/`nsubstitute`/`bogus`) — the package
      name itself already carries that distinction.
- [ ] Add `Microsoft.DotNet.PackageValidation`
      (`EnablePackageValidation=true`) to `Directory.Build.props`'s
      packable `PropertyGroup`, with **no static
      `PackageValidationBaselineVersion` value** — `publish-preview.yaml`
      publishes automatically on every non-docs `main` push with no human
      release step in between, so a baseline only a person sets when
      "cutting a release" would never apply to that continuous stream,
      which is most of what actually publishes. Instead, add a step to
      **both** `publish-preview.yaml` and `publish-release.yaml` that
      queries nuget.org for each package's currently-latest published
      version before packing and passes it as an MSBuild property
      override (`-p:PackageValidationBaselineVersion=<prior-version>`).
      The very first-ever publish has nothing to query and validation
      stays inert for exactly that one case; every publish after it —
      preview or production — gets a real baseline automatically, with no
      manual step.
- [ ] Reconfigure `.github/release-drafter.yml`'s `version-resolver` so
      the `breaking-change` label maps to `minor`, not the file's current
      `major` — as configured today, a labeled breaking-change PR
      resolves the next version as `1.0.0`, silently exiting the `0.x`
      preview line the first time anyone uses the label, rather than the
      deliberate `0.X+1.0` minor bump ADR-0031's compatibility policy
      requires. Leave `breaking-change` in `categories` unchanged — only
      its `version-resolver` bucket moves.
- [ ] Add a CI step that packs the four publishable packages and asserts
      each `.nupkg`'s file listing matches the expected shape per TFM
      (lib, README, icon, no stray build artifacts; `analyzers/dotnet/cs`
      for `Compono` specifically, containing `Compono.Generators.dll` —
      this is also where `Compono.Generators` itself gets verified, by
      content inspection rather than an independent pack).
- [ ] Extend the local-feed packed-consumer pattern (already used by
      `test/Compono.XunitV3.SampleTests`) to restore and smoke-test the
      four publishable packages together from one local feed, as a
      standing CI gate — not ad hoc per milestone.
- [ ] Verify (not redesign) `PrivateAssets`/analyzer-transitivity holds
      for every package, not just `Compono`/`Compono.Generators`.
- [ ] Spot-check of `Directory.Packages.props`'s current dependency
      licenses, plus a note in `contributing.md` (Phase 6) that any PR
      adding or bumping a dependency version — Dependabot-authored or
      not — gets its target package's license checked as part of normal
      review, per ADR-0031: Dependabot's own flow catches vulnerabilities,
      not licenses, so this is an ongoing review habit, not a one-time
      task that's done after Phase 0.

## Phase 1: API reference toolchain evaluation and wiring

**Status:** Not Started

Executes [ADR-0032](../adr/0032-api-reference-documentation-toolchain.md).
Depends on Phase 0 only loosely (needs real packages to generate against,
but can run against `main`'s current build) — sequenced early because
`reference/api/` is a dependency for later cross-links (Concepts,
Package Guides, Cookbook) that are expected to point into it.

- [ ] Time-boxed bake-off: `DefaultDocumentation` vs. `xmldocmd` (plus any
      other maintained candidate surfaced) against a representative slice
      of Compono's real public API, scored against ADR-0032's evaluation
      criteria (generics, overloads, inheritance, extension methods,
      attributes, nullable signatures, `<see>`/cross-package refs, doc-tag
      coverage, stable filenames/anchors, MkDocs Material readability,
      deterministic output, maintenance/TFM compatibility). Record the
      result in this plan's Notes.
- [ ] Wire the winning tool into CI: generates `docs/reference/api/`
      Markdown from each package's compiled DLL + XML doc file.
- [ ] Add the drift-detection CI gate (regeneration produces no
      uncommitted diff) and the missing-XML-doc-comment gate where the
      tool supports it.
- [ ] `reference/index.md` states the "supplements, never replaces"
      philosophy (already drafted per ADR-0030 Amendment 1's framing).

## Phase 2: Core documentation and README

**Status:** Not Started

**Checkpoint: Documentation Foundation Complete** — a newcomer has a
complete, real (non-stub) linear path from zero knowledge to productive
use; every later section can safely link back into this one.

The primary learning path a newcomer needs before anything
package-specific or task-specific makes sense — sequenced before Phases
3-5 since How-to Guides/Package Guides/Cookbook/Best Practices all assume
Concepts, and Samples/Migration Guide pages link back into Getting
Started.

- [ ] `docs/index.md` — fix the `Compono.Create(builder => ...)` example
      to the real `Composer.Create(builder => ...)` API
      (`src/Compono/Composer.cs`).
- [ ] Repository-root `README.md` — review and update (distinct artifact
      from `docs/getting-started/*`, per `docs/mvp.md`'s Milestone 8
      scope). Apply ADR-0030 Amendment 2's benchmark-claims policy: no
      comparative AutoFixture performance claims.
- [ ] `getting-started/index.md`, `installation.md`, `first-test.md`,
      `learning-paths.md`, `next-steps.md`.
- [ ] `concepts/index.md`, `composition-model.md`, `profiles.md`,
      `registrations-and-rules.md`, `shared-values.md`, `providers.md`,
      `determinism-and-seeding.md`, `collections.md`.
- [ ] `how-to/index.md`, `create-an-object.md`,
      `write-a-composed-theory.md`, `customize-a-member.md`,
      `register-a-type.md`, `use-profiles.md`,
      `share-a-value-across-a-test.md`.

## Phase 3: Package guides, migration guide, troubleshooting, reference

**Status:** Not Started

Content with the most existing raw material to draw from (Milestone 7's
research/migration-guide evidence, real diagnostics already shipped) —
sequenced after Phase 2 (assumes Concepts) and Phase 0 (packages must be
final-shaped to describe accurately) and Phase 1 (`reference/diagnostics.md`/
`glossary.md` complete the Reference section Phase 1 started).

- [ ] `packages/index.md` (ecosystem map table), `packages/compono.md`,
      `packages/compono-xunitv3.md`, `packages/compono-nsubstitute.md`,
      `packages/compono-bogus.md`.
- [ ] Polish `docs/migrating-from-autofixture.md` to publication-ready
      (content refinement only, per ADR-0030's "content-stable" framing —
      no structural change).
- [ ] `troubleshooting/index.md`, `troubleshooting/common-errors.md`
      (start from the real `CMP0001` finding), `troubleshooting/faq.md`
      (start from the real gap-3 "why fail-fast" finding).
- [ ] `reference/diagnostics.md` (every `CMP` code), `reference/glossary.md`.
- [ ] Explicit known-limitations content, surfaced from Getting Started,
      Package Guides, Troubleshooting, and release notes (not one
      obscure page) — sourced from `docs/research/0001-autofixture-comparison.md`'s
      recorded findings (`CMP0001`, fail-fast recursion vs. omission, the
      Compose-family stacking constraint, `Compono.Bogus`'s exact
      member-name-matching limits) plus `docs/mvp.md`'s Non-goals list.

## Phase 4: Samples, cookbook, best practices

**Status:** Not Started

**Checkpoint: Public Documentation Feature Complete** — every
`docs/documentation-architecture.md` section that depends on real,
runnable code (Samples, Cookbook) now has it; only architecture
consolidation, contributor readiness, and final hardening remain.

Executes [ADR-0033](../adr/0033-public-preview-samples-strategy.md) for
samples. Sequenced after Phase 2 (Concepts/Package Guides content to link
back to) and Phase 0 (packed-package verification needs hardened
packages).

- [ ] `samples/Compono.Samples.BasicUsage/` — real project, added to
      `Compono.slnx`, `ProjectReference`s during authoring.
      `docs/samples/basic-usage.md` overview page.
- [ ] `samples/Compono.Samples.AspNetApi/` — real project, added to
      `Compono.slnx`. `docs/samples/aspnet-api.md` overview page.
- [ ] `docs/samples/index.md` overview; record the five deferred
      candidates (CQRS, Clean Architecture, Minimal APIs, MediatR, EF
      Core) as future candidates, not silently dropped.
- [ ] Local-feed packed-package verification job for both samples
      (reuses Phase 0's local-feed infrastructure).
- [ ] `cookbook/index.md` (flat, alphabetical, per ADR-0030 Amendment 2's
      deferred-navigation decision) plus the first recipe batch (5-10
      pages): generate a realistic email, freeze a shared
      `HttpMessageHandler`, override one field only for one test, seed a
      specific failing case for reproduction, compose a substitute with
      one method stubbed. Each recipe captures the stable front matter
      (`title`, description, `packages`, `concepts`) ADR-0030 Amendment 2
      requires even though nothing consumes it for navigation yet.
- [ ] `best-practices/index.md`, `organizing-profiles.md`,
      `large-test-suites.md`, `naming-conventions.md`,
      `reusing-configuration.md`, `performance-recommendations.md`,
      `deterministic-and-non-brittle-tests.md`.

## Phase 5: Architecture consolidation and legacy retirement

**Status:** Not Started

Executes ADR-0030 Amendment 2's "one canonical home" principle. Sequenced
after Phase 2 (Concepts must exist for Architecture pages to cross-link
back to) — the last phase touching `docs/architecture.md`/
`docs/performance.md`/`docs/design-principles.md`/`docs/manifesto.md`/
`docs/public-api.md`'s real pre-existing content, so it can safely
consume and then retire them.

- [ ] `architecture/index.md`, `architecture/design-principles.md`
      (absorbs `docs/design-principles.md`/`docs/manifesto.md`'s
      content).
- [ ] `architecture/current/source-generation.md`,
      `generated-plans-and-discovery.md`, `provider-pipeline.md`,
      `deterministic-seeding.md`, `performance.md` (moves
      `docs/performance.md`'s real methodology/results, publishing them
      per ADR-0030 Amendment 2's benchmark-claims policy).
- [ ] `architecture/decision-log.md` (public-facing index into
      `docs/adr/`).
- [ ] Retire `docs/public-api.md`/`docs/manifesto.md` once every
      cross-reference has a new home; delete their `mkdocs.yml`
      "(legacy)" nav entries.
- [ ] `roadmap/index.md`, `roadmap/proposed-adrs.md`,
      `roadmap/future-packages.md` (`roadmap/post-mvp.md` already real
      content from PLAN-0007 Phase 3 — just needs its nav confirmed).

## Phase 6: Contributor and repository readiness

**Status:** Not Started

Executes ADR-0030 Amendment 2's governance-scope decision. Independent of
the documentation content phases above — could run in parallel with
Phases 2-5 in principle, sequenced here mainly for reviewability (one
focused PR, not interleaved with doc-content PRs).

- [ ] `contributing.md` — build/test/PR expectations, cross-linking this
      skill's public-facing equivalents.
- [ ] `SECURITY.md` — vulnerability reporting process.
- [ ] `CODE_OF_CONDUCT.md` — standard, uncustomized Contributor Covenant.
- [ ] GitHub issue templates: bug report, feature/roadmap proposal.
- [ ] One lightweight PR template.
- [ ] "Good first issue" candidates identified from the Cookbook recipe
      backlog (a natural first-PR shape, per ADR-0030 Amendment 1's own
      framing of Cookbook recipes).

## Phase 7: Final navigation, link, and snippet validation pass

**Status:** Not Started

A dedicated hardening phase before publication — every prior phase wrote
content against its own section; this phase verifies the whole site holds
together, not just each page in isolation.

- [ ] `mkdocs.yml` final nav pass: every "(legacy)" entry retired or
      resolved (Phase 5), nav matches `docs/documentation-architecture.md`'s
      tree exactly.
- [ ] Site-wide broken-link check (internal cross-links, per "every page
      leads somewhere").
- [ ] Code-snippet compilation check where practical (snippets drawn from
      real sample/test code, per `documentation.md`'s "prefer real
      examples" quality bar — verify they still compile against current
      `main`, not just that they did when written).
- [ ] Spelling/style pass across the full site.

## Phase 8: Clean-room public-preview acceptance test and first publication

**Status:** Not Started

**Checkpoint: Release Candidate** — every prior phase is done; this phase
either confirms the milestone is genuinely ready to publish or sends
findings back to an earlier phase before anything ships.

The milestone's actual proof point — depends on every content and
package-readiness phase above being done.

- [ ] Clean-room acceptance test: a fresh project, following only the
      public docs site and consuming only published (or local-feed, for
      pre-publish verification) packages — the five-minute Getting
      Started path, one How-to Guide task, one Cookbook recipe, one
      Package Guide's "when to install" decision, one Troubleshooting
      lookup, all followed literally as written, no ADR/internal-repo
      knowledge assumed. See "Public-preview acceptance checklist" below
      for the full list.
- [ ] Cut the first real `0.x` release: publish a GitHub Release and mark
      it published (not draft) — this is the actual "does this look
      done" gate (`publish-release.yaml`, triggered by
      `release: types: [published]`), completely independent of
      `publish-preview.yaml`'s `preview` identifier (renamed in Phase 0,
      unrelated to this step).
- [ ] Verify all four publishable packages installable from nuget.org
      post-publish (not just the local-feed pre-check); verify
      `Compono.Generators` is present inside the installed `Compono`
      package's `analyzers/dotnet/cs` and actually runs.
- [ ] Verify the documentation site is live at its public URL with the
      final nav.

## Phase 9: Final MVP documentation and closeout

**Status:** Not Started

**Checkpoint: Milestone 8 / MVP Complete** — `docs/mvp.md` reflects the
real, final outcome and every MVP success criterion has an honest
verdict.

- [ ] `docs/mvp.md`'s Milestone 8 section: outcome, links to all four
      ADRs and this plan, exit-criteria results.
- [ ] Final MVP success-criteria review (`docs/mvp.md`'s "Success
      Criteria" list) — each marked met/partially met/unmet, honestly,
      against real evidence from this milestone and Milestone 7's.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` — confirm all rows
      accurate (already updated for ADR-0031/0032/0033 during this
      design pass).

## Package-readiness checklist

The concrete, executable form of
[ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)'s
package-readiness bar — that ADR states the long-lived policy (what must
be true of a package before release), this plan owns *how* it gets
verified. Applied to all five packages — four independently published,
plus `Compono.Generators`, verified by content inspection inside
`Compono.nupkg` rather than an independent pack (it's `IsPackable=false`,
per ADR-0003). Phase 0's Tasks above are this checklist:

- [ ] `publish-preview.yaml`'s `prereleaseIdentifier` renamed from
      `alpha` to `preview`.
- [ ] `Directory.Packages.props`'s `PackageVersion` entries use exact-pin
      bracket syntax (`[3.2.2]`), not bare versions — bare versions are a
      NuGet minimum floor, not a hard pin.
- [ ] `PackageTags`/`PackageReleaseNotes` set once in
      `Directory.Build.props` (one uniform value for all five packages,
      not per-project) — `PackageReleaseNotes` points at the repo's
      stable releases index (`$(PackageProjectUrl)/releases`), not a
      per-version tag URL, since most published preview versions have no
      matching GitHub Release/tag to link to (see Phase 0's Tasks above).
- [ ] `Microsoft.DotNet.PackageValidation` enabled
      (`EnablePackageValidation=true`), with no static baseline value; a
      CI step in both `publish-preview.yaml` and `publish-release.yaml`
      queries nuget.org for each package's latest published version and
      passes it via `-p:PackageValidationBaselineVersion=<prior-version>`
      at pack time — automatic on every publish (including the continuous
      preview stream), not a manually-set property.
- [ ] `.github/release-drafter.yml`'s `version-resolver` remaps
      `breaking-change` from `major` to `minor` (its `categories` entry
      is unchanged) — otherwise a labeled breaking-change PR would
      silently resolve to `1.0.0`, exiting `0.x` by accident.
- [ ] CI step asserting each publishable `.nupkg`'s file listing matches
      the expected per-TFM shape (lib, README, icon, no stray build
      artifacts; `analyzers/dotnet/cs` for `Compono` specifically,
      containing `Compono.Generators.dll` — this is `Compono.Generators`'
      own verification, not a separate pack of it).
- [ ] Local-feed packed-consumer smoke test covers the four publishable
      packages together, as a standing CI gate.
- [ ] `PrivateAssets`/analyzer transitivity verified for every package.
- [ ] Dependency license spot-check against `Directory.Packages.props`'s
      current set, plus a `contributing.md` note (Phase 6) making license
      review part of reviewing any future dependency-version-change PR.

## Release-readiness checklist

- [ ] `publish-preview.yaml`'s identifier renamed from `alpha` to
      `preview` (Phase 0) — the actual "does this look done" gate is the
      manually-published GitHub Release below, not this rename.
- [ ] All four publishable packages pass
      `Microsoft.DotNet.PackageValidation`; the CI-automated baseline
      lookup (Phase 0) actually resolved a real prior version for this
      publish, not a first-ever/inert run silently treated as normal.
- [ ] `.github/release-drafter.yml`'s `breaking-change` label resolves to
      a minor bump, confirmed against a real labeled PR before the first
      one ships for real (Phase 0).
- [ ] Local-feed packed-consumer smoke test passes for the four
      publishable packages together (Phase 0).
- [ ] Package-contents inspection CI step passes for the four publishable
      packages, and separately confirms `Compono.Generators.dll` is
      present inside `Compono.nupkg`'s `analyzers/dotnet/cs` (Phase 0).
- [ ] Every public member has an XML doc comment (no `CS1591` warnings) —
      enforced by the existing `Directory.Build.props` setting plus
      Phase 1's reference-generation gate.
- [ ] `docs/roadmap/index.md`'s compatibility framing and every affected
      Package Guide are current with the version about to publish.
- [ ] If this release includes a breaking-change-labeled PR, the
      generated release notes carry the "⚠️ Breaking Changes" section
      (release-drafter's `categories` grouping renders it automatically —
      nothing to check if no such PR is included this time; the absence
      of the section is itself the "nothing broke" signal, per ADR-0031).
- [ ] Documentation site deploys successfully from the same `main` commit
      being released (verified via `docs.yml`, not a separate manual
      check).

## Public-preview acceptance checklist

Run during Phase 8, using only public artifacts (no internal-repo
knowledge, no ADR references, no local source checkout beyond what's
needed to author the fresh test project):

- [ ] A stranger can find Compono via GitHub search or nuget.org search
      (package tags/description, Phase 0) and land on a README that
      states what it is, why it exists, and what to do next within
      seconds of scrolling.
- [ ] `dotnet add package Compono`/`Compono.XunitV3` (the common-case
      pair, per Getting Started) succeeds from a clean project against
      published nuget.org packages.
- [ ] The five-minute Getting Started path succeeds verbatim, start to
      finish, with no undocumented step.
- [ ] At least one How-to Guide task, followed literally, succeeds.
- [ ] At least one Cookbook recipe, copy-pasted, works without
      modification.
- [ ] The relevant Package Guide's "when to install" section is
      sufficient to decide whether to add `Compono.NSubstitute`/
      `Compono.Bogus` without reading any other page.
- [ ] A deliberately-triggered composition failure produces a readable
      error and a reproducible seed, and Troubleshooting's
      `common-errors.md` resolves it by diagnostic code.
- [ ] The API reference (`reference/api/`) answers at least one "what
      does this method do / what does it throw" question the guides
      don't already answer inline.
- [ ] Every link followed during the acceptance pass resolves (no 404s,
      no dead cross-references).
- [ ] Nothing in the acceptance pass required reading `docs/adr/`,
      `docs/plans/`, or `docs/research/` — confirming those stay
      internal engineering artifacts, not a hidden prerequisite.

## Exit criteria

Milestone 8 — and the entire MVP — is complete only when every item below
is true, checked honestly (met / partially met / unmet is an acceptable
outcome for the final MVP review in Phase 9, but every item here must be
individually resolved, not left ambiguous):

- [ ] All four publishable `0.x` packages are available on nuget.org and
      installable in a clean project; `Compono.Generators` is present and
      running inside the installed `Compono` package.
- [ ] Packed-package consumer verification passes (Phase 0's local-feed
      gate, plus Phase 8's post-publish nuget.org verification).
- [ ] The documentation site is publicly deployed and live at its stated
      URL.
- [ ] The root `README.md` accurately directs each audience (newcomer,
      AutoFixture migrator, contributor) to its right next step.
- [ ] The five-minute Getting Started flow succeeds from a clean project
      (Phase 8's acceptance test).
- [ ] All public APIs have useful XML documentation (Phase 0/1's gates).
- [ ] API reference (`reference/api/`) and diagnostics reference
      (`reference/diagnostics.md`) are published (Phase 1/3).
- [ ] Both required samples (Basic Usage, ASP.NET API) build in CI
      (Phase 4).
- [ ] Benchmark methodology and results are published, without
      unsupported comparative claims (Phase 5, per ADR-0030 Amendment 2).
- [ ] Known limitations and the `0.x` compatibility policy are explicit
      and discoverable from multiple entry points, not one obscure page
      (Phase 3, per ADR-0031).
- [ ] Contribution, security, and release guidance exist
      (`contributing.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, issue/PR
      templates — Phase 6).
- [ ] The AutoFixture migration guide is publication-ready (Phase 3).
- [ ] Every package and documentation link is valid (Phase 7).
- [ ] The public-preview acceptance test (above) passes end to end using
      only public-facing instructions (Phase 8).
- [ ] `docs/mvp.md`'s MVP-wide Success Criteria are reviewed one final
      time and each honestly marked met, partially met, or unmet
      (Phase 9).

## Critical Files

- Every path listed in each phase's Tasks above, under `docs/` — most
  already exist as Phase 5 (PLAN-0007) stubs; this plan replaces stub
  content with real content, not new files, **except**: `docs/index.md`
  and repository-root `README.md` (pre-existing real pages this plan
  corrects); `samples/*`'s real runnable projects (new, Phase 4);
  `contributing.md`/`SECURITY.md`/`CODE_OF_CONDUCT.md`/issue-template/
  PR-template content (new, Phase 6); `reference/api/` (new, Phase 1,
  generated); `docs/reference/diagnostics.md`/`glossary.md` content
  (Phase 3).
- `mkdocs.yml` — nav updated per phase as content lands; final pass in
  Phase 7.
- `.github/workflows/publish-preview.yaml`/`publish-release.yaml` — a
  new step querying nuget.org for each package's prior version and
  passing `-p:PackageValidationBaselineVersion=<prior-version>` at pack
  time; `publish-preview.yaml`'s `prereleaseIdentifier` renamed from
  `alpha` to `preview` (Phase 0, both changes).
- `.github/release-drafter.yml` — `breaking-change` remapped from
  `major` to `minor` in `version-resolver` (Phase 0).
- `Directory.Packages.props` — `PackageVersion` entries converted to
  exact-pin bracket syntax (Phase 0).
- `Directory.Build.props` — package-validation and tags/release-notes
  properties added, shared across all five packages (Phase 0).
- `Compono.slnx` — both new sample projects added (Phase 4).
- `docs/documentation-architecture.md` — Open Items section already
  updated to reflect all six resolutions as part of this design pass;
  further updated in place as content lands and stub statuses flip to
  real.
- `docs/mvp.md` — Milestone 8 section (Phase 9).

## Test Plan

Documentation content itself has no automated test suite beyond the
link/snippet-validation gates in Phase 1 (API reference drift) and Phase
7 (site-wide link/snippet check). Both samples (Phase 4) get real,
buildable projects with, where practical, their own tests demonstrating
the pattern they showcase, matching `testing.md`'s bar for any real code
this plan produces. Package-readiness changes (Phase 0) are verified by
the new CI gates themselves (package validation, contents inspection,
local-feed restore) rather than a separate hand-run test plan. Phase 8's
acceptance checklist is this plan's actual end-to-end verification.

## Notes

The original PLAN-0008 draft (produced by PLAN-0007 Phase 5) was a flat,
unphased backlog, deliberately left unphased pending this design pass —
see that phase's own Notes in
[PLAN-0007](0007-milestone-7-dogfooding.md#phase-5-2026-08-03) for why.
This rewrite is that design pass: same total scope (every page, every
package-readiness item, every repository-process artifact the draft
listed), now split into ten independently-shippable phases (0-9), with
four new/amended ADRs
([ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md),
[ADR-0032](../adr/0032-api-reference-documentation-toolchain.md),
[ADR-0033](../adr/0033-public-preview-samples-strategy.md), and
[ADR-0030 Amendment 2](../adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items))
settling every decision the draft's Tasks section left implicit. Anything
discovered while actually executing a phase that suggests the
*architecture* (not just this plan's task list) needs to change is
recorded here, then routed to `tasks/design.md` for an ADR-0030 Amendment
— per this plan's own Scope section above.
