# Migrating from AutoFixture to Compono

**Status:** Phase 1 complete (test-kit migration done, all 73 `cosmere-tracker`
tests passing under Compono — the 72 migrated tests plus one new capability
test for the `ClientTestProfile`/`IHttpClientProvider` pattern); post-migration
metrics/classification still to come in Phase 2/3

This guide is a living deliverable of Milestone 7's dogfooding pass
([ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md),
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md)) — it exists to help a
real AutoFixture user move to Compono, drawn from an actual migration
(`ncipollina/cosmere-tracker`'s `test/Cosmere.Tracker.TestKit` and its three
consuming test projects), not synthetic examples.

For each concept, an entry covers: the AutoFixture approach, the Compono
approach, why the Compono approach was chosen, a better/equivalent/tradeoff
verdict, links to the relevant ADR(s)/research findings, and a real
before/after code example.

## Referencing Compono packages from a separate repository

`cosmere-tracker` is a sibling repo to `compono`, not part of this monorepo,
with its own GitHub-hosted CI that has no access to a local `compono`
checkout to pack from. A local NuGet feed (packing `compono`'s source
on-demand, mirroring `test/Compono.XunitV3.SampleTests`' own pattern) was
tried first and rejected — it only works on a machine that happens to have
both repos checked out side by side, and silently breaks the moment this
work is pushed and CI tries to restore it. Instead, `cosmere-tracker`'s
`Directory.Packages.props` pins `Compono`/`Compono.XunitV3`/
`Compono.NSubstitute`/`Compono.Bogus` to a real published prerelease from
nuget.org (`0.1.0-alpha.33` at time of writing) — `compono`'s own
`publish-preview.yaml` publishes a fresh `alpha` prerelease on every
non-docs-only push to its `main` branch (it has `paths-ignore: docs/**,
README.md`, so a docs-only PR merge — like this one — does *not* trigger a
new publish), so a recent one is available whenever `main`'s actual source
changes. Bump all four versions together when a newer alpha is needed; there
is no local feed or pack step to run.

## `AutoDataAttribute`/`InlineAutoDataAttribute` and customizations

AutoFixture's `[AutoData]`/`[InlineAutoData]` pair, wrapped in
`cosmere-tracker`-specific subclasses that baked in an `IFixture` factory
(`CosmereTrackerAutoDataAttribute`, `ClientAutoDataAttribute`,
`EndpointAutoDataAttribute`, `PersistenceAutoDataAttribute` — one per test
project, each combining `BaseFixtureFactory` with its own customization).
Compono's idiomatic shape is a single `[Compose<TProfile>]` attribute per
test method, with the profile doing what the custom `AutoDataAttribute`
subclass used to do implicitly
([ADR-0022](../adr/0022-compono-xunit-package-design.md)).

**Better** — every one of the four custom `AutoDataAttribute` subclasses was
removed entirely; nothing replaced them as a named type, since
`[Compose<TProfile>]` is Compono.XunitV3's own attribute, applied directly.
The four wrapper classes existed purely to bind a specific `IFixture` factory
to an attribute; Compono has no equivalent indirection to wrap.

**Positive finding — a pure-inline `[Theory]` needs no Compono attribute at
all.** `TextNormalizerTests` had 7 `[InlineCosmereTrackerAutoData(...)]` rows
where every parameter was supplied inline (no AutoFixture-composed value was
ever used) — AutoFixture still required routing through the
`InlineAutoDataAttribute` subclass regardless. Compono's `[Compose]` only
applies where a parameter is actually composed; when every value is inline,
plain xUnit `[InlineData]` is correct and simpler:

```csharp
// Before (AutoFixture)
[Theory]
[InlineCosmereTrackerAutoData(null!, "")]
[InlineCosmereTrackerAutoData("Kaladin Stormblessed", "kaladin-stormblessed")]
public void Normalize_ProducesExpected(string? input, string expected) { ... }

// After (Compono) — no Compono attribute needed at all
[Theory]
[InlineData(null, "")]
[InlineData("Kaladin Stormblessed", "kaladin-stormblessed")]
public void Normalize_ProducesExpected(string? input, string expected) { ... }
```

**Real limitation found — stacking more than one Compose-family attribute on
one method has no direct Compono equivalent.** A test that needs *both*
several distinct inline rows *and* one or more genuinely composed parameters
in each row (AutoFixture handles this by stacking `[InlineAutoData(...)]`
instances) can't be expressed this way in Compono today. The failure mode is
more specific than "fails to compile," though: `[AttributeUsage(AllowMultiple
= false)]` is enforced by the compiler per *exact* attribute type, so two
*different* Compose-family types (e.g. `[Compose]` plus `[Compose<MyProfile>]`,
or two differently-closed `[Compose<TProfile>]` forms) compile without
complaint — nothing at the type-attribute level stops stacking them. Compono.
XunitV3's own `BindingPlan.ValidateSignature` (`src/Compono.XunitV3/Binding/BindingPlan.cs`)
explicitly counts the whole Compose-family regardless of closed type and
throws a `CompositionException` at data-binding time (when the test's data is
actually generated), not at compile time. Only two instances of the *exact
same* closed attribute type are a genuine compiler error, via
`AllowMultiple = false` on that one type. `cosmere-tracker`'s migration
didn't hit a real test needing the multi-row-plus-composed-parameter
combination (`TextNormalizerTests`' rows were pure-inline, per above), so
this is recorded as a discovered constraint, not a blocking gap — but it is a
real further finding for Milestone 7's evidence beyond the three named gaps
(candidate roadmap item; needs its own design pass, not decided here per
ADR-0029's evidence-driven restraint).

For the common "one row, some inline, some composed" case, `[Compose]` works
exactly as expected — see `CursorEncoderTests`:

```csharp
// Before
[Theory]
[CosmereTrackerAutoData]
public void EncodeDecode_RoundTrips(Guid id) { ... }

// After
[Theory]
[Compose]
public void EncodeDecode_RoundTrips(Guid id) { ... }
```

## `ICustomization` and composition profiles

AutoFixture's `ICustomization` versus Compono's `ICompositionProfile`
([ADR-0018](../adr/0018-composition-profiles.md)). `cosmere-tracker`'s
`CosmereTrackerCustomization` turned out to be an empty stub (commented-out
examples only, never actually customized anything) — there was no real
intent to port:

```csharp
// Before — CosmereTrackerCustomization.cs, entirely commented-out
public sealed class CosmereTrackerCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        // Add specimen builders for domain objects as needed
        // Example:
        // fixture.Customizations.Add(new QuestionSpecimenBuilder());

        // Freeze common dependencies
        // Example:
        // fixture.Freeze<ILogger>();
    }
}

// After — deleted outright, nothing replaced it (there was no real
// customization to carry forward)
```

It was deleted outright rather than migrated to a profile; `SharedCustomization`
(in `Cosmere.Tracker.Shared.TestKit`), by contrast, did real work (registered
four domain-item specimen builders) and became `SharedTestKitProfile :
ICompositionProfile`, composed into each consuming project's own profile via
`builder.AddProfile<SharedTestKitProfile>()`:

```csharp
// Before — SharedCustomization.cs
public sealed class SharedCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new CharacterItemSpecimenBuilder());
        fixture.Customizations.Add(new BookItemSpecimenBuilder());
        fixture.Customizations.Add(new WorldItemSpecimenBuilder());
        fixture.Customizations.Add(new EdgeItemSpecimenBuilder());
    }
}

// After — SharedTestKitProfile.cs (see the specimen-builder section below
// for what each Register<T> call itself became)
public sealed class SharedTestKitProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.UseBogus<BookItem>(ConfigureBookItem);
        builder.UseBogus<CharacterItem>(ConfigureCharacterItem);
        builder.UseBogus<WorldItem>(ConfigureWorldItem);
        builder.Register(CreateBookCharacterEdge);
        // ...remaining edge-item registrations
    }
}
```

## `AutoNSubstituteCustomization` (`ConfigureMembers`) — gap 2

`BaseFixtureFactory` applied `AutoNSubstituteCustomization { ConfigureMembers
= true }` — every generated substitute had its members auto-configured
(sensible return values, including a recursively-constructed object for a
`Task<T>`-returning member) rather than returning `default`. Compono's
`Compono.NSubstitute` ([ADR-0025](../adr/0025-compono-nsubstitute-package-design.md))
deliberately returns a bare `Substitute.For<T>()` — no auto-configuration.

**Real evidence, both directions.** Migrating away from `ConfigureMembers`
surfaced two distinct patterns:

- **Zero workaround cost, most call sites.** ~30 endpoint tests
  (`ListWorldsEndpointTests`, etc.) took `[Frozen] ICosmereTrackerRepository
  repo` purely to get "a substitute for this interface" — the repo was passed
  explicitly to `Factory.Create<TEndpoint>(repo)` and never reused elsewhere
  in the same composition. Compono composes an interface parameter to a
  substitute automatically once `UseNSubstitute()` is active — no annotation
  needed at all:
  ```csharp
  // Before
  public async Task HandleAsync_WhenSortInvalid_DoesNotCallRepo(
      [Frozen] ICosmereTrackerRepository repo) { ... }

  // After — [Frozen] wasn't sharing anything; plain composition suffices
  public async Task HandleAsync_WhenSortInvalid_DoesNotCallRepo(
      ICosmereTrackerRepository repo) { ... }
  ```
  This is a genuinely simpler result than AutoFixture's own idiom, not just an
  equivalent one — `[Frozen]` read as "this is shared," which was never true
  here.
- **Real sharing, `CosmereTrackerRepository` persistence tests.** Here
  `[Frozen] IDynamoPartiqlClient partiql` genuinely mattered: `sut` (a
  concrete `CosmereTrackerRepository`, constructor-injecting
  `IDynamoPartiqlClient`) is auto-constructed by the fixture/composition, and
  the same substitute instance needs to be visible as a test parameter for
  stubbing. Compono's explicit `[Shared] IDynamoPartiqlClient partiql`
  parameter is the direct, low-cost equivalent — same shape, same intent,
  just spelled out:
  ```csharp
  // Before
  public async Task GetWorldByIdAsync_UsesPkSkPartiql(
      [Frozen] IDynamoPartiqlClient partiql,
      CosmereTrackerRepository sut,
      WorldItem world) { ... }

  // After
  public async Task GetWorldByIdAsync_UsesPkSkPartiql(
      [Shared] IDynamoPartiqlClient partiql,
      CosmereTrackerRepository sut,
      WorldItem world) { ... }
  ```
- **Where `ConfigureMembers` silently mattered — a real regression caught by
  the test suite itself.** Two tests
  (`ListWorldsAsync_WhenSortEmpty_DefaultsToName`,
  `ListCharactersAsync_WhenSortEmpty_DefaultsToName`) called
  `sut.ListWorldsAsync(...)`/`sut.ListCharactersAsync(...)` and asserted
  `NotThrowAsync()`, with **no explicit stub at all** on
  `partiql.ExecuteAsync(...)`. Under AutoFixture's `ConfigureMembers = true`,
  the auto-configured substitute recursively produced a non-null `PartiqlPage`
  for any unstubbed call, so the repository's internal use of the result never
  threw. Under Compono's bare `Substitute.For<T>()`, the same unstubbed call
  returns `Task.FromResult<PartiqlPage>(null)` (NSubstitute's own default for
  an unconfigured `Task<T>`-returning member), and the repository's own code
  throws `NullReferenceException` dereferencing it. This is exactly the gap-2
  evidence ADR-0029 asked for — a call site that genuinely relied on
  AutoFixture's auto-configuration — fixed by adding the explicit stub the
  test always should have had:
  ```csharp
  public async Task ListWorldsAsync_WhenSortEmpty_DefaultsToName(
      [Shared] IDynamoPartiqlClient partiql,
      CosmereTrackerRepository sut)
  {
      partiql
          .ExecuteAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AttributeValue>>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .ReturnsForAnyArgs(new PartiqlPage([], null));
      // ...
  }
  ```
  **Verdict:** this is a real, material workaround cost (an explicit stub a
  test previously didn't need to write) — but arguably a correctness
  improvement, not just friction: the test's true dependency on
  `ExecuteAsync`'s return shape was previously hidden by auto-configuration,
  and is now visible in the test body. Full classification (acceptable
  alternative vs. intentional design difference) is Phase 3's job; this
  section records the evidence.

## Recursion behaviors (`OmitOnRecursionBehavior` vs. fail-fast) — gap 3

`BaseFixtureFactory` swapped AutoFixture's default `ThrowingRecursionBehavior`
for `OmitOnRecursionBehavior`. **No construction-cycle failure was ever
triggered during this migration** — none of `cosmere-tracker`'s composed
types (`BookItem`/`CharacterItem`/`WorldItem`/edge items,
`CosmereTrackerRepository`) form a self-referencing graph; edges reference
other entities by string id, not by object reference. This is itself the
gap-3 finding for Phase 1: **zero observed frequency** for this migration.
Compono's fail-fast `CompositionException` with a path-annotated message
([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md))
was never exercised, positively or negatively, by this codebase.

## Specimen builders and registrations

AutoFixture's `ISpecimenBuilder`/`IRequestSpecification` pattern versus
Compono's `CompositionBuilder.Register<T>(Func<ICompositionContext, T>)`
([ADR-0024](../adr/0024-public-provider-extensibility-model.md)). Every
domain-item specimen builder in `Cosmere.Tracker.Shared.TestKit`
(`BookItemSpecimenBuilder`, `CharacterItemSpecimenBuilder`,
`WorldItemSpecimenBuilder`, `EdgeItemSpecimenBuilder`) became a
`Register<T>` call inside `SharedTestKitProfile`, one per type — direct,
equivalent translation, no `NamedRequest`/`ParameterInfo` pattern-matching
needed since Compono's registration is keyed by exact type, not by
inspecting the request shape by hand. `BookItem`/`CharacterItem`/`WorldItem`
went through `UseBogus<T>()` instead (see the `Compono.Bogus` section
below); edge items have no semantic string fields, so they stay a plain
`Register<T>` factory — `EdgeItemSpecimenBuilder`'s `BookCharacterEdgeItem`
case is representative of all six:

```csharp
// Before — EdgeItemSpecimenBuilder.cs (one case of a six-way switch handling
// all edge-item types plus their Type/ParameterInfo/NamedRequest shapes)
public sealed class EdgeItemSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        return request switch
        {
            Type t when t == typeof(BookCharacterEdgeItem) => CreateBookCharacterEdge(null, context),
            ParameterInfo p when p.ParameterType == typeof(BookCharacterEdgeItem) => CreateBookCharacterEdge(p.Name, context),
            NamedRequest { InnerRequest: Type t } nr when t == typeof(BookCharacterEdgeItem) => CreateBookCharacterEdge(nr.Name, context),
            // ...five more cases, one per remaining edge-item type
            _ => new NoSpecimen(),
        };
    }

    private static BookCharacterEdgeItem CreateBookCharacterEdge(string? name, ISpecimenContext context)
    {
        var seed = name ?? context.Create<string>();
        return new BookCharacterEdgeItem
        {
            Id = Guid.NewGuid().ToString("D").ToLowerInvariant(),
            BookId = DeterministicGuid.CreateBookId($"testkit:{seed}:book"),
            CharacterId = DeterministicGuid.CreateCharacterId($"testkit:{seed}:char"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
        };
    }
}

// After — SharedTestKitProfile.cs (one Register<T> call per edge type, no
// request-shape pattern-matching needed at all)
private static BookCharacterEdgeItem CreateBookCharacterEdge(ICompositionContext context) => new()
{
    Id = NewId(context),
    BookId = NewId(context),
    CharacterId = NewId(context),
    CreatedAt = Timestamp(context),
    UpdatedAt = Timestamp(context),
};
```

**One AutoFixture-era specimen builder had zero real call sites, but was
migrated anyway.** `HttpClientSpecimenBuilder`/`HttpClientSpecification`
(gap 1's original named case) — `ClientAutoDataAttribute`/
`InlineClientAutoDataAttribute` were never used by any of the three
consuming test projects; a repo-wide search found call sites only inside
`Cosmere.Tracker.TestKit`'s own definition files. "Zero observed frequency"
is still real gap-1 evidence (the rubric's question 1), but this specific
capability (a frozen, substitute `HttpMessageHandler` behind a configured
`HttpClient`) is one the repo owner explicitly wants preserved for future
HTTP-client tests, not dropped just because nothing uses it *yet* — so it
was ported, not deleted, and is documented here as a real, working pattern.

**Real limitation found: `HttpClient` can't be composed directly as a test
parameter at all**, regardless of any registration. `Compono.Generators`'
constructor-selection validation (diagnostic `CMP0001`,
[ADR-0002](../adr/0002-constructor-selection-algorithm.md)) inspects a
composed parameter's type at *compile time*, purely from its constructor
count on the Roslyn symbol — it has no visibility into any *runtime*
`CompositionBuilder.For<T>().Use(...)` rule that would actually construct
the type. `HttpClient` has 3 accessible constructors, so composing it
directly fails with `CMP0001: 'System.Net.Http.HttpClient' has 3 accessible
constructors and no way to disambiguate them`, even with an explicit rule
registered for it. ADR-0002 anticipated needing a `[CompositionConstructor]`
disambiguation attribute for exactly this case but never shipped one — this
is a genuine, currently-unfilled gap in Compono itself, not a migration
mistake. The workaround: compose an interface instead of `HttpClient`
directly — an interface is always treated as a provider-resolved leaf, so it
never reaches constructor-selection:

```csharp
// Cosmere.Tracker.TestKit/Http/IHttpClientProvider.cs
public interface IHttpClientProvider
{
    HttpClient Create();
}

internal sealed class HttpClientProvider(HttpMessageHandler handler) : IHttpClientProvider
{
    public HttpClient Create() => new(handler) { BaseAddress = new Uri("https://localhost/") };
}

// Cosmere.Tracker.TestKit/Profiles/ClientTestProfile.cs
public sealed class ClientTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.Register<HttpMessageHandler>(_ => Substitute.For<HttpMessageHandler>());
        builder.Register<IHttpClientProvider>(context => new HttpClientProvider(context.Resolve<HttpMessageHandler>()));
    }
}

// usage
[Theory]
[Compose<ClientTestProfile>]
public async Task Client_UsesTheSharedHandlersConfiguredResponse(
    [Shared] HttpMessageHandler handler,
    IHttpClientProvider clientProvider)
{
    handler.ReturnsResponse(HttpStatusCode.OK, new { ok = true });
    var client = clientProvider.Create();
    var response = await client.GetAsync("/ping", TestContext.Current.CancellationToken);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

**Verdict: a real workaround cost, not a clean equivalent.** AutoFixture's
`Freeze<HttpMessageHandler>()` + `HttpClientSpecimenBuilder` let a test just
ask for `HttpClient` directly; Compono needs an extra interface + wrapper
class because of `CMP0001`'s compile-time-only view. This is itself
Milestone 7 evidence beyond gap 1's original framing — roadmap-candidate
material for Compono itself, tracked for Phase 3's classification, not
designed here per ADR-0029's evidence-driven restraint. The candidate is
**support for disambiguating construction of a registered/external ambiguous
type generically**, not specifically "ship the `[CompositionConstructor]`
attribute ADR-0002 anticipated" — `HttpClient` is a BCL type `cosmere-tracker`
doesn't own, so a source attribute on its constructor was never going to be
the fix for *this* case regardless of whether that attribute ships; whatever
mechanism Phase 3's own design pass picks has to work for a type the
consumer can't annotate, which the originally-anticipated attribute
mechanism doesn't cover on its own.

- `DynamoDbResponseSpecimenBuilder` — composed a `PartiqlPage` as a test
  parameter, matching a naming convention (`"empty"`/`"multiple"`/`"paged"`
  substrings in the requesting parameter's name) to decide its shape. No test
  in `Cosmere.Tracker.Shared.Tests` ever requested a `PartiqlPage` this way;
  every real usage constructs `PartiqlPage` directly in the test body and
  stubs `IDynamoPartiqlClient.ExecuteAsync` explicitly. Dropped entirely.

`DynamoDbOptionsSpecimenBuilder`, by contrast, had a real, load-bearing call
site — `CosmereTrackerRepository`'s constructor requires `IOptions<DynamoDbOptions>`
whenever `sut` is composed — and became a straightforward
`builder.Register<IOptions<DynamoDbOptions>>(() => ...)` call in
`PersistenceTestProfile`.

## `Compono.Bogus`: realistic domain data

`cosmere-tracker`'s AutoFixture kit had no equivalent concept — it only ever
produced anonymous specimens. Phase 0 identified the candidate members:
`BookItem.Title`, `CharacterItem.Name`, `WorldItem.Name`, `WorldItem.SystemName`.

**Real limitation found: exact member-name matching can't disambiguate two
types that share a member name with different semantics.**
`BogusMemberNameProvider` matches purely on `request.Name` (the member/
parameter name), regardless of the requesting type
(`src/Compono.Bogus/BogusMemberNameProvider.cs`). `CharacterItem.Name` (a
person's name) and `WorldItem.Name` (a place name) share the literal member
name `"Name"` but need different generators — a single package-wide
`BogusOptions.AddAlias("Name", ...)`/`AddConvention("Name", ...)` cannot
serve both correctly. This is exactly the kind of domain `Compono.Bogus`'s
built-in allowlist wasn't designed around (ADR-0029's own framing), and a
genuine finding from dogfooding it against a non-person-centric domain.

**Resolution used: `UseBogus<T>()`, Compono.Bogus's real whole-object
sugar — an earlier attempt at this section wrongly bypassed it.** The first
version of this migration hand-rolled `Register<T>(context => { var faker =
new Faker<T>().UseSeed(context.DeriveSeed()); ...; return faker.Generate();
})` per type, on the claim that `UseBogus<T>(Action<Faker<T>>)`'s
`configureFaker` callback has no access to the resolving
`ICompositionContext`. That claim was **wrong**, caught in PR review:
`CompositionBuilderExtensions.UseBogus<T>` already does exactly
`new Faker<T>(locale).UseSeed(context.DeriveSeed())` internally, *before*
invoking `configureFaker` — so every `RuleFor` inside the callback already
runs against a `Faker<T>` seeded from the composition's own context. There
was never a reason to bypass the package's own integration point; doing so
meant Phase 1 recorded "successful dogfooding" without ever calling a
`Compono.Bogus` API. Corrected to use `builder.UseBogus<T>(...)` directly:

```csharp
public void Configure(CompositionBuilder builder)
{
    builder.UseBogus<BookItem>(ConfigureBookItem);
    // ...
}

private static void ConfigureBookItem(Faker<BookItem> faker)
{
    faker.RuleFor(b => b.Id, f => f.Random.Uuid().ToString());
    faker.RuleFor(b => b.Title, f => f.Commerce.ProductName());
    faker.RuleFor(b => b.TitleNormalized, (_, b) => TextNormalizer.Normalize(b.Title));
    faker.RuleFor(b => b.CreatedAt, f => f.Date.PastOffset(2, ReferenceDate).ToString("O"));
    faker.RuleFor(b => b.UpdatedAt, (f, b) => DateTimeOffset.Parse(b.CreatedAt!).AddMinutes(f.Random.Int(0, 1440)).ToString("O"));
}

// Bogus's Date.PastOffset defaults its refDate to the current system clock, which would make
// CreatedAt depend on when the test actually runs rather than just the seed - a fixed reference
// date keeps it fully seed-deterministic (compono PR #40 review).
private static readonly DateTimeOffset ReferenceDate = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
```

`TitleNormalized`/`UpdatedAt` both use Bogus's own sibling-property access
(the `(f, instance)` `RuleFor` overload) to stay consistent with `Title`/
`CreatedAt` — no direct `ICompositionContext` access needed anywhere in the
callback, since the seeded `Faker<T>` instance (`f`) is already enough for
every value these types need, including `Id` (`f.Random.Uuid()`) and the
timestamps (`f.Date.PastOffset(2, ReferenceDate)`, pinned to a fixed
reference date rather than `Date.PastOffset`'s own current-clock default).
This exercises Compono.Bogus's actual public API and its determinism
contract ([ADR-0026](../adr/0026-deterministic-seed-derivation-for-providers.md))
exactly as designed — no workaround needed at all, once the (incorrect)
assumption about context access was dropped. Edge items (`BookCharacterEdgeItem`
etc.) have no semantic string fields, so they stay a plain `Register<T>`
factory — `Compono.Bogus` has nothing to add there.

**Recommendation:** `Compono.Bogus`'s `UseBogus<T>(Action<Faker<T>>)` sugar
is a clean fit for `cosmere-tracker`'s semantic string fields (`Title`,
`Name`, `SystemName`) — a genuine win over the AutoFixture-era specimen
builders, not just a tradeoff: fewer lines, no hand-rolled seeding, and
Bogus's own realistic generators (`Commerce.ProductName()`, `Name.FullName()`,
`Address.Country()`) read better than the old `"book-{hash}"`-style
placeholder strings. Recommend `UseBogus<T>()` as the default for any new
domain type with a semantic string field.

## Reflection-based NSubstitute stubbing (`HttpMessageHandlerExtensions`)

Not an AutoFixture concept — a plain NSubstitute extension method
(`ReturnsResponse`, using `BindingFlags.NonPublic` reflection to stub
`HttpMessageHandler`'s protected `SendAsync`), unrelated to the AutoFixture→
Compono migration. Left unchanged: it has no AutoFixture dependency to
migrate away from. Noted during Phase 1's initial call-site audit as having
zero real callers at that point; now exercised by `ClientTestProfileTests`
(see the `HttpClient`/`CMP0001` discussion above) alongside the newly-ported
`ClientTestProfile`.

## What disappeared entirely vs. what was merely replaced

Per [ADR-0029 Amendment 2](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-2-2026-08-02-removed-concepts-get-their-own-explicit-inventory-not-just-a-count),
these are two distinct categories, not one — a concept that disappeared with
*nothing* taking its place represents a real drop in conceptual complexity;
a concept replaced one-for-one by a Compono equivalent does not, even though
the AutoFixture-era name is gone either way.

**Removed entirely — no replacement concept exists:**

| Concept | Why nothing replaced it |
|---|---|
| `IFixture` | Composition is per-test-method via `[Compose<TProfile>]` — there's no fixture object at all, configured or otherwise |
| `IRequestSpecification` | Compono's registration is keyed by exact type; no separate request-matching type is needed |
| `NamedRequest` | `Register<T>` factories don't need to pattern-match the requesting parameter's shape |
| `DynamoDbResponseSpecimenBuilder` | Zero real call sites (see above) — dropped, not ported |
| `BaseFixtureFactory` *(the factory abstraction itself)* | Nothing — there's no single place bundling multiple fixture behaviors together anymore; see the split below for what happened to the two behaviors it configured |

**Replaced one-for-one with a Compono equivalent:**

| Concept | Compono equivalent |
|---|---|
| `ICustomization` | `ICompositionProfile` (only one project actually had real customization logic to carry over) |
| `ISpecimenBuilder` | `CompositionBuilder.Register<T>(...)` inside a profile |
| Custom `AutoDataAttribute`/`InlineAutoDataAttribute` subclasses — four pairs, eight classes total (`CosmereTrackerAutoDataAttribute`/`InlineCosmereTrackerAutoDataAttribute`, `ClientAutoDataAttribute`/`InlineClientAutoDataAttribute`, `EndpointAutoDataAttribute`/`InlineEndpointAutoDataAttribute`, `PersistenceAutoDataAttribute`/`InlinePersistenceAutoDataAttribute`) | `[Compose]`/`[Compose<TProfile>]` (Compono.XunitV3's own attribute, used directly — no per-project wrapper) |
| `BaseFixtureFactory`'s `AutoNSubstituteCustomization { ConfigureMembers = true }` setup | `builder.UseNSubstitute()` (one line in each profile) — see gap 2 above; this is only *one* of the two behaviors `BaseFixtureFactory` bundled, not the whole factory |
| `HttpClientSpecimenBuilder`/`HttpClientSpecification`/`ClientAutoDataAttribute` | `ClientTestProfile` + `IHttpClientProvider` (ported despite zero real call sites at migration time — an explicit request to keep this capability for future tests; see above) |
| `SpecimenBuilderHash` | `Bogus.Randomizer`/`Faker<T>.UseSeed` (Compono.Bogus's own deterministic-seed mechanism replaces the hand-rolled SHA256 hash-prefix helper) |

`BaseFixtureFactory`'s *other* behavior — swapping in `OmitOnRecursionBehavior`
— has no Compono equivalent at all, replaced or otherwise: gap 3 (above)
found zero construction-cycle failures during this migration, so there was
nothing to port. Splitting `BaseFixtureFactory` across both tables (as the
removed abstraction) and this one (for the one behavior of its two that maps
onto a Compono call) is the accurate accounting per Amendment 2 — treating
the whole factory as "replaced by `UseNSubstitute()`" would overstate what
that one line actually covers.

## Multi-tier fixture stacks

`cosmere-tracker`'s AutoFixture setup was layered across three tiers
(`Cosmere.Tracker.TestKit` → `Cosmere.Tracker.Shared.TestKit` → per-suite
local kits). The migrated Compono setup keeps the same three tiers, but each
tier is now a much thinner layer: `Cosmere.Tracker.TestKit` contributes far
less composition code after migration (`BaseFixtureFactory` and the
AutoFixture-era attributes were all deleted; the HTTP-client capability was
ported forward, not deleted, as `ClientTestProfile`/`IHttpClientProvider`
alongside the unrelated `HttpMessageHandlerExtensions` helper),
`Cosmere.Tracker.Shared.TestKit`
is one profile (`SharedTestKitProfile`) registering the four domain types,
and each consuming project's local profile (`EndpointTestProfile`,
`PersistenceTestProfile`) composes the shared profile plus its own
project-specific registration via `builder.AddProfile<SharedTestKitProfile>()`.
The tier count didn't collapse, but the amount of code living in the
lowest tier (`Cosmere.Tracker.TestKit`) dropped from ~185 AutoFixture-specific
lines to zero.
