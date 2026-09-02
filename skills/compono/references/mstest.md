# Compono.MSTest

Only relevant if the project references `Compono.MSTest`. Requires real
MSTest (`MSTest.TestFramework` **`4.0.0` or later** — `3.x` ships under a
different, binary-incompatible assembly identity and is not supported;
see ADR-0057 Amendment 1). Depends on `Compono` (the source generator
flows through transitively).

The full attribute family: `[Compose]`, `[Compose<TProfile>]`, and
`[Compose<TProfile, TConfig>]`, method-parameter-only — see ADR-0057 for
the full design.

## `[TestMethod]` + `[Compose]` — never `[DataTestMethod]`

```csharp
[TestClass]
public class OrderServiceTests
{
    [TestMethod]
    [Compose]
    public void ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }

    [TestMethod]
    [Compose(42, "widget")]           // inline binds positionally left-to-right
    public void InlineValuesAreUsedDirectly(int quantity, string productName) { }

    [TestMethod]
    [Compose(42)]                     // quantity inline, productName composed
    public void MixesInlineAndComposedValues(int quantity, string productName) { }

    [TestMethod]
    [Compose(Seed = 4219)]
    public void ReproducesTheSameComposedValues(Order order) { }
}
```

`[TestMethod]` alone is correct — never suggest `[DataTestMethod]`. It
"provides no additional value over `TestMethodAttribute`" and is actively
flagged by analyzer `MSTEST0044` for removal upstream. `ComposeAttribute`
implements `ITestDataSource` directly on a plain `Attribute`, matching
every current MSTest documentation example — it does not derive from
`DataTestMethodAttribute` or any other MSTest attribute base type.

- Inline values bind **positionally**, never by parameter name.
- `Seed` is a plain non-negative `int`; negative throws immediately,
  before any row state is reported.
- `[Shared]` parameters compose first, in declaration order, before
  non-shared parameters — a distinct attribute type from
  `Compono.XunitV3.SharedAttribute`/`Compono.TUnit.SharedAttribute`, with
  identical binding rules (duplicated per ADR-0057's binding-logic
  decision, same reasoning as ADR-0040's own).
- A row's seed is surfaced via `GetDisplayName`'s output —
  `"{methodName} (Compono, seed: {seed})"`. This is the *only*
  seed-reporting surface — no `TestContext.Properties`/`TestProperty`
  usage, unlike what you might expect from `Compono.XunitV3`'s trait
  mechanism or `Compono.TUnit`'s discovery-event property. **Verified
  where it actually appears**: `GetDisplayName` is called during
  *discovery*/listing (`--list-tests`, `dotnet vstest -lt`, Test
  Explorer's tree population) under both MTP and the classic VSTest
  adapter — confirmed directly. It is **not** called during an ordinary
  `dotnet test`/`dotnet vstest` execution run under either runner —
  don't tell a user to look for the seed in a plain execution console
  summary; point them at discovery/listing output, or at the
  seed-enriched `CompositionException` message on an actual composition
  failure (a separate mechanism, always present regardless of
  `GetDisplayName`).
- **`GetData`/`GetDisplayName` may run more than once for one eventual
  test case** — see "Discovery/execution repeat-composition behavior"
  below before assuming a `Register<T>()` factory runs exactly once.

## `[Compose<TProfile>]`

```csharp
[TestMethod]
[Compose<OrderTestProfile>]
public void Creates_service(
    [Shared] IOrderRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

Same behavior as `[Compose]`, but applies `TProfile.Configure` to the
row's builder first — this is how a test picks up
`UseNSubstitute()`/`UseBogus()`/registrations for that specific test.

## `[Compose<TProfile, TConfig>]`

```csharp
public enum RepositoryKind { Player, Game }

public sealed record RepositoryConfig(RepositoryKind Repository);

public sealed class RepositoryProfile : ICompositionProfile
{
    public RepositoryProfile(RepositoryConfig config) => Config = config;
    public RepositoryConfig Config { get; }
    public void Configure(CompositionBuilder builder) =>
        builder.Register<IRepository>(_ => RepositoryFactory.Create(Config.Repository));
}

[TestMethod]
[Compose<RepositoryProfile, RepositoryConfig>(RepositoryKind.Player)]
public void Handles_PlayerRepository(IRepository repository) { }
```

Use this when a profile needs a value only known at **this specific
test's call site** — not a fixed, default-constructed profile the way
`[Compose<TProfile>]` always is. `TConfig`'s constructor arguments here
(**profile configuration arguments**) are a completely different binding
target from this file's inline values above — they never bind to the
test method's own parameters, all of which are still composed in full.

- `TConfig` must have exactly one public constructor; `TProfile` must have
  exactly one public constructor accepting exactly one `TConfig`-typed
  parameter. Either shape being wrong is a clear `CompositionException`
  raised during composer/profile initialization (`ApplyProfile`, inside
  the base class's cached `Lazy<Composer>`) — before `BindingPlan` is
  ever built, not a compile error (`[Compose<TProfile>]`'s `new()`
  constraint doesn't carry over to this form).
- **Use the strongest attribute-legal type for each argument** — an
  `enum` for a finite choice, `typeof(...)` for a CLR type, `bool`/numeric
  where that's already the real meaning.
- **Don't reach for this form by default.** If a fixed, default-constructed
  profile already covers it, the plain `[Compose<TProfile>]` form is
  enough.

## `[DataRow]`/`[DynamicData]` coexistence — independent rows, never merged

`[DataRow]`, `[DynamicData]`, and `[Compose]` are **independent, complete-row
data sources** on the same method — they never merge into one row.

```csharp
[TestMethod]
[DataRow(1, "known-value")]
[Compose]
public void RunsTwoIndependentCases(int number, string text)
{
    // Case 1: number=1, text="known-value" (from [DataRow])
    // Case 2: number and text both composed (from [Compose])
    // [Compose] never "fills in" a parameter [DataRow] left unspecified -
    // there is no such thing as a partial [DataRow] row here.
}
```

Don't suggest per-parameter mixing (`[DataRow]` supplying some parameters,
`[Compose]` supplying the rest on the *same* row) — MSTest's own
`ITestDataSource` model doesn't support it, and neither `Compono.XunitV3`
nor `Compono.TUnit` support the equivalent either.

## Discovery/execution repeat-composition behavior — read before assuming exactly-once

**MSTest may invoke a `[Compose]` attribute's `GetData` more than once
across separately-invoked discovery and execution sessions.**
Consequently, composition — including any side-effecting `Register<T>()`
factory or `ICompositionValueProvider` — may also run more than once for
one eventual test case.

Confirmed, current-version, PID-tagged proof (not just RESEARCH-0017's
original evidence): a single `dotnet test`/`dotnet vstest` invocation
(**either** MTP or the classic VSTest adapter) produces exactly one
`GetData` call per method. It's specifically *separate process*
invocations — a discovery process (`--list-tests`/`-lt`) followed by a
separate execution process — that produce two calls, one per process,
under **both** runners (not a VSTest-only quirk). This is the realistic
Visual Studio Test Explorer workflow: discover once when the tree
populates, execute separately and possibly repeatedly afterward.

**Never tell a user `Register<T>()`/`ICompositionValueProvider` factories
are guaranteed to run exactly once under `Compono.MSTest`.** No such
contract exists — the safety contract `ICompositionValueProvider` does
carry ("must be safe to invoke repeatedly, including concurrently") is
about not crashing/corrupting state, not about purity. Deterministic
seeding keeps the *values* two independently-composed rows produce
logically equivalent, even though they're distinct object instances —
only an *observable side effect* a factory performs genuinely repeats.

`[Shared]`/`Share<T>()` are never split across calls — each `GetData`
invocation gets its own fresh `CompositionRow`, so sharing stays correct
*within* one call regardless of how many times `GetData` itself runs.

## Synchronous-only composition

`ITestDataSource.GetData` returns `IEnumerable<object?[]>` — no `Task`/
`ValueTask` anywhere in its signature, a **harder** constraint than
`Compono.XunitV3`'s own `ValueTask`-returning `GetData`. Never suggest an
`async`-composed `[Compose]` parameter for MSTest.

## Disposal — Compono never owns it

`Compono.MSTest` does not dispose composed argument objects — no
`IDisposable`/`IAsyncDisposable` cleanup of any kind. A composed value's
provenance (freshly constructed vs. shared/cached) is indistinguishable
from Compono's own vantage point, so no disposal-tracking mechanism is
introduced that could risk disposing an externally-owned instance. Point
the user at MSTest's own `[TestCleanup]`/`IDisposable` on the test class
itself for their own disposal needs — don't assume `Compono.TUnit`'s
"MSTest disposes root arguments automatically" story carries over; it
doesn't, `Compono.MSTest` makes no such promise.

## `TestContext` — never auto-injected

`Compono.MSTest` does **not** compose `TestContext` as a `[Compose]`
parameter. MSTest already provides it idiomatically (constructor injection
on MSTest 3.6+, or the classic `public TestContext TestContext { get; set; }`
property) with ownership unambiguously MSTest's — never suggest routing it
through `[Compose]`.

## MTP and VSTest — both supported, neither required

`Compono.MSTest` introduces no runner-selection logic of its own — MTP
(the `dotnet new mstest` default) and the classic VSTest adapter
(`<UseVSTest>true</UseVSTest>`) both work identically. Don't tell a user
they need MTP specifically.

## Hard constraint: one Compose-family attribute per method

`[Compose]`, `[Compose<TProfile>]`, and `[Compose<TProfile, TConfig>]` are
all `ComposeAttribute` subclasses. Two **different** Compose-family
attributes on one method (e.g. `[Compose]` + `[Compose<ProfileA>]`)
*compile* but throw `CompositionException` at data-generation time, not
compile time. The identical attribute type twice on one method **is** a
compiler error (`AllowMultiple=false`).

**There is no equivalent of stacking multiple data-source attributes on
one method** to get several independent inline+composed combinations —
split into separate `[TestMethod]` methods instead.

## No fixture object

There's nothing like AutoFixture's `IFixture` to hold onto across a test
class.

## Real examples in this repo

- `test/Compono.MSTest.SampleTests/CompositionTests.cs` — a plain
  `[Compose]`-composed `OrderService` through the real packaged
  `Compono.MSTest -> Compono` dependency (not a `ProjectReference`).
- `test/Compono.MSTest.SampleTests/SharedTests.cs` — `[Shared] Repository
  repository, OrderService service`.
- `test/Compono.MSTest.SampleTests/NSubstituteTests.cs` —
  `[Compose<NSubstituteTestProfile>] void Saves_order([Shared]
  IOrderRepository repository, CreateOrderHandler handler, PlaceOrder
  command)`.
- `test/Compono.MSTest.Tests/RealRunnerRowIdentityTests.cs` — real,
  actually-executed `[TestMethod]` + `[Compose]` tests proving the seed/
  display-name bridge under a genuine MSTest test host, not a direct
  `GetData` call.
- `test/Compono.MSTest.Tests/DataRowCoexistenceTests.cs` — the
  `[DataRow]` + `[Compose]` independent-row proof under a real run.
