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
