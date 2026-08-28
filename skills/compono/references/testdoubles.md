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
   Configuration is selected by the interface-typed receiver, so generated
   `Configure()`/`Verify()` bridges for different interfaces do not require
   aliases or special imports. Keep the receiver typed as its interface;
   casting it to `object` removes the extension-method surface.
- **`.Returns(...)`/`.Throws(...)`** per member. Each `Configure()` call
  **appends** an independent response configuration instead of overwriting
  the prior one, and dispatch picks the most recently registered entry
  whose matchers all match — see "Multiple response configurations per
  member" below. Within one entry, calling `.Returns(...)` after
  `.Throws(...)` clears its exception (and vice versa). Across entries,
  last matching registration wins, but an earlier entry remains reachable
  when a later entry's matchers don't cover a call.
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

## Overloaded members (v2)

An overloaded interface member now gets its own per-overload `Configure()`
surface instead of an all-or-nothing rejection (see
`docs/adr/0044-compono-testdoubles-v2-overloads-generics-verification.md`) —
the generated configuration extension for an overloaded member takes the
same real parameter types the interface overload declares, purely so
ordinary C# overload resolution picks the right one (the values themselves
are still discarded, same as the non-overloaded, zero-argument case).
`Verify()` reuses this same per-overload surface - `Verify().Speak("hi")`
selects the same overload-specific counter `Configure().Speak("hi")`
would:

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

**Still unsupported:** a generic method whose return type depends on its
own type parameter (`T Get<T>()`) - no constructible fallback body, whole
interface falls back (`CMP0031`). **Any** type parameter used as `T?` in a
parameter is diagnosed and excluded too (`CMP0026`) - constrained or
unconstrained, regardless of which constraint; correctly modeling exactly
when (and with which keyword) a constraint restatement is required isn't
attempted.

## Call verification (v2)

`Verify()` — parallel to and independent from `Configure()`, returning a
distinct wrapper so the two never collide — asserts how many times a
member was actually called
(`docs/adr/0044-compono-testdoubles-v2-overloads-generics-verification.md`
Requirement 3). `Never()`/`Once()`/`Exactly(n)` only, reusing the same
per-overload discriminator `Configure()` does. For an eligible member
(see "Argument matching and argument-filtered verification" below),
`Verify()` takes `Match<T>` per parameter and counts only calls whose
arguments satisfy every matcher; for an ineligible member (or when you
omit the matchers), `Verify()` stays argument-independent, exactly as
`Configure()` does:

```csharp
repository.Configure().CountAsync().Returns(Task.FromResult(5));
var order = await service.PlaceAsync(3);
repository.Verify().CountAsync().Once();
repository.Verify().Save().Once();
repository.Verify().UtcNow().Never(); // never read in this call path
```

A failing assertion throws `Compono.TestDoubleVerificationException` (a
plain exception, not a framework assertion type). A call counts whether it
hits configured, default, or thrown behavior.

**Still deliberately minimal** — `Never`/`Once`/`Exactly(n)` only, no
`AtLeast`/`AtMost`, no `ReceivedCalls()`-style enumeration, no call-order
verification. If a test needs anything this page doesn't cover (call-order
verification, an overloaded member's own argument matching,
`ReturnsForAnyArgs`, etc.), use `Compono.NSubstitute` for that interface
instead — the two providers can coexist (see "Precedence with
`Compono.NSubstitute`" below).

## Argument matching and argument-filtered verification (v3)

For a member that is the only overload of its name in the interface, has
no real parameter referencing the member's own open generic type
parameter, has no real parameter of a ref-like type (`Span<T>` and
similar can't be a generic type argument), has no derived internal field
name colliding with another member's, and isn't a one-parameter `Equals`
(its extension would share arity with the inherited `object.Equals(object)`
and never actually be reachable) — five conditions, all required
(`docs/adr/0048-testdoubles-argument-matching-and-call-verification.md`
and its Amendment 1) — `Configure()`/`Verify()` accept `Compono.Match<T>`
per parameter instead of just the return value: a literal (equality
match), `Match.Any<T>()` (matches anything, same as omitting a matcher),
or `Match.Is<T>(predicate)`:

```csharp
repository.Configure()
    .Withdraw("acct-1", Match.Any<decimal>(), Match.Is<bool>(allowed => allowed))
    .Returns(true);

repository.Withdraw("acct-1", 50m, overdraftAllowed: true);  // true — every matcher satisfied
repository.Withdraw("acct-2", 50m, overdraftAllowed: true);  // falls through — accountId doesn't match

repository.Verify()
    .Withdraw(Match.Is<string>(id => id == "acct-1"), Match.Any<decimal>(), Match.Any<bool>())
    .Once();
```

An eligible member also keeps its original zero-argument `Configure()`/
`Verify()` spelling (`repository.Configure().Withdraw().Returns(...)`,
argument-independent, exactly v1/v2's shape) — the two aren't mutually
exclusive, and a member with no real parameters only ever had the
zero-argument form to begin with. A call whose arguments don't satisfy a
configured matcher is treated identically to an unconfigured member
(falls through to a computed default, or to "Configuration-required
members"'s throwing behavior below) — not a distinct failure mode.

**Why this doesn't apply to an overloaded member.** A real compiler spike
(ADR-0048's Decision Outcome) proved that wrapping every overload's
parameters in a matcher type breaks C#'s own overload resolution
unpredictably for several realistic parameter-type families (base/derived
class hierarchies, `string[]` vs. `IEnumerable<string>`, even plain `int`
vs. `long` widening) — there's no reliable per-family fix, so argument
matching is scoped out entirely for any member with more than one
overload. An overloaded member's `Configure()`/`Verify()` stay exactly
the per-overload discriminator shape above, unchanged. The same reasoning
excludes a generic method whose real parameters reference its own type
parameter (an `ILogger<TState>.Log<TState>`-shaped member) — a per-member
call log can't hold an open type parameter's value, so that shape keeps
its existing argument-independent `Configure()`/`Verify()` too, exactly
as it already worked.

**Why `Match<T>`, not `Arg<T>`.** `Compono.Arg` would collide with
`NSubstitute.Arg` for any consumer whose own namespace nests under
`Compono` (this repo's own samples convention) or who combines `Compono`
with `Compono.NSubstitute` directly — confirmed with a real failing build
during this feature's implementation, not a theoretical concern. `Match`
avoids the collision entirely and names the actual Compono concept
(matching an argument), rather than borrowing NSubstitute's own
vocabulary.

## Multiple response configurations per member (v3)

An eligible member (see above) — or a closed-instantiation-eligible
member (a generic method whose return type *is* its own sole type
parameter, or the sole type argument of `Task<T>`/`ValueTask<T>` including
the `T?` forms when `T : class`; see
`docs/packages/compono-testdoubles.md`'s "Per-closed-instantiation
configuration" section) — isn't limited to one `Configure()` call. Each
call **appends** a new, independent response configuration instead of
overwriting the previous one — a broad default and one or more narrower,
argument-distinguished overrides can coexist on the same member in the
same test:

```csharp
repository.Configure()
    .Withdraw(Match.Any<string>(), Match.Any<decimal>(), Match.Any<bool>())
    .Returns(false);
repository.Configure()
    .Withdraw("acct-1", Match.Any<decimal>(), Match.Any<bool>())
    .Returns(true);

repository.Withdraw("acct-1", 50m, overdraftAllowed: true);  // true — the more specific entry
repository.Withdraw("acct-9", 50m, overdraftAllowed: true);  // false — falls through to the default entry
```

**Precedence: last matching registration wins.** A call dispatches to the
*most recently registered* `Configure()` entry whose matchers all match —
registration order, not matcher "specificity", decides which entry wins
when more than one entry could match the same call. There's no comparison
between matchers (a `Match.Is<T>(predicate)` entry is never treated as
"more specific" than a `Match.Any<T>()` entry, for example) — if two
entries could both match a call, whichever was configured later wins,
full stop. This keeps dispatch simple and its outcome fully determined by
the order `Configure()` calls appear, with no ranking heuristic to reason
about.

**Compatibility note (pre-1.0).** Before this capability existed, a
second `Configure()` call on the same member *overwrote* the first —
observable as the second call always winning, since only one
configuration could exist at a time. That's now a special case of
"last matching registration wins": a second call still wins whenever it
could have won before (it's always the most recently registered, and an
argument-independent `Configure()` call always matches), so ordinary,
single- or sequential-override usage is unaffected. What changes is that
the *first* configuration is no longer discarded — it's still reachable
by any call the second configuration's matchers don't cover, rather than
falling through to the member's deterministic default. This is an
intentional pre-1.0 semantic correction, not a breaking change to guard
against: the previous overwrite behavior was never separately documented
as guaranteed, and every existing single-`Configure()`-call usage keeps
its exact same observable behavior.

**What this deliberately doesn't do.** No matcher-specificity ranking
(see above). No sequential/call-count-based responses (`Configure()`
doesn't support "return X on the first call, Y on the second"). No
`Returns(Func<...>)` callback responses. Verification (`Verify()`) is
completely unaffected — it stays a count over the member's shared call
log, independent of how many response configurations exist.

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

## What it deliberately doesn't do

Argument matching and argument-filtered verification exist now, but only
for a member satisfying all five eligibility conditions — see "Argument
matching and argument-filtered verification" above. Multiple response
configurations per member are supported for those same eligible members
(and their closed-instantiation-eligible counterparts) — see "Multiple
response configurations per member" above — but strictly
last-matching-registration-wins, with no matcher-specificity ranking.

Still unsupported: argument matching on an overloaded member (a real
compiler spike proved it — see ADR-0048's Decision Outcome), call-order
verification, `ReturnsForAnyArgs`/`When().Do(...)`/strict or partial
substitutes/recursive auto-configuration, and no support for classes,
delegates, indexers, or events. If a test needs any of those, use
`Compono.NSubstitute`'s `UseNSubstitute()` for that interface instead —
the two providers can coexist; registration order decides which one
resolves first, see "Precedence with `Compono.NSubstitute`" below. Don't
try to work around a gap by polling state or inventing a callback-shaped
member on the interface just to observe a call — that's fighting the
framework, not using it.

## Unsupported shapes are compile-time diagnostics, not silent gaps

**Classes and delegates are not test-double candidates at all** —
`LeafTypeClassifier` only ever admits interfaces for generated-double
eligibility, so neither one is diagnosed here or falls back to this
package's provider; a concrete class still composes through ordinary
constructor selection, and a delegate leaf stays provider-resolved (a
runtime `CompositionException` if no provider handles it, not a `CMP002x`
diagnostic).

For an eligible **interface**, indexers, events, a genuinely unimplemented
static abstract member, a generic method whose return type depends on its
own type parameter, a generic type parameter used as `T?` (constrained or
not), and a handful of narrower shapes (set-only properties,
pointer/function-pointer parameters or returns, ref-like returns) can
withhold generated-double support at compile time. Whole-interface codes
fall back to the ordinary runtime-provider path, same as an interface the
compile-time opt-in never reached. Scoped codes only withhold the affected
member's `Configure()`/`Verify()` surface or DIM fallback; `CMP0032` is an
informational count of configuration-required members. All
generated-test-double diagnostics (`CMP0020`-`CMP0032` and
`CMP0035`-`CMP0037`) are informational — see `diagnostics.md` for each
code's exact scope and disposition before guessing a fix. Overloaded
members, a `ref`/`out`/`in` parameter, and a generic method independent of
its own type parameter are narrower now (see above) — only the specific
colliding/unsupported overload loses its surface, not the whole interface.
A non-nullable-reference return with no deterministic default no longer
rejects the whole interface either (v2, see "Configuration-required
members" above) — unless it also lacks a `Configure()` surface for one of
those other reasons, in which case it still does.

A static abstract member declared on a base interface but already
resolved by a more-derived interface's own concrete implementation (C#'s
"most specific implementation" rule — the `IAmazonS3`/`IAmazonService`
shape) is **not** a genuinely unimplemented member at all and doesn't
reject anything; only a static abstract member with no override anywhere
in the interface's hierarchy still whole-interface-rejects (ADR-0046).

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
