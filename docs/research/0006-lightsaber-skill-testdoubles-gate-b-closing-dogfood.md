# [RESEARCH-0006] `lightsaber-skill` Gate-B Closing Dogfood: Full `Compono.NSubstitute` Removal

## Scope

[PLAN-0046](../plans/0046-static-abstract-member-conformance-only-generation.md)'s
own closing acceptance test: re-run `lightsaber-skill`'s test suite
against the newly-published Compono `0.5.0-preview.74` package (built
from [ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)'s
merged fix, [compono#99](https://github.com/LayeredCraft/compono/pull/99))
and confirm the explicit Gate-B criterion in full:

> I need `Compono.TestDoubles` to be capable of completely replacing
> `Compono.NSubstitute` in `lightsaber-skill`.

Not "fewer NSubstitute call sites" (the third pass's own bar,
[RESEARCH-0005](0005-lightsaber-skill-testdoubles-v2-third-dogfood.md)) —
full removal: `Compono.NSubstitute` and `NSubstitute` gone from the
project's package references entirely, confirmed absent even
transitively, with the full test suite passing through the built test
executable.

## Result: full removal achieved

Every bullet Gate-B requires, verified directly, not assumed:

- **`IAmazonS3` generates and resolves through `UseGeneratedTestDoubles()`
  alone.** Confirmed via a clean `dotnet build` of
  `Lightsaber.Skill.Tests.csproj` against `0.5.0-preview.74` — no
  `CMP0021`, no fallback to a runtime provider. `IAmazonS3.CreateDefaultClientConfig()`
  (inherited from `IAmazonService`, ADR-0046's real motivating case) is
  now correctly recognized as already resolved by `IAmazonS3`'s own
  concrete override, exactly as ADR-0046 designed.
- **The remaining ~9 `NSubstitute` call sites migrated.**
  `LightsaberHandlerTests.cs`'s last holdout —
  `Substitute.For<IAmazonS3>()`, `Arg.Any<ListObjectsV2Request>()`,
  `.Returns(...)` on `ListObjectsV2Async` — is now
  `composer.Create<IAmazonS3>()` + `Configure().ListObjectsV2Async().Returns(...)`,
  the same `Compono.TestDoubles` pattern every other test file in this
  suite already used. `IOptions<LightsaberOptions>`/`ILogger<T>` in the
  same helper method moved to `composer.Create<T>()` too, replacing the
  last raw `Substitute.For<T>()` calls anywhere in the project.
  `ListObjectsV2Async` turned out not to be overloaded (verified via
  reflection against the real `AWSSDK.S3` package before writing the
  migration, not assumed) — its `Configure()` extension is the ordinary
  zero-argument form, no discriminator needed.
- **`Compono.NSubstitute`/`NSubstitute` removed entirely.**
  `Directory.Packages.props` and `Lightsaber.Skill.Tests.csproj` no
  longer reference either package.
  `dotnet list package --include-transitive` confirms `NSubstitute` isn't
  pulled in even transitively — Gate-B's exact wording, not just "no
  direct reference." `GeneratedTestDoublesProfile`'s own
  `builder.UseNSubstitute()` call was removed along with it —
  `UseGeneratedTestDoubles()` alone now covers every interface this suite
  composes.
- **Full 77-test suite passes via the built executable.** Run directly
  (`Lightsaber.Skill.Tests.dll` via the Microsoft.Testing.Platform
  runner), not just a clean compile — same verification bar
  RESEARCH-0004/RESEARCH-0005 held themselves to. 77/77 passed, 0
  skipped.

No partial result to report this time — every bullet passed. Recorded as
a follow-up PR ([lightsaber-skill#108](https://github.com/ncipollina/lightsaber-skill/pull/108))
on top of the branch RESEARCH-0005's baseline/migration commits
(`192d334`, `8078054`) already used, per PLAN-0046's own note that this
step needed a newly published package before it could even start.

## Result: performance (Gate-B closing benchmark)

Same methodology as RESEARCH-0005: `hyperfine --warmup 3 --runs 15`
against the built Microsoft.Testing.Platform executable directly
(`Lightsaber.Skill.Tests`, Release config, `dotnet build -c Release
--no-restore` run separately beforehand and excluded from the timed
command). Environment: macOS 26.6.1 (25G76), Apple M3 Max, .NET SDK
`11.0.100-preview.7.26381.103`, xunit.v3.mtp-v2 `3.2.2`, 77 tests (0
failed) in both configurations. Baseline built in an isolated `git
worktree` at `192d334` so both binaries could be built and benchmarked
without switching branches mid-comparison.

| | Baseline (`192d334`, NSubstitute) | Migrated (`df0a7f5`, TestDoubles only, no NSubstitute) |
|---|---|---|
| Compono / Compono.TestDoubles / NSubstitute | `0.5.0-preview.73` / `0.5.0-preview.73` / `6.2.0` | `0.5.0-preview.74` / `0.5.0-preview.74` / removed entirely |
| mean | 3.879 s | 3.995 s |
| stddev | 0.183 s | 0.191 s |
| min / max | 3.619 s / 4.401 s | 3.796 s / 4.499 s |

**Absolute difference (mean): +0.116 s. Relative: migrated ran 1.03× ±
0.07 slower than baseline** (hyperfine's own relative-uncertainty
report). The uncertainty band (±0.07) comfortably contains 1.00× — this
is not a meaningful difference, it's inside normal run-to-run noise for
a 77-test suite this size dominated by process startup, JIT, and (for
the infra tests) AWS CDK synthesis overhead, not test-double dispatch
cost. Unlike RESEARCH-0005's own benchmark (which still had
`Compono.NSubstitute` active for `IAmazonS3` in its "migrated"
configuration, so it explicitly wasn't a clean provider comparison),
**this one is clean**: the migrated build has zero `NSubstitute`
anywhere in its dependency graph, direct or transitive. Even under a
clean, complete before/after, replacing every test-double call site with
`Compono.TestDoubles` produced no observable wall-clock change on this
real suite. As before, this is one real project's honest result, not a
general Compono performance claim.

## What this closes, and what it doesn't

Gate-B — `lightsaber-skill`'s test project fully replacing
`Compono.NSubstitute` with `Compono.TestDoubles` — is now real, not
planned. This is the fourth real project state RESEARCH-0004/0005/0006
have tracked in sequence for this one project:

1. RESEARCH-0004: six of seven interfaces blocked, `Compono.NSubstitute`
   required project-wide.
2. RESEARCH-0005: six of seven interfaces resolved, one (`IAmazonS3`)
   still blocked by what looked like a genuine static-abstract-member
   capability gap — `Compono.NSubstitute` still required, for one
   interface's sake.
3. This pass: `IAmazonS3` resolved too, once the real root cause (an
   analyzer bug misreading an already-resolved inherited member, not a
   genuine gap — see ADR-0046) was found and fixed. `Compono.NSubstitute`
   fully removed.

What this does **not** establish: that every real-world interface with a
static abstract member will resolve the same way `IAmazonS3` did.
`IAmazonS3`'s shape — a base interface declares the member abstractly, a
more-derived interface re-implements it concretely — is common enough to
be the shape .NET's own BCL/AWS SDK teams reach for, but a genuinely
unresolved static abstract member (no override anywhere in an
interface's closure) still whole-interface-rejects, unchanged, and per
ADR-0046's own second finding, C# itself (`CS8920`) makes such an
interface uncomposable through Compono's `Resolve<TValue>()` regardless —
not a `Compono.TestDoubles` gap to close, a language constraint no
dogfooding pass could work around.

## Decisions

`PLAN-0046` is complete: both the generator-side fix
([compono#99](https://github.com/LayeredCraft/compono/pull/99)) and this
closing dogfood are done. `docs/roadmap/post-mvp.md`'s entry for this
candidate moves from outstanding to a fully-shipped state — this is a
real graduation, not a partial one like RESEARCH-0005's own "4 of 5
files" result was for the prior pass.

## Links

- [RESEARCH-0005](0005-lightsaber-skill-testdoubles-v2-third-dogfood.md) —
  the prior pass whose sole remaining blocker this one closes, and whose
  reclassification (against the same explicit Gate-B requirement quoted
  above) opened the roadmap candidate ADR-0046 designed a response to.
- [ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md) —
  the design (and, after implementation-time compile spikes disproved its
  first accepted version, the redesign) this pass verifies against a real
  project.
- [PLAN-0046](../plans/0046-static-abstract-member-conformance-only-generation.md) —
  the implementation plan this pass's own task list came from.
- [compono#99](https://github.com/LayeredCraft/compono/pull/99) — the
  merged generator fix, published as Compono `0.5.0-preview.74`.
- [lightsaber-skill#108](https://github.com/ncipollina/lightsaber-skill/pull/108) —
  this pass's actual migration.
