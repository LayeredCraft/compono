# Compono.TestDoubles

A fallback, source-generated double for an otherwise-unresolvable
**interface** leaf in a composition graph — an AOT-safe alternative to
`Compono.NSubstitute`'s runtime-proxy dependency for the common case.

## When to install

You want `composer.Create<T>()` to satisfy an interface dependency with a
generated double, without pulling in `Compono.NSubstitute`'s runtime proxy
dependency (or when you need the composed path to survive `PublishAot`):

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.TestDoubles --prerelease
```

`Compono.TestDoubles` is not a general-purpose mocking framework — see
[ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
Non-Goals. If you need call verification, argument matchers, or a familiar
runtime-proxy substitute, use
[`Compono.NSubstitute`](compono-nsubstitute.md) instead; the two packages
are not mutually exclusive.

## Compile-time opt-in

Generation is gated behind an MSBuild property — set it in the consuming
project, not just referencing the package:

```xml
<PropertyGroup>
  <ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>
</PropertyGroup>
```

Without this, `Compono.Generators` never emits a double for any interface,
regardless of whether `Compono.TestDoubles` is referenced or
`UseGeneratedTestDoubles()` is called — the two gates (compile-time opt-in,
runtime provider registration) are independent and both required.

## What it gives you

```csharp
var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());

var service = composer.Create<OrderService>();
service.Repository.Configure().CountAsync().Returns(Task.FromResult(4));
```

- **`UseGeneratedTestDoubles()`** — registers the generated-double provider
  as a pipeline stage. Once active, any interface-typed request the
  compile-time opt-in generated a double for resolves to that double
  instead of failing composition.
- **A generated double per discovered interface** — `Compono.Generators`
  emits one `internal` double type per interface leaf, walking the
  interface's **full transitive base-interface closure**, not just its own
  declared members. A base-interface member (e.g. `IClock.UtcNow` inherited
  by `IRepository : IClock`) is implemented too.
- **`Configure()`** — a generator-emitted extension bridge
  (`this IRepository`) reachable with **no `using` needed**, regardless of
  which namespace the call site is in — every generated type lives in the
  global namespace specifically so this holds without an import.
- **Per-member `.Returns(...)`/`.Throws(...)`** — configure a method or
  property's behavior; last configuration wins (calling `.Returns(...)`
  after an earlier `.Throws(...)` on the same member clears the exception,
  and vice versa). Configuration is member-level and **argument-
  independent** — there are no argument matchers in v1.
- **Deterministic defaults for unconfigured members** — primitives,
  nullable references, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`, and
  known collection shapes (arrays, `List<T>`, `Dictionary<TKey,TValue>`,
  etc.) return their deterministic default rather than throwing. A
  non-nullable reference return (`string`, a non-nullable class) has no
  deterministic default and is a compile-time diagnostic instead — see
  below.
- **Combine with `[Shared]`** (`Compono.XunitV3`/`Compono.TUnit`) to
  configure the exact double instance wired into a composed system under
  test — see [Shared Values](../concepts/shared-values.md).
- **AOT-safe** — no runtime proxy generation, no reflection. Verified with
  a real `dotnet publish -p:PublishAot=true` execution, not just static
  analysis.

## Precedence with `Compono.NSubstitute`

If both packages are installed and both providers registered, registration
order decides which one resolves an interface request first — register
`UseGeneratedTestDoubles()` before `UseNSubstitute()` if you want the
generated double to take precedence for interfaces it covers, matching
[ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
"tried in registration order" provider contract. Neither package special-
cases the other.

## What it deliberately doesn't do

No call recording, no verification (`Received()`-style assertions), no
argument matchers, and no support for classes, delegates, indexers,
events, generic methods, `ref`/`out`/`in` parameters, or static abstract
members — see [ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
Non-Goals for the full v1 scope boundary. An unsupported member shape is a
compile-time diagnostic (`CMP0020`-`CMP0028`), not a silent gap.

## Next

- [Shared Values](../concepts/shared-values.md) — asserting against a
  configured generated double.
- [Providers](../concepts/providers.md) — where the generated-double
  provider sits in the resolution pipeline.
- [`Compono.NSubstitute`](compono-nsubstitute.md) — the runtime-proxy
  alternative, for call verification/argument matchers.
