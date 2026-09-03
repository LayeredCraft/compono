# Compono.XunitV3.SampleTests

**This is a packaged-consumer/real-runner validation fixture, not a
user-facing sample.** Despite the `SampleTests` name, this project's job is
to prove `Compono`/`Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`
work correctly when consumed exactly as an external package consumer would —
via `PackageReference` against a freshly-packed local feed (see this
project's own `PackToLocalFeed` MSBuild target), never a `ProjectReference`.
If you're looking for a user-facing example of how to use Compono, see
`samples/` at the repository root instead (ADR-0033).

## Running this project directly

Two of this project's test classes — `FailingCompositionTests` and
`FailingConfigProfileTests` — **fail by design**, on every run. They exist
to prove a real composition/binder failure's message (including its
reproducing seed) actually reaches a real xUnit v3 runner's output, through
the real packaged pipeline — not just an in-process `GetData()` call
(ADR-0022's Testing Strategy).

A bare `dotnet test test/Compono.XunitV3.SampleTests/Compono.XunitV3.SampleTests.csproj`
will therefore report those two classes as failing. **That is expected, not
a broken project.** The correct invocation — the one CI actually runs
(`.github/workflows/package-validation.yaml`'s "Local-feed packed-consumer
smoke test" step) — excludes them:

```bash
dotnet test test/Compono.XunitV3.SampleTests/Compono.XunitV3.SampleTests.csproj \
  -c Release \
  -- --filter-not-class "Compono.XunitV3.SampleTests.Failing*"
```

The wildcard is trailing-only (`Failing*`, not `Failing*Tests`) — the
Microsoft Testing Platform CLI rejects a wildcard placed in the middle of a
filter expression.

This project is intentionally excluded from `Compono.slnx` for the same
reason (a solution-wide `dotnet test` would hit the same two
deliberately-failing classes) — see
`docs/plans/0004-milestone-4-xunit-integration.md`.
