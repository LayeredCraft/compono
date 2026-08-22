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
each overload gets its **own** `Configure()` surface, disambiguated by
ordinary C# overload resolution — the generated configuration extension
for an overloaded member takes the same real parameter types the interface
overload declares (the values themselves are discarded, exactly like the
non-overloaded, zero-argument case). `Verify()` call verification reuses
this same per-overload surface — see "Call verification" below.

```csharp
public interface IResponseBuilder
{
    void Speak(string? text);
    void Speak(params ISsml[] parts);
}

builder.Configure().Speak("hello").Throws(new InvalidOperationException());   // the string? overload
builder.Configure().Speak(new ISsml[] { ssml }).Throws(new InvalidOperationException()); // the params overload
```

`.Speak(...)` alone only *selects* an overload's configuration handle
(`ReturnConfigBuilder<Unit>`) — like any `Configure()` call, it does
nothing to the double until you chain `.Returns(...)` or `.Throws(...)`.

Two edge cases stay narrower than full per-overload support:

- **A diamond collision** — the exact same signature independently declared
  by two different base interfaces — can't be disambiguated at all (both
  identities are structurally identical). That one identity gets no
  `Configure()` surface (an informational `CMP0022`), but every other
  member of the interface, including any other overload sharing the same
  name, is unaffected.
- **A `ref`/`out`/`in` parameter** on one overload falls back to a
  deterministic-default dispatch body with no configuration surface for
  *that* overload (an informational `CMP0030`) — its sibling overloads keep
  their own surface unaffected. A return type (or `out` parameter) with no
  deterministic default still has no constructible body at any granularity
  and rejects the whole interface, same as the non-overloaded case
  (`CMP0026`).

## Generic methods

A generic method whose return type doesn't reference its own type
parameter is supported (v2, [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)
Requirement 2) — the motivating shape is
`Microsoft.Extensions.Logging.ILogger`'s own `Log<TState>`/`BeginScope<TState>`:

```csharp
public interface ILoggerLike
{
    void Log<TState>(int logLevel, TState state, Exception? exception);

    IDisposable? BeginScope<TState>(TState state) where TState : notnull;
}

logger.Configure().Log().Throws(new InvalidOperationException());
logger.Configure().BeginScope().Returns(myScope);
// Applies regardless of what TState the real caller closes BeginScope<TState> to - the
// configuration extension stays non-generic, member-level, exactly like an ordinary member.
```

The explicit interface implementation stays generic — type parameters
copied, constraints left unstated (they're inherited automatically from
the interface and can't be redeclared, `CS0460`). The
`Configure()`/`Verify()` extension itself stays **non-generic** for a solo
generic member: the backing slot's type never depends on the method's own
type parameter, so one slot covers every closed instantiation a real
caller exercises.

**Overloaded *and* generic together** (Amendment 1) — when a generic
method's name is also shared by another overload, its configuration
extension becomes generic too, purely for compile-time overload selection
(the backing slot still doesn't vary per closed type) — this extension
*does* carry its constraint clauses, copied verbatim, since it's an
ordinary standalone generic method rather than an interface
implementation and has no other way to stay type-safe:

```csharp
public interface IWidget
{
    void Process<T>(T value);
    void Process<T>(IEnumerable<T> values);
}

widget.Configure().Process(0).Throws(new InvalidOperationException());        // T inferred int
widget.Configure().Process<string>(someListOfString).Returns(default);        // explicit type argument
```

**What stays unsupported:**

- A generic method whose return type references its own type parameter
  anywhere in its symbol graph (`T Get<T>()`, `Task<T> GetAsync<T>()`,
  `IEnumerable<T> Filter<T>()`) — no constructible fallback body, so the
  whole interface falls back to the runtime-provider path (`CMP0031`).
- **Any** type parameter used as `T?` in a parameter (or the method's own
  declaration) — constrained or unconstrained, regardless of which
  constraint. Correctly modeling exactly when (and with which keyword) a
  C# 9+ constraint restatement is required on the explicit implementation
  isn't something this feature attempts — two review rounds gave
  conflicting answers even for the constrained case — so every `T?`-using
  type parameter is diagnosed and excluded alike (`CMP0026`).

## Call verification

`Verify()` — parallel to and independent from `Configure()` — asserts how
many times a member was actually called (v2,
[ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)
Requirement 3). `Never()`/`Once()`/`Exactly(n)` only:

```csharp
service.Repository.Configure().CountAsync().Returns(Task.FromResult(5));

var order = await service.PlaceAsync(3);

service.Repository.Verify().CountAsync().Once();
service.Repository.Verify().Save().Once();
service.Repository.Verify().UtcNow().Never(); // never read in this call path
```

A failing assertion throws `Compono.TestDoubleVerificationException` (a
plain exception, not an xUnit/TUnit/AwesomeAssertions assertion type - core
`Compono` has no reference to any of them) naming the expected and actual
counts. A call counts whether it hits configured, default, or thrown
behavior - counting and configured `Returns`/`Throws` dispatch never
interfere with each other. Verification reuses the same per-overload
discriminator mechanism `Configure()` does: `repository.Verify().Speak("x")`
selects the same overload-specific counter `repository.Configure().Speak("x")`
would.

**Still deliberately minimal** - `Never`/`Once`/`Exactly(n)` only, no
`AtLeast`/`AtMost`, no `ReceivedCalls()`-style enumeration, and (see below)
no call-order verification. Argument-aware recording *is* available for
one specific class of member - see "Argument matching and argument-filtered
verification" below. If a test needs anything else this page doesn't cover
(call-order verification, an overloaded member's own argument matching,
`ReturnsForAnyArgs`, etc.), use `Compono.NSubstitute` for that interface
instead - the two providers can coexist (see below).

## Argument matching and argument-filtered verification

For a member that is the only overload of its name in the interface, has no
real parameter referencing the member's own open generic type parameter, has
no real parameter of a ref-like type (`Span<T>` and similar can't be a
generic type argument), has no derived internal field name colliding with
another member's, and isn't a one-parameter `Equals` (its extension would
share arity with the inherited `object.Equals(object)` and never actually be
reachable) — five conditions, all required (v3,
[ADR-0048](../adr/0048-testdoubles-argument-matching-and-call-verification.md)
and its Amendment 1) — `Configure()`/`Verify()` accept `Compono.Match<T>`
per parameter instead of just the return value - a literal (equality match), `Match.Any<T>()`
(matches anything, same as omitting a matcher), or `Match.Is<T>(predicate)`:

```csharp
repository.Configure()
    .Withdraw("acct-1", Match.Any<decimal>(), Match.Is<bool>(allowed => allowed))
    .Returns(true);

repository.Withdraw("acct-1", 50m, overdraftAllowed: true);  // true - every matcher satisfied
repository.Withdraw("acct-2", 50m, overdraftAllowed: true);  // falls through - accountId doesn't match

repository.Verify()
    .Withdraw(Match.Is<string>(id => id == "acct-1"), Match.Any<decimal>(), Match.Any<bool>())
    .Once();
```

An eligible member also keeps its original zero-argument `Configure()`/
`Verify()` spelling (`repository.Configure().Withdraw().Returns(...)`,
argument-independent, exactly v1/v2's shape) - the two aren't mutually
exclusive, and a member with no real parameters only ever had the
zero-argument form to begin with. A call whose arguments don't satisfy a
configured matcher is treated identically to an unconfigured member (falls
through to a computed default, or to
[Configuration-required members](#configuration-required-members)'
throwing behavior below) - not a distinct failure mode.

**Why this doesn't apply to an overloaded member.** A real compiler spike
(ADR-0048's Decision Outcome) proved that wrapping every overload's
parameters in a matcher type breaks C#'s own overload resolution
unpredictably for several realistic parameter-type families (base/derived
class hierarchies, `string[]` vs. `IEnumerable<string>`, even plain `int`
vs. `long` widening) - there's no reliable per-family fix, so argument
matching is scoped out entirely for any member with more than one overload.
An overloaded member's `Configure()`/`Verify()` stay exactly the
[per-overload discriminator shape](#overloaded-members) above, unchanged.
The same reasoning excludes a generic method whose real parameters
reference its own type parameter (an `ILogger<TState>.Log<TState>`-shaped
member) - a per-member call log can't hold an open type parameter's value,
so that shape keeps its existing argument-independent
`Configure()`/`Verify()` too, exactly as it already worked.

Three more exclusions found during implementation (ADR-0048 Amendment 1),
each falling back to the same existing argument-independent shape: a
member with a ref-like parameter type (`Span<T>` etc. - can't be used as a
generic type argument); a member whose derived internal field names would
collide with another member's; and a one-parameter `Equals` (its extension
would share arity with the inherited `object.Equals(object)` and C# always
prefers an applicable instance method over an extension method, so the
generated extension would never actually be reachable).

**Why `Match<T>`, not `Arg<T>`.** `Compono.Arg` would collide with
`NSubstitute.Arg` for any consumer whose own namespace nests under
`Compono` (this repo's own samples convention) or who combines `Compono`
with `Compono.NSubstitute` directly - confirmed with a real failing build
during this feature's implementation, not a theoretical concern. `Match`
avoids the collision entirely and names the actual Compono concept
(matching an argument), rather than borrowing NSubstitute's own
vocabulary.

## Configuration-required members

A member returning a non-nullable reference type (or a `Task<T>`/
`ValueTask<T>` wrapping one) with no deterministic default no longer
rejects the whole interface at generation time (v2,
[ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)) —
provided it would otherwise have a real `Configure()`/`Verify()` surface,
the double still generates and that specific member becomes
**configuration-required**: it throws
`Compono.TestDoubleNotConfiguredException` if invoked before
`Configure().Member(...).Returns(...)`/`.Throws(...)` configures it,
instead of falling back to a computed default:

```csharp
public interface ILambdaContext
{
    string AwsRequestId { get; }
}

// AwsRequestId has no deterministic default (a non-nullable string) - it
// generates as configuration-required rather than rejecting the whole
// interface. Configure it before the code under test reads it:
context.Configure().AwsRequestId().Returns("test-request-id");

// An unconfigured call throws instead of silently returning a made-up value:
var act = () => context.AwsRequestId;
act.Should().Throw<Compono.TestDoubleNotConfiguredException>();
```

`Configure()`/`Verify()` work exactly the same as any other member -
`ReturnConfig<T>`/`ReturnConfigBuilder<T>` never depended on `T` having a
default to begin with. This applies identically to a method, a property,
an async (`Task<T>`/`ValueTask<T>`) method, and a fluent self-returning
member (`IResponseBuilder`-shaped `Speak(...)` returning `IResponseBuilder`
itself) - none of these get special-cased; a fluent member is
configuration-required like any other non-nullable reference return, and
`Configure().Speak(...).Returns(self)` works for a chained-call test.

The generator reports `CMP0032` once per interface (a count of how many
members require configuration, not one per member) so you know to expect
this before your first unconfigured call - see
[Diagnostics](../reference/diagnostics.md#cmp0032-test-double-members-require-explicit-configuration).
`CMP0025` still rejects the whole interface, unchanged, for the shapes
this doesn't apply to: a ref-like, by-ref, or pointer/function-pointer
return **always**, and a no-default non-nullable reference return when the
member *also* has no `Configure()` surface for an unrelated reason - a
diamond collision, a zero-argument-extension collision, an overloaded
`ref`/`out`/`in` parameter, or (for a method) a collision with an inherited
`object` member.

## Static abstract members inherited from a base interface

An interface that declares a static abstract member (C# 11+) still rejects
the whole interface at generation time if that member is genuinely
unimplemented anywhere in the interface's own hierarchy — but if a
**more-derived interface in the same hierarchy already provides a concrete
implementation** for it (C#'s own "most specific implementation" rule for
static interface members), that's not an unimplemented requirement at all,
and the double generates normally
([ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)):

```csharp
public interface IAmazonService
{
    static abstract AmazonS3Config CreateDefaultClientConfig();
}

public interface IAmazonS3 : IAmazonService
{
    // IAmazonS3 re-implements IAmazonService's static abstract member with
    // a real body - CreateDefaultClientConfig() is fully resolved from
    // IAmazonS3's own perspective, even though IAmazonService itself only
    // declares it abstract.
    static AmazonS3Config IAmazonService.CreateDefaultClientConfig() => new();

    Task<GetObjectResponse> GetObjectAsync(string bucketName, string key);
}

// Generates and resolves through UseGeneratedTestDoubles() alone - every
// instance member (GetObjectAsync, and the 20+ others a real S3 client
// interface declares) works exactly as it would if the static abstract
// member didn't exist.
var s3 = composer.Create<IAmazonS3>();
s3.Configure().GetObjectAsync().Returns(response);
```

A genuinely unresolved static abstract member (no override anywhere in the
interface's hierarchy) still rejects the whole interface (`CMP0021`) — and
this isn't a gap Compono.TestDoubles can close on its own: C# itself
forbids using an interface with a genuinely unresolved static abstract
member as a type argument to *any* generic method, constrained or not
(`CS8920`), and Compono's own composition mechanism resolves every
interface through exactly such a call. An interface in that state was
never actually composable through Compono at all, with or without a
generated double.

## Precedence with `Compono.NSubstitute`

If both packages are installed and both providers registered, registration
order decides which one resolves an interface request first — register
`UseGeneratedTestDoubles()` before `UseNSubstitute()` if you want the
generated double to take precedence for interfaces it covers, matching
[ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
"tried in registration order" provider contract. Neither package special-
cases the other.

## What it deliberately doesn't do

Argument matching and argument-filtered verification exist now, but only
for a member satisfying all five eligibility conditions — see "Argument
matching and argument-filtered verification" above (and ADR-0048 Amendment
1 for the three conditions added after initial release). Still no argument
matching on an overloaded member (a real compiler
spike proved it, see above), no call-order verification, no
`ReturnsForAnyArgs`/`When().Do(...)`/strict or partial substitutes/
recursive auto-configuration, and no support for classes, delegates,
indexers, events, or a generic method whose return type depends on its own
type parameter — see
[ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
Non-Goals and [ADR-0048](../adr/0048-testdoubles-argument-matching-and-call-verification.md)'s
Non-Goals for the full scope boundary. A genuinely unimplemented static
abstract member still rejects its whole interface, the same as the shapes
above — but one already resolved via a more-derived interface's own
concrete implementation is fully supported; see "Static abstract members
inherited from a base interface" above
([ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)).
Overloaded members, a `ref`/`out`/`in` parameter's own overload, generic
methods independent of their own type parameter, and minimal call
verification (`Never`/`Once`/`Exactly(n)`) are now supported (see above,
[ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)).
An unsupported member shape is a compile-time diagnostic
(`CMP0020`-`CMP0032`), not a silent gap.

## Next

- [Shared Values](../concepts/shared-values.md) — asserting against a
  configured generated double.
- [Providers](../concepts/providers.md) — where the generated-double
  provider sits in the resolution pipeline.
- [`Compono.NSubstitute`](compono-nsubstitute.md) — the runtime-proxy
  alternative, for call verification/argument matchers.
