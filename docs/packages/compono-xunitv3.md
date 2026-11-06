# Compono.XunitV3

xUnit v3 integration — theory data attributes that compose test parameters
directly, instead of hand-building `[MemberData]` rows or a custom
`AutoDataAttribute` wrapper.

## When to install

You write xUnit v3 tests (`xunit.v3` + the Microsoft Testing Platform
runner) and want theory parameters composed automatically:

```bash
dotnet add package Compono
dotnet add package Compono.XunitV3
```

`Compono.XunitV3` doesn't add an xUnit v3 test host for you — it integrates
with an existing one. If your project targets NUnit, MSTest, or plain
xUnit v3 without composed data, you don't need this package; core `Compono`
still works standalone via `Composer.Create()` and the resulting
composer's own `Create<T>()`.

**Publishing your test project as Native AOT?** This package depends on
xUnit v3's reflection-mode assemblies and cannot be combined with xUnit's
Native AOT package family (`xunit.v3.aot.mtp-v2`) in the same project — see
[`Compono.XunitV3.Aot`](compono-xunitv3-aot.md) instead.

## What it gives you

- **`[Compose]`** — every theory parameter is composed. See
  [Your First Composed Theory](../getting-started/first-test.md).
- **`[Compose<TProfile>]`** — same, with a specific
  [`ICompositionProfile`](../concepts/profiles.md) applied.
- **`[Compose<TProfile, TConfig>]`** — same, with a profile built from
  call-site-known profile configuration arguments — see
  [Profile configuration arguments](#profile-configuration-arguments)
  below.
- **Inline + composed mixing** — `[Compose(42, "widget")]` binds inline
  values left-to-right; anything left over is composed. See
  [How Do I Write a Composed Theory?](../how-to/write-a-composed-theory.md).
- **`[Shared]`** — reuse one composed instance across every parameter (or
  nested dependency) in the same row that requests the same type. See
  [Shared Values](../concepts/shared-values.md).
- **`Compose(Seed = ...)`** — reproduce a specific composed row exactly;
  every row is also tagged with a `Compono.Seed` trait, and a *composition*
  failure's message includes the seed that produced it. For an assertion
  failure in the test body — composition succeeded, but the test itself
  failed — the message won't have it; use the `Compono.Seed` trait
  instead. See
  [Determinism and Seeding](../concepts/determinism-and-seeding.md).

## Profile configuration arguments

`[Compose<TProfile>]` selects a fixed, default-constructed profile type —
the same profile, configured the same way, for every caller. When a
profile needs to be built differently per test call site (drawn from real
migration evidence — see
[Migrating from AutoFixture](../migrating-from-autofixture.md#migrate-a-parameterized-custom-autodataattribute)),
`[Compose<TProfile, TConfig>]` binds this attribute's own constructor
arguments — **profile configuration arguments**, a distinct concept from
this package's inline values above — positionally to `TConfig`'s single
public constructor, then constructs `TProfile` from that `TConfig`:

```csharp
public enum RepositoryKind
{
    Player,
    Game,
}

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

**Inline values vs. profile configuration arguments — never the same
thing.** Inline values (`[Compose(42, "widget")]`) bind to the **test
method's own parameters**. Profile configuration arguments
(`[Compose<TProfile, TConfig>(...)]`) bind to **`TConfig`'s
constructor**, which builds the profile applied to the whole row — they
never bind to the test method's parameters, all of which are composed in
full under this attribute form.

**Prefer the strongest attribute-legal type for each argument.**
`params object?[]` is a binding mechanism forced by C#'s
attribute-argument-must-be-a-compile-time-constant rule, not a license to
design `TConfig` around magic strings — use an `enum` for a finite choice
(`RepositoryKind.Player`, not `"Player"`), `typeof(...)` for a CLR type, a
`bool`/numeric value where that's already the real meaning.

**Constructor contracts are narrow and deterministic, not "best match."**
`TConfig` must have exactly one public constructor; `TProfile` must have
exactly one public constructor accepting exactly one `TConfig`-typed
parameter. Either shape being missing or ambiguous is a clear, cached
`CompositionException` — computed once per attribute instance, never on
the per-row path. See
[Troubleshooting: Common Errors](../troubleshooting/common-errors.md) for
each specific message. This is a deliberate tradeoff:
`[Compose<TProfile>]`'s `TProfile : ICompositionProfile, new()` constraint
rejects an invalid profile type at **compile time**; this form's
constructor-shape checks can only happen at runtime, since "has a
constructor accepting exactly this type" isn't expressible as a C# generic
constraint.

## Using xUnit fixtures in profiles

When a composed value needs something xUnit already initialized for you —
a class, collection, or assembly fixture (a `WebApplicationFactory<Program>`,
a live database connection, a real handler) — call
`TestContext.Current.RequireFixture<TFixture>()` **inside a `Register`
factory** and register what it returns:

```csharp
using Xunit;

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

public sealed record ApiClient(HttpClient Client) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class ApiTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Register(
        () => new ApiClient(
            TestContext.Current.RequireFixture<WebAppFixture>().Factory.CreateClient()));
}

public sealed class ApiTests : IClassFixture<WebAppFixture>
{
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task Client_reaches_the_fixture(ApiClient client)
    {
        await using (client)
        {
            client.Client.BaseAddress.Should().NotBeNull();
        }
    }
}
```

Three things to know:

- **Read it inside the `Register` factory.** Compono composes rows at test
  *execution* time, so the factory — invoked while xUnit is running the
  test — sees a live `TestContext.Current` with its fixtures already
  initialized, and it runs per row. `TestContext.Current` is ambient and
  available during profile construction and `Configure` too, but a profile
  instance can outlive a single row (under JIT, `Configure` runs once per
  attribute instance and any captured value is reused for every row that
  instance composes). Reading inside the factory avoids capturing stale or
  cross-test state and behaves the same on both engines.
- **Exact-type matching only.** xUnit's `GetFixture` returns `null` when
  the requested exact fixture type isn't available (a missing fixture, or a
  base-type/interface request against a concrete fixture);
  `RequireFixture<TFixture>()` throws a descriptive
  `InvalidOperationException` instead of returning `null`.
- **It never waits.** If the fixture hasn't finished initializing, the
  call throws immediately instead of blocking. Declare the fixture
  concretely (`IClassFixture<T>`, `ICollectionFixture<T>`, or
  `[assembly: AssemblyFixture(typeof(T))]`) so xUnit initializes it before
  the test runs.

`Compono.XunitV3.Aot` exposes this same method in namespace
`Compono.XunitV3.Aot`. See the
[Use xUnit Fixture Values in a Profile](../cookbook/use-xunit-fixture-values-in-a-profile.md)
recipe, and [ADR-0069](../adr/0069-xunitv3-require-fixture-accessor.md) for
the design. Compono doesn't own or dispose composed values — dispose what
you create (above, the fixture disposes its `WebApplicationFactory` and the
test disposes the `ApiClient`). The fixture itself stays xUnit's, and
`RequireFixture` never disposes it or changes its lifetime.

**No ambient `TestContext`?** `RequireFixture` reads
`TestContext.Current`, so it only works under a running xUnit test. To stay
framework-neutral, take the fixture as a profile constructor argument and
build the composer by hand — a profile such as
`FixtureBackedApiProfile(WebAppFixture fixture)` that registers
`new ApiClient(fixture.Factory.CreateClient())` from its own field:

```csharp
await using var fixture = new WebAppFixture();
await fixture.InitializeAsync();

var composer = Composer.Create(b => b.AddProfile(new FixtureBackedApiProfile(fixture)));
await using var client = composer.Create<ApiClient>();
```

That works with any test framework (the caller supplies the fixture and
owns disposal) but does not use attribute-driven parameter composition.

## What it deliberately doesn't do

- **No stacking distinct Compose-family attributes on one method.** A test
  needing several inline rows *plus* composed parameters in each — the
  AutoFixture idiom of stacking multiple `[InlineAutoData(...)]` instances
  — has no direct equivalent here. Two different Compose-family attribute
  types (e.g. `[Compose]` and `[Compose<ProfileA>]`) compile without
  complaint, but `BindingPlan.ValidateSignature` throws a
  `CompositionException` at data-binding time once it sees more than one
  applied to the same method. Only the exact same closed attribute type
  twice is a compiler error (`AllowMultiple = false`). If you need this
  shape, pick one Compose-family attribute per method and supply the
  varying rows another way (e.g. inline `[Theory]`/`[InlineData]` rows for
  the parts that need no composition, per
  [Migrating from AutoFixture](../migrating-from-autofixture.md)).
  Composed theory rows (one method, many rows, each with its own profile
  configuration and expected values) are now decided by
  [ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md)
  (`[ComposeData]`, issue #163). That decision is accepted but
  **isn't implemented yet**. This section describes current behavior.
- **No AutoFixture-style fixture object.** There's nothing analogous to
  AutoFixture's `IFixture` — configuration lives in a profile
  (`[Compose<TProfile>]`), applied per test method, not a shared mutable
  object. (This is unrelated to **xUnit** fixtures, which are supported —
  see [Using xUnit fixtures in profiles](#using-xunit-fixtures-in-profiles)
  above.)

## Next

- [Share a Value Across a Test](../how-to/share-a-value-across-a-test.md)
- [Use Profiles](../how-to/use-profiles.md)
- [Migrating from AutoFixture](../migrating-from-autofixture.md) — the
  full `AutoDataAttribute`/`InlineAutoDataAttribute` mapping, from a real
  migration.
