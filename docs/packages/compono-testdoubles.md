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
- **Known v1 limitation: first-registration-wins across assemblies.**
  `GeneratedTestDoubleRegistry` is `Type`-keyed. If two separately-compiled
  consumer assemblies loaded into the same process both discover a
  generated double for the *same* shared interface, whichever assembly's
  `[ModuleInitializer]` runs first wins the registration — the other
  assembly's `Configure()` bridge then throws a cast exception at runtime
  (its message names this scenario explicitly). If you hit this in a
  multi-project test host, it's this documented limitation, not a missing
  `using` or a generation failure — see
  [ADR-0043 Amendment 3 Finding C](../adr/0043-compono-generated-test-doubles-design.md#amendment-3-2026-08-13-public-cross-assembly-state-contract-overloadname-collision-diagnostics-documented-multi-assembly-registry-limitation).
- **Per-member `.Returns(...)`/`.Throws(...)`** — configure a method or
  property's behavior; last configuration wins (calling `.Returns(...)`
  after an earlier `.Throws(...)` on the same member clears the exception,
  and vice versa). Configuration is member-level and **argument-
  independent** — there are no argument matchers in v1.
- **Deterministic defaults for unconfigured members** — primitives,
  nullable references, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`, and
  known collection shapes (arrays, `List<T>`, `Dictionary<TKey,TValue>`,
  etc.) return their deterministic default rather than throwing. For
  `Task<T>`/`ValueTask<T>` this recurses into `T` — `Task<int>` defaults
  fine, but `Task<Customer>` (a non-nullable reference result) has no
  deterministic default for `T` and hits the same diagnostic as a bare
  non-nullable reference return, below. A non-nullable reference return
  (`string`, a non-nullable class) has no deterministic default and is a
  compile-time diagnostic instead — see below.
- **Combine with `[Shared]`** (`Compono.XunitV3`/`Compono.TUnit`) to
  configure the exact double instance wired into a composed system under
  test — see [Shared Values](../concepts/shared-values.md).
- **AOT-safe** — no runtime proxy generation, no reflection. Verified with
  a real `dotnet publish -p:PublishAot=true` execution, not just static
  analysis.

## Overloaded members

An interface declaring overloaded members is no longer an all-or-nothing
rejection (v2, [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)):
each overload gets its **own** `Configure()`/`Verify()` surface, disambiguated
by ordinary C# overload resolution — the generated configuration extension
for an overloaded member takes the same real parameter types the interface
overload declares (the values themselves are discarded, exactly like the
non-overloaded, zero-argument case):

```csharp
public interface IResponseBuilder
{
    void Speak(string? text);
    void Speak(params ISsml[] parts);
}

builder.Configure().Speak("hello");                 // configures the string? overload
builder.Configure().Speak(new ISsml[] { ssml });     // configures the params overload
```

Two edge cases stay narrower than full per-overload support:

- **A diamond collision** — the exact same signature independently declared
  by two different base interfaces — can't be disambiguated at all (both
  identities are structurally identical). That one identity gets no
  `Configure()`/`Verify()` surface (an informational `CMP0022`), but every
  other member of the interface, including any other overload sharing the
  same name, is unaffected.
- **A `ref`/`out`/`in` parameter** on one overload falls back to a
  deterministic-default dispatch body with no configuration surface for
  *that* overload (an informational `CMP0026`) — its sibling overloads keep
  their own surface unaffected. A return type (or `out` parameter) with no
  deterministic default still has no constructible body at any granularity
  and rejects the whole interface, same as the non-overloaded case.

## Precedence with `Compono.NSubstitute`

If both packages are installed and both providers registered, registration
order decides which one resolves an interface request first — register
`UseGeneratedTestDoubles()` before `UseNSubstitute()` if you want the
generated double to take precedence for interfaces it covers, matching
[ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
"tried in registration order" provider contract. Neither package special-
cases the other.

## What it deliberately doesn't do

Still no call recording, no verification (`Received()`-style assertions),
no argument matchers, and no support for classes, delegates, indexers,
events, generic methods (tracked, not yet shipped —
[PLAN-0044](../plans/0044-compono-testdoubles-v2.md) Phase 1), or static
abstract members — see
[ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
Non-Goals for the full scope boundary. Overloaded members and a
`ref`/`out`/`in` parameter's own overload are now supported per-overload
(see above, [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)).
An unsupported member shape is a compile-time diagnostic
(`CMP0020`-`CMP0028`), not a silent gap.

## Next

- [Shared Values](../concepts/shared-values.md) — asserting against a
  configured generated double.
- [Providers](../concepts/providers.md) — where the generated-double
  provider sits in the resolution pipeline.
- [`Compono.NSubstitute`](compono-nsubstitute.md) — the runtime-proxy
  alternative, for call verification/argument matchers.
