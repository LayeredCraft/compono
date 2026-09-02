# [RESEARCH-0016] Async Composition Viability for Compono 1.0

**Status:** Research complete, Outcome C accepted in principle by the
requester. No ADR yet — scoped to research only, per the request that
produced it. Async composition is **not** a required pre-1.0 feature; the
purpose of this document is narrower: determine whether Compono 1.0's
current public contracts leave enough room to add genuinely useful async
composition/resolution later without breaking existing APIs or requiring
a major architectural rewrite.

**Revision note (post-acceptance corrections):** two corrections made
after initial review, both in place below and neither changing Outcome C:
(1) §5's first pass incorrectly claimed `await` cannot appear inside a
constructor-call argument list — that is wrong C# (`await` is legal there;
verified directly) — corrected while preserving the architectural findings
that don't depend on that error (async color propagation to the root,
compile-time invisibility of runtime registrations, the real interface/
member-shape reasons a template variant is still needed); (2) §5a is new —
the "new `Register<T>(Func<..,Task<T>>)` overload is unambiguous" claim in
§2/§6 was verified with an actual compile-only spike (8 call shapes,
including the adversarial `T = Task<Gadget>` case) rather than asserted
from overload-resolution intuition; result confirmed clean, 0 warnings/
errors, no ambiguity.

**Central question:** does freezing today's public API at 1.0 accidentally
foreclose a reasonable future async-composition design?

**Input taken as settled from RESEARCH-0015:** disposal and creation are
separable concerns — async disposal does **not**, by itself, justify
async creation (`CreateAsync<T>()`). `IAsyncDisposable` on a composed type
is not used as evidence anywhere in this research.

## 1. What "async composition" could mean — definitions used here

Eight distinct concepts, deliberately kept apart because they have
different answers:

1. **Asynchronous root creation** — `Composer.CreateAsync<T>()`-shaped:
   the top-level `Create<T>()` call itself becomes awaitable.
2. **Asynchronous nested dependency resolution** — a constructor
   parameter several levels deep in the graph needs `await` to produce.
   This is the hard case: it forces (1) to exist too, transitively,
   because a generated plan's `new T(...)` argument list can't contain an
   awaited value without becoming an async method itself (§5).
3. **Async registration factories** — `Register<T>(Func<ICompositionContext,
   Task<T>>)` or similar — a leaf-level authoring convenience distinct
   from (2): whether *this* factory is async, vs. whether *resolution as a
   whole* must become async to accommodate it.
4. **Async value providers** — the `ICompositionValueProvider` extension
   point growing an async counterpart.
5. **Async external-service resolution** — a provider or factory that
   calls out over the network/disk to produce a value (a token endpoint,
   a container startup).
6. **Async test-data generation** — bulk/streamed data generation
   (`CreateMany<T>()`-shaped) requiring async work per item or as a batch.
7. **Asynchronous initialization after synchronous construction** — a type
   constructed synchronously via a generated plan, then requiring a
   separate `await obj.InitializeAsync()` step before use. This is
   explicitly **not** "composition requires async" — it's a second,
   distinct lifecycle step outside the object graph, analogous to how
   xUnit's `IAsyncLifetime.InitializeAsync()` runs after a test class is
   already constructed (§4/§9).
8. **Asynchronous disposal** — settled by RESEARCH-0015 §7: separable from
   creation, excluded from this research's evidence base per the framing
   above.

The sharp distinction this research repeatedly returns to: **"a type has
async methods on it" is not evidence for "constructing/resolving this type
inherently requires asynchronous work."** Almost every dependency a test
composes has async methods somewhere (any HTTP client, any DB client) —
that alone motivates nothing. The relevant question is whether *producing
the composed value itself* — not calling it afterward — needs an `await`.

## 2. Audit of the current synchronous architecture

Every contract in the pipeline was read directly. All are synchronous,
without exception — no `Task`, `ValueTask`, or `async` appears anywhere in
`src/Compono` or `src/Compono.Generators` outside test/build tooling
(verified: `grep -rln "Task\|async\|ValueTask" src/Compono/*.cs
src/Compono.Generators/**/*.cs` returns nothing in the composition/
generation code paths).

| Contract | File | Shape today | Async-later classification |
|---|---|---|---|
| `Composer.Create<T>()` | `Composer.cs:61-70` | `T Create<T>()`, builds a `CompositionContext`, calls `ResolveRoot<T>()` synchronously | **Additive** — a sibling `CreateAsync<T>()` is a new method; nothing about the existing signature blocks it |
| `Composer.CreateMany<T>()` | `Composer.cs:97-105` | `IReadOnlyList<T> CreateMany<T>(int)`, loop of synchronous `ResolveRoot` | **Additive** — same reasoning; a `CreateManyAsync` returning `Task<IReadOnlyList<T>>` doesn't collide |
| `Composer.CreateRow()` | `Composer.cs:117-131` | Builds one `CompositionContext`, wraps in `CompositionRow`, fully synchronous | **Additive but duplicative** — see §6, a row's parameters are bound one at a time by framework binding code, not batch-async |
| `CompositionRow` | `CompositionRow.cs` | `sealed class` implementing `ICompositionContext`, all members synchronous | **Additive** — could grow `ResolveAsync<T>`/`ResolveSharedAsync<T>` members later; sealed doesn't block new methods |
| `ICompositionContext` | `ICompositionContext.cs:16-90` | `TValue Resolve<TValue>(in CompositionRequestDescriptor)`, `TValue Resolve<TValue>()`, `int DeriveSeed()`, `int ResolveCollectionSize()` — every member synchronous, `in`-parameter descriptor (a `readonly struct`, can't cross an `await` boundary as a `ref`/`in` parameter without copying first) | **Breaking to retrofit an async method onto this interface directly** (would need to be a new interface — see §6); the interface itself doesn't need to change to stay valid for sync use |
| Generated `ICompositionPlan<T>.Compose` | `ICompositionPlan.cs`, `Templates/CompositionPlan.scriban` | `T Compose(ICompositionContext context) => new T(context.Resolve<...>(...), ...)` — **the constructor call's argument list is built inline from `Resolve<T>()` calls**, all resolved eagerly before `new T(...)` executes | **Architecturally central finding, §5** — this shape cannot represent "one of these arguments must be awaited" without restructuring to temp-variable assignment + `await`, which is a different generated shape entirely, not an additive change to the existing one |
| `CompositionBuilder.Register<T>(Func<ICompositionContext,T>)` / `Register<T>(Func<T>)` | `CompositionBuilder.cs:75-89` | Two overloads, both synchronous delegate shapes, stored as `Func<ICompositionContext, object?>` internally (`CompositionBuilder.cs:22`) | **Additive — verified by compile-only spike, not assumed** (see §5a): new `Register<T>(Func<ICompositionContext, Task<T>>)` overloads are legal C# overload resolution; existing overloads untouched |
| `ICompositionValueProvider.TryProvide` | `ICompositionValueProvider.cs` | `CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)`, synchronous, `in`-parameter request struct | **Not directly retrofittable** (interface method, same `in`-struct-parameter friction as `ICompositionContext`) — but a **new**, parallel `IAsyncCompositionValueProvider` interface is legal; see §6 for whether the *pipeline* can actually merge sync and async provider results coherently |
| `CompositionProviderResult` | `CompositionProviderResult.cs` | `readonly struct` with exactly two static factory members (`NotHandled`, `Handled(object?)`), `internal` fields | **Additive** — a parallel `AsyncCompositionProviderResult` (wrapping `ValueTask<object?>` or similar) can exist independently; the existing struct's shape doesn't need to change |
| Built-in providers (stages 4-7) | `CompositionContext.cs` internal pipeline | All internal, synchronous, `ICompositionProvider.TryCompose` returns `CompositionResult` (not `Task<CompositionResult>`) | **Internal — no compatibility constraint at all**, this is engine-private and can change freely pre- or post-1.0 |
| `IServiceProvider` fallback | `CompositionContext.cs:578-611` | Calls `_serviceProvider.GetService(requestedType)` synchronously | **Fixed by .NET itself** — `IServiceProvider.GetService` has no async form and MS's own guidance (§4) says this is deliberate; not Compono's contract to change |
| `Share<T>()` / `[Shared]` | `CompositionBuilder.cs:117`, `CompositionScope.cs` | Compile-time `HashSet<Type>` config; runtime scope is a plain `Dictionary<Type, object?>`, **no locking, no concurrency primitives at all** | **Architecturally exposed by async, not merely additive** — see §10; a plain `Dictionary` is not safe under concurrent first-request races, which only become possible once resolution can suspend mid-graph |
| Profiles (`ICompositionProfile`) | `ICompositionProfile.cs` | Synchronous `Configure(CompositionBuilder)` | **Additive** — profile configuration itself never needs to be async; it only registers factories/rules, doesn't invoke them |
| `Compono.XunitV3` binding (`ComposeAttribute.GetData`) | `ComposeAttribute.cs:144` | `public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo, DisposalTracker)` — **already `ValueTask`-returning**, xUnit v3's own `DataAttribute` base class contract | **Zero retrofit needed at the framework-integration boundary** — the extension point Compono already implements is async-capable; Compono's own body is synchronous today (wraps the result in a completed `ValueTask`, `ComposeAttribute.cs:250`), by choice, not by constraint. See §8. |
| `Compono.TUnit` binding (`ComposeAttribute.GenerateDataSources`) | `ComposeAttribute.cs:97` | `protected override IEnumerable<Func<object?[]?>> GenerateDataSources(DataGeneratorMetadata)` — synchronous override of `UntypedDataSourceGeneratorAttribute` | **Sync today, but a sibling async extension point exists in the TUnit package Compono depends on** (`IAsyncUntypedDataSourceGeneratorAttribute`, `AsyncDataSourceGeneratorAttribute<T>` — confirmed present via binary inspection, §8) — switching base classes/overrides later is a `Compono.TUnit`-internal implementation change, not a Compono-core compatibility question |
| `Compono.TestDoubles` / `Compono.NSubstitute` | `ICompositionValueProvider` implementations | Same synchronous `TryProvide` shape as any other provider | Same classification as `ICompositionValueProvider` above — no distinct new risk |
| `Compono.Bogus` | Same provider shape | Same | Same |
| `Compono.Logging` | Provider + activation emitter (`LoggingActivationEmitter.cs`) | Synchronous; produces `ILogger<T>`/test-sink wiring at construction time, no I/O | **No async pressure at all** — a logger's construction is inherently synchronous everywhere in .NET |
| `Compono.DependencyInjection` | `CompositionRow.TryResolveConfigured` (`CompositionRow.cs`, ADR-0047) | Synchronous, mirrors the `IServiceProvider` fallback's own sync contract | Same classification as `IServiceProvider` fallback |
| `Compono.Http` | Provider(s) over `HttpClient`/`HttpMessageHandler` | Composes clients/handlers, not HTTP calls themselves — package exists in `src/Compono.Http`, confirmed | **No pressure found** — see §3, the package composes a *client instance* synchronously; it does not perform requests during composition |

**Summary of §2:** the overwhelming majority of the pipeline (Composer's
public entry points, registrations, `CompositionRow`, profiles, and every
integration package's provider shape) is **additive** — new async-shaped
siblings can be added without touching existing signatures. The two
places that are genuinely load-bearing, not merely inconvenient, are (a)
the generated plan's inline-constructor-argument shape (§5) and (b)
`ICompositionContext`/`ICompositionValueProvider` as *interfaces* a
provider author already implements against — a new method can't be added
to a public interface without a breaking change, so any async counterpart
must be a **new, parallel interface**, never a modification of the
existing ones (§6).

## 3. Real consumer evidence — dogfood repos

Searched for genuine async-construction-required patterns (not merely
"async methods exist somewhere in the test file") across:

- `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft` — full
  tree, `grep -rl` for `CreateAsync|OpenAsync|ConnectAsync|StartAsync|
  InitializeAsync|Testcontainers|LocalStack` across `*.cs`. One match
  (`ProgressiveResponseTests.cs`), and inspection shows it's an ordinary
  `async Task` **test method** calling an already-composed SUT's own async
  method — not a composition-time async need. No `Testcontainers`,
  `LocalStack`, or container-based fixture found anywhere in the repo.
- `/Users/ncipollina/source/repos/layered-craft/dynamodb-distributed-lock`,
  branch `feat/compono-0.9.0-preview.88` (the real Compono migration
  branch — confirmed via `git -C <path> grep` against that branch
  specifically, not the default checkout, per the lesson from
  RESEARCH-0015's own initial miss on this repo). Same search terms:
  zero matches for `Testcontainers`/`LocalStack`/`DynamoDbLocal` in any
  `.cs`/`.csproj` file. `IAmazonDynamoDB` is composed as a `[Shared]`
  test double (an NSubstitute-backed fake, per the earlier disposal
  research's own finding on this repo), not a real async-initialized
  client.

**No genuine async-composition-required pattern was found in either
dogfood repo.** Neither repo starts a container, opens a real network
connection, or awaits anything before a composed value becomes usable —
every "async" surface found is either a test method's own body (already
outside composition) or nonexistent. Applying the research's own test
("does Compono actually need to own this?") to the strongest hypothetical
candidate this codebase's own domain suggests — `dynamodb-distributed-lock`
against a real (not faked) DynamoDB, via Testcontainers/DynamoDB Local —
that pattern doesn't exist in the repo today, so there's nothing concrete
to classify. If it existed, the natural boundary (per §4's Testcontainers
findings) would be the test class's own `IAsyncLifetime.InitializeAsync()`
starting the container once, then registering the now-ready client
synchronously into Compono via `Register<T>(() => alreadyStartedClient)`
— composition consuming an already-initialized resource, not initializing
it. This mirrors RESEARCH-0015's own finding for disposal: the framework
fixture, not Compono, owns the async lifecycle boundary.

**Conclusion for §3: weak-to-no evidence of real async-composition
pressure from this project's own dogfood consumers today.**

## 4. Prior art

**Microsoft.Extensions.DependencyInjection** — current official guidance
([Dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines),
fetched directly, `ms.date: 2026-01-14`, live doc):

> "`async/await` and `Task` based service resolution isn't supported.
> Because C# doesn't support asynchronous constructors, use asynchronous
> methods after synchronously resolving the service."
>
> "Keep DI factories fast and synchronous."

And explicitly named as an anti-pattern: **"Async DI factories can cause
deadlocks"** — calling `.Result` inside a synchronous `Func<IServiceProvider,T>`
factory deadlocks; the doc's own worked example shows exactly this. This
is the single most directly relevant piece of prior art: the .NET
ecosystem's own dominant DI container deliberately never grew async
resolution, for 10+ years, across multiple major versions, and states the
reason in its own docs (C# has no async constructors — a resolved
service's constructor call is fundamentally synchronous C#, whatever
container builds it).

**AutoFixture** — [Issue #304, "Support heuristic for async static factory
methods"](https://github.com/AutoFixture/AutoFixture/issues/304), filed
2014, still open, never implemented. AutoFixture — Compono's closest peer
in problem shape — has had an explicit, unresolved feature request for
async construction support for over a decade. This is independent
negative evidence, not merely "nobody asked": someone asked, and the
maintainers never shipped it. [Issue #309, "Cannot await tasks generated
by AutoFixture"](https://github.com/AutoFixture/AutoFixture/issues/309)
shows the same gap from the consumer side.

**Testcontainers for .NET** — the async lifecycle (`StartAsync()`,
`DisposeAsync()`) is owned by the container object itself and driven by
the **test framework's own fixture lifecycle** (xUnit `IAsyncLifetime.
InitializeAsync()`/`IClassFixture<T>`), never by a composition/DI
container. The pattern is: start the container once in fixture setup,
then hand an already-running connection string/client into whatever
composes the SUT. This directly confirms the §3 conclusion: async
external-resource setup is a **fixture-lifecycle boundary**, not a
composition-time concern, in the actual .NET testing ecosystem's own
convention — not merely Compono's hypothesis.

**xUnit v3** — `DataAttribute.GetData(MethodInfo, DisposalTracker)` returns
`ValueTask<IReadOnlyCollection<ITheoryDataRow>>` (confirmed directly:
`Compono.XunitV3/ComposeAttribute.cs:144` already implements this exact
signature, since it must to compile against xUnit v3's base class). xUnit
v3's own core data-source extension point is **async-capable by design**,
independent of whether any given `DataAttribute` implementation chooses to
use that capability. `IAsyncLifetime` is xUnit's separate, established
mechanism for async test-class setup/teardown (used by the Testcontainers
pattern above) — a second, distinct async extension point from the data
attribute one.

**TUnit** — binary inspection of `TUnit.Core.dll` (NuGet package
`tunit.core` v1.65.38, installed in this repo's own `~/.nuget/packages`)
confirms an `AsyncDataSourceGeneratorAttribute<T>` family and an
`IAsyncUntypedDataSourceGeneratorAttribute` interface exist alongside the
synchronous `UntypedDataSourceGeneratorAttribute` `Compono.TUnit`'s
`ComposeAttribute` currently derives from (`Compono.TUnit/ComposeAttribute.cs:26,97`).
TUnit's extension-point family explicitly separates sync and async data
generation as parallel, coexisting base classes — direct architectural
precedent for the "new parallel interface, not a modified existing one"
shape §6 concludes Compono would need.

**NUnit / MSTest** — per `docs/roadmap/future-packages.md:85-92` (this
repo's own admitted-candidate list, current as of this research), the
extension points these packages would bind to are `IParameterDataSource`/
`ITestBuilder`/`IFixtureBuilder` (NUnit) and `ITestDataSource` (MSTest).
Both are long-standing, synchronous-only extension points (`ITestDataSource.
GetData` returns `IEnumerable<object[]>`, no `Task`/`ValueTask` variant in
either framework's public API) — legacy .NET Framework-era design,
predating both frameworks' own async-test-method support. **Neither
NUnit's nor MSTest's data-source binding mechanism can produce test
arguments asynchronously today**, unlike xUnit v3 and TUnit.

**WebApplicationFactory / ASP.NET Core test hosting** — host startup
(`WebApplicationFactory<T>.CreateClient()` internally builds the host) is
asynchronous under the hood, but the public `CreateClient()`/`Services`
surface a consumer touches is synchronous-looking (internal async-over-sync
inside the framework's own code, not exposed as a contract Compono would
compose through). Consistent with §9's conclusion: Compono would consume
an already-built `IServiceProvider`/`HttpClient` from this ecosystem, not
drive its async startup itself.

**Net effect:** every piece of prior art — MS DI's explicit, documented
refusal to support async factories; AutoFixture's decade-old unresolved
request; Testcontainers' fixture-lifecycle-owns-async-setup convention;
xUnit v3's and TUnit's genuinely async-capable data-source extension
points existing specifically at the *framework binding* layer, not the
*composition engine* layer — converges on the same shape: **async setup
belongs to fixture/framework lifecycle; the object-graph-construction step
itself has never been made async by any comparable .NET library, and the
one place real async capability already exists in this ecosystem is the
test-framework data-source boundary Compono sits behind, not the engine
underneath it.**

## 5. Source-generation implications

The generated plan template (`src/Compono.Generators/Templates/CompositionPlan.scriban`)
emits exactly this shape for every discovered type:

```csharp
public {{ fully_qualified_name }} Compose(global::Compono.ICompositionContext context) =>
    new {{ fully_qualified_name }}(
        context.Resolve<...>(new CompositionRequestDescriptor(...)),
        context.Resolve<...>(new CompositionRequestDescriptor(...)),
        ...
    );
```

A single expression-bodied method: the constructor call's entire argument
list is built from nested `Resolve<T>()` calls, evaluated left-to-right,
before `new T(...)` executes. This is the architectural crux:

- **If any one `Resolve<T>()` call in that argument list needed to be
  `await`ed**, the surrounding method could not stay an expression-bodied
  *synchronous* method — it would need to become `async`. **Correction to
  an earlier pass of this research:** the first pass claimed `await` is
  not legal as a sub-expression inside a constructor-call argument list.
  That is wrong C# — `await` is an ordinary expression and is legal
  directly inside an argument list, e.g.
  `new Service(await context.ResolveAsync<DependencyA>(context.Descriptor), await context.ResolveAsync<DependencyB>(context.Descriptor))`
  compiles and runs correctly (verified directly — see the compile-only
  spike below), with the compiler lowering it to sequential awaits with
  the results captured into compiler-generated temporaries under the
  hood; no hand-written local variables are required at the source level.
  So the specific claim "requires assigning to a local variable first"
  does not hold, and the async version of the template is not
  structurally forced into a multi-statement body purely by that
  constraint — it *could* stay a single `async` expression-bodied method
  with `await` calls inlined in the constructor argument list, mechanically
  similar to today's shape.

  **What does still hold, and is the actual reason this is "a different
  generated shape, not a drop-in change":** the method signature itself
  must change (`ICompositionPlan<T>.Compose(ICompositionContext) : T`
  cannot become `async Task<T> Compose(...)` or `async ValueTask<T>
  Compose(...)` without changing the interface `T` implements — see §6),
  every `Resolve<T>()` call in an async-capable plan would need to become
  `ResolveAsync<T>()`  (a different member on a different, parallel
  interface, not an overload of `Resolve<T>()` — `Resolve` and
  `ResolveAsync` returning `T` vs `ValueTask<T>`/`Task<T>` are not
  themselves overload-ambiguous, but they *are* different generator output
  depending on which interface the plan implements), and the generator
  would need to decide, per type, whether to emit the sync template, the
  async template, or both — which is exactly the runtime-visibility
  problem discussed below. The template variant is real; it's driven by
  the interface/member-shape change and the generator's compile-time
  blindness to runtime registrations, not by an `await`-placement
  restriction that doesn't actually exist in C#.
- **Does one async dependency make the entire parent chain async?**
  Yes, transitively, exactly like normal C# async color propagation:
  if `Service`'s constructor needs `DependencyA`, and `DependencyA`'s own
  plan needs an async-resolved value, `Service`'s own `Compose` becomes
  async too, and so does whatever calls `Service`'s plan, all the way to
  the root. This is the standard "async is viral" problem, not
  Compono-specific — but it interacts badly with generated code because
  **the generator cannot know ahead of time which leaf types will
  eventually need async resolution**, since that's runtime configuration
  (next point).
- **Can sync and async plans coexist for the same type?** In principle
  yes — nothing stops emitting both `ICompositionPlan<T>.Compose` (sync)
  and a hypothetical `IAsyncCompositionPlan<T>.ComposeAsync` (async) for
  the same `T`, used depending on which root API (`Create<T>()` vs a
  future `CreateAsync<T>()`) is invoked. The complication is deeper: a
  **single** plan for `Service` needs to know, per parameter, whether that
  parameter's own resolution might go through an async path — and that's
  determined by **runtime configuration** (a `Register<T>(Func<..,Task<T>>)`
  call, or an async provider), which the generator, running at compile
  time against `CompositionBuilder` configuration it never sees (confirmed:
  `src/Compono.Generators/ComponoIncrementalGenerator.cs` has no reference
  anywhere to `CompositionBuilder`/`Register`/provider registration — the
  generator only ever discovers *types* via Roslyn symbol analysis, never
  runtime registration calls, which are ordinary C# executed inside a
  lambda the generator cannot see into), cannot observe.
- **Would the generator need to know statically whether a dependency is
  async?** Yes, to emit the cheapest correct code (a fully sync plan when
  nothing in the graph is ever async). It **cannot** know this reliably —
  registrations and providers are runtime data, and the same generated
  `Service` type could be composed once through an all-sync `Composer`
  configuration and once through a configuration where one of its
  dependencies is registered with an async factory. **A single
  compile-time-generated plan cannot be correct for both cases
  simultaneously without either (a) always emitting the async-capable
  shape (defeating the sync fast path — see §12) or (b) generating both
  shapes and deciding at runtime which to use** (a real, if more complex,
  option — see §6/§14).
- **Would runtime provider selection make static async-vs-sync generation
  insufficient?** Yes — this is the same point restated: since a type's
  eventual resolution path (registration vs. provider vs. generated plan)
  is a runtime decision (the pipeline's own stage ordering,
  `CompositionContext.cs` stages 1-9), and *which* registration/provider is
  configured is also runtime data, the generator fundamentally cannot
  prove at compile time that a given constructor parameter will never
  route through an async source. Any async-composition design has to
  accept this and solve it at the **runtime dispatch** layer (a plan or
  provider reporting/being invoked in a way the caller can react to,
  e.g. returning a `ValueTask<T>` that happens to already be completed for
  the common sync case), not by trying to prove sync-ness statically.
- **Would `ValueTask<T>` be materially useful, or merely an optimization
  detail?** Materially useful, if async composition is ever built:
  `ValueTask<T>` lets the overwhelmingly common case (every dependency
  resolves synchronously, which is true for essentially all of today's
  dogfood evidence per §3) complete synchronously with no `Task` allocation
  — the exact pattern xUnit v3's own `GetData` already uses
  (`ComposeAttribute.cs:250` returns a `ValueTask` wrapping an
  already-computed result with no actual async machinery invoked). This
  is not a detail to defer; it's the mechanism that would make "support
  async" not mean "make every composition allocate," addressing §12
  directly.
- **Would supporting both sync and async resolution double generated
  surface area or create ambiguous behavior?** Very likely doubled
  surface area if pursued naively (a sync plan and an async plan per
  type) — a real cost, not free, and a reason this belongs firmly in
  "post-1.0, only if justified" territory (§15) rather than something to
  build speculatively. Ambiguity is avoidable if the *dispatch* decision
  (which plan to use) is made once, deterministically, by which root API
  the caller invoked (`Create<T>()` always uses the sync plan and fails
  loudly if it hits an async-only dependency, per §7 — never silently
  picks one).

**No generator prototype was built.** This section's conclusions come from
reading the actual emitted template and generator source directly — the
static/runtime-visibility question (can the generator see registrations?)
is answered definitively by `ComponoIncrementalGenerator.cs` containing no
such reference, not by a spike.

### 5a. Registration-overload compile spike (verifying, not assuming)

The claim that a future `Register<T>(Func<ICompositionContext, Task<T>>)`
overload would sit cleanly beside today's
`Register<T>(Func<ICompositionContext, T>)` (`CompositionBuilder.cs:75-89`)
was checked with a real compile-only spike rather than left to overload-
resolution intuition, per the request. A throwaway console project
(outside the repo, in the scratch directory, deleted afterward — not
committed, no trace left in `git status`) declared a minimal two-overload
`Builder.Register<T>` — one accepting `Func<Context, T>`, one accepting
`Func<Context, Task<T>>` — and called it with:

1. a synchronous expression lambda (`c => new Widget()`);
2. a synchronous block lambda (`c => { return new Widget(); }`);
3. an async expression lambda (`async c => await Task.FromResult(new Widget())`);
4. an async block lambda (`async c => { await Task.Delay(0); return new Widget(); }`);
5. a sync method group (`MethodGroupSource.MakeWidgetSync`);
6. an async, `Task<Widget>`-returning method group (`MakeWidgetAsync`);
7. the deliberately adversarial case the request flagged — `T` itself
   instantiated as `Task<Gadget>`, called with a *synchronous* lambda that
   itself returns a `Task<Gadget>` (`Register<Task<Gadget>>(c =>
   Task.FromResult(new Gadget()))`) — checking whether this collides with
   the async overload's `Func<Context, Task<Gadget>>` shape for `T = Gadget`;
8. an explicitly-typed delegate cast forcing the sync overload
   (`(Func<Context, Widget>)(c => new Widget())`).

**Result: `dotnet build` — 0 warnings, 0 errors. Every call compiled and
resolved to exactly the intended overload**, confirmed by having each
overload print which one ran: cases 1, 2, 5, 7, 8 resolved to the sync
overload; cases 3, 4, 6 resolved to the async overload. No ambiguous-call
diagnostic (`CS0121`) or any other warning was produced for any case,
including the adversarial case 7 — `Register<Task<Gadget>>` and
`Register<Gadget>` are different closed generic methods (different `T`),
so the delegate-shape overload resolution that distinguishes
`Func<Context,T>` from `Func<Context,Task<T>>` never has to disambiguate
against itself.

**This confirms the research's overload-additivity claim rather than
requiring the narrower fallback wording the request offered as a
contingency** ("an additive async registration surface remains possible,
but its exact API shape should be designed later") — the specific shape
tested (a sibling `Func<..., Task<T>>` overload) is verified clean, not
merely plausible. The narrower fallback wording is unnecessary here; it
would still be the right instinct for a future design task to re-verify
against the *actual* future API shape once one is proposed, since this
spike only tested the one shape named in the request, not every
conceivable async registration API.

## 6. Public extensibility contract risk — the most important section

For each contract, the real question is not "could C# add another
interface" (yes, always) but **"would a parallel async contract actually
be usable by the existing pipeline's provider discovery, precedence, and
generated-plan interaction, or would it require rewriting how those
already work?"**

- **`ICompositionContext`** — a provider author's `TryProvide` receives
  this today and calls `context.Resolve<T>()` for nested composition. A
  parallel `IAsyncCompositionContext` (or an async method added *to a new
  interface*, not this one — adding a method to a published public
  interface is a breaking change for any external implementer, though
  Compono has none today per `docs/public-api.md`'s "core doesn't know
  about integrations" boundary, so the practical breakage risk is lower
  than for a typical public interface, but the *principle* still holds)
  could exist side by side. The genuine risk isn't the interface
  addition — it's that **an async provider's own nested `Resolve<T>()`
  call, if it needs to go async itself, has nowhere to `await` inside the
  current synchronous `ICompositionContext.Resolve<TValue>()` signature**.
  A provider that's handed only the sync interface cannot itself do
  nested async resolution — it would need to be handed the async
  interface instead, which means the pipeline has to know, per provider,
  which interface to construct and pass in. This is a real design
  question for a future ADR, not resolved here, but it is **answerable
  additively**: the pipeline gains a second code path (`TryProvideAsync`
  dispatch handing out `IAsyncCompositionContext`) alongside the existing
  one, not a replacement of it.
- **`ICompositionPlan<T>`** — per §5, a parallel `IAsyncCompositionPlan<T>`
  (or similar) is the only viable shape; modifying the existing interface
  breaks every already-`Accepted`, already-shipped generated plan (every
  compiled consumer's generated code implements `ICompositionPlan<T>.Compose`
  today — this is the one interface where "just add a method" is
  concretely, immediately breaking, not hypothetically). A new interface
  the generator additionally emits (or emits *instead of*, per a
  to-be-decided rule) for types that need it is additive to the *type
  system*, but requires new **generator** logic (§5) and new **dispatch**
  logic (`PlanCache<T>` today only knows about `ICompositionPlan<T>` —
  confirmed via `src/Compono/PlanCache.cs` — an async variant needs its
  own cache/lookup, not a natural extension of the existing one without
  new code).
- **`ICompositionValueProvider`** — same shape of answer as
  `ICompositionContext`: a parallel `IAsyncCompositionValueProvider` is
  legal and doesn't break existing sync providers (`Compono.TestDoubles`,
  `Compono.NSubstitute`, `Compono.Bogus` all implement the sync interface
  today and would be completely unaffected). The real architectural
  question is **provider precedence across sync and async providers
  registered for the same requested type** — today, stage
  ordering/precedence is a strict, single, synchronous sequence
  (`CompositionContext.cs`'s stages 4-7, tried in order, first handled
  result wins). If both a sync `ICompositionValueProvider` and an async
  `IAsyncCompositionValueProvider` could both claim the same type, the
  pipeline needs a **defined interleaving rule** — this is genuinely new
  design surface, not something today's contracts pre-answer, but nothing
  about today's contracts blocks defining that rule later, because
  today's sync-only pipeline simply has no async providers to interleave
  yet.
- **`CompositionProviderResult`** — `readonly struct`, two static
  factory members, `internal` fields (`CompositionProviderResult.cs`). A
  parallel `AsyncCompositionProviderResult` (e.g. wrapping `ValueTask<object?>`)
  is trivially additive — this struct's shape imposes no constraint on a
  sibling type.
- **Registration APIs/delegates** (`CompositionBuilder.Register<T>`) —
  confirmed additive in §2: new overloads accepting `Func<..., Task<T>>`
  are legal C# and don't collide with the existing `Func<ICompositionContext,T>`/
  `Func<T>` overloads (different delegate return types resolve
  unambiguously). No risk here.
- **Any public generator-facing interface** — none exist. The generator
  emits code against `Compono`'s own public types (`ICompositionPlan<T>`,
  `ICompositionContext`, `CompositionRequestDescriptor`); there is no
  separate "generator contract" a third party implements, so no
  additional risk surface beyond what's already covered above.

**Bottom line for §6:** the existing seams (new registration overloads, a
new provider interface, a new plan interface) are all legal, additive C#
— **the question the request specifically warns against ("we could
declare another interface" isn't sufficient) is answered concretely
above: the seam exists at the type-declaration level for every contract
examined, and the harder, still-open work is at the *pipeline dispatch and
precedence* level (which plan/provider interface gets invoked, in what
order, relative to sync alternatives) — but that dispatch logic is 100%
internal, engine-owned code (`CompositionContext.cs`, `PlanCache.cs`), not
public API. Nothing about today's *public* contracts forces a particular
answer to the dispatch question, which means nothing about today's public
contracts needs to change now to keep that question open for later.**

## 7. Sync/async coexistence semantics — open questions, not resolved

If async composition were added, synchronous composition would obviously
remain the default/primary path (per §4's ecosystem-wide precedent and
§12's performance argument). Questions a future design would have to
answer, left open here per the request's own instruction not to settle
them unless necessary to prove compatibility:

- **What should `Create<T>()` do if the graph requires an async-only
  dependency?** Must fail clearly, never sync-over-async (per MS DI's own
  named anti-pattern, §4) — likely a `CompositionException` at the point
  the async-only registration/provider is hit, analogous to today's
  existing "nothing could satisfy this request" failure mode, just with a
  different, explicit message.
- **Could async registration/provider configuration make a graph that
  previously worked synchronously become async-only?** Yes, if a consumer
  swaps a sync `Register<Foo>` for an async one — this is a configuration
  change with a real behavioral consequence (their existing `Create<T>()`
  calls for anything depending on `Foo` would start throwing), which is
  exactly why such a change should be **visible and explicit** in whatever
  future API exists, not silent.
- **Could `CreateAsync<T>()` resolve both sync and async dependencies?**
  Plausible design direction — an async root entry point can freely await
  a sync-resolved value trivially (no cost beyond the already-sync path);
  the asymmetry only cuts one way (sync can't consume async, async can
  consume sync for free).
- **Would async composition need a separate configuration universe?**
  Not obviously — registrations/providers as *data* (which factory is
  registered for which type) don't need duplicating; only the *dispatch*
  needs to know which shape (sync/async) a given factory/provider is.
- **Could the same profile be used by both sync and async composition?**
  Plausibly yes, if a profile just calls `Register<T>(...)` with whichever
  overload — the profile mechanism itself is dispatch-agnostic.
- **How would precedence work if both a sync and async source can satisfy
  `T`?** Open question, flagged in §6 — no evidence today forces one
  answer over another.
- **Does `Share<T>()` need different behavior under async resolution?**
  Yes — this is significant enough to be its own section (§10).

None of these block a 1.0 API freeze; they're design questions for
whichever future ADR actually proposes async composition, not
compatibility risks in today's contracts.

## 8. Test-framework integration feasibility — framework by framework

| Framework | Extension point Compono binds/would bind to | Async-capable? | Classification |
|---|---|---|---|
| **xUnit v3** (`Compono.XunitV3`) | `DataAttribute.GetData(MethodInfo, DisposalTracker)` → `ValueTask<IReadOnlyCollection<ITheoryDataRow>>` | **Yes, natively** — Compono's own `ComposeAttribute.GetData` (`ComposeAttribute.cs:144`) already implements this async-returning signature; today it just never actually suspends (wraps a synchronously-computed result in a completed `ValueTask`, `ComposeAttribute.cs:250`) | **Naturally supported** — no framework-integration work needed to make async composition reachable through `[Compose]` theory parameters someday; only Compono's own internal body would need to change to actually `await` |
| **TUnit** (`Compono.TUnit`) | Currently: `UntypedDataSourceGeneratorAttribute.GenerateDataSources` → sync `IEnumerable<Func<object?[]?>>` (`ComposeAttribute.cs:97`). TUnit also ships `IAsyncUntypedDataSourceGeneratorAttribute`/`AsyncDataSourceGeneratorAttribute<T>` (confirmed present in the installed `tunit.core` 1.65.38 package binary) | **Possible with framework-specific lifecycle machinery** — TUnit provides the async extension point, but `Compono.TUnit` would need to switch which base class/interface `ComposeAttribute` implements (a `Compono.TUnit`-internal change, not a Compono-core one) | **Possible, requires integration-package work, not a core-Compono blocker** |
| **NUnit** (planned `Compono.NUnit`, not yet built) | `IParameterDataSource`/`ITestBuilder`/`IFixtureBuilder` per `docs/roadmap/future-packages.md:85-87` | **No** — these are long-standing synchronous-only NUnit extension points; no async data-source variant exists in NUnit's public API | **Fundamentally incompatible with the current parameter-data extension point** — async composition could never reach a `[Compose]`-style NUnit test parameter through this mechanism, only through a manual `Composer` API call inside the test body |
| **MSTest** (planned `Compono.MSTest`, not yet built) | `ITestDataSource.GetData` → sync `IEnumerable<object[]>` per `docs/roadmap/future-packages.md:89-91` | **No** — same shape of limitation as NUnit, this extension point predates async-test support in MSTest and has no async counterpart | **Fundamentally incompatible with the current parameter-data extension point**, same conclusion as NUnit |

**This is decisive for one part of the picture, per the request's own
framing:** async composition, if ever built, could reach test parameters
naturally through xUnit v3 and (with integration-package work) TUnit, but
**could never reach `[Compose]`-style parameters through NUnit or MSTest's
existing data-source mechanisms** — only through manual, in-test-body
`Composer`/`CompositionRow` API calls in those two frameworks. This is a
real, permanent ecosystem constraint (not a Compono design choice) worth
recording now, before `Compono.NUnit`/`Compono.MSTest` are designed, so
their own future design doesn't quietly assume a capability their host
framework's extension point can't deliver.

## 9. DependencyInjection and WebApplicationFactory boundaries

Both **reduce** rather than increase pressure for Compono-owned async
resolution:

- **Microsoft.Extensions.DependencyInjection's own graph resolution is
  synchronous** (§4, confirmed directly from current official docs) —
  `Compono.DependencyInjection`'s bridge (`CompositionRow.TryResolveConfigured`,
  ADR-0047) delegates to `IServiceProvider.GetService`, which is
  synchronous by MS's own explicit design. There is no async surface on
  the DI side for Compono to even bridge to.
- **Host/application startup is asynchronous** (`IHost.RunAsync()`,
  `WebApplicationFactory`'s internal host build), but that startup
  happens **once**, before any test composes anything, and produces an
  already-built, synchronously-queryable `IServiceProvider`/`HttpClient`
  by the time a test touches it. Compono would consume the *result* of
  that async startup, never drive it.
- The risk named explicitly in the request — "avoid designs where Compono
  accidentally starts owning an async host/scope lifecycle that belongs
  to another framework" — mirrors RESEARCH-0015 §10's identical warning
  about disposal ownership almost exactly, and the same boundary answer
  applies: a future `WebApplicationFactory` integration package would own
  its own async startup lifecycle entirely outside Compono's resolution
  pipeline, registering already-ready values into Compono synchronously,
  never asking Compono to await anything on its behalf.

**Conclusion: these ecosystems are evidence *against* Compono needing
async resolution, not evidence for it** — the pattern in both cases is
"async setup happens once, upstream, outside composition; composition
consumes the synchronous result."

## 10. Interaction with `Share<T>()`

ADR-0056's `Share<T>()` is compile-time configuration (a `HashSet<Type>`
frozen into `CompositionConfiguration`, `CompositionBuilder.cs:117` /
`CompositionBuilder.cs:328`); the runtime mechanism it rides on
(`CompositionScope`, `CompositionScope.cs`) is a **plain, unsynchronized
`Dictionary<Type, object?>`** — confirmed by direct inspection, no lock,
no concurrency primitive anywhere in the type. This is completely correct
and sufficient today because **a single `CompositionContext`'s resolution
is entirely synchronous and single-threaded** — nothing can race, because
nothing ever yields control mid-resolution.

**Async resolution would break this invariant.** If resolving a shared
type's first request could suspend (an `await` inside the factory/provider
that produces it), and something else in the same graph's resolution
could run concurrently (e.g. two sibling constructor parameters both
depending on the same `[Shared]` type, resolved "in parallel" by some
future async fan-out), the plain-`Dictionary` scope has no protection
against:

- **Concurrent first-request races** — two callers both observe "not yet
  in scope," both start the async factory, both eventually try to
  `Set()` the result — the *last writer wins* today's `Set()` semantics
  (`CompositionScope.cs`'s `Set` is an unconditional dictionary write),
  which would silently violate `Share<T>()`'s own "exactly one instance"
  contract if two concurrent async creations both ran to completion.
- **Exactly-once creation under concurrency** requires either serializing
  first-access (an async lock / single-flight `Task` cached and awaited
  by all concurrent callers — the standard .NET pattern for this,
  `Lazy<Task<T>>` or an equivalent) or Compono deliberately choosing not
  to support concurrent async resolution within one graph at all (forcing
  sequential await, sidestepping the race entirely, at some throughput
  cost).
- **Failure/cancellation during first creation** — today, if a shared
  value's factory throws, nothing is ever written to scope (confirmed:
  `Set()` is only called after a factory/provider call completes
  successfully — `CompositionContext.cs`'s `StoreSharedAndReturn` path).
  An async equivalent needs the same guarantee: a failed async creation
  must not leave scope in a state where a second, concurrent caller
  either double-runs the factory or observes a poisoned cached failure
  incorrectly reused for an unrelated later graph (a `CompositionContext`
  is single-root-scoped, so "unrelated later graph" mostly doesn't apply
  within one context — but *within* one context, whether a second
  concurrent request retries or awaits the same in-flight failure is a
  real design choice).
- **Sync and async requests targeting the same shared type** — not
  currently meaningful, since nothing is async yet; if it became possible
  to have a `[Shared] Foo foo` parameter satisfied by a factory that's
  sometimes invoked from a sync-only root and sometimes from an
  async-capable one, that's exactly the "same type behaves differently
  depending on caller" ambiguity §7 already flags as unresolved and not
  urgent to resolve now.

**Does this mean ADR-0056's public semantics are wrong or need
reopening?** No — per the request's own instruction, ADR-0056 is not
modified here, and its public contract ("at most one shared value per
type per graph, first request wins, computed lazily") **remains a
perfectly correct semantic description under async resolution too** — the
issue is entirely in the *runtime implementation* (`CompositionScope`'s
lack of concurrency safety), which is `internal` (`CompositionScope.cs`'s
class itself, and `CompositionContext`'s `_scope` field, are both
`internal`/`private` — confirmed), not public API. **This is a genuine
architectural finding worth surfacing (per the request), but it is not a
1.0 API-freezing concern** — `CompositionScope` can be rewritten to be
concurrency-safe (e.g. backed by a `ConcurrentDictionary` plus a
single-flight `Task` cache) whenever async composition is actually built,
with zero public API impact, because nothing public exposes this type or
its locking behavior today.

## 11. Cancellation

If genuine async composition existed, `CancellationToken` propagation
would be the standard expectation (matching every other async .NET API
convention, and matching xUnit v3's own `TestContext.Current.CancellationToken`
convention this repo's own dogfood evidence already uses —
`dynamodb-distributed-lock`'s `feat/compono-0.9.0-preview.88` branch
history shows a commit titled exactly "fix: use TestContext.Current.
CancellationToken for the outer SUT call", confirming this convention is
already live in a real Compono-adjacent consumer, just not through
Compono itself).

Retrofitting cancellation later is **not blocked by today's contracts**:
none of `Create<T>()`, `CreateMany<T>()`, `Register<T>()`, or
`ICompositionValueProvider.TryProvide` accept or need to accept a
`CancellationToken` today (nothing in the synchronous pipeline can be
cancelled mid-flight — there's no `await` point to cancel at). A future
`CreateAsync<T>(CancellationToken)` overload, or an async provider
interface method accepting one, is purely additive — optional-parameter
or overload-based, same pattern as every other .NET async API that added
cancellation support after its initial sync version shipped. No finding
here changes anything about §14's conclusion.

## 12. Performance and allocation implications

Making the *existing* pipeline "universally async-capable" (every
`Resolve<T>()` returning `ValueTask<T>` unconditionally, every generated
plan `async`) would impose a real, permanent tax on the dominant case —
today's 100%-synchronous composition, which per §3 is also apparently the
*only* case any real dogfood consumer currently needs:

- **`ValueTask<T>` at every resolution** still costs something over a bare
  `T` return even when synchronously completed (a larger struct return,
  branch to check `IsCompletedSuccessfully`) — small per-call, but
  Compono's own design principles (source-generation-first, no reflection
  by default, ADR-0001) already optimize hard for the synchronous fast
  path; this would be working directly against that stated goal for a
  capability with, per §3, no current evidence of real demand.
- **Async state machines in generated plans** — an `async` `Compose`
  method compiles to a state-machine allocation-heavy shape in the general
  case (mitigated somewhat by `ValueTask` custom awaiters when nothing
  actually suspends, but the state machine itself still has to exist in
  IL and its own overhead) — a nontrivial generator/codegen complexity
  increase for every single composed type, not just the ones that need
  it.
- **Comparison: a separate async path, built only if/when justified,**
  costs nothing on the existing hot path — the sync pipeline stays
  exactly as fast as it is today, unconditionally, and a hypothetical
  future async pipeline pays its own (justified, opt-in) allocation cost
  only when a consumer actually asks for it.

**Given Compono's stated performance goals, "make everything async just
in case" fails the bar this research was asked to hold it to** — there is
no concrete evidence (§3) that justifies paying this cost universally, and
the additive-path alternative (§2/§6's conclusion) means paying it
universally isn't even necessary to preserve the option.

## 13. Spike

**None performed.** The two questions the request flags as the strongest
spike candidates were both answerable directly from source/package
inspection with high confidence, not requiring a throwaway prototype:

- *"Can we distinguish generated-plan creation from provider-supplied
  values reliably [for async purposes]?"* — not the relevant question
  here (that was RESEARCH-0015's disposal-provenance question); the
  relevant async equivalent — "can the generator know statically which
  dependencies are async" — is answered definitively in §5 by
  `ComponoIncrementalGenerator.cs` having no visibility into
  `CompositionBuilder` registration calls at all (verified by direct
  `grep`, not inference).
- *"Can xUnit v3/TUnit asynchronously supply theory arguments through the
  extension mechanism Compono actually uses?"* — answered directly and
  concretely: xUnit v3's mechanism already is async (`ComposeAttribute.cs:144`'s
  own already-compiled, already-shipped signature is definitive proof, not
  a hypothesis to spike), and TUnit's async data-source classes were
  confirmed present in the actual installed package binary
  (`~/.nuget/packages/tunit.core/1.65.38`) via direct string inspection —
  stronger evidence than a spike would produce, since it's the real
  shipped package this repo already depends on, not a constructed test
  case.

No temporary files were created; no code was written or modified during
this research. `git status` was clean throughout.

## 14. What 1.0 would freeze dangerously — the key deliverable

Going through every contract flagged as worth checking in §2/§6:

| Public contract | Safe to freeze as-is? | Reasoning |
|---|---|---|
| `Composer.Create<T>()` / `CreateMany<T>()` | **Yes** | A sibling `CreateAsync<T>()`/`CreateManyAsync<T>()` is additive; no signature collision, no semantic ambiguity |
| `CompositionRow` | **Yes** | New async members (`ResolveAsync<T>`, etc.) are additive to a `sealed` class with no existing async surface to collide with |
| `ICompositionContext` | **Yes, with a caveat already covered in §6** | The interface itself doesn't need to change; a parallel async interface is the correct future shape. The caveat is a design question (how does an async provider get handed the right context type), not a compatibility risk to today's shipped interface |
| Generated `ICompositionPlan<T>` | **Yes** | Not because async is trivial here — §5 shows it's the hardest part of this whole research — but because the *existing* interface doesn't need to change; a parallel plan interface and new generator logic solve it without touching `ICompositionPlan<T>` itself or breaking any already-compiled consumer's generated code |
| `ICompositionValueProvider` | **Yes** | Same shape of answer; parallel interface, no modification needed |
| `CompositionProviderResult` | **Yes** | Struct, no async-blocking shape; a sibling async result type is unconstrained by it |
| `Register<T>()` overloads | **Yes** | New async-factory overloads are ordinary, unambiguous C# overload resolution — verified by compile spike (§5a), not assumed |
| `Share<T>()` / `[Shared]` public semantics | **Yes for the public contract, No for the current internal implementation** | ADR-0056's stated semantics remain correct under async; `CompositionScope`'s concurrency-unsafety (§10) is a purely internal implementation gap with zero public API surface, fixable freely whenever async composition is actually built |
| `IServiceProvider` fallback | **Yes** | Constrained entirely by .NET's own `IServiceProvider` contract, not Compono's choice either way |
| `Compono.XunitV3`'s `ComposeAttribute` | **Yes** | Already implements xUnit v3's async-capable `GetData` signature; no freeze risk exists here at all |
| `Compono.TUnit`'s `ComposeAttribute` | **Yes** | Currently binds to TUnit's sync base class; switching to TUnit's own async base class later is entirely internal to `Compono.TUnit`, no public-facing Compono contract is at risk |

**No public contract examined in this research is dangerous to freeze as
of 1.0.** Every seam that would matter for a future async design is
either already additive by construction, or (for `CompositionScope`'s
concurrency gap) entirely internal with no public exposure today.

**Is there a small compatibility-preserving change worth making before
1.0 anyway, even with async deferred?** No concrete evidence in this
research justifies one. The request is explicit that any pre-1.0 change
needs concrete architectural evidence, not speculative "might someday
help" abstraction — and every contract examined already has a clean,
additive path forward without modification. Recommending a change here
would be exactly the "invent a seam nothing has asked for yet" pattern
RESEARCH-0015 (via ADR-0022 Amendment 4) already found and rejected once
in this codebase for a different question — the same discipline applies
here.

## 15. Recommendation: **Outcome C — Fully additive post-1.0 capability**

Current contracts provide adequate seams for separate async
interfaces/APIs later, and **nothing needs changing before 1.0**.
Reasoning, tied directly to the evidence above:

- Every public contract audited (§2, §14) already supports a parallel
  async counterpart without modification — this isn't an assertion, it's
  demonstrated per-contract above, including the two contracts (generated
  `ICompositionPlan<T>`, `ICompositionValueProvider`) where "just add a
  method" would actually be breaking, precisely because the answer for
  both is "new parallel interface," not "modify this one."
- Real dogfood evidence for async-composition pressure is **absent**, not
  merely weak (§3) — a materially different finding from RESEARCH-0015's
  disposal research, which found real (if low-stakes) evidence. This
  research found none. That absence is itself evidence against Outcome A
  and against inventing speculative compatibility scaffolding for
  Outcome B.
- Every piece of relevant prior art (§4) — MS DI's explicit, sustained
  refusal to support async factories; AutoFixture's decade-unresolved
  request; the fixture-lifecycle-owns-async-setup convention Testcontainers
  and `IAsyncLifetime` embody — converges on "object graph construction
  stays synchronous; async setup is a separate, framework-owned lifecycle
  concern." Compono's current design already matches this converged
  industry shape by construction, not by accident.
- The one place a future async design would face genuine, non-trivial
  work is entirely **internal**: the generator needing new template logic
  (§5) and the pipeline needing new dispatch/precedence logic for
  coexisting sync/async plans and providers (§6), plus making
  `CompositionScope` concurrency-safe (§10). None of that is public API,
  and none of it is blocked or made harder by anything Compono ships at
  1.0.
- The one real, permanent constraint discovered — NUnit's and MSTest's
  data-source extension points cannot carry async arguments (§8) — is not
  a Compono compatibility question at all; it's an upstream ecosystem
  fact those two future integration packages will have to design around
  regardless of what Compono's core API looks like.

This is **not** Outcome D ("async composition doesn't belong in
Compono, full stop") — the evidence doesn't support a permanent
prohibition; xUnit v3's and TUnit's own async-capable data-source
extension points are real, already-available capability sitting directly
adjacent to where Compono plugs in, and if real dogfooding evidence for
genuine async-construction pressure emerges later (a scenario not found
in this pass but plausible as more integration packages and consumers
land, per the same reasoning RESEARCH-0015 applied to disposal), building
it would be additive, not a rearchitecture. It is **not** Outcome A —
nothing found here rises to "Compono cannot reasonably serve important
scenarios without this today." It is **not** Outcome B either — no
concrete evidence identifies even a *small* compatibility-preserving
change worth making now; unlike RESEARCH-0015 (which also landed on
"post-1.0 additive," Outcome C there too), this research didn't even
surface a documentation-guidance action item rising to "record this now"
urgency beyond what §16 already proposes as ordinary good practice.

**Should async composition remain on the pre-1.0 list after this
research?** No — nothing here supports keeping it there. It can move to
the same "post-1.0, additive, revisit if real pressure appears" status
this research assigns it, mirroring RESEARCH-0015's own disposal outcome
almost exactly.

## 16. Documentation/skill implications (not applied — description only)

Even with no feature and no API change recommended, one documentation gap
is worth naming: nothing today explicitly states that Compono's
composition/resolution model is synchronous by design, which means a
future contributor or consumer could reasonably wonder whether that's an
accident or a deliberate stance. If this research's Outcome C is
accepted, the minimal documentation content worth recording:

- **`docs/architecture.md`** — the composition pipeline description
  should state explicitly that resolution is synchronous end-to-end
  today, with a one-line pointer to this research for the reasoning
  (mirroring how RESEARCH-0015's disposal stance should be recorded per
  its own §13).
- **`docs/public-api.md`** — `Create<T>()`/`CreateMany<T>()`/`Register<T>()`
  could each note "synchronous only; no async composition today" so a
  consumer reaching for a `CreateAsync<T>()` that doesn't exist finds an
  explanation rather than silence.
- **`skills/compono/SKILL.md`** — should carry the pattern this research's
  §3/§9 conclusions imply: when a test needs an async-initialized
  resource (a started container, an async-obtained token), the guidance
  is to perform that setup in the test framework's own fixture/lifecycle
  mechanism (xUnit `IAsyncLifetime`, TUnit's equivalent) and then register
  the already-ready value into Compono synchronously via `Register<T>(()
  => alreadyReadyValue)` — not to expect Compono to await anything.
- **Framework integration docs** (`Compono.XunitV3`, `Compono.TUnit`, and
  especially any future `Compono.NUnit`/`Compono.MSTest` design) — the
  §8 finding (NUnit/MSTest data-source mechanisms can never carry async
  arguments) should be recorded explicitly before those packages are
  designed, so their own design doesn't assume a capability that doesn't
  exist upstream.
- **Examples/evals** — any example involving an external/async-initialized
  resource (a database, an HTTP dependency needing setup) should
  demonstrate the fixture-owns-async-setup pattern directly.

No existing documentation was found to be factually wrong about current
behavior during this research — no out-of-band corrections were made.

## Evidence index

- `src/Compono/Composer.cs`, `CompositionContext.cs`, `CompositionRow.cs`,
  `CompositionScope.cs`, `CompositionBuilder.cs`, `ICompositionContext.cs`,
  `ICompositionPlan.cs`, `ICompositionValueProvider.cs`,
  `CompositionProviderResult.cs`, `CompositionProviderRequest.cs`,
  `PlanCache.cs` — read directly for §2/§5/§6/§10/§14.
- `src/Compono.Generators/ComponoIncrementalGenerator.cs`,
  `Emitters/CompositionPlanEmitter.cs`,
  `Templates/CompositionPlan.scriban` — read directly for §5.
- `src/Compono.XunitV3/ComposeAttribute.cs` — read directly for §2/§8/§13
  (the already-`ValueTask`-returning `GetData` override).
- `src/Compono.TUnit/ComposeAttribute.cs` — read directly for §2/§8.
- `~/.nuget/packages/tunit.core/1.65.38/lib/net9.0/TUnit.Core.dll` —
  string-inspected directly (`strings` + grep) to confirm
  `AsyncDataSourceGeneratorAttribute<T>`/`IAsyncUntypedDataSourceGeneratorAttribute`
  exist in the actual installed package; primary evidence for §4/§8/§13.
- `docs/roadmap/future-packages.md:85-92` — NUnit/MSTest candidate
  extension points; primary evidence for §8.
- [ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md) —
  `Share<T>()`'s public semantics, not modified; primary evidence for §10.
- [RESEARCH-0015](0015-disposal-ownership-research.md) — settled input on
  disposal/creation separability (§1), structural/rigor model, and the
  dynamodb-distributed-lock branch-checking lesson applied in §3.
- `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft` — grepped
  directly for async-construction patterns; primary evidence for §3.
- `/Users/ncipollina/source/repos/layered-craft/dynamodb-distributed-lock`,
  branch `feat/compono-0.9.0-preview.88` — grepped directly via
  `git grep` against that branch; primary evidence for §3.
- [.NET dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) —
  current official MS Learn doc, fetched directly (`ms.date: 2026-01-14`);
  primary evidence for §4/§9.
- AutoFixture [Issue #304](https://github.com/AutoFixture/AutoFixture/issues/304)
  ("Support heuristic for async static factory methods", open since 2014)
  and [Issue #309](https://github.com/AutoFixture/AutoFixture/issues/309)
  ("Cannot await tasks generated by AutoFixture") — via web search; primary
  evidence for §4.
- Testcontainers for .NET / xUnit `IAsyncLifetime` pattern — via web
  search of current Testcontainers/Docker/xUnit documentation and
  community guides; primary evidence for §4/§9.

## Links

- Directly informs any future `Compono.NUnit`/`Compono.MSTest` design
  (§8's finding that their data-source mechanisms can't carry async
  arguments) — should be read before those packages reach a design pass.
- Directly informs any future `WebApplicationFactory`/ASP.NET Core
  integration design (§9) — the same "consume the async result, don't own
  the async startup" boundary RESEARCH-0015 already established for
  disposal applies here too.
- Feeds a future ADR only if/when real async-composition pressure
  materializes (per Outcome C) — no ADR drafted by this research.
- Should be cross-referenced from RESEARCH-0015 (or vice versa) as two
  halves of the same "what does Compono 1.0 accidentally freeze"
  pre-1.0 gate exercise, since both landed on the same Outcome (C) via
  largely parallel reasoning (additive future capability, no evidence of
  urgent need, no compatibility-preserving change required now).
