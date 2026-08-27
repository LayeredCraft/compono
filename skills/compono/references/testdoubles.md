# Compono.TestDoubles

Only relevant if the project references `Compono.TestDoubles`, sets
`<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>` in its own
`.csproj`, **and** calls `UseGeneratedTestDoubles()` when building the
composer. All three are required — the compile-time property alone only
generates the doubles, without `UseGeneratedTestDoubles()` nothing
registers them into the pipeline; the package reference alone does
nothing without the property set. Never suggest any one or two of the
three alone.

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
- **`.Returns(...)`/`.Throws(...)`** per member configuration.
  Zero-argument `Configure().Member()` configures the member regardless of
  arguments. For eligible parameterized members, `Configure().Member(...)`
  accepts `Match<T>` arguments: literal values match by equality,
  `Match.Any<T>()` matches anything, and `Match.Is<T>(predicate)` matches
  by predicate.
- **`Verify()`** — parallel to and independent from `Configure()`, returning
  a verifier surface for `Never()`/`Once()`/`Exactly(n)`. Zero-argument
  `Verify().Member()` counts every call to that member. For eligible
  parameterized members, `Verify().Member(...)` uses the same `Match<T>`
  argument shape to perform argument-filtered verification.
- **Multi-entry argument-distinguished configuration** — for matching-
  eligible members, multiple `Configure().Member(...)` calls can coexist.
  Dispatch uses the most recently registered matching entry; precedence is
  registration order, not matcher-specificity ranking.
- **Full base-interface closure.** If `IRepository : IClock`, the generated
  double implements `IClock.UtcNow` too, configurable via
  `repository.Configure().UtcNow().Returns(...)` — not just `IRepository`'s
  own declared members.
- **Deterministic defaults** for any unconfigured member: primitives,
  nullable references, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`, and
  known collection shapes return their deterministic default (empty
  collections, never `null`). `Task<T>`/`ValueTask<T>` recurse into `T` —
  `Task<int>` is fine, but `Task<Customer>` (a non-nullable reference `T`)
  may require explicit configuration rather than a default. See
  "Configuration-required members" below.

## Argument matching and filtered verification

Do not conflate argument matching with argument capture. Current
`Compono.TestDoubles` supports ordinary matcher-based configuration and
verification for eligible members; it does not expose an arbitrary call log
or invocation callback API.

### NSubstitute migration mapping

When migrating from NSubstitute, do not introduce a hand-written recording
fake merely because the old test uses `Arg.Is`, `Arg.Any`, `Received`, or
`DidNotReceive`. For an eligible generated-double member, translate the
concepts directly:

| NSubstitute | Compono.TestDoubles |
|---|---|
| `Arg.Is<T>(predicate)` | `Match.Is<T>(predicate)` |
| `Arg.Any<T>()` | `Match.Any<T>()` |
| literal argument | literal argument (equality match) |
| `Received(1).Member(...)` | `Verify().Member(...).Once()` |
| `Received(n).Member(...)` | `Verify().Member(...).Exactly(n)` |
| `DidNotReceive().Member(...)` | `Verify().Member(...).Never()` |

Real AWS Secrets Manager Provider migration shapes:

```csharp
configurationBuilder.Verify()
    .Add(Match.Is<IConfigurationSource>(source => source is SecretsManagerConfigurationSource))
    .Once();
```

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(request => request.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

### Eligibility boundary

Argument-aware `Configure().Member(...)`/`Verify().Member(...)` is generated
for a member only when the member is eligible: it is not overloaded, its
real parameters do not reference the member's own open generic type
parameter, its real parameters are usable as generic type arguments, the
derived generated field names do not collide, and its generated extension
would not be hidden by an inherited `object` member. If a member is scoped
out of argument-awareness, keep using the existing argument-independent
surface (`Configure().Member()` / `Verify().Member()`) or choose another
test seam/provider when the test genuinely needs argument distinction.

Overloaded members keep the discriminator-only shape described below: their
arguments select the overload at compile time; they are not matchers.

## Multiple response configurations per member

For matching-eligible members, each `Configure().Member(...)` call appends a
new response configuration. Dispatch walks matching entries from newest to
oldest and uses the first match:

```csharp
repository.Configure()
    .Withdraw(Match.Any<string>(), Match.Any<decimal>(), Match.Any<bool>())
    .Returns(false);
repository.Configure()
    .Withdraw("acct-1", Match.Any<decimal>(), Match.Any<bool>())
    .Returns(true);
```

`Withdraw("acct-1", ...)` returns `true`; other accounts return `false`.
There is no matcher-specificity ranking — if two entries both match, the
one configured later wins.

This is still not sequential/call-count-based responses. There is no
"return X on the first call, Y on the second" API.

## Overloaded members (v2)

An overloaded interface member gets its own per-overload `Configure()`
surface instead of an all-or-nothing rejection (see
`docs/adr/0044-compono-testdoubles-v2-overloads-generics-verification.md`) —
the generated configuration extension for an overloaded member takes the
same real parameter types the interface overload declares, purely so
ordinary C# overload resolution picks the right one. The values themselves
are still discarded and are **not** argument matchers. `Verify()` reuses
this same per-overload surface - `Verify().Speak("hi")` selects the same
overload-specific counter `Configure().Speak("hi")` would:

```csharp
public interface IResponseBuilder
{
    void Speak(string? text);
    void Speak(params ISsml[] parts);
}

builder.Configure().Speak("hello").Throws(new InvalidOperationException());
builder.Configure().Speak(new ISsml[] { ssml }).Throws(new InvalidOperationException());
```

`.Speak(...)` alone only selects an overload's configuration handle -
nothing is configured on the double until `.Returns(...)`/`.Throws(...)`
is chained, same as any non-overloaded `Configure()` call.

Two things still don't get a surface: a **diamond collision** (the exact
same signature independently declared by two different base interfaces —
nothing to disambiguate) and a `ref`/`out`/`in` parameter's own overload
(falls back to a deterministic default, informational `CMP0030`) — in both
cases only that one identity loses its surface, every other member and
overload of the interface is unaffected.

A base interface's abstract declaration resolved by a more-derived
interface's own concrete (default-interface-member) redeclaration via `new`
is **not** a diamond collision (ADR-0044 Amendment 20) - the dominant
(derived) declaration gets a real `Configure()`/`Verify()` surface, and its
unconfigured fallback runs the interface's own real body instead of a
computed default; the losing (base) declaration purely forwards to it, so
both interface views share one call-recording state. See
`docs/packages/compono-testdoubles.md`'s "Default interface members" section
for the full example.

## Generic methods (v2)

A generic method is supported when its return type doesn't reference its
own type parameter (Requirement 2) - `ILogger<T>`'s `Log<TState>`/
`BeginScope<TState>` is the motivating shape. The explicit implementation
stays generic (type parameters copied, constraints left unstated - they're
inherited automatically and redeclaring them is `CS0460`); the
`Configure()` extension itself stays **non-generic** for a solo generic
member - one backing slot covers every closed instantiation:

```csharp
public interface ILoggerLike
{
    void Log<TState>(int logLevel, TState state, Exception? exception);
    IDisposable? BeginScope<TState>(TState state) where TState : notnull;
}

logger.Configure().Log().Throws(new InvalidOperationException());
logger.Configure().BeginScope().Returns(myScope);
```

**Overloaded and generic together** (Amendment 1): the configuration
extension becomes generic too, purely for compile-time overload selection
- the backing slot still doesn't vary per closed type. This extension
*does* carry its constraint clauses verbatim (it's an ordinary standalone
generic method, not an interface implementation). An explicit type
argument is needed at the call site whenever ordinary overload-resolution
betterness rules wouldn't otherwise pick that overload (same as a real
call to the interface member itself).

A generic method whose return type depends directly on its own type
parameter (`T Get<T>()`, `Task<T> GetAsync<T>()`, and supported nullable
variants) can have per-closed-`T` configuration. Argument matching is
available for that shape only when the member's parameters are otherwise
matching-eligible and do not use the method's own open type parameter.

Still unsupported: unsupported generic-return shapes beyond the documented
per-closed-`T` cases, value-type-constrained `T?` (`System.Nullable<T>`),
and parameter shapes the generator cannot represent without reflection or
boxing. See `diagnostics.md` before guessing a workaround.

## Call verification (v2+)

`Verify()` — parallel to and independent from `Configure()`, returning a
distinct wrapper so the two never collide — asserts how many times a member
was actually called. `Never()`/`Once()`/`Exactly(n)` are the terminal
count assertions. For argument-aware members, filtering happens in the
generated `Verify().Member(...)` extension before the terminal
`CallVerifier` is returned:

```csharp
repository.Configure().CountAsync().Returns(Task.FromResult(5));
var order = await service.PlaceAsync(3);
repository.Verify().CountAsync().Once();
repository.Verify().Save().Once();
```

```csharp
repository.Verify()
    .Save(Match.Is<Order>(order => order.Id == expectedId))
    .Once();
```

A failing assertion throws `Compono.TestDoubleVerificationException` (a
plain exception, not a framework assertion type). A call counts whether it
hits configured, default, or thrown behavior.

## Configuration-required members (v2)

A member returning a non-nullable reference type (or `Task<T>`/
`ValueTask<T>` wrapping one) with no deterministic default used to reject
the *whole interface* (v1's `CMP0025`). As of v2
(`docs/adr/0045-testdoubles-configuration-required-members.md`), that
member instead generates as **configuration-required**, provided it would
otherwise have a real `Configure()`/`Verify()` surface — it throws
`Compono.TestDoubleNotConfiguredException` if invoked before
`Configure().Member(...).Returns(...)`/`.Throws(...)`, rather than falling
back to a computed default:

```csharp
context.Configure().AwsRequestId().Returns("test-request-id");
```

**Migration implication, not just a new feature:** when migrating a test
off `Compono.NSubstitute`, "the interface now generates" is no longer
proof every member is safe to call unconfigured — some members that used
to block the whole interface now generate *and* require explicit setup
before use. Check the generator's `CMP0032` diagnostic (one per interface,
a count) to know how many members on a given interface need
`Configure(...)` before the test exercises them; don't assume every
generated member has a usable default just because generation succeeded.
This applies identically to sync/async/property members and to a fluent
self-returning member (`IResponseBuilder`-shaped) — none of those get
special-cased, all follow the same rule.

## The #1 AutoFixture/NSubstitute-habit trap: matching is not capture

`Compono.TestDoubles` is not a general-purpose mocking framework, but it
does support argument matching and argument-filtered verification for the
eligible member shapes above. The remaining boundary is stronger behavior
that needs access to the actual invocation as a first-class value:

- true argument capture for later arbitrary inspection outside a generated
  `Verify().Member(Match...)` count assertion;
- invocation-aware callback responses (`Returns(call => ...)`,
  `Returns(Func<CallInfo, T>)`, or "invoke this delegate argument and use
  its result");
- callback side effects based on the actual invocation;
- call-order verification;
- sequential/call-count-based responses;
- strict mode, partial substitutes, recursive auto-configuration;
- classes, delegates, indexers, events, and other unsupported shapes listed
  below.

If a test only needs "this member was called once with an argument matching
this predicate," use `Verify().Member(Match.Is<T>(...)).Once()`. If it
needs to store every argument for arbitrary later inspection, run code from
a callback, or invoke a delegate argument, that is a different capability;
use an existing project-local fake or `Compono.NSubstitute` where the
project intentionally keeps that dependency, and treat any real
`Compono.NSubstitute`-can/`Compono.TestDoubles`-cannot case as roadmap
evidence under ADR-0042 Amendment 2.

## Unsupported shapes are compile-time diagnostics, not silent gaps

**Classes and delegates are not test-double candidates at all** —
`LeafTypeClassifier` only ever admits interfaces for generated-double
eligibility, so neither one is diagnosed here or falls back to this
package's provider; a concrete class still composes through ordinary
constructor selection, and a delegate leaf stays provider-resolved (a
runtime `CompositionException` if no provider handles it, not a `CMP002x`
diagnostic).

For an eligible **interface**, indexers, events, a genuinely unimplemented
static abstract member, unsupported generic-method return/parameter shapes,
and a handful of narrower shapes (set-only properties,
pointer/function-pointer parameters or returns, ref-like returns) still
reject the **whole interface** at compile time (`CMP0020`-`CMP0031`,
informational severity — they don't fail the build): it falls back to the
ordinary runtime-provider path, same as any interface the compile-time
opt-in never reached. Overloaded members, a `ref`/`out`/`in` parameter,
and a generic method independent of its own type parameter are narrower now
(see above) — only the specific colliding/unsupported overload loses its
surface, not the whole interface. A non-nullable-reference return with no
deterministic default no longer rejects the whole interface either (v2,
see "Configuration-required members" above) — unless it also lacks a
`Configure()` surface for one of those other reasons, in which case it
still does. See `diagnostics.md` for the full code table before guessing a
fix.

A static abstract member declared on a base interface but already resolved
by a more-derived interface's own concrete implementation (C#'s "most
specific implementation" rule — the `IAmazonS3`/`IAmazonService` shape) is
**not** a genuinely unimplemented member at all and doesn't reject anything;
only a static abstract member with no override anywhere in the interface's
hierarchy still whole-interface-rejects (ADR-0046).

## Precedence with `Compono.NSubstitute`

```csharp
var composer = Composer.Create(builder => builder
    .UseGeneratedTestDoubles()
    .UseNSubstitute());
```

Both providers can be registered together. Registration order decides which
one resolves an interface request first — `UseGeneratedTestDoubles()`
registered before `UseNSubstitute()` means any interface the generator
emitted a double for resolves to the generated double; an interface that
never got a generated double falls through to `NSubstituteProvider` (or to
composition failure if neither provider claims it). This is the same "tried
in registration order" contract every provider already follows — no
special-cased precedence logic exists between these two specifically.

## Combining with `[Shared]`

`Compono.XunitV3`:

```csharp
[Theory]
[Compose<GeneratedTestDoubleProfile>]
public async Task Saves_order([Shared] IRepository repository, OrderService service)
{
    repository.Configure().CountAsync().Returns(Task.FromResult(4));
    var order = await service.PlaceAsync(6);
    repository.Verify().Save(Match.Is<Order>(saved => saved.Id == order.Id)).Once();
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
    repository.Verify().Save(Match.Is<Order>(saved => saved.Id == order.Id)).Once();
}
```

`[Shared]` (in `Compono.XunitV3` or `Compono.TUnit`) is what lets you both
configure/verify a double *and* have it wired into the composed system under
test — see `registrations-profiles-and-scopes.md`. Without `[Shared]`, a
double-typed parameter and a double nested inside another composed type
would be two different generated-double instances.
