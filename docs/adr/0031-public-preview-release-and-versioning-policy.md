# [ADR-0031] Public Preview Release and Versioning Policy

**Status:** Accepted

**Date:** 2026-08-04

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

`docs/mvp.md`'s Milestone 8 scope says "publish `0.x` packages" without
defining what that actually commits Compono to: whether the five packages
version together or independently, what `0.x` promises a consumer during
the preview, how a breaking change gets communicated, and what must be
true of a package before it's allowed into the first preview at all. Left
undefined, "0.x" risks meaning nothing more than "not 1.0 yet" — an
excuse for undisciplined churn rather than a real compatibility contract a
consumer can plan around.

This isn't starting from zero. `Compono`/`Compono.Generators`/
`Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus` already publish
continuously today: `publish-preview.yaml` pushes a fresh `alpha`
prerelease to public nuget.org on every non-docs-only push to `main`
(`prereleaseIdentifier: alpha`, via the shared
`LayeredCraft/devops-templates` pipeline), and `cosmere-tracker`
(Milestone 7's dogfooding target) already consumes real published
prereleases from there (`0.1.0-alpha.33` at time of writing) rather than a
local feed. `publish-release.yaml` also already exists, firing on a
GitHub Release being published. A release-drafter draft `v0.1.0` GitHub
Release already exists alongside the real `v0.0.0` tag. `docs/migrating-
from-autofixture.md` already documents an informal practice: "bump all
four versions together when a newer alpha is needed." The release
*pipeline* (version calculation, build/test gates, the two-workflow
preview/production split, NuGet push via the shared devops-templates
actions) is explicitly **not** being redesigned by this ADR — it's
reused as-is. What's undecided is the policy layer on top of it: what
version scheme, what compatibility promise, and what gate a package must
clear before it's included.

## Decision Drivers

- The existing "bump together" practice is already real, working
  behavior with a real external consumer (`cosmere-tracker`) depending on
  it — a decision that overturns it has to justify breaking that, not
  just be theoretically cleaner.
- A public preview needs a compatibility story a stranger can trust
  enough to adopt Compono in a real project, not just "anything can
  change, it's 0.x."
- The release pipeline itself is a solved problem (existing, working,
  shared org infrastructure) — this ADR's job is the policy the pipeline
  executes, not the pipeline's mechanics.
- `docs/mvp.md`'s non-goals list explicitly excludes "Stable 1.0 API" from
  the MVP — this ADR must not accidentally promise 1.0-grade stability
  during 0.x.

## Considered Options

### Versioning model

1. **Lockstep** — one version number shared by all five packages, bumped
   together every release regardless of which package(s) actually
   changed.
2. **Independent per-package semver** — each package versioned on its own
   change history.

### Preview publishing identifier

1. **Keep the `alpha` prerelease identifier** — `main` continues
   publishing `0.x.y-alpha.N`.
2. **Rename it to `preview`** — `main` publishes `0.x.y-preview.N`
   instead. `publish-preview.yaml`'s `prereleaseIdentifier` input always
   produces a SemVer prerelease version (nuget.org and `dotnet add
   package` both treat any `-`-suffixed version as prerelease, excluded
   from a default, non-`--prerelease` install) — there is no pipeline
   path from this input to a bare, non-prerelease `0.x.y`. Only a
   manually-published GitHub Release (`publish-release.yaml`, triggered
   by `release: types: [published]` — a human moving a draft release to
   live) produces a real, non-prerelease version; the identifier input
   has no bearing on that gate at all.

## Decision Outcome

**Lockstep versioning (Option 1), by explicit decision.** Independent
per-package versioning was rejected: it would require publishing a
compatibility matrix (which `Compono.XunitV3` versions work with which
`Compono` core versions) that a five-package, single-maintainer preview
has no real need for yet, and it would break the "bump all four versions
together" practice `cosmere-tracker` already depends on today. One shared
version number across `Compono`/`Compono.XunitV3`/`Compono.NSubstitute`/
`Compono.Bogus` (and `Compono.Generators`, versioned identically even
though it's never independently referenced by a consumer — see
"Package set" below) is the simplest statement of "these five packages
are one coherent release," which is exactly what the ecosystem actually
is during preview.

**Rename `alpha` to `preview` (Option 2), by explicit decision.** The
release pipeline itself is unchanged — this is a one-line change to
`publish-preview.yaml`'s `prereleaseIdentifier` input, from `alpha` to
`preview`. `0.x` is already the correct, conventional SemVer signal that
the API isn't yet stability-guaranteed; a separate `-alpha` suffix on top
of it communicates nothing a consumer doesn't already know from the
major version being `0`, and it's what currently makes
`cosmere-tracker`'s `Directory.Packages.props` pin an alpha-suffixed
version. `preview` is the more conventional label for a continuously-
published prerelease stream and matches the workflow's own name
(`publish-preview.yaml`). **This belongs in Phase 0, not a later
checkpoint** — an earlier draft of this ADR mistakenly assumed dropping
the identifier entirely would produce a bare, non-prerelease `0.x.y` and
sequenced the change at a release checkpoint to avoid publishing
something that looked "done" too early. That assumption was wrong: every
version `publish-preview.yaml` produces is a SemVer prerelease regardless
of what the identifier string is (`-alpha.N` or `-preview.N` are both
still prereleases, both still excluded from a plain `dotnet add package`
install and both still visibly flagged as prerelease on nuget.org) —
renaming the label carries no risk of a half-finished milestone looking
publicly "done," so there's no reason to defer it. The actual "does this
look done" gate is entirely separate: a human manually publishing a
GitHub Release (`publish-release.yaml`, `release: types: [published]`),
which this rename has no effect on either way — see
[PLAN-0008](../plans/0008-milestone-8-public-preview.md)'s Phase 8 for
that gate.

### Package set for the first preview

All five packages (`Compono`, `Compono.Generators`, `Compono.XunitV3`,
`Compono.NSubstitute`, `Compono.Bogus`) ship in the first preview —
`docs/mvp.md`'s MVP package set is already complete and dogfooded
end-to-end (Milestone 7). `Compono.Generators` is packed transitively
inside `Compono`'s own `.nupkg` (`analyzers/dotnet/cs`,
[ADR-0003](0003-generator-package-distribution.md)) and is never
independently referenced or independently versioned by a consumer — it
still moves in lockstep with the other four for internal consistency, but
has no separate NuGet listing of its own.

**What would block a package from the first preview** (none of the five
are currently blocked, this is the standing bar for any future addition
too): a failing build/test gate on the release pipeline; a missing
required package-readiness item (see below); a known correctness bug with
no workaround, per `docs/adr/0029-...`'s "Bug handling" precedent
(fix it or hold the release, don't ship a known-broken package); or
missing XML documentation on any public member (`documentation.md`'s
existing hard requirement — a public preview package without doc comments
fails its own stated bar for discoverability before it fails anything
external).

### `0.x` compatibility policy

**What a consumer may reasonably depend on during `0.x`:**

- A patch-version bump (`0.x.Y` → `0.x.Y+1`) never contains a breaking
  API change — only bug fixes, documentation, or additive, backward-
  compatible surface.
- A minor-version bump (`0.X.y` → `0.X+1.0`) *may* contain a breaking
  change — during `0.x`, SemVer itself doesn't distinguish "breaking" at
  the minor level the way it does past 1.0 (where only a major bump may
  break), so Compono treats every `0.x` minor bump as a potential
  breaking-change boundary, always called out explicitly (see below).
- All five packages at the same version number are the tested,
  guaranteed-compatible combination. Mixing version numbers across
  packages (e.g. `Compono 0.3.0` with `Compono.Bogus 0.2.0`) is
  unsupported — lockstep versioning exists specifically so "install
  matching versions" is the entire compatibility rule a consumer needs to
  remember.
- A generated composition plan (the primary execution path,
  [ADR-0001](0001-source-generation-first.md)) for a type that compiled
  and passed under one `0.x` version continues to behave identically
  under a later `0.x` patch version, absent an explicitly documented
  behavior change.

**How a breaking change is communicated**, per bump:

1. The PR/commit introducing it carries `.github/release-drafter.yml`'s
   `breaking-change` label. **That label's `version-resolver` mapping
   must be `minor`, not release-drafter's configured `major`, while
   Compono stays `0.x`** — this is a deliberate override of the file's
   current mapping, not a restatement of it: today, a `breaking-change`-
   labeled PR resolves the next version as `1.0.0`, which would exit the
   `0.x` preview line by accident rather than by the deliberate decision
   this ADR's own compatibility policy above requires. The label stays in
   `categories` unchanged (see point 2) — only its `version-resolver`
   bucket moves from `major` to `minor`; see
   [PLAN-0008](../plans/0008-milestone-8-public-preview.md) Phase 0 for
   the actual config change.
2. The generated GitHub Release notes carry an explicit "⚠️ Breaking
   Changes" section whenever a `breaking-change`-labeled PR is included
   (release-drafter's existing `categories` grouping already does this —
   no template change needed). A release with no breaking-change-labeled
   PR simply has no such section; that *absence* is itself the signal
   that nothing broke, not a gap to fill with an empty section — release-
   drafter's category rendering is inherently conditional on the labels
   actually present, and this ADR doesn't ask for a template override to
   force an unconditional heading.
3. `docs/roadmap/index.md`'s "available today / experimental / planned"
   framing and the relevant Package Guide/Concepts page are updated in
   the same PR that ships the break, per this repo's existing "docs
   change in the same PR" rule.
4. A migration note is added to the affected page(s) when the break isn't
   self-explanatory from the API diff alone (e.g. a renamed method needs
   no note; a changed default behavior does).

**Experimental vs. supported APIs.** Everything shipped in a package's
public surface is supported by default — Compono does not ship an
`[Experimental]`-attributed API surface in the first preview (no such
capability exists yet). If a future capability ships behind an opt-in
experimental flag (per `docs/documentation-architecture.md`'s Roadmap
area, e.g. a future reflection-compatibility mode per
[ADR-0001](0001-source-generation-first.md)'s own Consequences section),
it carries an explicit "Experimental" admonition in its documentation and
is exempt from the patch-bump non-breaking guarantee above until it
graduates — that policy is recorded here so a future ADR introducing such
a capability doesn't need to re-litigate it.

**Support policy for target frameworks and compiler/SDK versions.**
Compono targets `net10.0;net11.0` today (`Directory.Build.props`/each
package's `.csproj`) — `net10.0` is the current GA release; `net11.0` is
the *next* release, tracked ahead of its own GA (`global.json` pins the
`11.0.100-preview.6` SDK at time of writing, not a GA SDK). Policy:
Compono supports the current GA .NET release plus the next release in
development, tracked continuously through its preview SDKs into its own
eventual GA — a rolling two-TFM window one release *ahead*, not one
release behind. This is a deliberate choice to stay current with the
platform, not a claim that `net11.0` is itself GA-supported today; each
Package Guide's metadata states plainly which of its two TFMs is GA and
which is a preview build at the time a given version ships. The oldest
TFM is dropped only on a minor-version bump (never silently in a patch).
`Compono.Generators` targets `netstandard2.0` (required for Roslyn
analyzer/source-generator compatibility across host SDKs) — a separate,
wider constraint, unrelated to the two-TFM consumer policy above, and
already correct as shipped.

## Package-readiness policy

The bar every package (`Compono`, `Compono.Generators` where applicable,
`Compono.XunitV3`, `Compono.NSubstitute`, `Compono.Bogus`) must clear
before the first preview publish, and every future package before it
joins the set — stated as requirements, not a how-to. The concrete,
executable checklist derived from this bar (specific MSBuild properties,
CI steps, tool wiring) lives in
[PLAN-0008](../plans/0008-milestone-8-public-preview.md)'s
"Package-readiness checklist," which Phase 0 executes — this ADR doesn't
duplicate it.

- **Complete discovery metadata.** Package ID, title, description, tags,
  license, project/repository URL, icon, and embedded README — a stranger
  finding the package on nuget.org must be able to tell what it is and
  where it comes from without leaving the listing page. Whatever doesn't
  need to vary per package (license, repository URL, icon, README, tags,
  release notes) is defined once, centrally, and inherited by all five —
  the packages distribute as one coherent set (this ADR's lockstep
  versioning above), so their shared metadata comes from one place, not
  five copies that can quietly drift apart. Only genuinely per-package
  content (the ID itself, the description) lives in each project.
- **Debuggable and verifiable provenance.** Source Link, deterministic
  builds, and embedded symbols (a portable PDB embedded in the primary
  DLL is Compono's chosen shape — see PLAN-0008 for why this doesn't need
  a separate symbols package) — a consumer can step into Compono's source
  from their own debugger, and the published artifact is traceable back
  to the exact commit that built it.
- **No dependency leakage.** Build-only/analyzer-only dependencies
  (Roslyn packages, SourceLink) never flow to a consumer's own dependency
  tree; `Compono.Generators`' analyzer packaging
  ([ADR-0003](0003-generator-package-distribution.md)) survives a real
  packed-consumer restore, not just a `ProjectReference` build.
- **Exact, tested dependency pins during `0.x`.** Compono's own
  dependencies stay pinned to exact versions (not ranges) while the
  ecosystem is young — "install the version we tested against," matching
  this ADR's own "install matching Compono versions" compatibility
  philosophy. **`Directory.Packages.props`'s current bare-version syntax
  (e.g. `3.2.2`) doesn't actually enforce this** — NuGet treats a bare
  version as a minimum-inclusive floor, not a hard pin, so a transitive
  requirement elsewhere in a consumer's graph can still resolve something
  newer than what Compono tested against. Exact-pin syntax
  (`[3.2.2]`) is required to make this bullet true rather than aspirational
  — see [PLAN-0008](../plans/0008-milestone-8-public-preview.md) Phase 0.
  Version ranges are a post-1.0 concern, once real consumer
  version-conflict evidence exists to justify them.
- **Automated package and API-compatibility validation** before every
  publish, once a second real version exists to validate against. This
  cannot be a manually-set property: `publish-preview.yaml` publishes
  automatically on every non-docs `main` push with no human release step
  in between, so a baseline that only a person sets "when cutting a
  release" would never apply to the continuous preview stream — the
  large majority of what actually gets published. The baseline lookup
  must instead be a CI-automated step, in both `publish-preview.yaml` and
  `publish-release.yaml`: before packing, query nuget.org for each
  package's currently-latest published version and pass it at pack time
  via an MSBuild property override (`-p:PackageValidationBaselineVersion=<prior-version>`),
  rather than relying on a static value baked into `Directory.Build.props`.
  A first-ever publish has nothing to query yet, so validation is
  inert for exactly that one case; every publish after it — preview or
  production — has a real prior version to compare against automatically,
  with no manual step required. See
  [PLAN-0008](../plans/0008-milestone-8-public-preview.md) Phase 0 for
  the CI implementation.
- **Verified against the packed artifact, not just a project reference.**
  Every publishable package (`Compono`, `Compono.XunitV3`,
  `Compono.NSubstitute`, `Compono.Bogus`) is restored and smoke-tested
  from a real local feed before it's trusted to publish — this repo's own
  `test/Compono.XunitV3.SampleTests` precedent (PLAN-0004/0005/0006),
  extended to cover all four together as a standing gate.
  `Compono.Generators` has no independent `.nupkg` to restore
  (`IsPackable=false` — it's embedded inside `Compono`'s own package, per
  ADR-0003); it's verified by inspecting `Compono.nupkg`'s
  `analyzers/dotnet/cs` contents directly, not by a separate restore.
- **No known vulnerability, and no unreviewed license, in the dependency
  tree.** These are two different checks, not one — Dependabot's existing
  security-update flow (`dependabot-auto-merge.yml`) genuinely covers
  vulnerability alerts, but Dependabot never inspects a dependency's
  *license*; nothing in this repo's tooling does. Rather than claim
  license risk is "covered" by infrastructure that doesn't check it, the
  actual policy is a manual review step: any PR that adds a new
  dependency or changes a `PackageVersion` (Dependabot-authored or not)
  gets its target package's license checked as part of normal PR review,
  not a one-time snapshot at launch and never again. No new automated
  tool is introduced for a dependency set this small — this is a review
  habit, not a gate.

Provenance/signing and trusted publishing are already handled by the
existing pipeline (NuGet.org's own signing infrastructure via the OIDC
trusted-publishing flow already wired into `nuget-push`) and are not
re-decided by this ADR.

## Release pipeline: failure and partial-publish handling

Not redesigned — the existing two-workflow split
(`publish-preview.yaml`/`publish-release.yaml`, both delegating to
`LayeredCraft/devops-templates`) stays exactly as-is beyond the
`alpha`→`preview` identifier rename above. Policy, recorded here because it
governs what "safe to retry" means rather than describing a CI step: all
four publishable packages are built and pushed from one coordinated
lockstep version (`Compono.Generators` builds alongside them but has
nothing of its own to push — it ships embedded inside `Compono.nupkg`),
so a partial publish failure means a transient push error, not a version
mismatch — the recovery procedure is simply re-running the push, and
NuGet's own idempotent-upload behavior (same content, same version →
no-op; different content, same version → rejected) makes that safe
without a new rollback mechanism. If an already-published package is
later found defective, the fix is always a new patch release (per SemVer
— never unpublishing or overwriting a live version); NuGet's unlist (not
delete) mechanism is available as a last resort for a genuinely broken
package, used only alongside a same-day fixed release, never as a
substitute for one.

## Positive Consequences

- A consumer gets one version number to track and one compatibility rule
  ("matching versions across all five packages") instead of a
  compatibility matrix.
- The existing, working release pipeline and the existing `cosmere-tracker`
  consumption pattern both continue to work unmodified beyond the
  `alpha`→`preview` identifier rename.
- The package-readiness checklist gives Phase 0 of PLAN-0008 a concrete,
  verifiable bar instead of an open-ended "make packages good" task.

## Negative Consequences

- Lockstep versioning means an unrelated single-package fix (e.g. a
  `Compono.Bogus`-only bug) still bumps every package's version number,
  which can read as noisier release history than independent versioning
  would produce. Accepted: the simplicity and existing-practice
  continuity outweighs this for a five-package preview at this stage: a
  future ADR can revisit this once real multi-package-divergence evidence
  exists (e.g. packages actually needing different release cadences).
- Treating every `0.x` minor bump as a potential breaking-change boundary
  (rather than only communicating breaks "when they happen," at whatever
  version level) means a reader can never assume a minor bump is safe
  just from its version number alone — they have to check whether that
  specific release's notes carry a "⚠️ Breaking Changes" section (present
  only when a `breaking-change`-labeled PR is actually included, per
  "How a breaking change is communicated" above) rather than relying on
  SemVer's own past-1.0 convention that only a major bump can break.
  Accepted: the alternative (inconsistent signaling of which bumps might
  break) is worse for a preview trying to earn trust.

## Pros and Cons of the Options

### Lockstep versioning (chosen)

- Good, because it matches the already-working `cosmere-tracker`
  consumption pattern exactly.
- Good, because "install matching versions" is the entire compatibility
  rule a consumer needs.
- Bad, because an unrelated single-package change still bumps every
  package's version.

### Independent per-package versioning

- Good, because it avoids bumping untouched packages.
- Bad, because it requires publishing and maintaining a compatibility
  matrix this project has no current evidence it needs.
- Bad, because it breaks the existing `cosmere-tracker` pinning practice.

### Keep the `alpha` identifier

- Good, because it requires no pipeline change at all.
- Bad, because it's a less conventional label than `preview` for a
  continuously-published prerelease stream, and is the reason
  `cosmere-tracker`'s `Directory.Packages.props` currently pins an
  alpha-suffixed version.

### Rename to `preview` (chosen)

- Good, because `preview` is the more conventional label for this
  publishing stream, and matches the workflow's own name.
- Good, because it's a one-line pipeline change, not a redesign.
- Good, because it carries no sequencing risk — every version this
  workflow produces stays a SemVer prerelease either way, so there's no
  reason to delay it behind a later checkpoint.
- Bad, because it still requires bumping `cosmere-tracker`'s pinned
  version once this ships, same as any other identifier change would.

## Links

- [ADR-0001](0001-source-generation-first.md) — source-generation-first
  default; this ADR's experimental-API carve-out references its own
  Consequences section
- [ADR-0003](0003-generator-package-distribution.md) — `Compono.Generators`'
  transitive packaging, verified (not redesigned) by this ADR's
  packed-consumer checklist item
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  "Bug handling" precedent this ADR's package-inclusion gate reuses
- `docs/migrating-from-autofixture.md` — the existing "bump all four
  versions together" practice this ADR formalizes
- `.github/workflows/publish-preview.yaml`/`publish-release.yaml` — the
  unmodified release pipeline this ADR's policy governs
- [PLAN-0008](../plans/0008-milestone-8-public-preview.md) — Phase 0
  executes this ADR's package-readiness checklist
