# Compono.XunitV3

Only relevant if the project references `Compono.XunitV3`. Requires real
xUnit v3 (`xunit.v3` + Microsoft Testing Platform runner) — not xUnit v2.
Depends on `Compono` (the source generator flows through transitively).

## `[Compose]`

```csharp
[Theory]
[Compose]
public void ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }

[Theory]
[Compose(42, "widget")]           // inline binds positionally left-to-right
public void InlineValuesAreUsedDirectly(int quantity, string productName) { }

[Theory]
[Compose(42)]                     // quantity inline, productName composed
public void MixesInlineAndComposedValues(int quantity, string productName) { }

[Theory]
[Compose(Seed = 4219)]
public void ReproducesTheSameComposedValues(Order order) { }
```

- Inline values bind **positionally**, never by parameter name.
- `Seed` is a plain non-negative `int`; negative throws immediately.
- `[Shared]` parameters compose first, in declaration order, before
  non-shared parameters — see `registrations-profiles-and-scopes.md`.
- Every row carries a `Compono.Seed` xUnit trait unconditionally, pass or
  fail — check it in test output before asking for a re-run.
- Composition happens at execution time, not discovery time — there's no
  separate "composed values shown in the test explorer" pass.

## `[Compose<TProfile>]`

```csharp
[Theory]
[Compose<OrderTestProfile>]
public void Creates_service(
    [Shared] IOrderRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

Same behavior as `[Compose]`, but applies `TProfile.Configure` to the
row's builder first — this is how a theory picks up
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

[Theory]
[Compose<RepositoryProfile, RepositoryConfig>(RepositoryKind.Player)]
public void Handles_PlayerRepository(IRepository repository) { }
```

Use this when a profile needs a value only known at **this specific
test's call site** - not a fixed, default-constructed profile the way
`[Compose<TProfile>]` always is. `TConfig`'s constructor arguments here
(**profile configuration arguments**) are a completely different binding
target from this file's inline values above - they never bind to the
test method's own parameters, all of which are still composed in full.

- `TConfig` must have exactly one public constructor; `TProfile` must have
  exactly one public constructor accepting exactly one `TConfig`-typed
  parameter. Either shape being wrong is a clear, cached
  `CompositionException` at binding-plan-construction time, not a compile
  error (`[Compose<TProfile>]`'s `new()` constraint doesn't carry over to
  this form - see `docs/adr/0036-parameterized-composition-profile-selection.md`).
- **Use the strongest attribute-legal type for each argument** - an
  `enum` for a finite choice, `typeof(...)` for a CLR type, `bool`/numeric
  where that's already the real meaning. `params object?[]` is a binding
  mechanism C# attribute rules force, not a reason to design `TConfig`
  around magic strings. Flag `[Compose<TProfile, TConfig>("SomeString")]`
  in review the same way you'd flag any other stringly-typed value
  standing in for a finite choice.
- **This is not the same problem as name-based value selection.** A value
  that varies by which parameter/member is *asking* (not by test call
  site) is a `CompositionProviderRequest.Name`-matching custom
  `ICompositionValueProvider` question - see
  `registrations-profiles-and-scopes.md`. Don't reach for
  `[Compose<TProfile, TConfig>]` for that case, and don't reach for a
  custom provider for this one.
- **Don't reach for this form by default.** If the "parameter" a
  migrated AutoFixture attribute takes is really just obtaining a
  substitute, or a single fixed value that never actually varies across
  real call sites, the plain forms already cover it - reserve this one
  for a value that's genuinely different per call site and needs to
  reach configuration logic running *inside* the profile.

## `[ComposeData]` — composed theory rows from a member

`[ComposeData]` composes **many** theory rows from a public static member,
instead of the single row `[Compose]` provides. Reach for it whenever one
`[Theory]` needs several rows and each row needs its **own** composed
values — especially its own per-row configuration — which `[Compose]`'s
single row can't express. See `docs/adr/0070-xunitv3-composedata-composed-theory-rows.md`
for the full design.

```csharp
// Arity 2 — every row carries its own TConfig (element 0) + values.
public static readonly object?[][] PricingRows =
[
    [new PricingConfig(0.10m), 100m, 90m],
    [new PricingConfig(0.25m), 80m,  60m],
];

[Theory]
[ComposeData<PricingProfile, PricingConfig>(nameof(PricingRows))]
public void Prices_by_discount(decimal price, decimal expected, PriceCalculator calculator) { }

// Arity 1 — one profile for every row; every element is a row value (no TConfig).
[Theory]
[ComposeData<AppProfile>(nameof(EmailCases))]
public void Validates(string email, bool expected, [Shared] IClock clock, EmailValidator sut) { }

// Non-generic — no profile; rows are just values + composed parameters.
[Theory]
[ComposeData(nameof(SharedRows))]
public void Reuses_the_row_repository(
    string label, [Shared] CatalogRepository repository, CatalogService service) { }
```

### The three forms

- `[ComposeData(string memberName)]` — no profile; every row element is a
  row value and every remaining parameter is composed normally.
- `[ComposeData<TProfile>(string memberName)]` — applies `TProfile` (built
  like `[Compose<TProfile>]`, its own parameterless ctor) to every row.
- `[ComposeData<TProfile, TConfig>(string memberName)]` — **element 0 of
  every row is that row's `TConfig`** (a non-null instance assignable to
  `TConfig`); elements `1..n` are that row's values. The profile is built
  **fresh per row** from that row's config, exactly like
  `[Compose<TProfile, TConfig>]` builds one from the attribute's own
  arguments — but the config comes from the row here, not the attribute.

A `MemberType` named argument points at the type that holds the member
(default is the test class); `Seed` pins every row (see below).

### Row source

The named member must be a **public static field, property, or parameterless
method** on the member type (inherited static members included). Return
shapes:

- `IEnumerable<TRow>` (not `string`) — e.g. `TheoryData<...>`,
  `TheoryDataRow<...>[]`, `List<object?[]>`, `object?[]`.
- `Task<IEnumerable>…`, `ValueTask<IEnumerable>…`, `IAsyncEnumerable<TRow>`.

Each element is one row. An element is read, in this order, as an xUnit
`ITheoryDataRow` (its `.Data`, `Label`, `Skip`, etc. are honored), else as
an `object?[]`, else as an `ITuple` (8+ values flattened). A single row as
the member's result, a null result, or a null row is an error. A member
requiring arguments or a generic member is an error — supply rows from a
parameterless member.

### Row layout and binding

Row values bind **leading-positionally**: row value `i` binds to test
parameter `i`; any parameters left over after the row's values are
composed. Binding is **strict** — no numeric widening and none of xUnit's
own `TypeHelper` conversions, the same strictness as `[Compose(42)]`:

- `null` is allowed only for a nullable value type or a `?`-annotated
  reference parameter.
- An oblivious reference parameter is treated as non-nullable.
- Too many values for the method's parameters is an error.

The same leading-positional, compose-the-rest algorithm `[Compose]` uses
(ADR-0022) applies to the composed parameters — `[Shared]` parameters
resolve first, then the rest. So write signatures as **row values first,
then `[Shared]`/composed parameters**. Binding by parameter name doesn't
exist; never suggest it.

**Reading the row's `TConfig` in the test body.** Element 0 is consumed by
the profile constructor and never binds to a parameter. Don't duplicate its
values as extra row elements, and don't invent a "bind `TConfig` to
parameter 0" feature. The supported recipe: the profile registers the config
it already holds, `builder.Register(() => config)`, and the test adds a
composed `TConfig` parameter after the row values. It resolves to that row's
own instance. Works identically on `Compono.XunitV3.Aot` (RESEARCH-0040).

```csharp
public sealed class RetentionProfile(RetentionWindow window) : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Register(() => window);
}

[Theory, ComposeData<RetentionProfile, RetentionWindow>(nameof(Cases))]
public void Keeps(int ageInDays, bool expected, RetentionWindow window, RetentionPolicy sut) { }
```

If many profiles repeat `builder.Register(() => config)`, a consumer-side
`ConfigProfile<TConfig>` base is an optional boilerplate reduction — not a
Compono type. A derived profile must not register `TConfig` again
(duplicate-registration error).

### Shared dependencies and `[Shared]`

`[Shared]` works on a `[ComposeData]` row exactly as on `[Compose]`: put a
row value for that type on the row and mark the parameter `[Shared]` and
the composed graph reuses that exact instance. A `[Shared]` parameter with
no row value is composed and shared like any other.

### Reading xUnit fixtures from a row member

Rows are read at **execution time**, after fixtures exist, so the row
member (or a profile's `Register` factory) can call
`TestContext.Current.RequireFixture<TFixture>()` — the same execution-time
path `[Compose]` uses. See the fixture section below.

### When the rows are read

`[ComposeData]` runs at execution time only — it is never enumerated at
discovery. The member is read and **all** rows are composed up front, when
xUnit builds the row's tests (before the first row runs). Nothing is
cached from discovery.

### Skip, labels, traits, and other metadata

Row metadata merges with the attribute's own by one rule:

- Skip/SkipType/SkipUnless/SkipWhen are **grouped** — the row's skip group
  wins if the row sets one, else the attribute's, else none (a row's `Skip`
  with the attribute's `SkipUnless` never mix).
- `Label`/`TestDisplayName`/`Timeout`/`Explicit` — row value, else the
  attribute's.
- `Traits` — the union of the row's and the attribute's, plus
  `Compono.Seed`.
- `DisableParallelization` (xUnit 4.x) — row else attribute.

`Label` gives the row its deterministic display name.

### Failures, seeds, and row labels

Reading the member or building row `i` throws `CompositionException` — the
whole theory fails **as a unit** (there are no placeholder rows). The
message names the member (`Type.Member`), the zero-based row index, and the
row label if it has one. All rows are built before any runs, so one row's
composition failure stops the rest — including rows that would have passed —
and the theory reports a single failed result instead of one per row, so
the reported test count can drop. Every data attribute on the method,
including `[InlineData]`, is affected. Fix the row and every row runs and is
counted again.

Exactly **one** seed source applies per row:

- the attribute's `Seed` (pins every row; must be non-negative), or
- the profile's `WithSeed` in `Configure` (pins every row), or
- otherwise a fresh non-negative seed per row.

Setting **both** the attribute `Seed` and a profile `WithSeed` is a
duplicate-configuration error — there is no override. Every row carries
`Compono.Seed` as an xUnit trait.

### Disposal

Compono registers nothing and disposes nothing for a `[ComposeData]` row —
same as `[Compose]`. Under **JIT** xUnit v3 disposes every `IDisposable`
theory argument, a `[Shared]` parameter included, so a fixture-owned instance
passed as a row value would be disposed by JIT; under **Native AOT** it is not
disposed. Keep fixture-owned disposables out of theory parameters (including
`[Shared]` ones) so JIT doesn't dispose the fixture's own instance.

### Mixing with xUnit's own data attributes

`[InlineData]`/`[MemberData]`/`[ClassData]` remain legal **alongside** a
Compose-family attribute and are never merged or composed — but a
`[ComposeData]`/`[Compose…]` method still allows **only one** Compose-family
attribute (see the hard constraint below).

### Native AOT

`Compono.XunitV3.Aot` ships the identical three `[ComposeData]` forms (in
namespace `Compono.XunitV3.Aot`). Composed theory rows are at **parity**
across both engines, including row `[Shared]` and per-row `TConfig`.
Generic test-class bases are supported on AOT. The differences are
diagnostic-time and the AOT-only rejections (open-generic member/skip
holders, `static abstract`/`static virtual` interface members and skip
properties, a member declared only on a derived class of an inherited theory
method, inaccessible nested member/skip holders) — diagnosed at **compile
time** with `CMP0051`-`CMP0057` (plus the `[ComposeData]`-specific uses of
`CMP0040`/`CMP0042`/`CMP0044`-`CMP0049`). For those AOT-only cases JIT
**accepts** the shape; for every other condition here the JIT engine reports
the equivalent at run time as a `CompositionException`. (The assembly-fixture
limitation is xUnit's own, not a Compono diagnostic.) See
`references/diagnostics.md`.

### Real examples in this repo

`test/Compono.XunitV3.SampleTests/ComposeDataTests.cs` and its AOT twin
`test/Compono.XunitV3.Aot.SampleTests/ComposeDataTests.cs` hold the three
canonical examples: per-row `TConfig`, a `[Shared]` row value, and a row
member reading an xUnit fixture.

## Hard constraint: one Compose-family attribute per method

`[Compose]`, `[Compose<TProfile>]`, `[Compose<TProfile, TConfig>]`, and
every `[ComposeData…]` form are all Compose-family. Two **different**
Compose-family attributes on one method (e.g. `[Compose]` +
`[Compose<ProfileA>]`, or `[Compose]` + `[ComposeData]`) *compile* but
throw `CompositionException` at data-binding time, not compile time — the
signature is only validated once xUnit actually asks the attribute for its
row data. The identical attribute type twice on one method **is** a
compiler error (`AllowMultiple=false`).

**The multi-row answer is one `[ComposeData]`, never several attributes
stacked.** If a test needs several independent inline+composed
combinations, that's the member-backed row source `[ComposeData]` reads —
don't layer multiple Compose-family attributes to get that effect.

## Disposal

`Compono.XunitV3` does not dispose composed values automatically. The test
owns any `IDisposable` or `IAsyncDisposable` root, shared value, or nested
dependency it creates and must dispose resources when their lifetime
matters. Do not copy `Compono.TUnit` disposal assumptions here: TUnit owns
composed root method arguments, while xUnit v3 does not.

## xUnit fixture values — `RequireFixture<TFixture>()`

To feed a value xUnit already initialized (a class, collection, or assembly
fixture) into a composition, call
`TestContext.Current.RequireFixture<TFixture>()` **inside a `Register`
factory**:

```csharp
public sealed class WebAppFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>();
        await Task.Yield();
    }

    public async ValueTask DisposeAsync() => await Factory.DisposeAsync();
}

public sealed class ApiTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Register(
        () => new ApiClient(
            TestContext.Current.RequireFixture<WebAppFixture>().Factory.CreateClient()));
}
```

- **Read it inside the `Register` factory**, not once from the profile
  constructor or a `Configure` body — Compono composes at test *execution*
  time, so the factory sees a live `TestContext.Current` with fixtures
  already initialized, and it runs per row. `TestContext.Current` is
  ambient and available during construction/`Configure` too, but a profile
  instance can outlive a single row (under JIT, `Configure` runs once per
  attribute instance and reuses any captured value across its rows), so
  capturing a fixture there risks stale or cross-test state. Calling it
  with no active test class throws `InvalidOperationException`.
- **Exact-type matching only.** xUnit's `GetFixture` returns `null` when the
  exact fixture type isn't available; `RequireFixture` throws a descriptive
  `InvalidOperationException` instead.
- **It never waits** — if the fixture hasn't finished initializing it throws
  immediately. Declare the fixture concretely (`IClassFixture<T>`,
  `ICollectionFixture<T>`, `[assembly: AssemblyFixture(typeof(T))]`) so xUnit
  initializes it before the test runs.
- **The fixture stays xUnit's.** `RequireFixture` never disposes it or
  changes its lifetime. Compono doesn't dispose composed values either — the
  test owns what it creates (dispose a composed `HttpClient`, etc.).
- **No ambient `TestContext`?** `RequireFixture` reads
  `TestContext.Current`, so it only works under a running xUnit test. For a
  framework-neutral profile, inject the fixture through the constructor and
  build the composer by hand:

  ```csharp
  public sealed class FixtureBackedProfile(WebAppFixture fixture) : ICompositionProfile
  {
      public void Configure(CompositionBuilder builder) =>
          builder.Register(() => new ApiClient(fixture.Factory.CreateClient()));
  }

  await using var fixture = new WebAppFixture();
  await fixture.InitializeAsync();
  var composer = Composer.Create(b => b.AddProfile(new FixtureBackedProfile(fixture)));
  await using var client = composer.Create<ApiClient>();
  ```

  `await using` guarantees disposal even when an assertion throws. That
  works with any test framework but skips attribute-driven parameter
  composition.
- This is **not** AutoFixture's `IFixture`: it's a read of a concrete xUnit
  fixture, not a shared mutable configuration object. (There is still no
  AutoFixture-style `IFixture`; configuration lives in a profile.)

## Compono.XunitV3.Aot differences

In the AOT package the same `RequireFixture<TFixture>()` method exists,
textually identical, in namespace `Compono.XunitV3.Aot` — only the `using`
changes (`using Compono.XunitV3.Aot;` instead of `Compono.XunitV3`). Class
and collection fixtures work fully. An **assembly fixture**
(`[assembly: AssemblyFixture(typeof(T))]`) currently fails to compile under
xUnit v3's Native AOT generator (4.0.1 / 4.0.2) — an upstream xUnit issue
(xunit/xunit#3664, reported fixed upstream in 4.0.3-pre.19 but not yet
validated by Compono), not a Compono limitation — so use a class or
collection fixture there.

## No AutoFixture-style fixture object

There is still nothing like AutoFixture's `IFixture` — no shared mutable
configuration object to hold onto across a test class. Configuration is
per-test via `[Compose<TProfile>]`. This is unrelated to **xUnit** fixtures,
which are read (not owned) via `RequireFixture<TFixture>()` above — don't
invent a shared fixture-*holder* pattern to carry Compono configuration.

## Real examples in this repo

- `test/Compono.XunitV3.SampleTests/FixtureProfileTests.cs` — class- and
  collection-fixture values registered through
  `TestContext.Current.RequireFixture<TFixture>()`.
- `test/Compono.XunitV3.Tests/RequireFixtureIntegrationTests.cs` — real
  xUnit class/collection/assembly fixtures, parallel-fixture isolation, and
  the missing-fixture failure path.
- `test/Compono.XunitV3.SampleTests/SharedTests.cs` — `[Shared] Repository
  repository, OrderService service, CreateOrder command`.
- `test/Compono.XunitV3.SampleTests/NSubstituteTests.cs` —
  `[Compose<NSubstituteTestProfile>] async Task Saves_order([Shared]
  IOrderRepository repository, CreateOrderHandler handler, PlaceOrder
  command)`.
- `test/Compono.XunitV3.SampleTests/BogusTests.cs` — a profile combining
  `UseBogus().UseNSubstitute()`, composing a `Customer` with `required
  string FirstName/LastName/Email` matched via Bogus conventions.
- `test/Compono.XunitV3.SampleTests/FailingCompositionTests.cs` — a
  deliberately failing `[Compose(Seed = 24601)]` test, useful as a
  reference for what the real `dotnet test` failure output looks like.
