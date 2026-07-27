# [ADR-0005] Source Generator Implementation Conventions

**Status:** Accepted

**Date:** 2026-07-27

**Decision Makers:** solo

## Context

Beyond *what* the generator discovers and *how* dispatch works
([ADR-0004](0004-composition-plan-discovery-and-dispatch.md)), Milestone 1
([PLAN-0001](../plans/0001-milestone-1-source-generation-foundation.md))
needs a set of implementation conventions for the generator project
itself: how C# source text actually gets assembled, how pipeline values
stay correctly cacheable for incremental generation, how diagnostics
survive that same caching, how the generated code achieves the
low-allocation shape `docs/manifesto.md`'s "Performance is a feature"
calls for, and how the generator itself gets tested. `dynamo-mapper`
(`LayeredCraft.DynamoMapper.Generators`) is an existing, working
incremental generator in this org solving all of these mechanically, and
is the explicit model to follow here rather than reinventing each one.
This is a light dive — the shape is already proven and working elsewhere
in this org; the only question is whether to adopt it, not what the
alternatives are.

## Decision Drivers

- `docs/manifesto.md`'s "Performance is a feature" — generated code
  allocates on every composed test, so its shape matters directly.
- Proven, working precedent already in production in this org
  (`dynamo-mapper`) lowers implementation risk versus a novel approach.
- Incremental generator pipeline values must have correct structural
  equality for Roslyn's caching to actually work — this is easy to get
  subtly wrong (e.g. `ImmutableArray<T>`/`List<T>` inside a pipeline
  record don't have the right equality semantics) and worth getting right
  from the start rather than discovering it via a perf regression later.

## Considered Options

1. Adopt `dynamo-mapper`'s conventions wholesale (Scriban templating,
   `EquatableArray<T>`, `WellKnownTypes` symbol cache, cacheable
   `DiagnosticInfo`/`LocationInfo`, low-allocation generated-code shape,
   `Verify.SourceGenerators` testing).
2. Reinvent each mechanic independently for Compono.

## Decision Outcome

Chosen option: **adopt `dynamo-mapper`'s conventions**, specifically:

- **Templating**: [Scriban](https://github.com/scriban/scriban), referenced
  `IncludeAssets="Build"` only (never flows to consumers). Template files
  live under `Templates/*.scriban`, embedded as resources
  (`<EmbeddedResource Include="Templates\*.scriban"/>`), loaded via
  `Assembly.GetManifestResourceStream` and cached in a
  `ConcurrentDictionary<string, Template>` keyed by resource name (parse
  once per generator-process lifetime). Scriban assembles **final file/class
  shape only** — per-member codegen logic (a single property's
  read/write expression, a helper method body) is rendered to a plain
  string by ordinary C# first and passed into the template model already
  formed; the template loops over and interpolates those pre-rendered
  fragments rather than expressing per-member logic in the templating
  language itself.
- **Incremental-cache-safe pipeline models**: every pipeline value that
  needs to survive Roslyn's incremental caching is a `record` (free
  structural equality) using `EquatableArray<T>` (not
  `ImmutableArray<T>`/`List<T>`) for any collection member. A pipeline
  stage that carries something *not* meant to participate in cache
  identity (a semantic-model handle needed only for a later rendering
  pass, say) overrides `Equals`/`GetHashCode` explicitly to exclude it,
  rather than accepting whatever the record's default comparison would do.
- **Symbol lookups**: a `WellKnownTypes`-style compilation-scoped cache
  (one instance memoized per `Compilation`) resolves and reuses
  `INamedTypeSymbol`s for Compono's own marker types/attributes via
  `SymbolEqualityComparer`, instead of scattering
  `GetTypeByMetadataName`/string-based type-name comparisons through the
  generator.
- **Diagnostics**: `DiagnosticDescriptor`s are `internal static readonly`
  fields grouped by an ID-range convention (documented inline, mirroring
  `dynamo-mapper`'s `DMxxxx` ranges — Compono's own prefix is a Milestone 1
  implementation detail, not a new architectural decision). Diagnostics
  travel through the pipeline as a small cacheable `DiagnosticInfo` record
  (descriptor + a serializable `LocationInfo` + message args) rather than
  a live `Diagnostic`/`Location`, and are only materialized/reported
  inside `RegisterSourceOutput` — `Diagnostic`/`Location` off Roslyn
  symbols aren't safely cacheable. `AnalyzerReleases.Shipped.md`/
  `Unshipped.md` track each ID per standard Roslyn analyzer-release-tracking
  conventions.
- **Generated-code shape** (the actual low-allocation goal): pre-sized
  collections where the size is known at generator time
  (`new Dictionary<TKey, TValue>(n)`, never grown/rehashed), fluent
  chaining over LINQ or intermediate local variables where a single
  expression can do the job, expression-bodied members preferred over
  block bodies whenever there's no branching/hook logic needed,
  fully-qualified type names in generated signatures (collision-proof
  regardless of a consumer's `using`s) with shorter names permitted in
  method bodies, and thin generated code that calls into ordinary
  hand-maintained runtime helper methods for any nontrivial logic rather
  than inlining that logic into every generated file. No `Task.Run`/async
  ceremony in generated code that doesn't need it.
- **Testing**: a dedicated `Compono.Generators.Tests` project using
  `Verify.SourceGenerators` + `Verify.XunitV3`, multi-targeted to match
  `Compono`'s own `TargetFrameworks`, with per-TFM
  `Basic.Reference.Assemblies.Net10/11` packages so generated code is
  compiled and verified against real reference assemblies. Each test
  supplies inline source, runs the generator via
  `CSharpGeneratorDriver`, asserts the generator itself reports no
  unexpected diagnostics, re-compiles the generated output back into the
  original compilation and asserts **zero errors** (proving generated code
  actually compiles, not just snapshotting text), and snapshots via
  `Verifier.Verify(...).UseDirectory("Snapshots")` with a scrubber
  replacing the embedded generator version so snapshots stay stable across
  version bumps. `.WithTrackingName(...)` is applied to every named
  pipeline stage from the start, so incrementality (cache-hit behavior)
  can be asserted in tests later without retrofitting.

### Positive Consequences

- Materially lower implementation risk — every one of these mechanics is
  already working, tested code in this org, not a first attempt.
- Correct incremental-caching behavior (via `EquatableArray<T>` +
  deliberate record equality) from the very first pipeline stage, instead
  of discovering a caching bug after the fact.
- Generated code is genuinely low-allocation by construction (pre-sized
  collections, no LINQ, fluent/expression-bodied shape), directly serving
  `docs/manifesto.md`'s performance principle rather than treating it as
  an afterthought.

### Negative Consequences

- Adds `Scriban` as a build-time-only dependency of `Compono.Generators` —
  no consumer-facing impact (`IncludeAssets="Build"`), but it is a new
  third-party dependency for this project to track.
- These conventions carry real mechanical complexity (a symbol cache, a
  cacheable-diagnostic wrapper type, a template-loading/caching layer) for
  what could, for a much smaller generator, be simpler hand-rolled code —
  accepted because Compono's generator is expected to grow well past
  "much smaller" over the MVP's remaining milestones, and retrofitting
  these mechanics onto a generator that didn't start with them is more
  expensive than adopting them up front.

## Pros and Cons of the Options

### Adopt dynamo-mapper's conventions (chosen)

- Good, because every mechanic is proven, working code in this org today.
- Good, because it directly produces the low-allocation generated-code
  shape this decision (and the user directing it) explicitly calls for.
- Good, because the incremental-caching correctness (`EquatableArray<T>`,
  deliberate record equality) is easy to get wrong from scratch and hard
  to retrofit later — starting correct avoids that entirely.
- Bad, because it's more mechanical surface area up front than a minimal
  first version would need.

### Reinvent independently

- Good, because a from-scratch approach could in principle be simpler for
  Compono's specific shape.
- Bad, because it discards proven, working precedent for an unproven one,
  with no concrete benefit identified over adopting it.
- Bad, because incremental-caching correctness and low-allocation codegen
  are both easy to get wrong on a first attempt, and this repo has a
  working reference implementation available to avoid that risk.

## Links

- Prior art: `dynamo-mapper`'s `LayeredCraft.DynamoMapper.Generators`
  project in full — `Emitters/TemplateHelper.cs` (template loading/caching),
  `WellKnownTypes/WellKnownTypes.cs` (symbol cache),
  `Diagnostics/DiagnosticDescriptors.cs`/`DiagnosticInfo.cs` (cacheable
  diagnostics), `Templates/Mapper.scriban` (templating style),
  `AttributeValueExtensions/*.cs` (thin-generated-code/fat-runtime-helper
  pattern), `test/LayeredCraft.DynamoMapper.Generators.Tests/GeneratorTestHelpers.cs`
  (testing harness).
- [ADR-0004](0004-composition-plan-discovery-and-dispatch.md) — the
  discovery/dispatch decision these conventions implement against.
- [PLAN-0001](../plans/0001-milestone-1-source-generation-foundation.md).
