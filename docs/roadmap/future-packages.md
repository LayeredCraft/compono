# Future Packages

Compono's MVP package set is fully shipped as of this milestone (see
[Package Guides](../packages/index.md)): four independently installable
packages — `Compono`, `Compono.XunitV3`, `Compono.NSubstitute`, and
`Compono.Bogus` — plus `Compono.Generators`, which is not a fifth
installable package at all. It's `IsPackable=false`
([ADR-0003](../adr/0003-generator-package-distribution.md)) and ships
embedded inside `Compono`'s own `.nupkg` as an analyzer
(`analyzers/dotnet/cs`) — a consumer never references it directly, and
it never appears on nuget.org on its own. No additional package is
currently committed via an `Accepted` ADR.

## Natural candidates, not yet designed

The core `Compono` package is deliberately free of any dependency on a
specific test framework or test-double/data library
([Design Principles](../architecture/design-principles.md)'s "modular
architecture" principle) — each of those integrations lives in its own
package, built on the same public extension points
(`AddSemanticProvider`/`AddTestDoubleProvider`, per
[ADR-0024](../adr/0024-public-provider-extensibility-model.md)) that
`Compono.NSubstitute`/`Compono.Bogus` already use. That pattern makes the
natural shape of a future package predictable, even though none of the
following is designed or committed yet:

- Additional test-framework integrations (e.g. NUnit, MSTest), following
  `Compono.XunitV3`'s `CompositionRow`-based model
  ([ADR-0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md)).
- Additional test-double integrations (e.g. Moq, FakeItEasy), following
  `Compono.NSubstitute`'s stage-6 provider model
  ([ADR-0025](../adr/0025-compono-nsubstitute-package-design.md)).
- A richer `Microsoft.Extensions.DependencyInjection` integration
  (`IServiceCollection` auto-registration, per-composition scoping, keyed
  services) on top of `UseServiceProvider(...)`'s existing BCL-only
  `IServiceProvider` support — explicitly out of scope for core per
  [ADR-0019](../adr/0019-registrations-and-service-provider-injection.md).
- A reflection-based compatibility mode or package, for the still-open
  runtime-reflection question tracked in
  [Source Generation](../architecture/current/source-generation.md).

Any of these becomes real roadmap content the moment real demand and a
concrete design exist — see [Post-MVP](post-mvp.md) for the
evidence-backed process that promotes a candidate out of this page and
into an ADR, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md).
A future package gets its own [Package Guide](../packages/index.md) entry
the moment it ships.
