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
- **`[ComposeData]` / `[ComposeData<TProfile>]` / `[ComposeData<TProfile, TConfig>]`** —
  one theory method, many rows read from a `[MemberData]`-style member, each
  row composed in its own scope with its own profile configuration and
  expected values. See
  [Composed theory rows](#composed-theory-rows-composedata) below.

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

## Composed theory rows (`[ComposeData]`)

`[Compose]` composes **one** row: each parameter is either bound from this
attribute's own inline values or composed. When a `[Theory]` needs **many**
rows — each with its own profile configuration and its own expected values —
use the `[ComposeData]` attribute family instead. It reads rows from a public
static member on the test class (or a type you name), exactly the shape
xUnit's own `[MemberData]` expects, and composes every row in its own scope.
This is decided by
[ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md)
(issue #163).

Reach for `[ComposeData]` when the varying thing is *data* (a table of inputs
and expected values) **and** the collaborators still need composing — and in
particular when those collaborators differ per row (a distinct profile
configuration each row). When you only need composed collaborators and no
per-row data, plain `[Compose]` is the right tool. When you only need
constant values and no composition, xUnit's own `[InlineData]`/`[MemberData]`
still apply and may be mixed in (see
[Mixing with xUnit's own data attributes](#mixing-with-xunits-own-data-attributes)).

### The three forms

All three share the same row-source member and take its name as the
attribute's only constructor argument:

- **`[ComposeData(nameof(Member))]`** — every element of a row is a row value
  bound positionally to a method parameter; any parameter left over is
  composed.
- **`[ComposeData<TProfile>(nameof(Member))]`** — same binding, with
  `TProfile` (`where TProfile : ICompositionProfile, new()`) applied to every
  row (a fresh instance per row).
- **`[ComposeData<TProfile, TConfig>(nameof(Member))]`** — element 0 of
  **every** row is that row's `TConfig`; `TProfile` is built from it per row
  (`TProfile` must have exactly one public constructor accepting a single
  `TConfig`), and elements 1..n are that row's values bound positionally.

Two optional properties refine the source:

- **`MemberType`** — the type that declares the member. The default
  (`null`) is the test class itself, matching xUnit's `[MemberData]`
  convention. Set it when the member lives on another type.
- **`Seed`** — pins every row the attribute produces to one explicit root
  seed, the same contract as `[Compose(Seed = ...)]` (restricted to
  non-negative values). See [Failures, seeds, and row
  labels](#failures-seeds-and-row-labels) below.

### The row source

The member is resolved against `MemberType`, or the test class when it's
unset, and must be a **public static field, property, or parameterless
method** (a hidden/inherited static member is found too). Its return shape
may be any of:

- an `IEnumerable` collection — `TheoryData<…>`, `TheoryDataRow<…>[]`,
  `List<object?[]>`, `object?[]`, and so on (never a `string`);
- `Task<IEnumerable>` or `ValueTask<IEnumerable>`;
- `IAsyncEnumerable<TRow>`.

Each element of the collection is one theory row, classified the way xUnit
classifies its own data: an `ITheoryDataRow`, then an `object?[]`, then an
`ITuple` (flattened when it has eight or more elements). A `TheoryData<T…>`
is a *collection* of `TheoryDataRow<T…>` elements, not itself a row. A member
returning a single row instead of a collection, a `null` result, or a `null`
row is a `CompositionException`. Member arguments (a member taking parameters)
are not supported.

The three canonical examples — per-row configuration, a `[Shared]` row value,
and a fixture-backed row source — live in
`test/Compono.XunitV3.SampleTests/ComposeDataTests.cs` (mirrored under
`Compono.XunitV3.Aot.SampleTests`) and run as part of the test suite.

### Row layout and binding

Binding is **leading-positional** (the same algorithm `[Compose]`'s inline
values use — see [ADR-0022](../adr/0022-compono-xunit-package-design.md)):
row value `i` binds to test parameter `i`, left to right, and any parameters
after the row values are composed. To put a composed collaborator before a
row value, reorder the method's parameters so the row values come first.

For `[ComposeData<TProfile, TConfig>]`, element 0 is consumed as the row's
configuration and is **not** a row value; elements 1..n bind to parameters
0..n-1. Row values are validated **before** anything is composed:

- **No numeric widening and no conversion.** A row value must already be
  assignable to its parameter's type — an `int` row value does not satisfy a
  `long` parameter. This is the same strictness as `[Compose(42)]`, not
  xUnit's own permissive `TypeHelper` conversion.
- **Nullability is enforced.** A `null` row value is allowed only for a
  nullable value type (`int?`) or a `?`-annotated reference parameter; an
  oblivious reference parameter is treated as non-nullable on both engines.
  A nullable value type in a row is boxed, so `int?` comes through as either
  the underlying `int` or `null`.
- **Too many values is an error** — a row may not supply more values than the
  method has parameters.

### Per-row configuration

Under `[ComposeData<TProfile, TConfig>]`, each row supplies its own `TConfig`
as element 0, and Compono builds a **fresh `TProfile` instance per row** from
it (`new TProfile(rowConfig)`). This is the multi-row counterpart to
`[Compose<TProfile, TConfig>]`: instead of one call-site configuration for the
whole method, every row carries its own. The profile (and everything it
registers) is never shared across rows.

### Shared dependencies and `[Shared]`

A `[Shared]` parameter works the same as under `[Compose]`: it names the one
instance of its type that every other parameter in the *same row* resolving
that type receives. When a row also supplies a value at that position, that
value becomes the shared instance for the row (and is returned in its place);
otherwise Compono resolves it. Sharing is scoped to a single row — nothing is
reused across rows. See [Shared Values](../concepts/shared-values.md).

### Reading xUnit fixtures from a row member

Rows are read at **test execution time**, after xUnit has initialized the
test's fixtures, so the row member itself may call
`TestContext.Current.RequireFixture<TFixture>()` — a table whose contents come
from a class, collection, or assembly fixture is fully supported. A profile
applied to the same row can read the same fixture inside a `Register` factory.
See [Using xUnit fixtures in profiles](#using-xunit-fixtures-in-profiles)
above and the
[Use xUnit Fixture Values in a Profile](../cookbook/use-xunit-fixture-values-in-a-profile.md)
recipe.

### When the rows are read

`[ComposeData]` runs **only at execution time** — its
`SupportsDiscoveryEnumeration()` returns `false`, so xUnit delays building the
theory's test cases until it runs them. The member is read once, then every row
is composed, all before the first row executes. Nothing is cached from
discovery, and the member is not invoked during a discovery-only pass. (A row
source that isn't pure — it mutates state, reads a clock, or reads a fixture —
therefore behaves exactly as it would inside the test body.) Rows from an
`IAsyncEnumerable<TRow>` are enumerated with the test's cancellation token,
checked between rows.

### Skip, labels, traits, and other metadata

Every xUnit row-metadata member (`Skip`, `SkipUnless`, `SkipWhen`, `SkipType`,
`Label`, `TestDisplayName`, `Timeout`, `Explicit`, `Traits`, and — on xUnit
4.x — `DisableParallelization`) can be set both on the attribute and on each
row, merged by one fixed rule:

- **`Skip`/`SkipUnless`/`SkipWhen`/`SkipType` are treated as a group** — if a
  row sets its own skip, the row's whole group is used; otherwise the
  attribute's group (if the attribute sets a skip) is used; otherwise neither.
  A row's skip never inherits part of the attribute's condition.
- **`Label`, `TestDisplayName`, `Timeout`, `Explicit`** — the row's value
  wins, else the attribute's.
- **`Traits`** — the union of the row's and the attribute's pairs, always
  including the row's `Compono.Seed` trait.
- **`DisableParallelization`** (xUnit 4.x only) — the row's value, else the
  attribute's.

A `Label` gives the row a deterministic display name, which is also what the
row's label shows up as in a failure. Conditions named by `SkipUnless`/
`SkipWhen` at the attribute level are read from a public static `bool`
property on `SkipType`, or the test class when `SkipType` is unset.

### Disposal

Compono itself registers nothing for disposal — the same ownership boundary as
`[Compose]`. Under ordinary JIT xUnit, xUnit disposes any `IDisposable`/
`IAsyncDisposable` object passed as a **theory argument**; xUnit's Native AOT
core does not. To stay portable, **don't pass a fixture-owned disposable as a
theory parameter** (a value bound from a row or a composed parameter) — JIT
disposes every `IDisposable` theory argument, a `[Shared]` parameter included,
so a fixture-owned instance would be disposed by JIT but not by AOT. Dispose
what you create; let the fixture own anything longer lived.

### Failures, seeds, and row labels

Every failure — reading the member, building one row's profile, or composing a
row — throws a `CompositionException` that names the member (`Type.Member`). A
row failure also names the **zero-based row index** and the row's label when it
has one, and (per the rule below) a seed; a member-level failure has no row
index. The whole theory fails as a unit: there are no placeholder rows
and no custom invoker to keep the other rows running. The original cause is
preserved as the exception's `InnerException`, and any `Diagnostic` is carried
through.

The seed comes from **exactly one** source, with no override:

1. the attribute's `Seed` (pins every row; a negative value is rejected before
   anything runs);
2. else a profile's `WithSeed(...)` in `Configure` (also pins every row); or
3. else Compono mints a fresh, non-negative seed per row.

Setting **both** the attribute's `Seed` and a profile `WithSeed` is the core's
duplicate-configuration error, not an override. The failure's seed line depends
on how far the row got:

- a member-level failure (reading the member itself) has no seed line;
- an `R0` row failure **before** the row's `CreateRow` — row shape, `TConfig`
  selection, profile construction, `Configure`, or the composer build — prints
  `Seed: {n}` when the attribute's `Seed` was set, otherwise `Seed: not assigned
  (the failure occurred before the row's composition started)`;
- a failure from `CreateRow` on — including row-value validation and too many
  values — prints `Seed: {row.Seed}`.

To reproduce a failing row, paste that seed into the attribute's `Seed`.
See [Seed a Specific Failing Case for
Reproduction](../cookbook/seed-a-specific-failing-case-for-reproduction.md)
and [Determinism and Seeding](../concepts/determinism-and-seeding.md).

### Mixing with xUnit's own data attributes

`[ComposeData]` counts as part of the Compose family for exclusivity: at most
**one** `[Compose…]`/`[ComposeData…]` attribute may sit on a method (see
[What it deliberately doesn't do](#what-it-deliberately-doesnt-do)). xUnit's
own `[InlineData]`, `[MemberData]`, and `[ClassData]` are a separate concern —
they may coexist with `[ComposeData]` on the same method, are never composed,
and are never merged into its rows. Each xUnit data attribute contributes its
own independent test cases alongside Compono's.

### Native AOT

`Compono.XunitV3.Aot` supports the same three `[ComposeData]` forms and the
same `[Shared]` behavior as this package, so moving a `[ComposeData]` theory
from a JIT project to a Native AOT project changes only the `using` and the
referenced packages. A handful of shapes the JIT/reflection path can run but
generated Native AOT code cannot name or call are rejected at **compile time**
on AOT with a `CMP` diagnostic instead — see the
[`Compono.XunitV3.Aot` diagnostics](../packages/compono-xunitv3-aot.md#composedata-diagnostics-on-native-aot)
and the [diagnostics reference](../reference/diagnostics.md).

## What it deliberately doesn't do

- **No stacking distinct Compose-family attributes on one method.** Two
  different Compose-family attribute types (e.g. `[Compose]` and
  `[Compose<ProfileA>]`) compile without complaint, but
  `BindingPlan.ValidateSignature` throws a `CompositionException` at
  data-binding time once it sees more than one applied to the same method.
  Only the exact same closed attribute type twice is a compiler error
  (`AllowMultiple = false`). The multi-row answer is a single
  [`[ComposeData]`](#composed-theory-rows-composedata) — one method, many
  rows, each composed in its own scope — never stacked attributes. See
  [ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md).
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
