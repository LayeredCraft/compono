# Contributing

Compono welcomes outside contributions. This page covers what to expect
day to day — build/test, PR conventions, and how dependency changes get
reviewed. For the project's conduct expectations, see the
[Code of Conduct](https://github.com/LayeredCraft/.github/blob/main/.github/CODE_OF_CONDUCT.md)
(applies org-wide via GitHub's community-health-file inheritance — Compono
doesn't keep its own copy). For reporting a security vulnerability, see
the [Security Policy](https://github.com/LayeredCraft/.github/blob/main/.github/SECURITY.md)
instead of opening a public issue.

## Before you start

- Check this docs site and `docs/adr/` before proposing an architecture
  change — an existing [Concept](concepts/index.md),
  [Architecture](architecture/index.md) page, or
  [ADR](architecture/decision-log.md) may already record the decision
  you're about to re-derive.
- For anything beyond a small fix (a new provider, a new registration
  behavior, a new package), open a
  [Feature Request](https://github.com/LayeredCraft/compono/issues/new/choose)
  issue first and let a maintainer weigh in before you invest in an
  implementation — this repo intentionally has one way to do each thing,
  and a design conversation up front is cheaper than a large PR that has
  to change direction in review.
- Looking for a first contribution? A missing [Cookbook](cookbook/index.md)
  recipe is the easiest way in — narrow in scope, easy to review, and
  doesn't require touching the composition engine itself. See the
  ["Good first issue" candidates](#good-first-issue-candidates) below.

## Build and test

```bash
dotnet restore Compono.slnx
dotnet build Compono.slnx -c Release
dotnet test Compono.slnx -c Release
```

The solution targets `net8.0`/`net9.0`/`net10.0`/`net11.0`; install all four
SDKs (or let `global.json`/CI resolve them) before building. Tests run on the
[Microsoft Testing Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
runner, not VSTest — use MTP's own filter syntax
(`--filter-not-class`/`--filter-class`, not VSTest-style `--filter`) if you
need to scope a run to one project or class.

## Making a change

- **Match the existing coding standards** — naming, nullable annotations,
  async patterns, DI-only wiring, traditional constructors. Don't
  introduce a new pattern (a new DI registration style, a new
  error-handling approach, a new test-framework helper) without raising it
  in an issue first.
- **Tests are expected** for new composition-engine, provider, or
  generator behavior — xUnit v3 on the Microsoft Testing Platform,
  handwritten/explicit test data (this repo deliberately doesn't use
  AutoFixture-style generated test data for its own tests — see
  [Architecture](architecture/index.md) if you're curious why).
- **XML doc comments are required on every new or changed public member**
  across all projects — `Compono` and its integration packages are
  published NuGet libraries, and IntelliSense is the primary
  discoverability surface for a consumer who's never read the source.
  `dotnet build -p:WarningsAsErrors=CS1591` fails a PR that's missing one,
  the same gate that runs in CI.
- **Update the relevant docs page in the same PR**, not as a follow-up —
  if your change affects behavior a Concept, How-to Guide, or Package
  Guide already describes, update that page alongside the code.
- **Keep PRs scoped to one decision or one feature.** Bundling an
  unrelated refactor into the same diff makes the change harder to review
  and harder to revert independently if something needs to be undone.

## Pull request conventions

- **PR titles follow [Conventional Commits](https://www.conventionalcommits.org/)**
  (`feat: ...`, `fix: ...`, `docs: ...`, `refactor: ...`, `test: ...`,
  `chore: ...`, `ci: ...`, `revert: ...`) — a CI check enforces this, and
  the type drives both the auto-generated release notes and the next
  version number. Add `!` after the type (e.g. `feat!: ...`) or apply the
  `breaking-change` label for a change that breaks an existing public API
  — see [Package Guides](packages/index.md) for what "public API" means
  for a published package.
- Fill in the PR template's checklist honestly — it exists to make review
  faster, not as ceremony.
- CI runs a full build/test pass, plus (for the seven publishable
  packages) a package-validation gate: API-compatibility baseline check,
  packed `.nupkg` contents inspection, and a local-feed consumer smoke
  test. All of these must pass before merge.

## Dependency changes

Any PR that adds or bumps a dependency version — including
Dependabot-authored PRs — gets its target package's license checked as
part of normal review: MIT, BSD-2/3-Clause, and Apache-2.0 are all
compatible with Compono's own MIT license; a copyleft license
(GPL/AGPL/LGPL) is not. This is an ongoing review habit for every
dependency change, not a one-time audit — Dependabot's own alerts catch
known vulnerabilities, not license compatibility, so a human still needs
to look at the target package's license on every bump.

## Good first issue candidates

The [Cookbook](cookbook/index.md) is deliberately structured for this: each
recipe is short, self-contained, and reviewable in isolation, without
touching the composition engine itself. Candidate recipes not yet written,
each following the same shape as the existing five:

- Compose a collection with a fixed, non-default size
- Share a value across multiple composed objects without using `[Shared]`
  on the type itself
- Register a custom naming convention for `Compono.Bogus`
- Verify an NSubstitute call received specific arguments after composing it
- Reproduce a `CreateMany<T>()` failure from a recorded seed
- Compose a required-member record type with one member overridden

Open a [Feature Request](https://github.com/LayeredCraft/compono/issues/new/choose)
issue (or comment on an existing one) before starting, so two people
don't write the same recipe at once.
