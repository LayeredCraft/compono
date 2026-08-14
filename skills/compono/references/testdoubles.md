# Compono.TestDoubles

Only relevant if the project references `Compono.TestDoubles` **and** sets
`<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>` in its own
`.csproj`. Both gates are required — the package alone does nothing;
`UseGeneratedTestDoubles()` without the compile-time property has no
generated doubles to register. Never suggest either half alone.

```csharp
var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());

var service = composer.Create<OrderService>();
service.Repository.Configure().CountAsync().Returns(Task.FromResult(4));
```

- `GeneratedTestDoubleProvider` runs at the test-double provider stage,
  same as `NSubstituteProvider`. It resolves a requested interface type to
  a **generated** double only if `Compono.Generators` actually emitted one
  for that interface at compile time. For an interface the compile-time
  opt-in never reached (project doesn't set
  `ComponoGeneratedTestDoubles=true`, or the interface was never requested
  anywhere the generator could discover it), `TryProvide` returns
  `NotHandled` — the pipeline moves on to the next registered provider
  (e.g. `NSubstituteProvider`, if also registered) exactly as it would if
  this provider weren't installed at all. It's only a genuine composition
  failure if no other provider claims the request either.
- **`Configure()`** — a generator-emitted extension bridge
  (`this IRepository`), reachable from **any namespace with no `using`
  needed** — every generated type lives in the global namespace by design.
  Don't add an import "just in case"; if `Configure()` doesn't resolve, the
  interface likely never got a generated double at all (check the
  compile-time opt-in is set and the interface is actually reached by
  something the generator's discovery walk covers — a
  `composer.Create<T>()`/`CreateMany<T>()` call site, a `[Compose]` theory/
  test method parameter, or a `[Composable]` declaration all feed the same
  closure walk).
- **`.Returns(...)`/`.Throws(...)`** per member. Argument-independent —
  there is no `Arg.Any<T>()`/argument-matcher equivalent; configuration
  applies to every call to that member regardless of arguments. Last
  configuration wins: calling `.Returns(...)` after an earlier
  `.Throws(...)` on the same member clears the exception (and vice versa).
- **Full base-interface closure.** If `IRepository : IClock`, the generated
  double implements `IClock.UtcNow` too, configurable via
  `repository.Configure().UtcNow().Returns(...)` — not just `IRepository`'s
  own declared members.
- **Deterministic defaults** for any unconfigured member: primitives,
  nullable references, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`, and
  known collection shapes return their deterministic default (empty
  collections, never `null`). `Task<T>`/`ValueTask<T>` recurse into `T` —
  `Task<int>` is fine, but `Task<Customer>` (a non-nullable reference `T`)
  has no deterministic default for its result and hits the same diagnostic
  as a bare non-nullable reference return. A member with **no**
  deterministic default — a non-nullable reference return (`string`, a
  non-nullable class), or a `Task<T>`/`ValueTask<T>` wrapping one — is a
  compile-time diagnostic instead; the generator never emits `null` for a
  non-nullable-annotated return.

## The #1 AutoFixture/NSubstitute-habit trap: not a general mocking framework

There is **no** call recording, **no** verification (`Received()`-style
assertions), and **no** argument matchers. If a test needs to assert a
method was called, or needs different return values for different
arguments, `Compono.TestDoubles` cannot do it — use
`Compono.NSubstitute`'s `UseNSubstitute()` for that interface instead (the
two providers can coexist; registration order decides which one resolves
first, see below). Don't try to work around the gap by polling state or
inventing a callback-shaped member on the interface just to observe a
call — that's fighting the framework, not using it.

## Unsupported shapes are compile-time diagnostics, not silent gaps

**Classes and delegates are not test-double candidates at all** —
`LeafTypeClassifier` only ever admits interfaces for generated-double
eligibility, so neither one is diagnosed here or falls back to this
package's provider; a concrete class still composes through ordinary
constructor selection, and a delegate leaf stays provider-resolved (a
runtime `CompositionException` if no provider handles it, not a `CMP002x`
diagnostic).

For an eligible **interface**, indexers, events, generic methods,
`ref`/`out`/`in` parameters, static abstract members, overloaded members,
and a handful of narrower shapes (set-only properties, pointer/function-
pointer parameters or returns, ref-like returns) are all diagnosed at
compile time (`CMP0020`-`CMP0028`, informational severity — they don't
fail the build) rather than emitted incorrectly or silently skipped: the
interface still falls back to the ordinary runtime-provider path, same as
any interface the compile-time opt-in never reached. See
`references/diagnostics.md` for the full code table before guessing a fix.

## Precedence with `Compono.NSubstitute`

```csharp
var composer = Composer.Create(builder => builder
    .UseGeneratedTestDoubles()
    .UseNSubstitute());
```

Both providers can be registered together. Registration order decides
which one resolves an interface request first — `UseGeneratedTestDoubles()`
registered before `UseNSubstitute()` means any interface the generator
emitted a double for resolves to the generated double; an interface that
never got a generated double falls through to `NSubstituteProvider`
(or to composition failure if neither provider claims it). This is the
same "tried in registration order" contract every provider already
follows — no special-cased precedence logic exists between these two
specifically.

## Combining with `[Shared]`

`Compono.XunitV3`:

```csharp
[Theory]
[Compose<GeneratedTestDoubleProfile>]
public async Task Saves_order([Shared] IRepository repository, OrderService service)
{
    repository.Configure().CountAsync().Returns(Task.FromResult(4));
    var order = await service.PlaceAsync(6);
    // repository is the exact double `service` was composed with
}
```

`Compono.TUnit` — same shape, `[Test]` instead of `[Theory]`:

```csharp
[Test]
[Compose<GeneratedTestDoubleProfile>]
public async Task Saves_order([Shared] IRepository repository, OrderService service)
{
    repository.Configure().CountAsync().Returns(Task.FromResult(4));
    var order = await service.PlaceAsync(6);
    // repository is the exact double `service` was composed with
}
```

`[Shared]` (in `Compono.XunitV3` or `Compono.TUnit`) is what lets you both
configure a double *and* have it wired into the composed system under
test — see `registrations-profiles-and-scopes.md`. Without `[Shared]`, a
double-typed parameter and a double nested inside another composed type
would be two different generated-double instances.
