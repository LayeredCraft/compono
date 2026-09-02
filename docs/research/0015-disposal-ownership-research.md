# [RESEARCH-0015] Disposal/Ownership Semantics for Composed Values

**Status:** Research complete, Outcome C accepted in principle by the
requester. No ADR yet — this document is scoped to research only, per the
request that produced it. Not a committed pre-1.0 requirement; the
purpose is to determine whether disposal ownership is a real pre-1.0 gap,
a post-1.0 feature, or something Compono should deliberately never own.

**Revision note (post-acceptance corrections):** two corrections were
made after initial review, both in place below: (1) §3's
dynamodb-distributed-lock finding was wrong in the first pass — it
checked the repo's default checkout rather than its
`feat/compono-0.9.0-preview.88` Compono-migration branch, which does
contain real Compono usage (13 `[Shared] Meter` registrations), now
documented and assessed rather than dismissed; (2) §6's `CompositionRow`
discussion is qualified to make clear it names an *existing abstraction
a future disposal boundary could attach to*, not a selected
implementation that already solves disposal for the dominant `[Compose]`
framework-attribute usage pattern, which would need its own
framework-side lifecycle wiring. Neither correction changes the Outcome C
recommendation (§12).

**Central question:** when Compono causes a disposable object to
participate in a composition graph, is Compono responsible for
disposing it?

**Headline finding:** this question was already asked and answered once,
inside this repo, by a real shipped-then-reverted feature —
[ADR-0022 Amendment 4](../adr/0022-compono-xunit-package-design.md) (PR
#24, 2026-07-31). Automatic disposal tracking was added, caught in
review as unsafe, and reverted in the same review cycle. That amendment
is treated here as primary evidence, not merely prior art — it is
Compono's own prior attempt at exactly the feature this research
evaluates, and its reasoning is verified against the current codebase
below (§1), not just restated.

## 1. Audit of current Compono behavior

Grepped the full non-generated source tree for disposal awareness:

```
grep -rln "IDisposable\|IAsyncDisposable\|\.Dispose(" src/ (excluding bin/obj)
```

Zero matches in `src/Compono`, `src/Compono.Generators`, or any of
`Compono.XunitV3`/`Compono.TUnit`/`Compono.NSubstitute`/`Compono.Bogus`/
`Compono.TestDoubles`/`Compono.DependencyInjection`/`Compono.Http`/
`Compono.Logging`. There is no disposal concept anywhere in the shipped
product today — not partial, not internal-only. This matches, rather
than contradicts, ADR-0022 Amendment 4's revert: the feature that briefly
existed was fully removed, not left as dead code.

Traced every path named in the request:

- **`Composer.Create<T>()` / `CreateMany<T>()`** (`src/Compono/Composer.cs:61-72`,
  `95-103`, `168-192`) — each call builds a fresh `CompositionContext`,
  calls `ResolveRoot<T>()`, and returns the bare `T`. The `CompositionContext`
  itself is not retained by the caller in any form; nothing survives the
  call except the returned value graph. No wrapper, no handle, no disposal
  hook.
- **`CompositionContext`** (`src/Compono/CompositionContext.cs`) — internal,
  one instance per root operation. Holds `_scope` (`CompositionScope`),
  `_activeFrames`/`_activeFactories`/`_activeProviderRequests` (transient
  recursion-detection stacks, popped as construction unwinds — not a
  retained creation-order record) and no list of "everything this context
  produced." Once `ResolveRoot<T>()` returns, this bookkeeping is gone.
- **`CompositionScope`** (`src/Compono/CompositionScope.cs`) — a plain
  `Dictionary<Type, object?>`, `TryGet`/`Set` only. No disposal awareness,
  no distinction between a scope entry that came from a generated plan vs.
  one that came from `ShareExplicit`.
- **`CompositionRow`** (`src/Compono/CompositionRow.cs`) — public, wraps one
  `CompositionContext`, forwards `Resolve`/`ResolveShared`/`ShareExplicit`/
  `TryResolveConfigured`. Does **not** implement `IDisposable`. It is the
  one object in the public API that already represents "one graph's
  lifetime" (ADR-0021), but today it is inert with respect to disposal —
  it has no state to disposal to act on, and nothing calls `Dispose` on
  anything it holds.
- **Generated composition plans / nested transitive composition** — a
  generated `ICompositionPlan<T>.Create(ICompositionContext)` constructs
  `T` directly and returns it; nested calls go through
  `context.Resolve<TNested>()`, recursing through the same pipeline. No
  provenance tag is attached to a constructed value distinguishing "built
  by a generated plan" from any other source once it's back in the
  pipeline's hands (this is exactly what Amendment 4 found — see below).
- **Exact `Register<T>()` / registration factories** — `CompositionRegistrations`
  stores caller-supplied factories (`Func<ICompositionContext, T>` shape);
  the registration stage invokes the factory and returns whatever it
  produced, with no ownership metadata captured about the factory's own
  intent (fresh-per-call vs. cached/shared instance — the factory author
  alone knows this, and nothing in the public registration API lets them
  declare it).
- **`ICompositionValueProvider` implementations / built-in providers** —
  `TryProvide(in CompositionProviderRequest, ICompositionContext)` returns
  a `CompositionProviderResult`; same shape, same absence of provenance
  metadata. Built-in providers (`Providers/BuiltInProviders.cs`,
  `PrimitiveValueProvider.cs`, etc.) construct only inert primitive/POCO
  values today — none currently produce a disposable type — but the
  interface itself carries no constraint preventing a future or
  TestDoubles/NSubstitute provider from doing so.
- **Configured `IServiceProvider` fallback** — `CompositionContext.cs:578-602`,
  stage 3b, tried only on exact-registration miss; calls
  `_serviceProvider.GetService(requestedType)` and validates the result.
  This is explicitly the case [ADR-0019](../adr/0019-registrations-and-service-provider-injection.md)
  already governs: "the caller owns the provider and its entire lifetime;
  Compono is a pure consumer." Compono never disposes anything reached
  through this path, by existing explicit decision, not by omission.
- **`Share<T>()` / `[Shared]`** — both ultimately write into the same
  `CompositionScope` via `ResolveDescriptorAsShared`/`ShareExplicitTestParameter`
  (`CompositionContext.cs`, `CompositionRow.cs:61-79`). Deduplicates to one
  instance per type per graph; no disposal behavior attached to that
  dedup — confirmed directly in ADR-0056's own "Disposal" section (see
  §9 below).
- **TestDoubles / NSubstitute / Logging providers** — these register
  providers into stage 5/6 the same as any `ICompositionValueProvider`.
  `Compono.TestDoubles` and `Compono.NSubstitute` produce test-double
  proxies (NSubstitute substitutes, `Compono.TestDoubles` fakes), none of
  which implement `IDisposable` today (spot-checked `Compono.TestDoubles`
  and `Compono.NSubstitute` source — no disposal interfaces present).
  `Compono.Logging`'s test logger/provider types were also checked; no
  `IDisposable`/`IAsyncDisposable` implementations exist there either.
- **Manual row APIs / `CreateRow`** — same `CompositionContext`-backed
  mechanism as everything else; no separate disposal path.

**Conclusion for §1:** Compono tracks ownership **nowhere**, not even
implicitly. There is no data structure today capable of answering "what
did this graph produce" after the fact, let alone "which of those
entries Compono itself constructed vs. received from an external
source." This isn't a partial gap with some paths covered — it's a
uniform, total absence across every entry point.

## 2. Ownership matrix

| Path | Who creates the object? | Who supplied factory/provider? | Externally owned? | Can Compono know reliably? | Disposing it: correct / wrong / ambiguous | Does sharing change the answer? | Natural disposal boundary today? |
|---|---|---|---|---|---|---|---|
| Generated plan (direct) | Compono's generated code, inline `new T(...)` | N/A — generator-emitted | No | Not distinguishable at the `CompositionScope`/`CompositionRow` boundary from any other source (Amendment 4) | Would be *clearly correct* in isolation, but Compono can't isolate it | No — same scope write path as everything else | None |
| Nested/transitive generated composition | Same as above, recursively | N/A | No | Same limitation, compounded — a nested disposable is even further from any caller-visible reference | Clearly correct in isolation; same detection problem | No | None — caller may never even hold a direct reference to the nested value |
| Exact `Register<T>()` | Consumer's factory delegate | Consumer | Ambiguous by construction — the factory might `new` a fresh instance every call, or close over and return a cached/shared one | No — the registration API has no "fresh vs. cached" declaration | Ambiguous — depends entirely on factory authorial intent Compono cannot observe | No | None |
| Registration factory using composition context | Consumer's factory, possibly delegating part of construction to `context.Resolve<TNested>()` | Consumer | Ambiguous, same as above | No | Ambiguous | No | None |
| `ICompositionValueProvider` implementation | Provider author's code | Provider author (built-in or public) | Ambiguous — a provider could wrap and return an externally-owned resource, or freshly construct one | No — `CompositionProviderResult` carries no ownership flag | Ambiguous | No | None |
| Built-in providers | Compono | Compono | No | Yes (today, only because none produce disposables — see §1) | N/A today; would be clearly correct if one ever did, since Compono authors these | No | None |
| TestDoubles / NSubstitute / Logging providers | Provider (Compono-authored, external state) | Compono-owned integration package | No (none are disposable today) | N/A today | N/A today | No | None |
| Configured `IServiceProvider` fallback | The external container | Consumer (`UseServiceProvider`) | **Yes, always** — ADR-0019 already settles this explicitly | Yes — this is the one path with an unambiguous, already-decided answer | **Clearly wrong** to dispose — would violate ADR-0019's stated contract and could dispose a live singleton reused elsewhere | No | The `IServiceProvider`/`IServiceScope` itself, owned and disposed by the consumer, entirely outside Compono |
| `Share<T>()` | Whatever underlying source produced the shared type (any of the rows above) | Inherits from source | Inherits from source | Inherits from source's answer | Inherits from source's answer | Deduplicates to one instance, but does not change who owns that instance | None — same scope, same absence of a boundary object |
| `[Shared]` | Same as `Share<T>()` — thin sugar over the same `CompositionScope` mechanism | Inherits from source | Inherits from source | Inherits from source | Inherits from source | Same as `Share<T>()` | None |
| `CompositionRow` | N/A (aggregator, not a producer) | N/A | N/A | N/A | N/A | N/A | **This is the closest thing to a graph-lifetime object that exists** — public, one per row, but implements no disposal interface and holds no record of what it produced |
| `Composer.Create<T>()` | Depends on which stage ultimately satisfies `T` | Depends | Depends | No — same limitation as every path above, now collapsed behind a single opaque `T` return | Depends, and Compono cannot tell which case applies | N/A | **None — `T` alone carries no lifetime handle** |
| `CreateMany<T>()` | Same as `Create<T>()`, once per item | Same | Same | Same | Same | Independent scopes per item (ADR-0011/ADR-0056) — sharing never crosses items | None |
| Manual row APIs | Same as `CreateRow` | Same | Same | Same | Same | Same | Same as `CompositionRow` above |

**The one path with a settled, unambiguous answer is the configured
`IServiceProvider` fallback** — ADR-0019 already decided Compono never
owns it. Every other path is either ambiguous by construction (any
consumer-authored factory/provider, because Compono cannot inspect
authorial intent) or currently moot only because no disposable value
happens to flow through it yet (built-in/TestDoubles/NSubstitute/Logging
providers). **Graph lifetime does not by itself resolve any of these
ambiguous cases** — a value being scoped to one `CompositionContext`
says nothing about who is entitled to end its lifetime, which is exactly
the distinction ADR-0056's own Disposal section draws for `Share<T>()`
(§9).

## 3. Real consumer evidence

### alexa-vox-craft — real, but low-stakes

Found one genuine disposable dependency composed through Compono:

`test/AlexaVoxCraft.Smapi.Tests/TestKit/HttpTestHarness.cs` and the
identical `test/AlexaVoxCraft.InSkillPurchasing.Tests/TestKit/HttpTestHarness.cs`:

```csharp
public sealed class HttpTestHarness : IDisposable
{
    private readonly TestHttpHandler _handler = new();
    ...
    public HttpClient CreateClient(Uri? baseAddress = null) => _handler.CreateClient(baseAddress);
    public void Dispose() => _handler.Dispose();
}
```

Registered as a Compono row-invoker parameter type (module initializer
calling `RowInvokerRegistry.Register`) and consumed as a `[Shared]`
composed theory parameter across **40+ test methods** in three test
classes (`AlexaInteractionModelClientTests`, `AlexaSkillInvocationClientTests`,
`SmapiDeveloperAccessTokenProviderTests`, `InSkillPurchasingClientTests`).
Grepped every consuming file for `.Dispose()`, `using var`, `using (`, or
any test-lifecycle cleanup referencing `HttpTestHarness` or `handler` —
**none found**. `Dispose()` is never called anywhere in the consuming
test code.

This is real, reproducible evidence of exactly the shape the research
question describes: a `[Shared]`, Compono-composed, `IDisposable` value
with no cleanup path. But its practical severity is low — `TestHttpHandler`
is a fake `HttpMessageHandler` with no real socket, file handle, or
unmanaged resource behind it; the "leak" is an unreleased managed object
for the remainder of the test process, not a resource exhaustion risk.
No test flakiness, CI resource pressure, or explicit workaround was found
in the repo's history or comments referencing this gap — the consumer
appears to be working correctly today, just not disposing something that
happens to implement `IDisposable`. Doc comments on the file describe it
solely as an IDE-discovery workaround, not a disposal workaround.

### dynamodb-distributed-lock — corrected: real evidence exists, on the migration branch

**Correction to the original pass of this research:** the initial finding
("this repo does not reference Compono at all") was wrong. It was checked
against the repo's default checkout, not the Compono migration branch. The
repo has two Compono-related branches —
`feat/compono-0.9.0-preview.88` (the PLAN-0056 dogfood migration itself,
commit `4f2e3bb feat: migrate to Compono (from AutoFixture) and
Compono.TestDoubles (from NSubstitute)`) and
`feat/compono-share-graph-wide-sharing` (a later `Share<T>()` dogfooding
pass) — and both do reference Compono. Re-verified directly with
`git grep`/`git show` against `feat/compono-0.9.0-preview.88`.

That branch contains **13 `[Shared] Meter` usages** (12 in
`test/DynamoDb.DistributedLock.Tests/DynamoDbDistributedLockTests.cs`, 1 in
`test/DynamoDb.DistributedLock.Tests/Retry/ExponentialBackoffRetryPolicyTests.cs`),
plus a separate, unrelated set of 12 `[Shared] IAmazonDynamoDB` usages on
the current default branch (`feat/compono-share-graph-wide-sharing`) — not
disposal-relevant, `IAmazonDynamoDB` there is a test double, not a real
client.

The `Meter` is registered as a factory in
`test/DynamoDb.DistributedLock.Tests/TestKit/Profiles/DynamoDbDistributedLockCompositionDefaults.cs`:

```csharp
builder.Register<Meter>(_ => new Meter(MetricNames.MeterName));
builder.Register<ILockMetrics>(context => new LockMetrics(context.Resolve<Meter>()));
```

`System.Diagnostics.Metrics.Meter` implements `IDisposable`. The doc
comment on this file explains *why* it's `[Shared]`: "a real Meter/ILockMetrics
pair (so a `[Shared] Meter` theory parameter lets a `TestMetricAggregator`
observe what the composed SUT actually publishes)" — i.e. `[Shared]` here
is doing identity/observation work (one `Meter` instance per test, shared
between the SUT and the assertion parameter), not disposal work. Grepped
every consuming test file for `Dispose`/`using` referencing `meter` —
**none found**; the only `Dispose`/`DisposeAsync` calls in that test file
are unrelated (`handle!.DisposeAsync()` on the lock handle under test).

**Is this meaningful disposal *ownership* evidence, or just an
`IDisposable`-shaped object that happens to be involved?** Mostly the
latter, and it's important not to overclaim here per the instruction not
to stretch a lifecycle-shaped API into disposal evidence merely because it
implements `IDisposable`. Concretely:

- The `Meter` is a **registration-factory-created** value (same pipeline
  path already covered in §2's ownership matrix), not a new path.
- `Meter.Dispose()` in .NET's own implementation only unpublishes the
  meter from `MeterListener` subscribers — no unmanaged handle, no socket,
  no file descriptor. Practically inert if never disposed in a
  short-lived test process, same severity class as the alexa-vox-craft
  `HttpTestHarness` finding (§ above), not a new, worse category.
- No test flakiness, listener leakage, or explicit workaround referencing
  `Meter` disposal was found in this repo's history or comments.

**What this correction actually adds:** a second, independent real
example of the same pattern already found in alexa-vox-craft — a
registration/factory-produced `IDisposable`, marked `[Shared]` for
identity/observation reasons unrelated to disposal, composed across
multiple tests, never disposed — this time via `Register<T>()` rather than
a row-invoker type. It broadens the evidence base for "this shape recurs
across independent dogfood repos" without introducing a new severity tier
or a new production path. It does not, on its own, change the weak-but-real
characterization below.

### Assessment

Two real examples now exist (alexa-vox-craft's `HttpTestHarness`,
dynamodb-distributed-lock's `Meter`), and both are weak evidence for
urgency: low-cost, no-observed-pain leaks of resources with no real
unmanaged handle behind them. They're useful as **proof of shape,
recurring independently across two unrelated dogfood repos** — this
pattern (a `[Shared]`, Compono-composed value that implements `IDisposable`,
never disposed) will plausibly recur with higher-stakes resources (a real
`HttpClient` against a containerized dependency, an `ActivityListener`, a
database connection) as more integration packages and dogfood repos land.
Neither example is, on its own, proof of a *current* production-blocking
gap. Per the instruction not to manufacture a requirement from weak
evidence: this finding supports "worth keeping the door open," not "must
ship before 1.0" — the second example strengthens the recurrence argument
but does not change that conclusion.

## 4. Prior art

**Microsoft.Extensions.DependencyInjection** (current official guidance,
[Dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)):

- "The container is responsible for cleanup of types **it creates**...
  Services resolved from the container should never be disposed by the
  developer." Ownership is determined purely by *who constructed the
  instance*, not by lifetime tier alone.
- **Explicit exception, stated by name:** `services.AddSingleton(new
  ExampleService())` — "the instance is not created by the service
  container... the framework does not dispose of the services
  automatically... the developer is responsible." This is the exact
  precedent ADR-0019 already independently arrived at for Compono's
  `IServiceProvider` fallback: container-constructed is owned,
  externally-supplied-instance is not.
- Transient/scoped disposal happens at scope end; singleton disposal at
  container disposal. Order is reverse-of-creation in the documented
  sample output (`ScopedDisposable.Dispose()` before
  `TransientDisposable.Dispose()`, i.e., the more recently resolved
  instance is disposed first).
- `ServiceProvider.DisposeAsync()` awaits each `IAsyncDisposable
  .DisposeAsync()` with `ConfigureAwait(false)` — async disposal is a
  first-class, distinct code path from sync `Dispose()`, not sync-over-async.
- Explicit anti-pattern warning: **"Async DI factories can cause
  deadlocks"** — calling `.Result`/`.GetAwaiter().GetResult()` on an async
  operation inside synchronous factory/disposal code is called out by
  name as something to avoid "at all costs." Directly relevant to §7.
- "Receiving an `IDisposable` dependency via DI doesn't require that the
  receiver implement `IDisposable` itself... shouldn't call `Dispose` on
  that dependency" — ownership doesn't propagate to consumers just
  because they hold a reference.

**AutoFixture** — the closest peer to Compono in problem shape (a
composition/data-generation library, not a runtime DI container).
Disposal tracking exists only as an **explicit opt-in**:
`DisposableTrackingCustomization`, which adds a `DisposableTrackingBehavior`
decorating the specimen builder pipeline. It is not enabled by default,
and using it correctly still requires the consumer to wire a test-lifecycle
hook (e.g. an `AutoDataAttribute` subclass disposing the customization in
an `After`/teardown step) — AutoFixture does not autonomously decide when
"the test is over" any more than Compono's `Create<T>()` does. This is
strong independent confirmation of the same conclusion Amendment 4 reached
for Compono: a general-purpose composition library, invoked as a pure
value factory with no natural teardown signal, cannot safely default to
owning disposal — it can only offer it as something the consumer turns on
and wires into their own lifecycle.

**xUnit v3** — disposal is tied to the *test class instance* lifecycle
(`IDisposable`/`IAsyncLifetime` implemented by the test class itself, or
class/collection fixtures), not to individual composed constructor/method
parameters. A `[Shared]`/`Compose`-composed parameter is not a test class
and gets no automatic disposal from xUnit itself — consistent with
Amendment 4's finding that `Compono.XunitV3`'s own `GetData` path has no
xUnit-provided hook to lean on for this either.

**TUnit** — not independently verified to the same depth (no compiled
source inspected); publicly documented shared-instance data sources do
have scoped-lifetime/keyed sharing concepts, but confirming their exact
disposal contract was out of scope for this pass. Flagged as a gap rather
than asserted.

**Net effect:** every piece of prior art examined converges on the same
principle Compono's own Amendment 4 already discovered independently:
*constructor-owns-it, externally-supplied-does-not*, and a
composition-only library (no lifecycle authority of its own) cannot
safely default to owning disposal — it can, at most, offer an explicit
opt-in.

## 5. Ownership models

**Model A — Compono owns nothing.** Matches current actual behavior
exactly (§1) and ADR-0019's already-adopted stance for the one
unambiguous path. Simple, and consistent with AutoFixture's default-off
posture. Cost: the §3 pattern (composed disposable, no cleanup) remains
the consumer's job to notice and solve, same as today.

**Model B — Compono owns values it constructs itself.** This is what
Amendment 4 actually tried to build, and it failed for a documented,
concrete reason: `CompositionRow.Resolve`/`ResolveShared` return "whatever
the pipeline produced with no visibility into which stage produced it" —
a generated-plan value is indistinguishable, at the point Compono would
need to decide, from a registration- or provider-returned one. This
research re-verified that claim directly against the current pipeline
(§1): no provenance tag exists anywhere between a value's construction and
its return. **Confirmed still true, not just historically true.** Building
this reliably would require new plumbing through every stage (generated
plans, every registration/provider kind, the `IServiceProvider` fallback)
to tag "Compono constructed this" — exactly the kind of "invent a query
the public API doesn't have, for one narrow purpose" Amendment 4 explicitly
declined to do without a real design dive.

**Model C — Ownership follows the producing registration/provider**
(explicit owned/external declaration on the registration or provider
itself). Feasible in principle — `Register<T>()`/`ICompositionValueProvider`
could grow an ownership flag — but no dogfood evidence today motivates the
added API surface (only one example exists, and it's the low-stakes case
in §3). This is really a variant of Model E scoped to the *producer* side
rather than the call site; treat as a design detail to weigh only if Model
E is pursued.

**Model D — Graph owns all disposable values that enter it.** This is
what a naive `CompositionScope`/`CompositionRow`-wide disposal stack would
look like, and it is the model Amendment 4's revert reasoning most directly
warns against: it would just as readily dispose an `IServiceProvider`-
returned singleton, or a registration-factory-returned cached instance a
consumer still holds a live reference to elsewhere, silently violating
ADR-0019's contract. Rejected for the same reason Amendment 4 rejected it,
independent of this research.

**Model E — Explicit opt-in ownership.** The consumer (or a future
registration/row API) explicitly marks a value as graph-owned; only those
values get disposed, and only through an API the consumer chose to invoke.
This is exactly the shape AutoFixture converged on (`DisposableTrackingCustomization`,
opt-in, wired into the consumer's own test lifecycle) and exactly what
Amendment 4's own "Consequence" paragraph already recommended as the
workaround: "compose an explicit factory/wrapper the test itself
disposes." **No new model needed beyond this — Model E is not a novel
proposal, it's already the de facto documented guidance from Amendment 4,
just not yet formalized as a first-class API.**

## 6. Where a disposal boundary would live

`Create<T>()` returns bare `T` — no session, lease, or wrapper object.
This is the crux of why Model B/D can't be retrofitted cheaply: there is
nowhere to attach a disposal handle to the return value of `Create<T>()`
without changing its signature, which would be breaking for every
existing caller.

`CompositionRow` is different: it's already a public, per-graph-lifetime
object (ADR-0021) distinct from the values it produces, and it's evidence
that a graph-lifetime abstraction *exists* today for the callers who
directly own a row — e.g. a future `CompositionRow.DisposeAsync()`/
`IAsyncDisposable` that disposes only values the consumer explicitly
registered as owned (Model E), never values it merely resolved. Adding
`IAsyncDisposable`/`IDisposable` to `CompositionRow` later is **additive,
not breaking** — it's a `sealed` class today, and implementing a new
interface on an existing type doesn't break existing callers who never
call `Dispose`/`DisposeAsync` on it (nothing about the class holds
unmanaged resources itself, so no finalizer obligation exists in the
interim).

**Qualification: this does not mean `CompositionRow` alone would solve
disposal for normal `[Compose]` framework usage.** In the `Compono.XunitV3`/
`Compono.TUnit` `[Compose]` binding path, the test method itself never
receives or controls the `CompositionRow` — `Composer.CreateRow` is called
internally by the bind-plan machinery (ADR-0022), and the row is
discarded after binding each parameter. A test author writing
`public void MyTest([Shared] Foo foo)` has no reference to a
`CompositionRow` to call `Dispose`/`DisposeAsync` on, even if that method
existed. So a future opt-in disposal design would need one or some
combination of:

- **framework lifecycle integration** — `Compono.XunitV3`/`Compono.TUnit`
  itself would need to hold onto the row for the test's duration and call
  `Dispose`/`DisposeAsync` on it at the point their own test-lifecycle
  hooks fire (e.g. after the test method returns, mirroring how xUnit
  disposes `IAsyncLifetime` test classes today per §4);
- **an explicit lifetime/session API** — a new, more session-shaped entry
  point a consumer opts into directly, distinct from today's
  fire-and-forget `Composer.CreateRow`/`Create<T>()`/`CreateMany<T>()`
  calls;
- **`CompositionRow` disposal for the subset of callers who already hold
  one directly** — e.g. a consumer calling `Composer.CreateRow(...)`
  themselves outside the `[Compose]` binding path, where they already
  have the reference needed to dispose it.

`CompositionRow` being public and already the graph-lifetime object is
evidence that the *object* a future disposal boundary would attach to
already exists — it is not, by itself, a selected or sufficient
implementation for the dominant `[Compose]` framework-attribute usage
pattern. Framework-side wiring would still be new work in whichever
package (`Compono.XunitV3`, `Compono.TUnit`) picks this up. This doesn't
change the §6 conclusion that today's public API doesn't foreclose the
option — it only clarifies that "doesn't foreclose" is not the same claim
as "already solves it."

`CreateMany<T>()` returns `IReadOnlyList<T>` — same bare-value problem as
`Create<T>()`, once per item, with no aggregate handle either.

**Does the current 1.0 public API foreclose a future disposal model?**
No. `Create<T>()`/`CreateMany<T>()` staying bare-`T`-returning is
consistent with them **never** growing ownership (Model A/D would need a
different entry point entirely, or a breaking return-type change — but
neither is being proposed). A future opt-in mechanism (Model E) doesn't
need to touch `Create<T>()` at all — it would live on `CompositionRow`
(already public, already the graph-lifetime object, for callers who
directly own one), on framework-side lifecycle wiring inside
`Compono.XunitV3`/`Compono.TUnit` for the `[Compose]` binding path (see
the qualification above), or on a new, additive, explicitly-named API
surface consumers opt into — none of which requires changing `Create<T>()`/
`CreateMany<T>()`'s signature. **No compatibility-preserving change is
needed pre-1.0** — this is the basis for Outcome C below, not Outcome B.
`CompositionRow` is evidence that a graph-lifetime abstraction already
exists, not a prematurely selected future disposal implementation.

## 7. Sync vs. async disposal

`IDisposable`-only values: no complication — a future `Model E` API could
dispose these synchronously from a synchronous `CompositionRow.Dispose()`.

`IAsyncDisposable`-only or dual-interface values: cannot be safely
disposed from a synchronous API. The MS DI guidelines page explicitly
names sync-over-async disposal/resolution as a deadlock-risk anti-pattern
("at all costs"); the same risk applies here — a hypothetical
`CompositionRow.Dispose()` calling `.GetAwaiter().GetResult()` on
`DisposeAsync()` would be exactly that anti-pattern, unacceptable for the
same documented reason MS DI warns against it.

**This does not create pressure for async composition.** Disposal happens
strictly *after* full graph construction completes — it is a teardown
concern, not a construction concern, and the two are separable in time.
A future `CompositionRow.DisposeAsync()` (async disposal) is fully
additive alongside `Composer.Create<T>()`/`CreateMany<T>()` staying
synchronous (ADR-0001's no-reflection/generation-first posture doesn't
speak to async at all, and nothing here requires revisiting it). This
directly answers the request's question: **disposal can be added later
without adding async creation** — confirmed, not assumed. (This finding
is scoped narrowly to disposal; it says nothing about whether a *future*
async-composition investigation is independently warranted for other
reasons — see the note in the conclusion.)

## 8. Disposal ordering

If Compono ever owned multiple disposables (Model E, opt-in), correct
teardown order is LIFO/reverse-construction-order — the same principle
MS DI's own sample demonstrates (later-resolved `TransientDisposable`
disposed before earlier-resolved `ScopedDisposable`) and the general
"a dependency must outlive the dependent that was built after it and may
reference it" invariant.

**Does the current engine have enough information to do this correctly
today?** No. `_activeFrames`/`_activeFactories`/`_activeProviderRequests`
on `CompositionContext` are transient recursion-detection stacks — pushed
on entry to a request, popped on exit, never retained after construction
finishes. There is no persisted "construction order" list anywhere. Any
future Model E implementation would need new state (e.g. an ordered list
appended to on each opt-in-owned construction) — this is a real, novel
piece of bookkeeping Compono does not have today, not a matter of exposing
something already tracked.

Additional considerations for a future implementation (not resolved
here, since no implementation is in scope):
- **Shared value referenced by multiple consumers, must dispose exactly
  once:** `CompositionScope`'s existing type-keyed dedup (`[Shared]`/
  `Share<T>()`) is a reasonable primitive to build "dispose once" on top
  of, since it already guarantees single-instance-per-type-per-graph — but
  the dedup mechanism itself has no disposal awareness today (§9).
- **Exceptions during composition (partial graph):** whatever was
  successfully constructed before a mid-graph failure would, under Model
  E, still need disposing in reverse order — today, a `CompositionException`
  mid-construction unwinds with no attempt to clean up partially-built
  disposable values, because nothing tracks them to begin with.
- **Exceptions during disposal:** unresolved design question for any
  future implementation — whether to aggregate (`AggregateException`,
  matching `ServiceProvider`'s own approach) or fail-fast. Out of scope to
  decide here; flagged for the future ADR if Model E is pursued.

## 9. Interaction with `Share<T>()` and `[Shared]`

ADR-0056 already answers most of this directly, in its own "Disposal"
section (read verbatim, not paraphrased from memory): `Share<T>()` "is
**not solved by this ADR**... introduces no new lifetime category beyond
the disposal questions ADR-0022 Amendment 4 already left open... a
`CompositionRow`/`Composer` has no visibility into which pipeline stage
produced a value, so it cannot safely decide whether disposing it is even
Compono's responsibility." ADR-0056 explicitly assigns this exact
research the job of resolving that question for both mechanisms
together, and states it doesn't foreclose the outcome.

This research's own findings confirm ADR-0056's framing was correct, not
merely cautious:

- **A graph-shared disposable should *not* be assumed disposed-once by
  virtue of being shared.** Graph-scoped identity (`[Shared]`/`Share<T>()`
  guaranteeing one instance per type per graph) and disposal ownership
  (who is responsible for ending that instance's lifetime) are genuinely
  separate questions — proven by the `IServiceProvider`-fallback case in
  the ownership matrix (§2): a shared value can originate from a source
  ADR-0019 already says Compono must never dispose, so "shared ⇒ dispose
  once" would be actively wrong for that source. Sharing narrows the
  *count* of instances Compono would need to manage; it says nothing about
  *whether* Compono is entitled to manage them at all.
- **`[Shared]` and `Share<T>()` need identical disposal semantics** — not
  a new finding, just confirmed: both are thin surface forms over the
  exact same `CompositionScope` write path (`ResolveDescriptorAsShared`/
  `ShareExplicitTestParameter`), so any future disposal design (Model E)
  necessarily treats them uniformly; there is no seam in the underlying
  mechanism to give them different behavior even if that were desired.
- **Externally-supplied shared values make automatic (Model D) graph
  disposal unsafe** — this is not new either; it's the same ADR-0019
  case, just reachable through one additional route (`ShareExplicit`/
  `[Compose(existingInstance)]`, where the consumer explicitly hands
  Compono a value it still holds a reference to and plans to reuse or
  dispose itself).

**No finding here requires revisiting ADR-0056's actual decision**
(lifetime boundary, creation timing, resolution precedence) — this
section only confirms what ADR-0056 already flagged as open and hands
back a direct answer to the question it deferred, per its own request.

## 10. Interaction with future integrations

Brief, as scoped — not a feature design. The governing principle already
exists and generalizes cleanly: **Compono must never become the owner of
another framework's lifecycle object.** ADR-0019's "the caller owns the
provider and its entire lifetime" stance, applied to future integrations:

- **MSTest / NUnit integration:** same shape as `Compono.XunitV3`/
  `Compono.TUnit` today — a composed parameter is produced fresh per
  test/row; whatever test-lifecycle hooks those frameworks offer
  (`TestContext`, `[TearDown]`, etc.) remain the consumer's/framework's own
  mechanism, not something Compono reaches into.
- **`WebApplicationFactory`/ASP.NET Core integration:** the clearest risk
  case named in the request — a `WebApplicationFactory<T>` or the
  `IServiceScope` it hands out must **not** become something Compono's row
  or graph accidentally owns and disposes; this would be a Model D-style
  mistake at integration-package scope rather than core-Compono scope, and
  the same Amendment 4/ADR-0019 reasoning rules it out identically.

No feature design is implied here; this is context confirming Model E
(and the `CompositionRow`-as-boundary idea from §6) is where any future
integration-specific disposal help would have to be layered on top of,
never a substitute for that integration's own lifecycle authority.

## 11. Spike

**None performed, deliberately.** The request's own spike-worthiness
questions — chiefly "can we distinguish generated-plan creation from
provider-supplied values reliably?" — were already answered by a real,
already-executed spike-equivalent inside this repo's own history:
ADR-0022 Amendment 4's PR #24 *was* that experiment, run against the real
production pipeline (not a throwaway prototype), and its finding (no,
`CompositionRow.Resolve`/`ResolveShared` carry no such distinction) was
independently re-verified in §1 of this research directly against the
current source, not merely cited from the ADR's prose. Re-running an
equivalent spike now would reproduce the same already-known negative
result. `git status` was left clean throughout this research — no code
was written or modified.

## 12. Recommendation: **Outcome C — Post-1.0 additive feature**

No disposal feature needs to ship before 1.0, and **no current 1.0
contract needs to change** to keep a sensible future disposal model
additive. Reasoning:

- Current behavior already **is** Model A in practice (§1) — nothing to
  undo or migrate away from.
- `Create<T>()`/`CreateMany<T>()` returning bare `T`/`IReadOnlyList<T>`
  does not foreclose a future opt-in mechanism, because that mechanism's
  natural home is `CompositionRow` (§6) — already public, already the
  graph-lifetime object for callers who directly own one, evidence that
  the abstraction exists rather than a prematurely selected
  implementation — plus, for the dominant `[Compose]` framework-attribute
  path, future lifecycle wiring inside `Compono.XunitV3`/`Compono.TUnit`
  themselves (§6). Adding a disposal interface to `CompositionRow` later
  is additive, not breaking.
- Model B/D are not merely undesirable, they're **provably infeasible
  without new pipeline-wide provenance plumbing** — Amendment 4 already
  tried and reverted the closest real attempt, and this research
  re-confirmed the same limitation still holds today.
- The one path with a clean answer (`IServiceProvider` fallback) already
  has one, settled by ADR-0019, unaffected by any of this.
- Real dogfood evidence exists (§3) but is weak — two independent
  low-stakes leaks (alexa-vox-craft's `HttpTestHarness`,
  dynamodb-distributed-lock's `Meter`), no reported pain in either — enough
  to justify *not closing the door*, and enough recurrence across
  unrelated repos to take the shape seriously, but not enough to justify
  building Model E now. AutoFixture's independent convergence on the same
  opt-in-only shape (§4) suggests Model E is the right target *if and
  when* real pressure (a costlier disposable resource, more dogfood
  friction) materializes.

This is **not** Outcome D ("deliberately never own disposal"): the
evidence doesn't support closing the door permanently — the
`HttpTestHarness`/`Meter` pattern is exactly the shape that gets worse
with real resources (a live `HttpClient`, an `ActivityListener`, a
database fixture) as more integration packages land, and Amendment 4's
own suggested workaround (an explicit consumer-owned wrapper) is already
informally Model E in embryo. It is **not** Outcome A either — nothing
found here rises to "must change 1.0 API now."

**Should this be added to the remaining pre-1.0 list?** No, as a feature.
Yes, as a documentation item (§13) — the current Model A behavior and the
Amendment-4-derived workaround pattern (compose an explicit
consumer-disposed wrapper for anything disposable, exactly like
`HttpTestHarness` already does) should be written down as explicit
guidance before 1.0, so it's a documented design stance rather than a
silent gap discoverable only by reading ADR-0022's amendment history.

## 13. Documentation/skill implications (not applied — description only)

**This is not a pre-1.0 code feature.** It is a pre-1.0 documentation
item: recording the current non-ownership contract explicitly, so it's a
documented design stance rather than a silent gap. The minimal content
that contract needs to state, wherever it lands (see the file list
below):

- Compono does not dispose composed values, regardless of source.
- `Share<T>()`/`[Shared]` define identity/lifetime-within-the-graph
  boundaries, not ownership (§9) — a shared value is not thereby a
  Compono-owned value.
- Values obtained via the `IServiceProvider` fallback remain externally
  owned (ADR-0019), unaffected by this research.
- Consumers remain responsible for disposing any disposable resource they
  compose through Compono today (the `HttpTestHarness`/`Meter`
  consumer-owned-wrapper pattern from §3).
- Framework-owned resources (an `IServiceScope`, a `WebApplicationFactory`,
  a host) remain owned by their own framework/consumer, never by Compono's
  row or graph (§10).

If Outcome C's recommendation is accepted, the concrete file-level changes
this implies:

- **`docs/architecture.md`** — the composition pipeline description
  should state explicitly that Compono never disposes any value it
  produces or hands back, regardless of source, with a one-line pointer
  to ADR-0022 Amendment 4 and this research for the reasoning.
- **`docs/public-api.md`** — `Create<T>()`/`CreateMany<T>()`/`CompositionRow`
  should each get an explicit "this API does not manage disposal of the
  value(s) it returns" note, mirroring how ADR-0019 already documents the
  `IServiceProvider`-fallback non-ownership contract, generalized to every
  path.
- **`skills/compono/SKILL.md`** (and any Compono-testing reference under
  it) — should carry the Amendment-4-derived guidance pattern: if a
  composed value is disposable, wrap it in a small consumer-owned type
  (the `HttpTestHarness` shape) and dispose that wrapper through whatever
  mechanism the consuming test framework already offers, rather than
  expecting Compono to do it.
- **Framework integration docs** (`Compono.XunitV3`, `Compono.TUnit`, any
  future MSTest/NUnit/`WebApplicationFactory` integration doc) — should
  each carry the §10 non-ownership boundary explicitly, especially before
  a `WebApplicationFactory`-style integration is designed, so its author
  isn't tempted to reach for Model D at integration scope.
- **Examples/evals** — any Compono example involving `HttpClient` or
  another disposable dependency should demonstrate the consumer-owned
  wrapper pattern directly, not merely mention it in prose.

No existing documentation was found to be factually wrong about current
disposal behavior during this research (there is simply no disposal
documentation yet to be wrong) — no out-of-band doc corrections were
needed or made.

## Evidence index

- `src/Compono/Composer.cs`, `CompositionContext.cs`, `CompositionRow.cs`,
  `CompositionScope.cs`, `ICompositionValueProvider.cs`,
  `CompositionRegistrations.cs` — read directly for §1/§2/§6/§8.
- [ADR-0019](../adr/0019-registrations-and-service-provider-injection.md) —
  the `IServiceProvider`-fallback non-ownership contract.
- [ADR-0022](../adr/0022-compono-xunit-package-design.md) Amendment 4
  (2026-07-31) — the shipped-then-reverted automatic disposal tracking
  attempt; primary evidence for §1/§2/§5/§11.
- [ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md) —
  `Share<T>()`'s own "Disposal" section, explicitly deferring to this
  research; primary evidence for §9.
- `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft`,
  `test/AlexaVoxCraft.Smapi.Tests/TestKit/HttpTestHarness.cs` and the
  `InSkillPurchasing.Tests` copy — primary evidence for §3.
- `/Users/ncipollina/source/repos/layered-craft/dynamodb-distributed-lock`,
  branch `feat/compono-0.9.0-preview.88`,
  `test/DynamoDb.DistributedLock.Tests/TestKit/Profiles/DynamoDbDistributedLockCompositionDefaults.cs`
  and `DynamoDbDistributedLockTests.cs`/`ExponentialBackoffRetryPolicyTests.cs` —
  primary evidence for §3 (corrected finding; the default checkout used in
  the original pass had no Compono usage, but this migration branch does).
- [.NET dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) —
  current official MS Learn doc, fetched directly; primary evidence for
  §4/§7/§8.
- AutoFixture `DisposableTrackingCustomization`/`DisposableTrackingBehavior`
  (via web search of AutoFixture's own GitHub issues/docs) — primary
  evidence for §4/§5.

## Links

- Feeds a future ADR only if/when Model E gains real motivating pressure
  (per Outcome C) — no ADR drafted by this research.
- Directly answers the deferral in ADR-0056's "Disposal" section.
- Should inform the later async-composition investigation only narrowly:
  §7 establishes that disposal and creation are separable concerns, so
  that investigation should not treat "we might need async disposal
  someday" as an argument for async `Create<T>()` — the two are
  independent questions with independent timelines.
