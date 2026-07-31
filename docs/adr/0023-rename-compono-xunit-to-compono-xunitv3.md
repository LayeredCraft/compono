# [ADR-0023] Rename `Compono.Xunit` to `Compono.XunitV3`

**Status:** Accepted

**Date:** 2026-07-31

**Decision Makers:** solo (confirmed with user)

## Context

`Compono.Xunit` (ADR-0021, ADR-0022) integrates Compono with **xUnit v3**
specifically — it derives from `Xunit.v3.DataAttribute`, reflected and
confirmed directly against the `xunit.v3.extensibility.core` 3.2.2
assembly, per Milestone 4's explicit "do not assume xUnit v2 behavior
applies" instruction. Nothing about the package works with xUnit v2 (a
different attribute-extensibility model entirely), and there is no
concrete plan to add v2 support.

The name `Compono.Xunit`, however, reads as version-agnostic — the same
naming shape as `Compono.NSubstitute`/`Compono.Bogus`, neither of which
have a version qualifier because neither integration is version-specific
the way this one is. A consumer skimming NuGet or this repo's package
list has no way to tell, from the name alone, that this package is
xUnit-v3-only and won't work against an xUnit v2 test project.

## Decision Drivers

- Avoid consumer confusion about which xUnit major version this package
  targets — the name should say so, not leave it to be discovered at
  restore/compile time.
- If xUnit v2 support is ever added, it needs its own distinctly-named
  package (`Compono.Xunit` or `Compono.XunitV2`) without a breaking rename
  of the v3 package out from under existing consumers.
- Minimize churn: this is a rename, not a redesign — every technical
  decision ADR-0021/ADR-0022 recorded (the binding algorithm, `[Shared]`
  semantics, seed policy, package boundaries) is unchanged and stays
  exactly as decided; only the package/project/namespace identifier
  changes.

## Considered Options

1. **Rename now, to `Compono.XunitV3`** (chosen) — while the package is
   still pre-1.0 and has no external consumers yet, a rename costs
   nothing beyond the mechanical change itself.
2. **Keep `Compono.Xunit`, document the v3-only scope prominently** —
   rejected: documentation is discoverable only after a consumer already
   has the package; the name itself is the artifact most likely to be
   seen first (NuGet search, an IDE's package manager), and getting it
   wrong there isn't fixed by better docs elsewhere.
3. **Defer the rename until xUnit v2 support is actually designed** —
   rejected: renaming later, once real consumers exist, is a breaking
   change requiring a deprecation/migration story this package doesn't
   need yet. Renaming now, before Milestone 4 ships and before any
   consumer takes a dependency, is strictly cheaper.

## Decision Outcome

**Chosen option: rename now.** `Compono.Xunit` → `Compono.XunitV3`,
mechanically, everywhere:

- `src/Compono.Xunit/` → `src/Compono.XunitV3/`; `Compono.Xunit.csproj` →
  `Compono.XunitV3.csproj`.
- `test/Compono.Xunit.Tests/` → `test/Compono.XunitV3.Tests/`;
  `Compono.Xunit.Tests.csproj` → `Compono.XunitV3.Tests.csproj`.
- Namespace `Compono.Xunit` → `Compono.XunitV3` (and
  `Compono.Xunit.Binding`/`Compono.Xunit.Tests.*` → `Compono.XunitV3.Binding`/
  `Compono.XunitV3.Tests.*`) throughout both projects' source.
- `PackageId`/`AssemblyName` follow the project name by MSBuild default
  (neither csproj overrides them), so the rename alone produces the new
  package identity — no separate property change needed.
- `InternalsVisibleTo` targets (`Compono.Xunit.Tests` →
  `Compono.XunitV3.Tests`) updated to match.
- `Compono.Generators`' `ComposeMethodDiscovery` metadata-name constants
  (`AttributeMetadataName`/`GenericAttributeMetadataName`, currently
  `"Compono.Xunit.ComposeAttribute"`/`"Compono.Xunit.ComposeAttribute\`1"`)
  updated to the new namespace — these are matched by
  `ForAttributeWithMetadataName` against the *compiled* attribute's fully
  qualified name, so this is a functional change, not just a comment
  update; a stale metadata name here would silently break discovery for
  `[Compose]`-attributed test methods, not fail loudly.
- `test/Compono.Generators.Tests`' same-metadata-name stand-in attribute
  source (used because that test project doesn't reference the real
  `Compono.XunitV3` assembly - `testing.md`'s established pattern) updated
  to declare its stand-in types under `Compono.XunitV3` too, so it keeps
  matching the generator's updated constant.
- Every **current-state** doc (`docs/mvp.md`, `docs/architecture.md`,
  `docs/index.md`, `README.md`, `AGENTS.md`) updated to the new name.
- `docs/adr/0008`, `docs/adr/0021`, `docs/adr/0022` are **not** edited —
  each is an immutable historical record of a decision made when the
  package was named `Compono.Xunit`; per `design-decisions.md`, an
  accepted ADR's own text doesn't change to track a later rename. Their
  prose still correctly describes the technical decisions those ADRs made
  - only the identifier those decisions attached to has since changed,
  and this ADR is that record.

### Positive Consequences

- The package name itself communicates xUnit-v3-only scope, without
  relying on a consumer reading documentation first.
- A future xUnit v2 integration (if ever built) gets a clean, separately-
  versioned name with no migration story needed for existing
  `Compono.XunitV3` consumers.
- Done before any real consumer exists, so this is a zero-cost rename, not
  a breaking change.

### Negative Consequences

- Every ADR-0021/ADR-0022 cross-reference to `Compono.Xunit` in this
  repo's docs now reads as a historical name that no longer matches the
  current package - mitigated by this ADR itself being the explicit,
  linkable record of the rename, so a reader hitting the old name in
  ADR-0021/0022 has somewhere to be pointed.

## Links

- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md) —
  the core-side extension point this package (under its new name) still
  depends on, unchanged.
- [ADR-0022](0022-compono-xunit-package-design.md) — the package's full
  technical design (attribute surface, binding algorithm, seed policy),
  unchanged; only the name recorded there is superseded by this ADR.
