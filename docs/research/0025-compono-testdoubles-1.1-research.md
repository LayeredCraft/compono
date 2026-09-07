# [RESEARCH-0025] Compono.TestDoubles 1.1 Research

**Status:** Done (research only; no ADR yet)

This is one of three parallel 1.1-scoping investigations (Http and Logging
are separate). It covers `Compono.TestDoubles` only.

## 1. Current package responsibility

`Compono.TestDoubles` gives Compono an AOT-safe, source-generated
alternative to `Compono.NSubstitute` for satisfying an otherwise-
unresolvable interface dependency in a composition graph — no runtime
proxy, near-zero reflection. It was admitted narrowly
([ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md))
specifically as "a fallback default-value generator for otherwise-
unresolvable composition-graph leaves," explicitly **not** a
general-purpose mocking framework competing with
NSubstitute/Moq/FakeItEasy on breadth. [ADR-0043](../adr/0043-compono-generated-test-doubles-design.md)
(21 amendments, all pre-implementation PR review) is the deep design;
[ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)
(overloads/generics/verification, 21 amendments),
[ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)
(configuration-required members),
[ADR-0048](../adr/0048-testdoubles-argument-matching-and-call-verification.md)
(argument matching),
[ADR-0049](../adr/0049-testdoubles-generic-return-closed-instantiation-configuration.md),
[ADR-0050](../adr/0050-testdoubles-multi-entry-argument-distinguished-configuration.md),
[ADR-0053](../adr/0053-testdoubles-invocation-aware-callback-responses.md), and
[ADR-0054](../adr/0054-testdoubles-sequential-call-count-based-responses.md)
each added a real, dogfooding- or requester-evidenced capability on top.
The package has shipped and iterated 16 times
(`git log --oneline -- src/Compono.TestDoubles` = 16 commits, PR #82
through #118) since 2026-08-13, entirely pre-1.0 — 1.0.0 shipped with the
full feature set described below already in place.

## 2. Current architecture

Three physical layers, deliberately split across two assemblies:

1. **Generator emission** (`src/Compono.Generators`) — `Discovery/TestDoubleAnalyzer.cs`
   (2,310 lines), `Discovery/TestDoubleDefaults.cs`,
   `Discovery/TestDoubleMemberIdentityResolver.cs`,
   `Emitters/TestDoubleEmitter.cs`, `Emitters/TestDoubleIdentifierNaming.cs`,
   `Emitters/TestDoubleOverloadIdentity.cs`, `Models/DiscoveredTestDoubleInfo.cs`
   and four sibling model types, `Templates/TestDouble.scriban` (690 lines).
   Extends `LeafTypeClassifier`'s existing interface-leaf branch
   ([ADR-0024](../adr/0024-public-provider-extensibility-model.md)
   Amendment 2) with a third, compile-time-gated outcome
   (`ComponoGeneratedTestDoubles=true/false`, default `false`, surfaced via
   a `CompilerVisibleProperty` core `Compono` ships). Ships inside core
   `Compono.Generators` — inert unless the MSBuild property is set — never
   inside the optional `Compono.TestDoubles` package.
2. **Core runtime primitives** (`src/Compono/`) — `ReturnConfig.cs`,
   `ReturnConfigBuilder.cs`, `Match.cs`, `CallVerifier.cs`,
   `SequenceOutcome.cs`, `Unit.cs`, `GeneratedTestDoubleRegistry.cs`,
   `TestDoubleNotConfiguredException.cs`, `TestDoubleVerificationException.cs`.
   All `namespace Compono`, all public types in core `Compono.dll`.
3. **Optional runtime package** (`src/Compono.TestDoubles/`) — exactly two
   public types: `GeneratedTestDoubleProvider` (an
   `ICompositionValueProvider`) and `CompositionBuilderExtensions`
   (`UseGeneratedTestDoubles()`), proven by
   `test/Compono.TestDoubles.Tests/PublicApiSurfaceTests.cs`:

   ```csharp
   publicTypeNames.Should().BeEquivalentTo([
       "Compono.GeneratedTestDoubleProvider",
       "Compono.CompositionBuilderExtensions",
   ]);
   ```

**End-to-end flow for a discovered interface leaf `IRepository`:**

- The generator walks the same composition-discovery closure Compono
  already computes for every other leaf (`composer.Create<T>()`/
  `CreateMany<T>()` call sites, `[Compose]` test-method parameters,
  `[Composable]`-declared graphs). If `ComponoGeneratedTestDoubles=true`
  and `IRepository` is reachable there, the generator emits (once per
  distinct interface symbol, `SymbolEqualityComparer`-deduplicated) a
  single file: an `internal sealed class <Hash>_Double : IRepository`
  (explicit-interface-implemented members only — no public surface on the
  concrete type), a companion `internal static class <Hash>_DoubleConfiguration`
  (same-named extension methods, resolved by ordinary overload resolution
  since the interface member isn't in scope on the concrete type),
  `Configure()`/`Verify()` bridge extensions on the *interface* type, and
  a `file`-scoped `[ModuleInitializer]` that calls
  `GeneratedTestDoubleRegistry.RegisterFactory<IRepository>(() => new <Hash>_Double())`.
- At runtime, `GeneratedTestDoubleProvider.TryProvide` calls
  `GeneratedTestDoubleRegistry.TryCreate(requestedType, out value)` — a
  plain `ConcurrentDictionary<Type, Func<object>>` lookup
  (`src/Compono/GeneratedTestDoubleRegistry.cs:33`), first-registration-wins,
  populated purely by consumer-generated module initializers, never by
  `Compono`/`Compono.TestDoubles` themselves.
- `[Shared] IRepository repository` works with **zero change** to
  `CompositionScope`'s existing exact-requested-type storage — the double
  is stored under `IRepository`, same mechanism `Compono.NSubstitute`
  already uses. `repository.Configure()` is a generator-emitted, provably
  safe-by-construction downcast (`repository as <Hash>_Double`).

**Why this shape exists** (three assembly-boundary defects found and fixed
during ADR-0043's own pre-implementation review, none discovered by
building and shipping — all caught by PR review before any code existed):
a runtime-package generic `Configure<T>()` method can't return a type that
doesn't exist until the *consumer's* later compilation (Amendment 1); a
precompiled `GeneratedTestDoubleProvider` can't reference a lookup that
only exists in the consumer's own generated code, forcing the registry and
`ReturnConfig<T>`/`ReturnConfigBuilder<T>` into core `Compono` instead
(Amendment 2); and `internal` fields don't cross the
core-`Compono`-to-consumer-assembly boundary, forcing `ReturnConfig<T>`'s
read side and `ReturnConfigBuilder<T>`'s constructor `public` (Amendment 3).

## 3. Current dependency graph

```
src/Compono.TestDoubles.csproj
  --ProjectReference--> src/Compono.csproj (PrivateAssets="none")

src/Compono.csproj
  --ProjectReference (analyzer-only)--> src/Compono.Generators.csproj (netstandard2.0)
```

Confirmed directly from `src/Compono.TestDoubles/Compono.TestDoubles.csproj:12-14`:

```xml
<ItemGroup>
    <ProjectReference Include="..\Compono\Compono.csproj" PrivateAssets="none" />
</ItemGroup>
```

`Compono.TestDoubles.dll` has a real, non-optional compile-time and
runtime dependency on `Compono.dll` — it isn't a thin veneer that merely
*happens* to layer on top; `GeneratedTestDoubleProvider` implements
`Compono.ICompositionValueProvider` and calls
`Compono.GeneratedTestDoubleRegistry.TryCreate`, and
`CompositionBuilderExtensions.UseGeneratedTestDoubles()` extends
`Compono.CompositionBuilder` directly. There is no reverse dependency —
core `Compono` never references `Compono.TestDoubles` (confirmed by
`GeneratedTestDoubleRegistry`'s own doc comment: "Read by
`Compono.TestDoubles`'s `GeneratedTestDoubleProvider` - core `Compono` has
no reference the other way").

`Compono.Generators` is shared, analyzer-only infrastructure — the exact
same generator project also emits the composition plan itself, row
invokers, and (per grep evidence below) `Compono.Logging`'s activation
code. It is not a `Compono.TestDoubles`-specific generator; it's core
Compono's own generator, extended with one more compile-time-gated
discovery branch.

Two other first-party packages already reuse the exact same core
primitives with **no generator involvement of their own**, evidence that
`CallVerifier`/`Match<T>` have already become de facto shared runtime
infrastructure, not `Compono.TestDoubles`-private types:

- `src/Compono.Http/Compono.Http.csproj:31` (comment): *"Match<T>/CallVerifier
  are reused directly from Compono with no generator involvement."*
  `HttpResponseRegistration.cs:56`: `public CallVerifier Verify() => new(_matchedCallCount, _description);`
- `src/Compono.Logging/LogVerificationBuilder.cs:8-10`: *"core `CallVerifier`
  - `Once`/`Never`/`Exactly` each build a `CallVerifier` from the filtered
  match count right here."*

## 4. Existing 1.0 public contract

**`Compono.TestDoubles.dll`** (2 public types, locked by
`PublicApiSurfaceTests`):

- `GeneratedTestDoubleProvider : ICompositionValueProvider`
- `CompositionBuilderExtensions.UseGeneratedTestDoubles()`

**Core `Compono.dll`** (public types a generated double's own generated
code depends on, per Section 2's assembly-boundary history):

- `ReturnConfig<T>` (struct — internal mutable fields, public
  `HasConfiguredValue`/`HasConfiguredException`/`HasConfiguredSequence`/
  `ConfiguredValue`/`ConfiguredException`/`ConfiguredCallCount`,
  `[EditorBrowsable(Never)]`-hidden `RecordCall()`/`ClearConfiguredResponse()`/
  `NextSequenceOutcome()`)
- `ReturnConfigBuilder<T>` (`ref struct`; `Returns`/`Throws`/`ReturnsSequence`,
  each last-configuration-wins over the other two)
- `SequenceOutcome<T>` / `SequenceOutcome.Throw(Exception)`
- `Match<T>` / `Match.Any<T>()` / `Match.Is<T>(predicate)`
- `CallVerifier` (`Never()`/`Once()`/`Exactly(n)`)
- `Unit` (void-marker struct)
- `GeneratedTestDoubleRegistry.RegisterFactory<T>`/`.TryCreate`
- `TestDoubleNotConfiguredException`, `TestDoubleVerificationException`

**Generated per-interface surface** (never hand-referenced by a
consumer): `Configure()`/`Verify()` extension bridges on the interface
type; per-member `Configure().Member(...)`/`Verify().Member(...)`
extensions on the generated double type; `<Member>Matching(...)` aliases
for eligible overloads (ADR-0044 Amendment 21); per-closed-`T`
configuration for a self-referencing generic return (ADR-0049).

**Diagnostics** — `CMP0020`–`CMP0032` and `CMP0035`–`CMP0037`, all
informational (`docs/reference/diagnostics.md:7-34`), never fail the
build; most reject a whole interface leaf back to the ordinary
runtime-provider path, a scoped subset (`CMP0022`, `CMP0029`, `CMP0030`,
`CMP0031`) withholds only one member's surface, `CMP0032` is an
informational count of configuration-required members.

## 5. Consumer ergonomics review

Classification against the requested scenario list, evidenced by
`skills/compono/references/testdoubles.md`, the ADRs above, and
`test/Compono.TestDoubles.AotSmokeTest/Program.cs` (a real, exercised
end-to-end proof of every listed capability under Native AOT):

| Scenario | Classification | Evidence |
|---|---|---|
| Exact call counts | **Already supported cleanly** | `CallVerifier.Exactly(n)` |
| Min/max call counts (`AtLeast`/`AtMost`) | **Unsupported, intentionally out of scope (for now)** | Explicitly and repeatedly excluded across ADR-0044 (`skills/.../testdoubles.md:363`), with ADR-0048's own admission note: *"Call-order verification has zero real evidence"* — same evidence bar applies; no dogfooding case has yet forced this |
| Negative verification | **Already supported cleanly** | `Verify().Member().Never()` |
| Argument capture (arbitrary later inspection) | **Unsupported but high-value** | Skill doc's own explicit boundary: "true argument capture for later arbitrary inspection outside a generated `Verify().Member(Match...)` count assertion" is named as the #1 remaining AutoFixture/NSubstitute-habit trap |
| Call inspection (see full argument list per call) | **Unsupported but high-value** | Same boundary — `Match.Is<T>(predicate)` tests one argument per configured entry at configure time; there is no way to retrieve the actual argument values used across a member's calls |
| Callbacks (invocation-aware responses) | **Already supported cleanly** | `ReturnsCallback((left, right) => left + right)` — ADR-0053, real generated strongly typed delegate per eligible member |
| Side effects (mutate external state from a callback) | **Supported but awkward** | Achievable only by closing over external state inside a `ReturnsCallback` closure — no dedicated `Callback(...)`-without-a-return-value primitive the way Rocks/NSubstitute expose one |
| Throwing exceptions | **Already supported cleanly** | `ReturnConfigBuilder<T>.Throws(exception)` |
| Sequential exceptions/responses | **Already supported cleanly** | `ReturnsSequence(...)` (ADR-0054), mixed value/exception, per-entry independent ordinal |
| Async `Task<T>` responses | **Already supported cleanly** | First-class; deterministic defaults (`Task.CompletedTask`, empty collections) plus configuration-required fallback for non-nullable `T` |
| Async `ValueTask<T>` responses | **Already supported cleanly** | Same treatment as `Task<T>` throughout |
| Cancellation-related behavior | **Supported but awkward** | A `CancellationToken` parameter is just another matchable/discardable argument — no dedicated cancellation-aware default (e.g. auto-throwing `OperationCanceledException` when a configured token is cancelled); consumers configure it manually like any other member |
| Property setters | **Already supported cleanly** | Real auto-property semantics (ADR-0043 Amendment 7) — getter returns last-written or configured value |
| Events | **Unsupported and intentionally out of scope** | ADR-0042 Non-Goal, unchanged through all amendments |
| `ref`/`out`/`in` parameters | **Unsupported and intentionally out of scope** | ADR-0042 Non-Goal; diagnosed (`CMP0026`/scoped `CMP0030`), falls back cleanly |
| Generic methods (return independent of own `T`) | **Already supported cleanly** | Non-generic `Configure()`/`Verify()` slot covers every closed instantiation |
| Generic methods (return depends on own `T`) | **Already supported cleanly** | Per-closed-`T` configuration (ADR-0049) |
| Generic methods (value-type-constrained `T?`) | **Unsupported and technically problematic** | Explicitly still unsupported — `System.Nullable<T>` shapes the generator "cannot represent without reflection or boxing" |
| Overload-heavy interfaces | **Already supported cleanly** | Per-overload `Configure()`/`Verify()` discriminator (ADR-0044), plus `<Member>Matching` alias for argument-level distinction within one overload (Amendment 21) |
| Indexers | **Unsupported and intentionally out of scope** | ADR-0042 Non-Goal; diagnosed, falls back cleanly |
| Default interface members | **Already supported cleanly** | Full DIM-fallback support, including the "derived `new` redeclaration wins, base view forwards and shares call-recording state" case (ADR-0044 Amendment 20) |
| Inherited interfaces | **Already supported cleanly** | "Full base-interface closure" — `IRepository : IClock` gets `IClock.UtcNow` too |
| Partial behavior (partial substitutes, some real/some faked) | **Unsupported and intentionally out of scope** | ADR-0042 Non-Goal ("no class/partial mocking"); interfaces only, no wrapping a real implementation |
| Reset/clear call history | **Unsupported but high-value** | No `ClearReceivedCalls()`-equivalent found anywhere in the public surface or docs; a fresh double must currently be re-composed (`composer.Create<T>()` again) to reset state, which is often not what a `[Shared]`-scoped multi-phase test wants |
| Multiple verifier operations (chained/independent assertions) | **Already supported cleanly** | Each `Verify().Member(...)` call is independent and stateless — no shared mutable verifier object to worry about ordering |
| Ordered verification (call-order across members) | **Unsupported and intentionally out of scope** | ADR-0044/ADR-0048 both record this as excluded for lack of real evidence, not infeasibility |
| Configuring based on arguments | **Already supported cleanly** | `Match<T>` (literal/`Any`/`Is<predicate>`) on eligible members, multi-entry with last-registration-wins precedence (ADR-0050) |

**The single "next missing primitive."** Weighing architectural leverage,
not raw feature count: **an exposed, per-member received-call record** —
a small, generated, argument-tuple log the existing call-count machinery
already almost has (`ReturnConfig<T>.RecordCall()` already increments a
counter on every dispatch; the missing piece is retaining the arguments,
not just the count). This one primitive would unlock, without a second
separate design effort each:

- **Argument capture** — directly, by exposing the retained tuples.
- **Call inspection** — directly, same mechanism.
- **A real `Reset()`/`ClearCalls()`** — a natural companion once there's a
  log object to clear, rather than only a scalar counter to zero.
- Partially, **min/max call counts** — `AtLeast`/`AtMost` become one-line
  additions to `CallVerifier` once `observedCount` already exists (they
  need no new state at all, independent of the capture question — this
  is genuinely the cheapest of the group and could ship alone).

By contrast, **generalized call-count constraints** alone (just adding
`AtLeast`/`AtMost` to `CallVerifier`) is real but narrower leverage — it
closes one explicitly-flagged gap without addressing the "matching is not
capture" boundary the skill doc itself names as the more consequential
one. **Response factories based on arguments** already shipped
(`ReturnsCallback`, ADR-0053) — not a remaining primitive.
**Configured exception responses** already shipped (`Throws`,
`ReturnsSequence`). **Exposed received-call records** is the one primitive
from the candidate list that is both still missing and has multi-scenario
leverage, matching the external-research precedent below (Rocks'
`Callback(a => value = a)` is exactly a hand-rolled version of "expose the
call" that a first-class captured-record primitive would make
unnecessary).

## 6. Observed or likely friction

- The skill doc's own dedicated section — "The #1 AutoFixture/NSubstitute-habit
  trap: matching is not capture" — is the single most load-bearing piece
  of friction documentation in the whole reference file, strongly
  suggesting real dogfooding pain drove it, not speculation. It tells a
  consumer migrating off `Compono.NSubstitute` to fall back to
  `Compono.NSubstitute` itself, or a project-local fake, for exactly this
  case — a permanent detour under ADR-0042 Amendment 2's own policy
  ("any real, evidenced case where `Compono.NSubstitute` can satisfy a
  shape `Compono.TestDoubles` cannot is, by definition, a roadmap
  candidate").
- No `Reset()`/`ClearReceivedCalls()` equivalent means a `[Shared]` double
  reused across multiple phases of one test (arrange a call, assert it,
  then arrange and assert a second distinct call on the same member)
  cannot cleanly separate the two phases' verification — the call count
  keeps accumulating with no way to zero it short of composing a fresh
  double.
- `AtLeast(n)`/`AtMost(n)` is a real, named, cheap gap (`CallVerifier`
  already carries `observedCount`) that keeps getting explicitly named
  and explicitly deferred across three ADRs (0042, 0044, 0048) for the
  same "no real evidence yet" reason — a candidate that is architecturally
  trivial but has so far failed Compono's own evidence bar, not failed on
  merit.
- Cancellation-aware behavior is a narrower, real .NET-idiom gap:
  Async interfaces (`IAmazonDynamoDB`-shaped clients, the repo's own
  worked examples) routinely take a `CancellationToken` as the last
  parameter, and a common real test wants "throw `OperationCanceledException`
  if the token passed in is already cancelled" without hand-wiring it via
  `Match.Is<CancellationToken>(ct => ct.IsCancellationRequested)` plus a
  manual `Throws`.

## 7. Candidate improvements

1. Exposed received-call records (argument capture + call inspection +
   `Reset()`).
2. Generalized call-count constraints (`AtLeast(n)`/`AtMost(n)` on
   `CallVerifier`).
3. `Reset()`/`ClearCalls()` as a standalone primitive, independent of (1).
4. Cancellation-aware default/helper for `CancellationToken`-shaped
   parameters.
5. A dedicated `Callback(Action<...>)`-only surface distinct from
   `ReturnsCallback` for pure side effects on a `void` member (partially
   redundant with (1)).
6. Ordered/sequenced verification across members — explicitly weighed and
   rejected below (Section 15), not a serious 1.1 candidate.
7. Extracting `Compono.TestDoubles` (or its primitives) into a fully
   independent, non-Compono-dependent package — the Part B question,
   analyzed in depth in Sections 9–10.

## 8. Detailed analysis of each serious candidate

### 8.1 Exposed received-call records

**Shape.** Extend `ReturnConfig<T>`'s existing `RecordCall()` call site
(already invoked by every generated dispatch body) to optionally retain
the invocation's real arguments in a small, bounded, per-member log —
gated the same way argument-aware `Configure()`/`Verify()` already is
(the five-condition eligibility test in ADR-0048: non-overloaded, no
open-generic-parameter-referencing real parameters, no ref-like
parameter, no field-name collision, not a one-parameter `Equals`). A
natural generated-code surface: `Verify().Member(...)` already returns a
filtered count; a sibling accessor (e.g. a `Calls` property on the
existing `Verify()` handle, or a distinct `Captured()` terminal next to
`.Once()`/`.Exactly(n)`) returning a read-only list of argument tuples,
matching exactly Rocks' own precedent (`Callback(a => value = a)`,
Section 12) but as a first-class, generated, strongly-typed API rather
than a workaround.

**Cost.** Real, bounded, and squarely inside the pattern this package
already uses everywhere else: a new generated field (an array or list
alongside the existing counter), a generated accessor, and eligibility
gating reusing ADR-0048's already-proven five conditions verbatim — no
new core-engine mechanism, no reflection, no expression trees. The
`ReturnsCallback` precedent (ADR-0053) already proves a generated
strongly-typed delegate per eligible member compiles and performs
correctly under Native AOT; a captured-arguments list is a strictly
simpler shape (no delegate invocation, just a snapshot append).

**Fit.** Directly closes the one gap the package's own documentation
flags as the most consequential remaining boundary, without expanding
scope toward strict mode, partial substitutes, or general expression-tree
matching — none of which this touches.

### 8.2 `AtLeast(n)`/`AtMost(n)`

**Shape.** Two more methods on `CallVerifier` alongside `Never()`/
`Once()`/`Exactly(n)`:

```csharp
public void AtLeast(int times) { if (observedCount < times) throw ...; }
public void AtMost(int times)  { if (observedCount > times) throw ...; }
```

**Cost.** Trivial — `CallVerifier` already carries `observedCount`
(`src/Compono/CallVerifier.cs:12`); this is a same-file, few-line
addition with no generator change at all (the generated `Verify().Member(...)`
surface already returns a `CallVerifier`; consumers would just get two
more terminal methods on the type they already hold).

**Fit.** Exactly closes a gap ADR-0044/ADR-0048 both explicitly named and
explicitly declined for lack of evidence, not for architectural
difficulty — the cheapest correction on this whole list, and the
2026-08-13-through-08-21 ADR trail already shows the project revisiting
"no real evidence yet" verdicts once evidence does arrive (ADR-0042
Amendment 2's entire point). Given `Compono.NSubstitute`'s
`Received.InRange`/`AtLeast`/`AtLeastOne` surface exists and is one
migration-mapping table entry away from being a real gap the moment a
consumer actually needs it, this is a low-risk, low-cost inclusion
candidate even without a specific dogfooding incident yet in hand.

### 8.3 `Reset()`/`ClearCalls()`

**Shape.** A method on the generated double (or a `Configure()`/`Verify()`-parallel
handle) that zeroes `ReturnConfig<T>.CallCount` for one member, or every
member, without touching configured `Returns`/`Throws`/`ReturnsSequence`
state (or with an overload that also clears configuration, mirroring
NSubstitute's `ClearReceivedCalls()`/`ClearSubstitute()` split).

**Cost.** Small — `CallCount` is already an ordinary mutable `internal`
field on `ReturnConfig<T>`; a generated `Interlocked.Exchange(ref __member.CallCount, 0)`-style
reset needs no new architecture. Slightly complicated by `ReturnConfig<T>`'s
existing `ClearConfiguredResponse()` (ADR-0053, already used internally
for `ReturnsCallback` transitions) — a `Reset()` would need to *not*
collide with or accidentally reuse that method's different intent
(clearing configured response vs. clearing recorded calls).

**Fit.** Real, if narrower than (8.1) — most naturally ships *alongside*
exposed received-call records (Section 5's "next missing primitive"
reasoning), since both touch the same call-log state; shipping it alone
is defensible but leaves the bigger capture gap open.

### 8.4 Cancellation-aware behavior

**Shape.** Unclear net win. A dedicated "auto-throw when the passed token
is cancelled" default would be new, member-shape-specific dispatch logic
(inspecting a `CancellationToken` parameter's runtime state, not just its
identity) — a real behavioral special case the generator doesn't have
today for any other parameter type, and arguably in tension with
"deterministic defaults" being purely return-shape-driven, never
parameter-content-driven. The existing `Match.Is<CancellationToken>(ct => ct.IsCancellationRequested)`
plus `.Throws(new OperationCanceledException())` already expresses this
without new generator work.

**Cost.** Real generator complexity for a narrow, arguably
already-served case.

**Fit.** Weak — recommend not pursuing without a real, specific
dogfooding incident (Section 15).

## 9. Standalone-package feasibility (deep)

### 9.1 Generator dependency

**Yes, structurally and irreducibly, for the discovery signal.**
`TestDoubleAnalyzer` is not a separate generator — it's an extension of
`LeafTypeClassifier`'s existing interface-leaf branch inside the single
`Compono.Generators` project, sharing the same composition-graph
discovery pass (constructor-parameter walking via the Roslyn semantic
model) every other Compono leaf classification already uses. This is not
an implementation convenience; it's the entire reason ADR-0042 concluded
Compono could do something no external source-generated mocking library
can: "no cross-generator dependency, ever... this is only
architecturally sound because `Compono.Generators` can own discovery and
generation in the same pass." A real two-generator Roslyn spike proved
(ADR-0042's Context) that TUnit.Mocks' own cross-generator bridge fails
identically to no trigger at all when the only trigger comes from a
sibling generator's output — the same failure mode a fully independent
`Compono.TestDoubles`-owned generator would hit trying to react to
Compono's own composition-graph output.

**Experimentally confirmed in this investigation** (Section 11, Experiment
1): a plain interface with **zero** `composer.Create<T>()`/`CreateMany<T>()`/
`[Compose]`/`[Composable]` reachability gets **no** generated double at
all, even with `ComponoGeneratedTestDoubles=true` and `Compono.TestDoubles`
referenced — `foo.Configure()` fails `CS1061` exactly like the interface
was never touched by the feature. The zero-declaration UX (the entire
differentiator per ADR-0042's Decision Drivers) depends 100% on the
interface already being reachable through *some* Compono composition
concept.

### 9.2 Runtime dependency

**No — the runtime primitives are, and have been since Amendment 2/3,
conceptually decoupled from composition; they live in core `Compono`
purely for cross-assembly accessibility, not because they need anything
`Composer`/`CompositionBuilder`/`CompositionScope` provides.**
`ReturnConfig<T>`, `ReturnConfigBuilder<T>`, `Match<T>`, `CallVerifier`,
`SequenceOutcome<T>`, `Unit`, and `GeneratedTestDoubleRegistry` (Section 2
above) reference no composition types anywhere in their own source — read
in full during this investigation, confirmed zero mention of `Composer`,
`CompositionBuilder`, `CompositionScope`, `ICompositionContext`, or any
composition-request type. `GeneratedTestDoubleRegistry` is a plain
`Type`-keyed `ConcurrentDictionary<Type, Func<object>>` — the same shape,
independently, `RowInvokerRegistry` uses for a completely different
purpose. **Experimentally confirmed** (Section 11, Experiment 3): a
hand-written double registered and retrieved directly through
`GeneratedTestDoubleRegistry.RegisterFactory<T>`/`.TryCreate`, configured
via `ReturnConfigBuilder<T>`, and asserted via `CallVerifier`, works
correctly with **zero** `Composer`/`CompositionBuilder` anywhere in the
program. `Compono.Http` and `Compono.Logging` already independently prove
this by reusing `CallVerifier`/`Match<T>` directly with "no generator
involvement" of their own (Section 3) — these types are already, in
practice, shared cross-package runtime utilities, not composition
primitives that happen to also serve test doubles.

**What is genuinely a fundamental (not merely historical) runtime
dependency:** `[Shared]`-scoped identity — the property that a
`[Shared] IRepository repository` test parameter and `Service`'s own
`IRepository` constructor dependency resolve to the *same* double
instance — is a real `CompositionScope` behavior (ADR-0011), not
something the double itself provides. A standalone (non-composing)
consumer never needs or gets this; it constructs and wires the double by
hand, same as any other manually-instantiated test double.

### 9.3 Activation dependency

**Currently, activation is 100% composition-mediated in every real usage
this repo has**, even though nothing in the double's own type structure
requires it. `GeneratedTestDoubleProvider` is reached only through
`CompositionBuilder`'s stage-6 provider pipeline
(`ICompositionValueProvider`/`AddTestDoubleProvider`); every existing
consumer — `Compono.TestDoubles.SampleTests`, `Compono.TestDoubles.AotSmokeTest`,
the skill doc's own examples — instantiates a double exclusively via
`composer.Create<T>()`. **No test anywhere in this repo constructs a
generated double directly** (confirmed by grep: no `new <Hash>_Double()`
or direct `GeneratedTestDoubleRegistry`/`Compono.TestDoubles.Generated`
usage in any `Compono.TestDoubles.*` test project except the runtime
package's own unit tests of the provider/extension methods themselves,
which mock the registry, not a real generated type).

A `TestDouble.Create<IMyService>()`-shaped standalone factory (this
research prompt's own suggested shape) is architecturally reachable —
`GeneratedTestDoubleRegistry.TryCreate(typeof(T), out value)` already *is*
almost exactly that, minus a thin generic wrapper — but the harder
question is **discovery**, not activation: `TryCreate` only succeeds for
a type the generator already decided to generate a double for, and today
that decision is inseparable from composition-graph reachability. Adding
a standalone `TestDouble.Create<T>()` API without also adding a
standalone *discovery trigger* (e.g., an explicit
`[GenerateTestDouble(typeof(IFoo))]` assembly-level attribute, the exact
shape every external library — TUnit.Mocks, Imposter, Rocks — already
uses, per ADR-0042's own Context) would just be a thin wrapper over a
registry that's empty for any interface a consumer never happened to run
through Compono composition somewhere else in the same compilation.
Native AOT/trimming/reflection posture is unaffected either way — no
reflection is needed for either the composition-mediated or a
hypothetical attribute-mediated trigger, since both ultimately populate
the same `[ModuleInitializer]`-registered, `Type`-keyed dictionary.

### 9.4 Configuration dependency

**No.** `Configure()`/`Verify()` and every builder behind them
(`ReturnConfigBuilder<T>`, `CallVerifier`) operate purely on the
generated double instance's own fields — nothing routes through
`CompositionBuilder`, a profile, or any other composition configuration
concept. This was true from the first working sketch (ADR-0043's
"Standalone usability" section: "falls out with essentially no extra
cost... `RepositoryDouble` and its `Configure(...)` surface have zero
dependency on `Composer`/`[Compose]`/`CompositionRow`") and remains true
after every subsequent amendment — none of ADR-0044/0045/0048/0049/0050/0053/0054
introduced any composition-configuration coupling either. There is no
"two confusing configuration models" risk here: `Configure()`/`Verify()`
already is the one and only configuration model, whether or not the
double was produced via composition.

## 10. Architectural options for independence

| Dimension | A: Status quo | B: Fully standalone, Compono integrates via hooks | C: `.Core`/integration split | D: Shared lower-level infra package |
|---|---|---|---|---|
| Consumer experience (Compono user) | Unchanged — `UseGeneratedTestDoubles()` + zero-declaration composition, exactly today | Same or worse — a standalone-first design would likely need an explicit per-type trigger even for Compono users, regressing the zero-declaration UX that is the package's entire reason to exist | Same as A if done carefully; risk of a confusing "which package do I reference" decision for every consumer, not just standalone ones | Unchanged for `Compono.TestDoubles` users; new packages (a hypothetical future non-Compono consumer) would reference the infra package directly |
| Consumer experience (hypothetical non-Compono user) | Not served at all today (proven, Section 11 Experiment 1) | Full standalone product — but with an explicit-trigger UX no better than Rocks/TUnit.Mocks/Mockolate already offer, and less mature | Same as B for the standalone half | Same as B for the standalone half — but the "product" on offer would just be `Match<T>`/`CallVerifier`/`ReturnConfig<T>` as bare utility types with no generator behind them, not a mocking library |
| Package naming | `Compono.TestDoubles` unchanged | New name needed (`Compono.TestDoubles` no longer describes a Compono-coupled thing); risks the exact `Compono.Mocks`/general-purpose-mocking-framework naming ADR-0042 explicitly rejected at admission time | `Compono.TestDoubles.Core` + `Compono.TestDoubles` (integration), mirroring no existing Compono package-split precedent | Something like `Compono.Testing.Primitives` — new, unprecedented naming category |
| Dependency graph | `Compono.TestDoubles` → `Compono` (one edge, confirmed Section 3) | `Compono.TestDoubles.Core` (or new name) has no Compono dependency; `Compono` or a thin adapter package depends on *it* — an inversion of today's direction | `Compono.TestDoubles.Core` (no Compono dep) ← `Compono.TestDoubles` (adapter, depends on both) | New `Compono.Testing.Primitives` (no Compono dep) ← `Compono` ← `Compono.TestDoubles`/`Compono.Http`/`Compono.Logging` all depend on the shared package instead of on `Compono.dll` for these types |
| Public API | Unchanged | `Match<T>`/`ReturnConfig<T>`/`CallVerifier`/etc. move out of `Compono.dll`'s public surface — a breaking change for any consumer (including `Compono.Http`/`Compono.Logging` internally) that references them as `Compono.Match<T>` today | Same breaking-change problem as B unless `Compono` re-exports type-forwards | Same breaking-change problem, mitigated only by type-forwarding shims |
| Binary/source compatibility | No change | Breaking: namespace/assembly move for public types current consumers (including two other first-party packages) already reference | Breaking, same reason, unless carefully forwarded | Breaking, same reason |
| Generator/analyzer packaging | Unchanged — one analyzer, `Compono.Generators`, ships inside core `Compono`'s nupkg | A standalone generator needs its **own** discovery trigger (an attribute), duplicated and maintained forever alongside the existing composition-graph trigger in the same generator, or an entirely separate generator package (reproducing ADR-0042's proven-bad cross-generator-handoff shape if it needs to cooperate with Compono's own discovery at all) | Same duplication problem as B, scoped to whichever half is "core" | N/A — infra package ships no generator at all |
| Generated-API compatibility | Unchanged | New standalone-triggered doubles would need their own (different) generated shape question resolved from scratch — the existing shape assumes the interface is already known to be composition-reachable | Same open question as B | N/A |
| Native AOT/trimming | Already proven (`Compono.TestDoubles.AotSmokeTest`, real `dotnet publish -p:PublishAot=true`) | No new AOT risk in principle (still zero reflection either way) but doubles the AOT-proof surface (composition-triggered path + attribute-triggered path) that needs its own smoke test | Same doubling | N/A — infra package has no generation to prove |
| Build behavior | Unchanged | New package, new pack/publish pipeline, new versioning story synced against `Compono`'s own | Two new packages instead of one | One new package plus type-forwarding shims in every consumer |
| Complexity | Lowest — this is the shape the deep-design ADR already converged on after 21 amendments of real review | Highest — reopens a discovery-trigger design question ADR-0042 spent real effort concluding Compono's zero-declaration answer to, for a benefit (serving non-Compono consumers) nobody has asked for | High — two packages to maintain, version, and keep mutually consistent, for the same underlying benefit as B | Medium — a real, defensible refactor (see below) but with real breaking-change cost for zero current behavioral gain |
| Maintenance cost | Lowest, proven by 16 real ship-and-iterate commits without needing this split | Highest — a second discovery trigger becomes permanent surface area, forever kept in sync with the composition-graph trigger's own eligibility rules (all 21+ ADR-0043/0044 amendments' worth of edge cases) | High, same reason as B, times two packages | Medium — real but bounded (move types, add forwards, update three consuming packages' `.csproj` comments) |
| Ability to evolve independently | Already true where it matters — `Compono.TestDoubles`'s own runtime package is 2 types and changes independently of core `Compono`'s composition-engine work today | No better than A in practice — the generator half still can't evolve independently of `Compono.Generators`' shared discovery infra (Section 9.1) | No better than A — same generator constraint | Somewhat better in principle (the primitives could version independently of `Compono`'s composition-engine surface) but no current evidence any consumer needs that independence |
| Coherent standalone product? | N/A (doesn't attempt this) | **No** — without a real per-type discovery trigger, a "standalone" consumer gets an empty registry; with one, the product is a worse-differentiated clone of Rocks/TUnit.Mocks/Mockolate, not a real Compono-flavored standalone tool | Same "no" as B for the standalone half | N/A — this option doesn't produce a mocking product at all, just relocates utility types |

## 11. Experiments performed

All three run from
`/private/tmp/claude-501/.../scratchpad/standalone-experiment` (a
throwaway project outside `src/`/`test/`, git-ignored, nothing committed
to the real repo). `Compono` and `Compono.TestDoubles` were packed
locally (`dotnet pack ... -p:Version=1.0.0 -o feed`) and restored via a
local `nuget.config` pointing at that folder, mirroring
`test/Compono.TestDoubles.SampleTests`' own real consumption pattern
(`PackageReference`, not `ProjectReference` — a `ProjectReference` to
`Compono.csproj` does not transitively carry the `Compono.Generators`
analyzer three hops deep, confirmed as a dead end before switching to the
pack-and-restore approach the AOT smoke test project's own comments
already document as the correct shape).

**Experiment 1 — does the generator trigger without any composition call
site?** A plain `IFoo { int GetValue(); }` interface, `ComponoGeneratedTestDoubles=true`,
`Compono.TestDoubles` referenced, **no** `composer.Create<T>()`,
`CreateMany<T>()`, `[Compose]`, or `[Composable]` anywhere in the
compilation. `foo!.Configure().GetValue()` fails to compile:

```
error CS1061: 'IFoo' does not contain a definition for 'Configure' and no
accessible extension method 'Configure' accepting a first argument of
type 'IFoo' could be found
```

**Result: confirmed — the zero-declaration generation trigger is
strictly composition-reachability-gated. No fallback "just referencing
the interface somewhere" trigger exists.** This directly evidences
Section 9.1/9.3's claim rather than merely restating the ADR's own
reasoning.

**Experiment 2 — positive control.** Identical project, one line added:
`var composer = Composer.Create(b => b.UseGeneratedTestDoubles()); var foo = composer.Create<IFoo>();`
before the `Configure()` call. Compiles and runs, printing `42` — the
configured value. **Result: confirms the trigger is exactly
`composer.Create<T>()` reachability, nothing more, nothing less** — the
smallest possible composition call site is sufficient, no `[Compose]`/`[Composable]`
attribute needed on top of it.

**Experiment 3 — do the runtime primitives need `Composer` at all?** A
hand-written `FooDouble : IFoo` (standing in for what the generator would
emit) registered directly via `GeneratedTestDoubleRegistry.RegisterFactory<IFoo>(() => new FooDouble())`,
retrieved via `GeneratedTestDoubleRegistry.TryCreate(typeof(IFoo), out value)`,
configured via a hand-written `ReturnConfigBuilder<int>`-based method, and
asserted via `CallVerifier` — with **zero** `Composer`/`CompositionBuilder`
anywhere in the program. Output:

```
42
PASS: registry + ReturnConfig/ReturnConfigBuilder/CallVerifier work with
zero Composer/CompositionBuilder involvement.
```

**Result: confirmed — every runtime primitive this feature depends on
(`ReturnConfig<T>`, `ReturnConfigBuilder<T>`, `GeneratedTestDoubleRegistry`,
`CallVerifier`) is already, today, usable with no composition engine
involved at all.** This directly supports Section 9.2's conclusion:
the runtime half is decoupled in substance already; only the *discovery
trigger* (Experiment 1) is genuinely, structurally tied to composition.

## 12. External/competitive research

**What was seen externally** (WebSearch against primary GitHub
sources/READMEs, cross-checked against ADR-0042's own prior spike
findings from the same investigation trail):

- **NSubstitute/Moq**: both rely on runtime proxy generation (Castle
  DynamicProxy / `System.Reflection.Emit`-adjacent mechanisms) and are
  reported as fundamentally incompatible with Native AOT — "Native AOT
  has limited support for features that depend heavily on runtime code
  generation." This matches ADR-0042's own stated motivation exactly and
  is the reason `Compono.NSubstitute` (not `Compono.TestDoubles`) carries
  that limitation.
- **Rocks** (JasonBock/Rocks): source-generator-based, triggered by an
  **assembly-level attribute** (`[Rock(typeof(ITarget), BuildType.Create)]`),
  generating a `{Type}{Kind}Expectations` class. Consumer flow: build
  `Setups` on the expectations object, call `.Instance()` to get the
  mock, exercise it, call `Verify()`. Argument matching: exact value,
  `Arg.Any<T>()`, `Arg.Validate<T>(predicate)` — directly analogous to
  Compono's `Match<T>`'s three cases. **Argument capture is real and
  supported**, via `Callback(a => value = a)` — exactly the "expose the
  invocation" primitive this research identifies as Compono's own biggest
  remaining gap (Section 5/8.1), confirming it as a real, demanded
  capability in a comparable ecosystem, not a speculative one. Stated
  limitations: no sealed-type mocking, no closed generic types (open
  forms only), `[Obsolete]`-marked members rejected, static-abstract
  members unimplemented (tracked as an open issue) — no reflection
  anywhere; fully AOT-compatible by construction (generates real C#
  source, not IL emission).
- **TUnit.Mocks / Imposter**: both require a compile-time-visible,
  per-type trigger written directly in consumer source
  (`Mock.Of<T>()`/`T.Mock()`/`[assembly: GenerateMock(typeof(T))]` for
  TUnit.Mocks; `[assembly: GenerateImposter(typeof(T))]` for Imposter) —
  already investigated in depth by ADR-0042's own prior spike (a real,
  reproduced cross-generator-handoff failure: a type whose only trigger
  came from a *different* generator's own emitted source never gets a
  mock, on clean or incremental builds alike). Not re-verified in this
  pass; cited as already-established primary evidence this repo already
  holds.
- **Newer entrants surfaced this pass** (not previously referenced by any
  Compono ADR, genuinely new information): **Skugga** ("Roslyn-based...
  leverages Source Generators and Interceptors to create static,
  AOT-safe mocks with zero runtime footprint") and **Mockolate**
  ("modern, strongly-typed, AOT-compatible mocking library... powered by
  source generators") — both confirm the broader .NET ecosystem is
  actively moving toward exactly Compono's own chosen mechanism
  (compile-time generation over runtime proxying) for AOT compatibility,
  independently arriving at the same architectural direction ADR-0042
  chose. Neither was fetched in depth (no primary-source README pull
  beyond the search-result summary) — flagged as worth a closer look in
  a future pass if either turns out to have real adoption, not
  incorporated into any recommendation here.

**What is recommended for Compono** (inspiration only, not a parity
target): Rocks' `Callback(a => value = a)` argument-capture pattern
directly supports Section 8.1's "exposed received-call records" as a
real, externally-validated capability worth Compono's own smaller,
strongly-typed treatment — not because Compono should chase Rocks'
breadth, but because this is independent evidence the specific gap
Compono's own skill doc already flags as its biggest boundary is a real
gap other libraries in this exact niche (source-generated, AOT-safe)
found worth solving too. Nothing else from this survey is recommended —
Rocks' assembly-attribute trigger, `Instance()`-then-configure two-phase
flow, and open-generics-only stance are all real design choices Compono
should **not** copy: they exist because Rocks has no composition-graph
discovery to piggyback on, which is precisely the differentiator ADR-0042
found Compono uniquely has and should keep leaning on rather than
abandon for standalone-library parity.

## 13. Compatibility implications

- **Source compatibility (candidates in Section 8):** additive only.
  `AtLeast`/`AtMost` on `CallVerifier`, a `Reset()`/captured-calls
  accessor on the generated `Verify()` surface — none change any existing
  generated signature or public type's existing members. No SemVer
  concern beyond a routine minor-version addition.
- **Binary compatibility:** `CallVerifier`/`ReturnConfig<T>` are public
  types in core `Compono.dll`; adding members to them is binary-compatible
  (no existing member signatures change). `ReturnConfig<T>` is a mutable
  struct with `internal` backing fields already extended twice before
  (Amendment 3 read-accessors, ADR-0054's `Sequence`/`SequenceOrdinal`
  fields) without any prior compatibility incident — real precedent this
  extends cleanly again.
- **Generated-source compatibility:** a captured-call-log or `AtLeast`/`AtMost`
  addition changes *future* generator output (new files emit the new
  surface) but never requires regenerating or invalidating any
  already-generated file from an older generator version — additive,
  matching every prior TestDoubles feature's own rollout shape (v1 →
  v2 → v3 in the skill doc's own section numbering, each purely additive).
- **Analyzer/diagnostics compatibility:** no new diagnostic codes are
  obviously required for (8.1)/(8.2) — the existing eligibility-gating
  diagnostics (`CMP0026`/`CMP0029`/`CMP0030`) already cover the same
  member shapes a captured-call-log would need to exclude (ref-like
  parameters, overloads, collisions); reusing them rather than adding new
  codes is the lower-risk default, to be confirmed at design time.
- **Runtime/AOT/trimming:** unaffected either way — every candidate in
  Section 8 is a plain-field-plus-branch addition to the same
  already-AOT-proven shape (`Compono.TestDoubles.AotSmokeTest` already
  exercises the closest-analogous existing feature,
  `ReturnsCallback`/`ReturnsSequence`, under real `PublishAot=true`).
- **Deterministic builds:** unaffected — no new nondeterminism source
  (hashing, ordering) is introduced by any Section 8 candidate; existing
  hash-suffixed naming and `.Collect()`+`SymbolEqualityComparer`
  deduplication are untouched.
- **SemVer:** every Section 8 candidate is additive-only and ships as a
  minor version bump (1.1.0), consistent with the "additive capabilities"
  framing this whole investigation was scoped against.

## 14. AOT/trimming/generator implications

No candidate in Section 8 introduces reflection, `Activator.CreateInstance`,
`MakeGenericMethod`, expression-tree compilation, or any other
AOT-hostile mechanism — every one is a straight extension of the existing
generated-field-plus-branch dispatch shape this package has used
unchanged since v1, already proven end-to-end under
`dotnet publish -p:PublishAot=true` (`Compono.TestDoubles.AotSmokeTest`).
A captured-call-log (8.1) needs a plain array/list field per eligible
member — no different in AOT posture from `ReturnConfig<T>.Sequence`
(ADR-0054), which already ships this exact shape (an array field, set
once, read many times, `Interlocked`-guarded ordinal). `AtLeast`/`AtMost`
(8.2) touch no generated code at all — pure core-library addition. Any
new standalone-discovery trigger (Section 10's Option B/C, explicitly not
recommended) would still be reflection-free by construction (an
attribute-driven Roslyn pass, same mechanism as the existing
composition-graph-driven one) — the AOT argument was never the reason to
reject standalone independence; the discovery-trigger-duplication and
product-differentiation arguments (Sections 9.1, 9.3, 10) are.

## 15. Rejected ideas

- **Call-order verification across members.** Explicitly and repeatedly
  rejected across ADR-0044 and ADR-0048 for lack of real evidence — "a
  direct search... [found] zero real evidence" (ADR-0048). Nothing
  surfaced in this investigation changes that; not re-recommended here.
- **Strict mode / partial substitutes / recursive auto-configuration.**
  ADR-0042 Non-Goals, unchanged through every subsequent amendment — a
  fundamental scope boundary, not a backlog item.
- **`ref`/`out`/`in` parameter support.** ADR-0042 Non-Goal; the generator
  already diagnoses and cleanly falls back (`CMP0026`/scoped `CMP0030`) —
  no evidence this boundary is causing real friction (unlike argument
  capture, which the skill doc calls out unprompted).
- **Class/protected-member mocking.** ADR-0042 Non-Goal, structurally
  distinct from this package's interfaces-only scope; would require an
  entirely different generation strategy (subclass proxying), not an
  incremental extension of the existing explicit-interface-implementation
  design.
- **Cancellation-aware auto-throw default (Section 8.4).** Considered
  seriously enough to analyze (Section 8.4) but rejected here for lack of
  a real dogfooding incident and a plausible-but-unproven complexity cost
  (parameter-content-driven dispatch behavior, a new category the
  generator doesn't have today) — flagged as worth revisiting only if a
  real case surfaces, per this package's own evidence-first policy
  (ADR-0042 Amendment 2).
- **Fully standalone `Compono.TestDoubles` (Option B/C, Section 10).**
  Analyzed in full depth (Sections 9–10); rejected as a 1.1 (or any
  near-term) candidate — see Section 16's verdict.
- **Shared lower-level infra package (Option D, Section 10).** A
  defensible refactor in the abstract (the primitives already function as
  shared infra in practice — Section 3's `Compono.Http`/`Compono.Logging`
  reuse evidence) but a real, avoidable breaking change for zero current
  behavioral benefit; no consumer has asked to version these primitives
  independently of `Compono.dll`. Revisit only if that changes.

## 16. Ranked recommendations (1.1 candidates)

1. **Exposed received-call records (argument capture + call inspection),
   paired with a `Reset()`/`ClearCalls()` primitive.** Highest leverage
   per Section 5's "next missing primitive" analysis — closes the one gap
   the package's own documentation names as its most consequential
   boundary, externally validated as a real, demanded capability in a
   directly comparable library (Rocks' `Callback(a => value = a)`,
   Section 12), and architecturally cheap (extends the existing
   `RecordCall()`/eligibility-gating machinery, no new core mechanism).
2. **`AtLeast(n)`/`AtMost(n)` on `CallVerifier`.** Lowest cost of any
   candidate on this list (a few lines, no generator change), closes a
   gap this project's own ADR trail has named three separate times
   without yet clearing its own evidence bar — worth including
   proactively in 1.1 given how cheap it is relative to its documented,
   repeated visibility, even absent a fresh dogfooding incident.
3. *(Lower priority, ship only if scope allows)* Nothing else on the
   candidate list clears the bar independently — cancellation-aware
   defaults (8.4) and a standalone side-effect-only `Callback(...)` (item
   5 in Section 7) are both weaker/narrower than 1 and 2 and are better
   left for a future pass with real evidence.

## 17. Recommended 1.1 scope

Ship recommendation 1 (captured-call records + `Reset()`) and
recommendation 2 (`AtLeast`/`AtMost`) together as `Compono.TestDoubles`'s
1.1 content. Both are additive, low-risk, and directly address the two
concrete, named gaps this investigation found the strongest evidence for
— one from the package's own documentation (argument capture) and one
from its own repeated-but-unresolved ADR history (`AtLeast`/`AtMost`).
Do not pursue standalone-package independence in 1.1 (Section 16's
verdict, below) — no consumer evidence justifies the real breaking-change
and discovery-trigger-duplication costs Section 10 documents.

## 18. Questions or evidence still unresolved

- The exact API shape for exposed received-call records (a `.Calls`
  property on `Verify()`'s handle vs. a distinct `.Captured()` terminal
  vs. something else) needs its own deep-design pass — this research
  identifies the primitive and its leverage, not the final API, matching
  this repo's own ADR-0029 restraint for problem-recording vs. solution-
  designing.
- Whether `AtLeast`/`AtMost` should ship with `Compono.NSubstitute`
  migration-mapping-table entries added alongside (matching the existing
  `Received(n)`/`DidNotReceive()` table in the skill doc) is a
  documentation-scope question for the implementing plan, not resolved
  here.
- Skugga and Mockolate (Section 12) were surfaced but not deeply
  researched in this pass — if either gains real traction, a future
  investigation should assess whether their approach reveals anything
  Compono's own design missed, though nothing found so far suggests it
  does.
- Whether a captured-call log needs a bounded/capped size (to avoid
  unbounded memory growth in a long-running or heavily-looped test) was
  not resolved here and should be a specific question for the
  implementing design pass.

## Conclusions

**(A) Top 1.1 feature recommendations, ranked:**

1. Exposed received-call records (argument capture + call inspection),
   paired with `Reset()`/`ClearCalls()`.
2. `AtLeast(n)`/`AtMost(n)` on `CallVerifier`.
3. No third candidate clears the bar independently in this pass — treat
   the rest of Section 7's list as deferred pending real evidence.

**(B) Standalone-viability verdict: No — coupling is fundamental and
appropriate.**

The runtime primitives (`ReturnConfig<T>`, `ReturnConfigBuilder<T>`,
`Match<T>`, `CallVerifier`, `SequenceOutcome<T>`, `GeneratedTestDoubleRegistry`)
are already, today, provably decoupled from `Composer`/`CompositionBuilder`
(Experiment 3, Section 11) — that half of the question is not in dispute.
But the capability that makes `Compono.TestDoubles` worth having at all —
zero-declaration double generation with no per-type consumer trigger — is
100% dependent on Compono's own composition-graph discovery (Experiment
1, Section 11: an interface with no `composer.Create<T>()`/`[Compose]`/
`[Composable]` reachability gets no generated double, full stop, even
with every other opt-in flag set). Making the package "standalone" would
mean inventing a second, non-composition discovery trigger (an
assembly-level attribute, the same shape Rocks/TUnit.Mocks/Imposter/Skugga/Mockolate
already use) that must be permanently maintained alongside the existing
one inside the same generator — precisely the differentiation-destroying,
complexity-adding outcome ADR-0042 evaluated and declined to chase, and
Decision Driver 3 explicitly pre-authorized abandoning ("standalone
usability must never justify added complexity, and must be dropped
rather than distort the architecture if it doesn't fall out cleanly").
It didn't fall out cleanly — the experiment proves it — so per the
package's own governing ADR, it should stay dropped.
