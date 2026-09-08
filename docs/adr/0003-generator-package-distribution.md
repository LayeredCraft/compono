# [ADR-0003] Generator Package Distribution

**Status:** Accepted

**Date:** 2026-07-27

**Decision Makers:** solo

## Context

`Compono.Generators` needs a physical home relative to the `Compono`
package consumers actually reference. A Roslyn incremental generator has
build requirements that don't match a normal runtime library (it targets
`netstandard2.0`, references `Microsoft.CodeAnalysis.CSharp` privately so
that dependency never leaks to consumers, and its output has to be packed
as an analyzer asset rather than a normal reference assembly) — so this
decision is about *how many NuGet packages get published* and *how the
generator's build output reaches consumers*, not about tearing apart the
existing `docs/architecture.md` "Package Boundaries" list of what
`Compono.Generators` owns. `docs/mvp.md` already hinted at the intended
direction ("`Compono.Generators` may be shipped as a transitive analyzer
dependency rather than a package users reference directly"), but left the
exact mechanism open, and `docs/architecture.md`'s "Open Architectural
Decisions" lists it explicitly ("Whether source-generation contracts live
in `Compono` or `Compono.Generators`"). This blocks Milestone 1's "Create
generator project" work item, which can't start cleanly without knowing
whether it's creating one project or two, and whether `Compono.Generators`
ever gets its own `nuget.org` listing.

## Decision Drivers

- `docs/public-api.md`'s API goal "Easy to discover, small enough to
  learn" — a consumer adding a second, generator-only package just to get
  `Compono` working would be a discoverability tax with no product
  benefit, since the generator isn't something a consumer configures or
  calls directly.
- Established precedent already in production in this org:
  `AlexaVoxCraft.MediatR` ships exactly this shape today
  (`AlexaVoxCraft.MediatR.Generators` is a sibling project, `IsPackable=false`,
  whose build output is packed directly into the `AlexaVoxCraft.MediatR`
  nupkg's `analyzers/dotnet/cs` folder — never published as its own
  package). Reusing a proven pattern lowers implementation risk for
  Milestone 1 versus inventing a new packaging shape from scratch.
- Keeping the generator's `netstandard2.0` / private-Roslyn-reference build
  requirements isolated from the runtime package's own build
  (`docs/mvp.md`'s multi-target ambitions for `Compono` itself) still
  requires the generator to live in its own `.csproj`, independent of
  whether that project's output ever gets its own NuGet listing.

## Considered Options

1. `Compono.Generators` published as its own NuGet package, referenced by
   `Compono` with `PrivateAssets="all"` so it flows transitively as an
   analyzer without consumers adding it themselves.
2. `Compono.Generators` as a sibling project that is never independently
   packed or published — its compiled output is packed directly into the
   `Compono` nupkg's `analyzers/dotnet/cs` folder, referenced from
   `Compono.csproj` as an `Analyzer`-only `ProjectReference`
   (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`).
3. No separate project at all — the generator's source lives directly
   inside `Compono.csproj`, built as part of the same compile as the
   runtime library.

## Decision Outcome

Chosen option: **"`Compono.Generators` as a sibling project, never
independently published."** `Compono.Generators` is its own `.csproj`
(`netstandard2.0`, `IsPackable=false`, `Microsoft.CodeAnalysis.*`
referenced with `PrivateAssets="all"`), referenced from `Compono.csproj`
as an `Analyzer`-only `ProjectReference`. `Compono.csproj` packs the
generator's built `.dll` directly into its own nupkg under
`analyzers/dotnet/cs`. Only `Compono` is ever published to NuGet;
`Compono.Generators` exists purely as a build-time implementation detail.
This mirrors `AlexaVoxCraft.MediatR`/`AlexaVoxCraft.MediatR.Generators`
exactly.

### Positive Consequences

- Consumers add exactly one package reference (`Compono`) and get
  generation for free — no discoverability tax, no risk of a consumer
  pinning `Compono.Generators` to a mismatched version relative to
  `Compono` (there's only ever one version, because there's only ever one
  published package).
- One fewer package to independently version, release-note, and publish
  on every release — `Compono.Generators`' changes ship exactly in lockstep
  with `Compono`'s, which is appropriate since the two are never meant to
  vary independently anyway (a generator update that doesn't match its
  runtime library is a broken pairing, not a valid combination to allow).
- Proven pattern already running in production in this org
  (`AlexaVoxCraft.MediatR`), rather than a first-time packaging design.

### Negative Consequences

- If a future need arises for something else to depend on
  `Compono.Generators`' contracts independently of the full `Compono`
  runtime package, that would require revisiting this decision (a new ADR
  superseding this one) rather than just adding a project reference —
  accepted as unlikely enough for the MVP to not design around
  preemptively (`docs/mvp.md`'s non-goals already exclude speculative
  extensibility this decision isn't aware of a concrete need for).

## Pros and Cons of the Options

### Compono.Generators as its own published package, private transitive dependency

- Good, because it matches how some other source-generator libraries in
  the wider .NET ecosystem ship.
- Good, because it still keeps consumers from needing a direct reference
  to it.
- Bad, because it doubles the number of packages to version and publish
  on every release for no discoverability or flexibility benefit specific
  to Compono's actual needs.
- Bad, because it introduces a possible (if usually avoidable) version
  mismatch between the two packages that the bundled option can't have by
  construction.
- Bad, because there's no existing precedent for this specific shape in
  this org to derisk it against, unlike option 2.

### Compono.Generators as a sibling project, never independently published

- Good, because it's a single published package — no discoverability tax,
  no version-mismatch risk.
- Good, because it's a proven, already-shipping pattern
  (`AlexaVoxCraft.MediatR`) rather than a novel one.
- Good, because the generator's `netstandard2.0`/private-Roslyn-reference
  build stays isolated in its own project despite not being independently
  published.
- Bad, because nothing outside `Compono`'s own build can ever reference
  `Compono.Generators`' contracts without that requirement prompting a
  revisit of this ADR.

### No separate project — generator source lives inside Compono.csproj

- Good, because it's the simplest possible project layout — one project,
  one build.
- Bad, because a Roslyn generator's build requirements (`netstandard2.0`
  target, private Roslyn references, `IncludeBuildOutput=false`) actively
  conflict with a normal runtime library's build requirements
  (`docs/mvp.md`'s multi-target and trimming/AOT goals for `Compono`
  itself) — forcing both into one project means fighting MSBuild
  conditionals instead of just having two projects with two straightforward
  configurations.
- Bad, because it has no precedent in this org to derisk it, and actively
  contradicts the one that does exist.

## Links

- `docs/architecture.md`'s "Package Boundaries" → `Compono.Generators`
  section and "Open Architectural Decisions" list.
- `docs/mvp.md`'s note on `Compono.Generators` distribution and Milestone 1
  scope ("Create generator project").
- Precedent: `AlexaVoxCraft.MediatR.csproj` /
  `AlexaVoxCraft.MediatR.Generators.csproj` in the `alexa-vox-craft` repo.

## Amendment 1 (2026-09-08): minimum-supported Roslyn/SDK version

**Context.** This ADR's own Milestone 1 review round (`docs/plans/0001-milestone-1-source-generation-foundation.md`)
already flagged pinning `Compono.Generators`' compile-time
`Microsoft.CodeAnalysis.CSharp` reference to the oldest supported
Roslyn/SDK version as "a real supply-chain concern," but the decision on
an actual minimum version was explicitly deferred at the time. That gap
became a real, live bug: `Directory.Packages.props` pinned
`Microsoft.CodeAnalysis.CSharp` at `5.6.0`–`5.9.0` from the very first
generator commit onward — every published `Compono` release, `v0.1.0`
through `v1.2.0`. Roslyn refuses to load an analyzer built against a
compiler *newer* than the host's own (silently — `CS9057`, a build
warning, never an error), so on any officially-supported `net8.0`/
`net9.0`/`net10.0` **stable** SDK, `Compono.Generators.dll` simply never
ran; a consumer's `Composer.Create<T>()` then failed at runtime with a
misleading "no generated plan" `CompositionException` instead of a
build-time signal pointing at the real cause. This repo's own CI, and
every dogfood consumer used to validate releases, all pin an
`11.0.100-preview` SDK (`global.json`), whose bundled Roslyn was new
enough — so nothing caught it until a Codex review on the fixing PR
(#136) pointed out the first attempted fix itself only verified against
one specific .NET 8 SDK patch build, not the actual documented minimum.

**Decision.** `Microsoft.CodeAnalysis.CSharp` is pinned to `4.11.0` —
confirmed to be a **hard technical floor**, not a chosen one:
`Compono.Generators/Discovery/TestDoubleAnalyzer.cs` uses
`ITypeParameterSymbol.AllowsRefLikeType`, a Roslyn API that only exists
starting at compiler package version `4.11.0` (bisected empirically:
`4.9.2`/`4.10.0` fail `CS1061`, `4.11.0` compiles). `4.11.0` first shipped
with .NET SDK **`8.0.4xx`** (the VS 17.11-era feature band) — so
Compono's real minimum-supported .NET 8 SDK is **`8.0.400`**, not
`8.0.100` (GA), which is a materially narrower floor than
`docs/getting-started/installation.md` previously implied (it stated only
the `net8.0`/`net9.0`/`net10.0`/`net11.0` TFM floor, with no SDK feature-
band minimum at all). `net9.0`/`net10.0`/`net11.0` have no equivalent
constraint — every released SDK for those TFMs already bundles a Roslyn
compiler `>= 4.11.0`.

Verified empirically across all four officially-supported SDKs (a
throwaway packed-consumer build/run against `8.0.408`, `9.0.304`,
`10.0.103`, and `11.0.100-preview.7`) both before (`CS9057` +
`CompositionException` on the three stable SDKs) and after (identical,
correct generated output on all four) this fix.

**Consequences.**

- `docs/getting-started/installation.md` now states the `8.0.400` minimum
  explicitly, closing the documentation gap the Codex review caught.
- `.github/dependabot.yml` ignores all automated updates to
  `Microsoft.CodeAnalysis.CSharp` — this floor is a deliberately
  cross-SDK/API-verified decision (both "does it load" and "does it even
  compile" constraints), not something that should move via an unattended
  bump; this repo's own CI wouldn't catch a regression here, since it
  pins the `11.0-preview` SDK.
- Raising this floor in the future (e.g. to use a newer Roslyn API) is a
  deliberate decision that should re-run this same empirical verification
  before merging, not something to infer is safe from a successful local
  build alone (a local build only proves the *compile-time* floor, not
  the *runtime-load* floor on every supported SDK — this amendment's own
  history is a direct example of that distinction actually mattering).
- This does not change ADR-0003's core decision (single sibling project,
  never independently published) — it only completes a previously
  explicitly-deferred piece of the same "how does a consumer's host
  actually consume this analyzer" question ADR-0003 already opened.
