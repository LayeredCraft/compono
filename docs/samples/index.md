# Samples

Complete, runnable applications demonstrating multiple Compono concepts
working together in realistic architecture — distinct from
[Cookbook](../cookbook/index.md)'s single-problem, copy/paste recipes.
Each sample below is real, buildable code living under this repository's
top-level `samples/` directory (sibling to `src/`/`test/`), built and
tested on every CI push like any other project — the pages here are a
short overview linking out to it, not the code itself.

## Launch samples

| Sample | Demonstrates | Packages |
|---|---|---|
| [Basic Usage](basic-usage.md) | The core workflow: `Composer.Create()`/`Create<T>()`/`CreateMany<T>()`, a reusable profile, registrations and member rules, a composed xUnit v3 theory, deterministic seed reproduction. | `Compono`, `Compono.XunitV3` |
| [ASP.NET API](aspnet-api.md) | The full ecosystem in one realistic API: a `[Shared]` NSubstitute substitute injected into the system under test, `Compono.Bogus`-generated request data, inline plus composed theory values, and one integration-style endpoint test. | `Compono`, `Compono.XunitV3`, `Compono.NSubstitute`, `Compono.Bogus` |

Both samples restore against `Compono`/`Compono.XunitV3`/`Compono.NSubstitute`/
`Compono.Bogus` via a `ProjectReference`, matching every other project in
this repository — the same package-readiness CI gates that already verify
the four publishable packages against a real packed consumer (Phase 0's
`Compono.XunitV3.SampleTests`) cover the packed-artifact risk without
needing every downstream project, including these two samples, to
duplicate that verification itself. See
[ADR-0033](../adr/0033-public-preview-samples-strategy.md) for why these
two, specifically, are the launch set.

## Future candidates

Five architecture-pattern samples were considered for this launch and
deliberately deferred, not dropped — each would be predominantly
architecture-pattern scaffolding (CQRS, Clean Architecture, Minimal APIs,
MediatR, EF Core) with Compono usage as a minority of the code, multiplying
CI/maintenance surface for marginal additional proof of Compono itself
(see ADR-0033's Decision Outcome). A future candidate graduates to a real
sample only once it would demonstrate a materially different Compono
pattern the two launch samples don't already cover — not merely a
different host framework:

- **CQRS** — command/query separation with composed command and query
  handlers.
- **Clean Architecture** — Compono usage across explicit
  domain/application/infrastructure layer boundaries.
- **Minimal APIs** — a second ASP.NET Core hosting style, distinct from
  the launch ASP.NET API sample only if it surfaces a Compono pattern the
  launch sample doesn't (otherwise redundant with it).
- **MediatR** — composed requests/handlers flowing through a mediator
  pipeline.
- **EF Core** — composed entities against a real (or in-memory) EF Core
  `DbContext`.

## Next

- Never used Compono before? Start with [Getting Started](../getting-started/index.md)
  first — both samples assume you've already seen
  [Your First Composed Theory](../getting-started/first-test.md).
- Want the mental model behind what a sample does, not just that it works? →
  [Concepts](../concepts/index.md).
- Looking for one narrow, copy/paste answer instead of a full application? →
  [Cookbook](../cookbook/index.md).
