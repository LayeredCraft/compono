# [PLAN-0060] Public Generator-Facing Runtime Infrastructure

**Status:** In Progress

**Implements:** [ADR-0058](../adr/0058-public-generator-facing-runtime-infrastructure.md)

## Goal

Apply ADR-0058's member-level IntelliSense policy consistently to every public
hook called only by Compono-emitted code, while preserving the public runtime
integration seams that framework packages call.

## Scope

Annotate the accepted inventory with `EditorBrowsableState.Never`, document the
rule, amend prior decisions that chose the old convention, and regenerate API
reference material. This plan does not change CLR accessibility, generated
source, or runtime behavior.

## Tasks

- [x] Add the policy to `coding-standards.md`.
- [x] Annotate the core cache, registry, and return-configuration hooks.
- [x] Annotate `LoggingFactoryRegistry.Register<TCategory>` only; retain its
      `TryCreate` integration seam as visible.
- [x] Update XML comments and append the ADR-0041 and ADR-0055 amendments.
- [x] Add reflection-based contract tests for the core and Logging inventories.
- [x] Regenerate API reference documentation.
- [ ] Run the full build/test suite and a packaged consumer compilation, then
      record the results and mark this plan Done.

## Critical Files

- `src/Compono/PlanCache.cs` and `src/Compono/CollectionPlanCache.cs` —
  generated-plan registration hooks.
- `src/Compono/RowInvokerRegistry.cs` and
  `src/Compono/GeneratedTestDoubleRegistry.cs` — generated registration hooks.
- `src/Compono/ReturnConfig.cs` and `src/Compono/ReturnConfigBuilder.cs` —
  generated test-double dispatch/configuration hooks.
- `src/Compono.Logging/LoggingFactoryRegistry.cs` — generated logging
  registration hook.
- `test/Compono.Tests/` and `test/Compono.Logging.Tests/` — policy contract
  coverage.

## Test Plan

Reflection tests assert that every direct generated-code hook is marked
`EditorBrowsableState.Never` and each named runtime-integration seam remains
undecorated. Rebuild the generated API reference, run `dotnet build` and
`dotnet test`, then compile a real consumer against freshly packed packages.

## Notes

PLAN-0060 replaces the originally assigned PLAN-0059 after `main` added the
independent Compono.NUnit plan with that number.

Validation on 2026-09-02:

- `dotnet build --no-restore` completed successfully (40 pre-existing xUnit
  analyzer warnings, no errors).
- The two new reflection contract tests passed when executed directly for
  `net10.0`.
- `dotnet test` was attempted with the repository's pinned SDK after adding
  its required target runtimes to a temporary SDK directory. The Microsoft
  Testing Platform exited with code 5 before executing the test assemblies;
  this is an environment/tooling issue, not a test failure. The full suite and
  packaged-consumer compilation remain required before marking the plan Done.
