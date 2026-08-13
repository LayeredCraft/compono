# [ADR-0043] Compono-Generated Test Doubles: Design

**Status:** Accepted

**Date:** 2026-08-13

**Decision Makers:** Nick Cipollina, Claude (design deep dive)

## Context

[ADR-0042](0042-compono-owned-source-generated-test-doubles.md) recorded
the admitted problem — Compono has no AOT-safe way to preserve
zero-declaration interface-dependency composition, because every external
source-generated mocking library requires a compile-time-visible
per-type trigger only `Compono.Generators` itself can supply, being the
only generator that already performs composition-graph discovery — and
deferred every API, architecture, and package-boundary question to this
deep-design pass. This ADR settles those questions.

Two forks were talked through explicitly with the requester before being
decided (per `design-decisions.md`'s deep-dive process), both grounded in a
real spike rather than assumption:

**Fork 1 — is interception the right mechanism for the control surface?**
A real two-generator Roslyn spike proved C# interceptors are stable
(non-experimental as of `Microsoft.CodeAnalysis.CSharp` 5.x/Roslyn's C# 14
support — `SemanticModel.GetInterceptableLocation` carried the
`RSEXPERIMENTAL002` diagnostic against 4.11.0 and did not against 5.6.0),
fully AOT-compatible (pure compile-time call-site substitution, zero
runtime proxy), single-generator-ownable (no cross-generator handoff — the
same generator both discovers the call site and emits the interceptor),
and activatable purely through an auto-imported MSBuild property
(`InterceptorsNamespaces`, injectable via a packaged `.props` file) with
zero hand-edited consumer `.csproj`. All of that was proven, not assumed,
against a clean build. But analyzing where it would actually be *needed*:
since a generated double implements its target interface directly (no
runtime proxy layer to redirect), its own generated method bodies can
already branch on configured state directly — interception would only
matter for making one method name serve both "real call" and "configure
this call" — and TUnit.Mocks already solves that more simply, with no
interception at all, by giving the configuration surface a **distinct
receiver type** from the real interface implementation (its own generated
`Mock<T>` wrapper vs. the interface-implementing inner type), so ordinary
C# overload resolution — not compile-time call-site rewriting — picks the
right member. **Decided: interception is rejected as v1's mechanism,
recorded as considered and proven viable, not left as an open question.**
It remains available for a genuinely different future problem that needs
compile-time call-site rewriting specifically; nothing about this decision
forecloses that.

**Fork 2 — does v1 include configured returns/exceptions, or stay
default-value-only?** Once interception was ruled unnecessary, a minimal
`Returns`/`Throws` surface (a couple of generated fields plus a branch per
member) costs little beyond what default-value-only doubles already need —
no proxy, no expression trees, no reflection, no new subsystem. **Decided:
v1 includes configured returns and configured exceptions.** Verification,
call recording, strict mode, argument matchers, callbacks, and sequential
returns stay excluded — [ADR-0042](0042-compono-owned-source-generated-test-doubles.md)'s
harder Non-Goals (no general-purpose mocking API, no protected-member/
static-abstract-member support, interfaces only) are unchanged by this ADR.

## Decision Drivers

- Every driver [ADR-0042](0042-compono-owned-source-generated-test-doubles.md)
  already recorded still applies unchanged: zero-declaration composition
  as the sole justification; no cross-generator dependency, ever;
  no-reflection-by-default and Native AOT/trimming safety; explicit-over-
  implicit activation (a compile-time-visible opt-in, never a runtime-only
  call the generator can't see); `Compono.NSubstitute` not being
  deprecated or replaced; Compono integration first, standalone usability
  only if it falls out cleanly without added complexity.
- **`[Shared]` identity must not require any change to
  `CompositionScope`'s existing exact-requested-type storage**
  ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)).
  A design that needs the shared-value engine to know "a concrete type and
  the interface it implements are the same shared slot" is new core
  engine work this ADR's own admission scope (`Compono.NSubstitute`-sized
  precedent, per ADR-0042) didn't budget for — the chosen design must get
  identity for free from the *existing* mechanism, the same way
  `Compono.NSubstitute`'s shared-substitute-reuse already does.
- **The generated double's real interface method and its configuration
  surface must never be ambiguous to the compiler at a given call site** —
  a consumer typing `IRepository` gets real dispatch; a consumer holding
  the generator's own configuration handle gets the builder API; nothing
  should require the consumer to disambiguate manually.

## Considered Options

### Control-surface mechanism (Fork 1)

1. **Interception-based**: the real interface member call itself gets
   intercepted at configured call sites, redirecting to generator-owned
   state.
2. **Distinct-receiver-type, no interception** (TUnit.Mocks' own proven
   pattern): the generated double type explicitly implements the target
   interface (so a plain `.Member()` call on it, viewed as the *concrete
   double type*, is not the interface member at all — explicit interface
   implementation removes it from that type's own public surface), and a
   same-named companion API (generated extension methods, or instance
   methods on a small paired builder) provides configuration, resolved by
   ordinary C# overload resolution on the concrete type, not call-site
   rewriting.

### `[Shared]` / control-surface access shape

1. **Test parameter typed as the concrete generated double type directly**
   (e.g. `[Shared] RepositoryDouble repository, Service sut`), relying on
   the shared-value engine to serve both that concrete-type request and
   `Service`'s own `IRepository` constructor request from one stored
   instance.
2. **Test parameter stays typed as the plain interface**
   (`[Shared] IRepository repository, Service sut`, unchanged from
   `Compono.NSubstitute`'s existing pattern), with a generated
   `Configure(IRepository)`-shaped static entry point that safely downcasts
   to the known concrete double type and returns the fluent builder.

### Package boundary

1. **Generator-emission logic ships inside core `Compono.Generators`
   (inert by default, behind a compile-time opt-in); the runtime-facing
   control surface (provider, builder types, activation extension method)
   ships in a new optional package.**
2. Everything (generator emission and runtime surface) inside core
   `Compono`.
3. Generator emission split into a separately-packaged analyzer.

## Decision Outcome

**Chosen: Option 2 for the control-surface mechanism (no interception,
distinct-receiver-type pattern), Option 2 for `[Shared]`/access shape
(interface-typed parameter + `Configure(...)` downcast helper), Option 1
for package boundary** — confirmed directly with the requester for Forks 1
and 2 above; the `[Shared]`/access-shape and package-boundary choices
follow directly from Fork 1/2's own reasoning and ADR-0042's existing
Gate-A architectural-fit finding, respectively.

### Generated code shape

For a discovered interface leaf (e.g. `IRepository` with
`Task<Customer?> FindAsync(Guid id, CancellationToken ct)` and
`void Save(Customer customer)`), `Compono.Generators` emits, once per
distinct interface symbol across the whole compilation (deduplicated via
`SymbolEqualityComparer`, matching `LeafTypeClassifier`'s own existing
caching shape):

```csharp
// Compono.Generators-emitted, into the consumer's compilation.
internal sealed class RepositoryDouble : IRepository
{
    // One configured-state slot per member - a plain struct, no boxing beyond
    // what the member's own return type already needs, no dictionary/lookup.
    internal ReturnConfig<Task<Customer?>> __findAsync;
    internal ReturnConfig<Unit> __save; // Unit: the existing internal void-marker shape, if Compono already has one; otherwise a small internal struct introduced by this feature.

    Task<Customer?> IRepository.FindAsync(Guid id, CancellationToken ct) =>
        __findAsync.HasException ? throw __findAsync.Exception!
        : __findAsync.HasValue ? __findAsync.Value!
        : Task.FromResult<Customer?>(null); // type-appropriate default, per Section "Deterministic defaults" below

    void IRepository.Save(Customer customer)
    {
        if (__save.HasException) throw __save.Exception!;
    }
}

// Same generation pass, same file or a companion one - the configuration surface.
internal static class RepositoryDoubleConfiguration
{
    public static ReturnConfigBuilder<Task<Customer?>> FindAsync(this RepositoryDouble self, Guid id, CancellationToken ct) =>
        new(ref self.__findAsync);

    public static ReturnConfigBuilder<Unit> Save(this RepositoryDouble self, Customer customer) =>
        new(ref self.__save);
}
```

`ReturnConfigBuilder<T>.Returns(T value)`/`.Throws(Exception exception)` are
small, generic, non-generated (ordinary library types in the runtime
package, Section "Package boundary" below) that write into the slot passed
by reference — no expression trees, no reflection, and the exact same
generated-field-plus-branch shape TUnit.Mocks' own `MockEngine`-based
dispatch already uses (directly observed in the prior session's spike
output), just without an intermediate engine object, since Compono's v1
scope is smaller.

**Why explicit interface implementation removes the ambiguity Fork 1
worried about:** `RepositoryDouble` does **not** have a public
`FindAsync`/`Save` member at all — only `IRepository.FindAsync`/`Save`,
reachable exclusively through an `IRepository`-typed reference. A
same-named extension method (`RepositoryDoubleConfiguration.FindAsync`)
declared on the *concrete* `RepositoryDouble` type is therefore never in
competition with the interface member for overload resolution — calling
`.FindAsync(...)` on a `RepositoryDouble`-typed reference always resolves
to the extension (configuration); calling it on an `IRepository`-typed
reference always resolves to the real, explicitly-implemented behavior.
This is exactly TUnit.Mocks' own proven mechanism (Considered Options,
Fork 1, Option 2), adapted to Compono's smaller v1 scope.

**Deterministic defaults** (unconfigured member, no `Returns`/`Throws`
set): `null` for nullable reference/`Task<T?>`/`ValueTask<T?>` returns,
`default` for value types, `Task.CompletedTask`/`ValueTask.CompletedTask`
for non-generic async returns, an empty collection (never `null`) for
`IEnumerable<T>`-shaped returns, matching Section 5 of the ADR-0042-feeding
Gate A check's minimum-feature-set analysis exactly — no change from that
finding.

### `[Shared]` identity: interface-typed parameter, no engine change

`[Shared] IRepository repository` in a test parameter list works
**exactly as it already does today** for `Compono.NSubstitute` — the
generated double is produced (by the new provider, below), stored into the
row's `CompositionScope` under `IRepository` per
[ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)'s
existing unconditional stage-2 mechanism, and reused for `Service`'s own
`IRepository` constructor parameter. **Zero change to `CompositionScope`,
zero new dual-type storage.** Configuration is reached through a generated
static entry point, not a differently-typed test parameter:

```csharp
[Compose<SomeProfile>]
public void Test([Shared] IRepository repository, Service sut)
{
    Compono.TestDoubles.Configure(repository).FindAsync(Arg.Any<Guid>(), default).Returns(Task.FromResult(customer));
    // sut received the SAME repository instance via [Shared] - ReferenceEquals holds,
    // because it IS the same object; Configure(...) is a safe downcast, not a lookup.
}
```

`Compono.TestDoubles.Configure<T>(T value)` is a small, non-generated,
generic runtime method: `where T : class => value as RepositoryDouble ??
throw new InvalidOperationException(...)` in spirit — an ordinary,
reflection-free type check the compiler already knows is safe by
construction (Compono generated `RepositoryDouble` specifically to
implement `IRepository`), returning a per-interface generated companion
handle. This was chosen over a concrete-double-typed test parameter
(Considered Options' access-shape Option 1) specifically because it needs
**no** shared-value-engine change at all, versus Option 1's real,
non-trivial requirement that `CompositionScope` learn to serve a shared
value under two different requested types (the concrete double type and
the interface) from one storage slot — a new core mechanism this ADR's own
Decision Drivers explicitly rule out taking on.

`Arg.Any<Guid>()`/similar exact-vs-wildcard argument matching for
multi-overload/parameterized members is real design surface this ADR
leaves for the implementing plan to work out mechanically (a small,
closed, generated-per-parameter shape, not a general expression-tree
matcher) — not a new Non-Goal, just not fully specified at the ADR level.

### Generator architecture

Extends `LeafTypeClassifier`'s existing interface/abstract-class/delegate
leaf branch ([ADR-0024](0024-public-provider-extensibility-model.md)
Amendment 2) with a third, **compile-time-gated** outcome. The gate is an
MSBuild property surfaced to the generator via
`AnalyzerConfigOptionsProvider` (the same mechanism generators already use
to read consumer-set configuration, not a new one) —
`ComponoGeneratedTestDoubles` (`true`/`false`, default `false`). When
`true`: for every interface leaf the generator would otherwise leave as a
bare `context.Resolve<T>()` call, it additionally emits that leaf's double
type (Section "Generated code shape") and registers it into a small,
generator-emitted internal lookup keyed by `System.Type` — deduplicated
once per distinct interface symbol across the whole compilation, same
`.Collect()` + `SymbolEqualityComparer` pattern already used elsewhere in
the generator (per ADR-0042's own generator-architecture note). When
`false` (default): **zero output changes from today** — the leaf is left
exactly as it already is, `context.Resolve<T>()`, deferring entirely to
the runtime provider pipeline. This satisfies ADR-0042's explicit-over-
implicit driver directly: upgrading core `Compono` alone changes nothing;
the compile-time opt-in must be set for any new behavior to appear at all.

### Runtime activation and precedence

A new `GeneratedTestDoubleProvider : ICompositionValueProvider`
(`NSubstituteProvider`-sized, per ADR-0042) reads the generator-emitted
lookup at runtime and is registered via `builder.UseGeneratedTestDoubles()`
— the same explicit, opt-in shape `UseNSubstitute()` already is, built
entirely on [ADR-0024](0024-public-provider-extensibility-model.md)'s
existing `AddTestDoubleProvider` stage-6 registration, zero new engine
mechanism. **Precedence, if both a generated double and
`Compono.NSubstitute` are installed and enabled**: explicit registration →
explicit `.For<T>()` provider rule → generated test double → NSubstitute —
`AddTestDoubleProvider` calls in that order (`UseGeneratedTestDoubles()`
before `UseNSubstitute()`) at `CompositionBuilder` configuration time,
consistent with ADR-0024's existing "registration order" dispatch rule,
made **explicit** by ordering guidance in this package's own documentation
rather than left to registration-order accident — directly resolving the
open gap ADR-0042 flagged (the same gap the TUnit.Mocks investigation
originally left open for any second test-double provider). A consumer who
registers both in the opposite order gets NSubstitute-first behavior for
any type both could satisfy — an explicit, documented consequence of
registration order, not silent or diagnosed-against in v1.

### Package boundary and naming

Per Considered Options' package-boundary Option 1, confirmed by ADR-0042's
own Gate-A finding: the compile-time-gated double-emission logic
(`LeafTypeClassifier` extension, the generated code shape above) ships
inside core `Compono`/`Compono.Generators` — it needs the same-pass
discovery access nothing else has, and per Decision Drivers, is inert
(zero behavior change) unless the compile-time opt-in is set, so it costs
nothing for a consumer who never enables it. The runtime-facing surface —
`GeneratedTestDoubleProvider`, `UseGeneratedTestDoubles()`,
`ReturnConfigBuilder<T>`, `Compono.TestDoubles.Configure<T>(...)` — ships
in a **new optional package, `Compono.TestDoubles`**. Ruled out per
ADR-0042's own naming guidance: `Compono.Mocks`/`Compono.Mocking` (implies
the general-purpose direction Gate A explicitly rejected). `Compono.TestDoubles`
was chosen over more elaborate alternatives (`Compono.GeneratedDoubles`,
`Compono.SourceGeneratedMocks`) for matching this repo's plain
`Compono.<EcosystemName-or-Concept>` naming convention exactly and reading
clearly next to the existing `Compono.NSubstitute` in a project reference
list.

### Standalone usability

Falls out with essentially no extra cost, confirmed by the generated-code
shape above: `RepositoryDouble` and its `Configure(...)` surface have zero
dependency on `Composer`/`[Compose]`/`CompositionRow` — they're ordinary
generated types a consumer could construct and configure directly
(`new RepositoryDouble()`, then `Compono.TestDoubles.Configure(...)`)
without any Compono composition involved at all, satisfying the SHOULD
priority from ADR-0042's Decision Drivers without any special design work
— it was never coupled to composition in the first place; only the
*discovery signal* (deciding a double should be generated for a given
interface at all) is Compono-composition-specific, and that discovery
still requires **some** compile-time trigger for a standalone consumer,
same as every external library researched (ADR-0042's Context) — expected,
not a contradiction, per ADR-0042's own framing. The exact standalone
trigger shape (an explicit attribute a non-Compono consumer could use
directly) is left to the implementing plan, not designed here — it's a
natural, low-risk consequence of this design, not a driver of it.

### Diagnostics

Compile time, extending [ADR-0042](0042-compono-owned-source-generated-test-doubles.md)'s
existing diagnostics strategy with no new category: unsupported member
shapes (indexers, events, generic methods, `ref`/`out`/`in` parameters,
static abstract members) get a clear diagnostic naming the member and
reason, with the leaf still deferring to the ordinary runtime-provider
path unchanged; `Configure(...)` called with a value that isn't a
Compono-generated double (compile-time-checkable in the common case via
generic constraints, with a clear runtime exception as the fallback for
anything the type system can't fully close). No runtime diagnostics beyond
that exception — no strict mode, no verification, matching v1's scope
exactly.

### Positive Consequences

- Both forks were resolved with real, checkable evidence (a working
  interceptor spike; TUnit.Mocks' own proven distinct-receiver-type
  pattern) rather than a preference call — the chosen mechanism is the
  simpler of the two genuinely viable options, not the only one considered.
- `[Shared]` identity requires zero change to `CompositionScope` — the
  access-shape choice was made specifically to avoid that cost, keeping
  this feature's core-engine footprint exactly as small as ADR-0042's own
  maintenance-cost driver expected.
- The compile-time opt-in (`ComponoGeneratedTestDoubles`) plus the
  explicit `UseGeneratedTestDoubles()` runtime call together give this
  feature two independent, both-required activation gates — a consumer
  who does neither sees no behavior change at all, addressing ADR-0042's
  "must never silently start manufacturing a double" driver twice over.
- Configured returns/exceptions land in v1 at low real cost, meaningfully
  narrowing the gap to `Compono.NSubstitute`'s usefulness for the common
  case without approaching its (or any external library's) verification/
  argument-matching/strict-mode feature set.

### Negative Consequences

- `Compono.TestDoubles.Configure<T>(...)`'s downcast is a real, if narrow,
  escape from pure static typing (a runtime type check) — accepted,
  because the alternative (Considered Options' access-shape Option 1)
  costs real new `CompositionScope` engine work this ADR's Decision
  Drivers rule out, and the downcast is provably safe by construction
  (Compono itself generated the concrete type to implement the requested
  interface) rather than a genuine runtime risk.
- The generated-per-member configuration extension methods add real,
  if bounded, generated-code volume per discovered interface — accepted,
  matching `Compono.XunitV3`/`Compono.TUnit`'s own precedent of real
  per-consumer-shape generated code being an expected cost of the
  source-generation-first architecture, not a regression specific to this
  feature.
- Registration-order-dependent precedence between `Compono.NSubstitute`
  and this feature (both installed) is resolved by documentation/
  convention, not a diagnostic — accepted for v1, flagged explicitly
  (Section "Runtime activation and precedence") rather than left as an
  ambient, undocumented risk the way ADR-0042 found it.

## Pros and Cons of the Options

### Control-surface mechanism — interception

- Good, because it was proven technically viable end-to-end (real spike),
  contradicting the assumption it might still be preview/unstable.
- Bad, because it solves a problem this design doesn't actually have — a
  directly-implementing generated type doesn't need call-site rewriting to
  control its own method bodies — making it pure, unnecessary indirection
  for v1's scope.

### Control-surface mechanism — distinct receiver type, no interception (chosen)

- Good, because it's proven by an existing, shipping library
  (TUnit.Mocks) rather than a novel mechanism, and needs nothing beyond
  ordinary C# overload resolution — no compiler-feature-stability risk,
  no position-hash fragility.
- Good, because it composes cleanly with explicit interface implementation
  to give an unambiguous, compiler-enforced separation between "real call"
  and "configure this call," with zero runtime cost either way.
- Bad, because it's marginally more generated code per member (an
  explicit-interface-implemented method plus a same-named extension) than
  a single dual-purpose method would be — accepted as a small, fixed cost.

### `[Shared]`/access shape — concrete-type-typed parameter

- Good, because it would let a test parameter list directly show "this is
  a configurable double" in its own type.
- Bad, because it requires `CompositionScope` to serve one shared value
  under two different requested types — new core engine work this ADR's
  Decision Drivers explicitly rule out for this feature's scope.

### `[Shared]`/access shape — interface-typed parameter + `Configure(...)` (chosen)

- Good, because `[Shared]` behaves identically to how it already does for
  `Compono.NSubstitute` today — zero new engine mechanism, zero risk of
  regressing existing shared-value behavior.
- Bad, because a reader has to know `Configure(...)` exists to find the
  configuration surface, rather than it being visible in the test's own
  parameter list — a real, accepted ergonomics cost against a real,
  avoided architecture cost.

### Package boundary — generator in core, runtime surface optional (chosen)

- Good, because it matches ADR-0042's own Gate-A finding exactly — the
  generator half structurally can't live anywhere else, and gating it
  behind a compile-time opt-in keeps it free for anyone who doesn't use it.
- Bad, because it's a more complex package story than a single self-
  contained package — accepted, since ADR-0042 already established this
  split isn't optional, just a consequence of where composition-graph
  discovery actually lives.

## Amendment 1 (2026-08-13): `Configure(...)` must be generator-emitted per interface, not a runtime generic method

Pre-implementation review of this ADR (before any code was written) caught
a real defect in the "`[Shared]` identity" section's sketch above:
`Compono.TestDoubles.Configure<T>(T value)` was described as "a small,
non-generated, generic runtime method" living in the precompiled
`Compono.TestDoubles` package, downcasting to the consumer-generated
`RepositoryDouble` and returning its generated configuration surface.
**That is not compile-valid.** `Compono.TestDoubles.dll` is compiled once,
before any consumer's `RepositoryDouble` exists — a generic method's return
type must be closed at the *caller's* compile time from types the method's
own signature can express (`T`, or a type built from `T`), and there is no
way for a runtime-package generic method to return "whatever
per-interface configuration type Compono's generator happened to emit for
this specific `T`," because that type was never part of `Configure<T>`'s
own signature and doesn't exist in any form `Compono.TestDoubles` could
reference when it was built. The original sketch's Decision Outcome text
above is left exactly as written, per `design-decisions.md`'s immutability
rule — this Amendment supersedes only that one method's design, not the
surrounding architecture, which holds up unchanged (see below).

**Corrected shape: `Configure` is generator-emitted, once per discovered
interface, in the same generation pass as that interface's `RepositoryDouble`
and `RepositoryDoubleConfiguration` (Section "Generated code shape" above)
— not a runtime package method at all.**

```csharp
// Compono.Generators-emitted, same pass as RepositoryDouble/RepositoryDoubleConfiguration.
namespace Compono.TestDoubles.Generated;

internal static class RepositoryConfigureExtension
{
    public static RepositoryDouble Configure(this IRepository repository) =>
        repository as RepositoryDouble
            ?? throw new InvalidOperationException(
                $"'{repository.GetType()}' is not a Compono-generated test double for 'IRepository'. " +
                "Configure(...) only works on a value produced by Compono's generated test-double provider.");
}
```

This is provably safe by construction the same way the original sketch
claimed (Compono generated `RepositoryDouble` specifically to implement
`IRepository`) — the only change is *where* that downcast lives: emitted
per interface, at the exact same compile time and in the exact same
generation pass as the type it downcasts to, rather than as a generic
runtime method that could never have known about that type. No reflection,
`dynamic`, or untyped control API was introduced to work around this — the
fix stays exactly within this ADR's existing no-reflection/AOT-safety
Decision Drivers.

**Consumer syntax changes shape, not spirit.** The static-call spelling
`Compono.TestDoubles.Configure(repository)` this ADR originally sketched is
dropped — a static method can't be generated "into" an existing static
class from a different assembly across per-interface call sites the way an
extension method naturally can. The corrected, idiomatic spelling is
ordinary C# extension-method syntax on the interface-typed value itself:

```csharp
repository.Configure().FindAsync(Arg.Any<Guid>(), default).Returns(Task.FromResult(customer));
```

`repository.Configure()` resolves to the one generated extension whose
receiver-parameter type (`IRepository`) matches `repository`'s static
type — ordinary overload resolution, no ambiguity with any other
discovered interface's own generated `Configure` extension, the same
mechanism this ADR's "explicit interface implementation" reasoning already
relies on elsewhere. `Compono.TestDoubles.Generated` (mirroring
TUnit.Mocks' own `TUnit.Mocks.Generated` global-using convention, directly
observed in this repo's own prior TUnit.Mocks spike) is added as a
package-level global `using` by `Compono.TestDoubles` itself (a shipped
`.props`/`GlobalUsings` file, the same packaging technique this repo's own
interceptor spike already proved works for auto-injecting MSBuild
properties without a consumer edit), so a consumer never has to write that
`using` by hand either.

**What does *not* change:** `Compono.TestDoubles`'s actual runtime
contents — `ReturnConfigBuilder<T>` and `GeneratedTestDoubleProvider`.
Neither was ever broken by this defect: `ReturnConfigBuilder<T>` is closed
over ordinary CLR types (`Task<Customer?>`, `string`, etc.) by *generated*
code at the generated call site, ordinary generic instantiation, not a
per-consumer generated type flowing backward into the runtime package's
own signature — a fundamentally different (and valid) situation from
`Configure<T>`'s. `GeneratedTestDoubleProvider` was never generic over a
per-consumer type either. The package boundary this ADR decided (generator
logic in core, runtime primitives in `Compono.TestDoubles`) is unchanged;
only the ownership of the `Configure` bridge moves from "runtime package
generic method" (invalid) to "generator-emitted, per interface" (valid).

PLAN-0043 is updated in the same pass as this Amendment to reflect the
corrected task breakdown (`Configure` extension generation moves to Phase
0's generator work; Phase 1's runtime-package task list drops the
now-nonexistent `Configure<T>` method).

## Amendment 2 (2026-08-13): cross-assembly bridge, generated-type collision safety, core/optional package boundary corrected, argument-matching sample struck

PR #82 review (Codex, four findings against this ADR's own text — three P1,
one P2) caught real gaps in the "Generated code shape," "`[Shared]`
identity," "Generator architecture," and "Package boundary and naming"
sections above, all discovered before any implementation code existed. The
original Decision Outcome text above (including Amendment 1) is left
exactly as written, per `design-decisions.md`'s immutability rule — this
Amendment supersedes the affected sketches, not the surrounding
architecture, which holds up unchanged (control-surface mechanism,
`[Shared]`/access-shape choice, and generator-architecture opt-in gate are
all unaffected).

**Finding 1 — the runtime provider cannot reach a lookup generated into the
consumer's own compilation.** The original "Generator architecture"
section described "a small, generator-emitted internal lookup keyed by
`System.Type`" living in the consumer's compilation. `GeneratedTestDoubleProvider`
is precompiled into `Compono.TestDoubles.dll` — it cannot reference an
`internal` type that doesn't exist until the consumer's own later
compilation, exactly the same class of defect Amendment 1 already fixed for
`Configure<T>`, this time in the opposite direction (a precompiled type
needing to reach *into* generated code, rather than a precompiled type
trying to *return* one). As written, satisfying the request would have
required reflection/`Activator.CreateInstance` from a bare `Type` — directly
contradicting this ADR's own no-reflection/AOT Decision Drivers.

**Finding 2 — the generator would need to know an optional package's type
shape.** The original "Package boundary and naming" section placed
`ReturnConfigBuilder<T>` in the optional `Compono.TestDoubles` package,
while requiring `Compono.Generators` (core) to emit code instantiating it —
meaning core's own hand-written generator source would have to hardcode an
optional integration package's type shape to emit correct code, reversing
this repo's required dependency direction
(`design-decisions.md` rule 3) even without an actual project reference.

**Corrected shape for both findings together:** a `Type`-keyed registry and
`ReturnConfig<T>`/`ReturnConfigBuilder<T>` move into **core `Compono`**
(`namespace Compono`), the same "extension-point contract lives in core, the
specific provider implementation lives in the optional package" split
[ADR-0024](0024-public-provider-extensibility-model.md) already established
for `ICompositionValueProvider`/`NSubstituteProvider`. Population uses the
exact mechanism this repo's own TUnit.Mocks investigation already found and
proved (`MockRegistry.RegisterFactory<T>` via `[ModuleInitializer]`) —
consumer-generated code registers itself into a core-owned registry at
module-load time, so `GeneratedTestDoubleProvider` never needs to reference
anything the consumer generated:

```csharp
// Core Compono - always present, inert unless a factory is ever registered.
namespace Compono;

public struct ReturnConfig<T>
{
    internal bool HasValue;
    internal T? Value;
    internal Exception? Exception;

    internal readonly bool HasException => Exception is not null;
}

public readonly ref struct ReturnConfigBuilder<T>
{
    private readonly ref ReturnConfig<T> _slot;

    internal ReturnConfigBuilder(ref ReturnConfig<T> slot) => _slot = ref slot;

    public void Returns(T value) => _slot.Value = value;
    public void Throws(Exception exception) => _slot.Exception = exception;
}

public static class GeneratedTestDoubleRegistry
{
    public static void RegisterFactory<T>(Func<T> factory) where T : class => /* keyed internally by typeof(T) */;
    public static bool TryCreate(Type requestedType, out object? value) => /* ... */;
}
```

```csharp
// Compono.TestDoubles (optional package) - unchanged in spirit from the original sketch,
// now reads the core registry instead of an unreachable consumer-generated lookup.
public sealed class GeneratedTestDoubleProvider : ICompositionValueProvider
{
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
        GeneratedTestDoubleRegistry.TryCreate(request.RequestedType, out var value)
            ? CompositionProviderResult.Handled(value)
            : CompositionProviderResult.NotHandled;
}
```

`Compono.Generators`' own hand-written source now only ever emits references
to core `Compono` types (`ReturnConfig<T>`, `ReturnConfigBuilder<T>`,
`GeneratedTestDoubleRegistry`) — it never needs to know `Compono.TestDoubles`
exists at all, resolving Finding 2 directly. `Compono.TestDoubles` shrinks to
exactly `GeneratedTestDoubleProvider` and `UseGeneratedTestDoubles()` — the
"Package boundary and naming" section's original reasoning for *why* a
package split exists is unchanged, only which primitives sit on which side
of it.

**Finding 3 — generated types must stay file-scoped.** The original
"Generated code shape" sketch emitted `internal sealed class RepositoryDouble`
and `internal static class RepositoryDoubleConfiguration` — compilation-visible
types, contradicting `coding-standards.md`'s "every generator-emitted type is
`file`-scoped" invariant (collision safety: two consumer interfaces, or a
consumer's own similarly-named helper, must never collide with a Compono-
generated type).

**Two drafts of this fix were written and experimentally disproven before
landing on the one below** — worth recording in full so neither dead end
gets rediscovered during implementation:

1. *File-scope every emitted type in this feature.* A throwaway spike
   confirmed an extension method declared in a `file`-scoped class compiles
   and is callable from the *same* file its receiver type is declared in,
   but a *different* file referencing that same `file`-scoped receiver type
   fails with `CS0246` ("type or namespace name could not be found") —
   `Configure()` and the per-member configuration extensions must be
   callable from the consumer's own test file, a different file than the
   one Compono generates, so this doesn't work for them.
2. *Keep only `RepositoryDouble` itself file-scoped, in the same physical
   file as `internal` configuration/bridge types that reference it by
   name.* A second spike disproved this too, with a harder compiler error:
   `CS9051` — **"File-local type cannot be used in a member signature in
   non-file-local type,"** even within the same file. `file`-scoping isn't
   just cross-file-invisible; a `file`-local type is flatly barred from
   appearing in *any* signature (parameter or return type) of a non-file-
   local member, full stop, co-located or not.

**Corrected shape, verified by a third spike that actually compiles and
runs:** none of this feature's generated types are `file`-scoped — every
one (the double, its per-member configuration extensions, the `Configure()`
bridge) is `internal`, made collision-safe the same way this generator
already keeps `AddSource` hint names collision-safe per `coding-standards.md`'s
existing "Hint names are readable + stable-hash-suffixed" rule: each type's
name is the sanitized interface name plus the same deterministic FNV-1a
hash suffix `GeneratedFileNaming.HintNameFor` already computes
(`src/Compono.Generators/Emitters/GeneratedFileNaming.cs`) — reused for type
names here, not just file hint names, so two differently-namespaced
interfaces that happen to share a simple name (`MyApp.Data.IRepository` vs.
`MyApp.Legacy.IRepository`) can never collide on the generated type name
either. This is a genuine, first-of-its-kind exception to `coding-standards.md`'s
file-scoping default — not a violation of its *purpose* (collision safety),
which this naming scheme still fully provides, just not through the file
boundary, because this feature is the first generated-code shape in this
codebase whose whole point is being referenced by name from outside its own
generated file:

```csharp
// Compono.Generators-emitted, single file, e.g. "RepositoryDouble.g.cs".
// Illustrative naming below - "IRepository_a1b2c3d4" stands in for whatever
// GeneratedFileNaming-style sanitized-name+hash the real implementation computes.

internal sealed class IRepository_a1b2c3d4_Double : IRepository
{
    internal global::Compono.ReturnConfig<Task<Customer?>> __findAsync;
    internal global::Compono.ReturnConfig<Compono.Unit> __save; // Unit: existing internal void-marker shape, or introduced by this feature.

    Task<Customer?> IRepository.FindAsync(Guid id, CancellationToken ct) =>
        __findAsync.HasException ? throw __findAsync.Exception!
        : __findAsync.HasValue ? __findAsync.Value!
        : Task.FromResult<Customer?>(null);

    void IRepository.Save(Customer customer)
    {
        if (__save.HasException) throw __save.Exception!;
    }
}

internal static class IRepository_a1b2c3d4_DoubleConfiguration
{
    // No arguments - argument-independent configuration, per Finding 4 below.
    public static global::Compono.ReturnConfigBuilder<Task<Customer?>> FindAsync(this IRepository_a1b2c3d4_Double self) =>
        new(ref self.__findAsync);

    public static global::Compono.ReturnConfigBuilder<Compono.Unit> Save(this IRepository_a1b2c3d4_Double self) =>
        new(ref self.__save);
}

internal static class IRepository_a1b2c3d4_ConfigureExtension
{
    public static IRepository_a1b2c3d4_Double Configure(this IRepository repository) =>
        repository as IRepository_a1b2c3d4_Double
            ?? throw new InvalidOperationException(
                $"'{repository.GetType()}' is not a Compono-generated test double for 'IRepository'.");
}

// file is fine here - a module initializer is invoked by the runtime itself,
// never called by name from consumer code, so it has no cross-file visibility
// requirement, and no collision-safety requirement beyond what one already-
// unique-per-interface generated file naturally provides.
file static class IRepository_a1b2c3d4_DoubleRegistration
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register() =>
        global::Compono.GeneratedTestDoubleRegistry.RegisterFactory<global::IRepository>(
            static () => new IRepository_a1b2c3d4_Double());
}
```

Verified directly (`Program.cs` in a separate file from the generated code,
calling `repository.Configure().Describe()` through an `IRepository`-typed
local, never naming the concrete generated type at all): this compiles and
runs correctly. The caller never needs to spell
`IRepository_a1b2c3d4_Double` — ordinary extension-method resolution and
type inference (`var`, or a chained call) carry the concrete type through
without it ever appearing in consumer-authored source, the same experience
Amendment 1's `Configure()` design always intended, now on a shape that
actually compiles.

**Finding 4 (P2) — the `[Shared]` identity sample used argument-matching
syntax (`Arg.Any<Guid>()`) that v1's own scope excludes.** The requester's
own fork-2 decision (recorded in this ADR's own history) explicitly put
argument matchers out of scope for v1. The original sample's
`Compono.TestDoubles.Configure(repository).FindAsync(Arg.Any<Guid>(), default).Returns(...)`
contradicted that directly, and the paragraph immediately after it
compounded the problem by treating argument matching as merely
"not fully specified" rather than already excluded. **Corrected:**
configuration is member-level and argument-independent — a configured
`Returns`/`Throws` applies to every call of that member regardless of
arguments, consistent with the existing Non-Goal, not a new one. The
corrected usage:

```csharp
[Compose<SomeProfile>]
public void Test([Shared] IRepository repository, Service sut)
{
    repository.Configure().FindAsync().Returns(Task.FromResult(customer));
    // sut received the SAME repository instance via [Shared] - ReferenceEquals holds,
    // because it IS the same object; Configure(...) is a safe downcast, not a lookup.
}
```

No argument-matching design surface remains open — the original sketch's
"real design surface this ADR leaves for the implementing plan to work out"
framing is retracted; there is nothing left to design for v1 on this point.

PLAN-0043 is updated in the same pass as this Amendment to reflect the
corrected task breakdown (the registry and `ReturnConfigBuilder<T>` move to
core `Compono`'s own critical files; one generated file per interface with
every type `internal` and hash-suffixed-collision-safe-named — reusing
`GeneratedFileNaming` — except the `file`-scoped module-initializer
registration class, which has no cross-file-visibility or collision-safety
requirement of its own; a module-initializer registration task; no
argument-matching task).

## Amendment 3 (2026-08-13): public cross-assembly state contract, overload/name-collision diagnostics, documented multi-assembly registry limitation

A second PR #82 review pass (Codex, four more P1 findings and one P2,
against Amendment 2's own corrected sketches — still before any
implementation code existed) caught further real gaps. Amendment 2's text
is left exactly as written, per the same immutability rule Amendment 2
itself followed for the original Decision Outcome — this Amendment
supersedes only the specific sketches below.

**Finding A — `ReturnConfig<T>`'s fields and `ReturnConfigBuilder<T>`'s
constructor are `internal`, but the generated double lives in a different
(consumer) assembly than core `Compono`.** `internal` doesn't cross
assembly boundaries — every opted-in consumer would hit `CS0122` the moment
generated dispatch code tried to read `__findAsync.HasException`/`.Value`,
or generated configuration code tried to call `new ReturnConfigBuilder<T>(...)`.

**Finding B — `Returns` never marked the slot configured.** `Returns(T value)
=> _slot.Value = value;` sets `Value` but never `HasValue`, and generated
dispatch selects the configured value only when `HasValue` is `true` — every
configured return would have silently fallen through to the deterministic
default.

**Corrected together:** `ReturnConfig<T>`'s backing fields stay `internal`
(only core `Compono`'s own `ReturnConfigBuilder<T>` writes them directly,
same-assembly access, no problem there), but gain `public` readonly
accessors for the generated dispatch code (a different assembly) to read.
`ReturnConfigBuilder<T>`'s constructor becomes `public` — it already only
touches same-assembly `internal` fields internally, so accessibility was the
only blocker. `Returns` now sets both fields:

```csharp
// Core Compono.
namespace Compono;

public struct ReturnConfig<T>
{
    internal bool HasValue;
    internal T? Value;
    internal Exception? Exception;

    public readonly bool HasConfiguredValue => HasValue;
    public readonly bool HasConfiguredException => Exception is not null;
    public readonly T ConfiguredValue => Value!;
    public readonly Exception ConfiguredException => Exception!;
}

public readonly ref struct ReturnConfigBuilder<T>
{
    private readonly ref ReturnConfig<T> _slot;

    public ReturnConfigBuilder(ref ReturnConfig<T> slot) => _slot = ref slot;

    public void Returns(T value)
    {
        _slot.Value = value;
        _slot.HasValue = true;
    }

    public void Throws(Exception exception) => _slot.Exception = exception;
}
```

Generated dispatch now reads only the public accessors:

```csharp
Task<Customer?> IRepository.FindAsync(Guid id, CancellationToken ct) =>
    __findAsync.HasConfiguredException ? throw __findAsync.ConfiguredException
    : __findAsync.HasConfiguredValue ? __findAsync.ConfiguredValue
    : Task.FromResult<Customer?>(null);
```

Mutable state (the backing fields) stays `internal` to core `Compono` —
only `ReturnConfigBuilder<T>`'s own `Returns`/`Throws` methods can ever
write them; a consumer can read a slot's configured state through the
public accessors but can't reach in and mutate it directly, exactly the
"read and mutate without exposing unnecessary mutable state" shape asked
for.

**Finding C — a registry keyed only by `System.Type` breaks when two
consumer assemblies discover the same shared interface.** If `IRepository`
is declared in a common library both assembly A and assembly B reference,
and both opt in, each generates its own distinct concrete double type and
both module initializers register a factory under the same
`typeof(IRepository)` key — whichever registration wins process-wide can
hand assembly B a double built for assembly A, and B's own `Configure()`
bridge then fails its concrete-type cast. Unlike the existing
`RowInvokerRegistry` precedent (`src/Compono/RowInvokerRegistry.cs`),
whose duplicate registrations are genuinely interchangeable, these
factories are not.

**Decided: a documented v1 limitation, not a core-engine redesign.** A
fully correct fix needs the registry to be assembly-aware — threading
"which assembly is asking" through `CompositionProviderRequest`/
`ICompositionContext` — real new core engine surface this ADR's scope
doesn't currently budget for, and confirmed directly with the requester as
out of scope for v1. `GeneratedTestDoubleRegistry.RegisterFactory<T>` keeps
first-registration-wins semantics (deterministic, not silently random), and
the `Configure()` bridge's cast-failure message is upgraded from a generic
`InvalidOperationException` to one that names this exact scenario, so a
consumer who hits it in a multi-assembly test host understands why rather
than seeing a confusing cast failure:

```csharp
public static IRepository_a1b2c3d4_Double Configure(this IRepository repository) =>
    repository as IRepository_a1b2c3d4_Double
        ?? throw new InvalidOperationException(
            $"'{repository.GetType()}' is not the 'IRepository' test double generated for this assembly. " +
            "If another assembly in this process also generated a double for 'IRepository', only one " +
            "registration wins process-wide (Compono.GeneratedTestDoubleRegistry, first-registration-wins) " +
            "- this is a known v1 limitation, not a bug in your test.");
```

**Finding D — overloaded interface members can't share one zero-argument
configuration entry point.** Amendment 2's own argument-independent fix
(Finding 4 there) removed all parameters from configuration extensions —
correct for a non-overloaded member, but for an interface declaring both
`Get(int)` and `Get(string)`, both would generate an identical
`Get(this <Double> self)` signature with no parameters to disambiguate them
by — a duplicate-member compile error, not merely unsupported behavior.
Overloaded members were never added to the unsupported-shapes list, so this
would have silently produced uncompilable generated code.

**Decided: diagnose and reject, matching the existing unsupported-shape
pattern** (indexers, events, generic methods, `ref`/`out`/`in`, static
abstract members) — confirmed directly with the requester over a genuinely
different alternative (keeping real parameter types as pure, value-ignored
overload discriminators), which was rejected as a subtler design not worth
it for v1. An interface with an overloaded member gets a clear compile-time
diagnostic naming the member; that leaf still defers to the ordinary
runtime-provider path unchanged, exactly like every other unsupported
member shape.

**Finding E (P2) — `Configure()` can be shadowed by an identically-named
interface member.** If `IRepository` itself declares a parameterless
`Configure()` method, `repository.Configure()` always resolves to that real
instance member — C# overload resolution prefers instance members over
extension methods unconditionally — silently making the generated bridge
unreachable with no compile error, just wrong (or simply confusing) runtime
behavior.

**Decided: diagnose it**, the same pattern as Finding D — an interface that
declares its own member named `Configure` with a signature that would
collide gets a clear compile-time diagnostic; that leaf still defers to the
ordinary runtime-provider path unchanged.

PLAN-0043 is updated in the same pass as this Amendment to reflect all five
corrections above.

## Amendment 4 (2026-08-13): compiler-visible opt-in property, retired stale global-using promise, accessible void marker, unsupported return-shape diagnostics

A third PR #82 review pass (Codex, four more P1 findings, still before any
implementation code existed) caught further real gaps — two against the
original Decision Outcome text, one against Amendment 1's own text, one
against Amendment 3's own fix. All prior Amendment text is left exactly as
written, per the same immutability rule already followed twice above.

**Finding F — the compile-time opt-in was never declared compiler-visible.**
"Generator architecture" above says `ComponoGeneratedTestDoubles` is "an
MSBuild property surfaced to the generator via `AnalyzerConfigOptionsProvider`
(the same mechanism generators already use to read consumer-set
configuration, not a new one)" — true for a *built-in* compiler-recognized
property (this ADR's own interceptor spike used exactly one,
`InterceptorsNamespaces`, which needs no extra declaration), but false for a
brand-new *custom* property like this one. Roslyn only surfaces a custom
MSBuild property to `AnalyzerConfigOptionsProvider` if it's also listed in a
`CompilerVisibleProperty` MSBuild item — without that declaration, a
consumer setting `ComponoGeneratedTestDoubles=true` would have no effect at
all; the generator would never observe it and the feature could never
activate, regardless of what a consumer sets.

**Corrected:** core `Compono` ships the declaration itself, via a packaged
build asset (the same `buildTransitive`-style packaging technique this
ADR's own interceptor spike already proved works for auto-injecting MSBuild
configuration with zero consumer `.csproj` edits):

```xml
<!-- Shipped by core Compono's own package build assets. -->
<ItemGroup>
  <CompilerVisibleProperty Include="ComponoGeneratedTestDoubles" />
</ItemGroup>
```

**Finding G — Amendment 1's global-using promise went stale.** Amendment 1
(above, left unchanged) says `Compono.TestDoubles` injects
`global using Compono.TestDoubles.Generated;` unconditionally, so a
consumer never has to write that `using` by hand. Amendment 2 then moved
every type this feature generates into per-interface `internal` types with
hash-suffixed names — none of them live in `Compono.TestDoubles.Generated`
anymore, gate on or off, eligible interfaces discovered or not. An
unconditional `global using` targeting a namespace that never has any
member anywhere in the compilation is a real compiler error, not a no-op —
directly contradicting this feature's own "zero behavior change when not
opted in" driver, this time as a **compile failure** merely from
referencing the `Compono.TestDoubles` package at all, gate off or on.

**Corrected: the global-using promise is retired, not repaired.** It was
only ever needed because Amendment 1's original sketch imagined a shared
namespace consumer code would otherwise have to `using` by hand — Amendment
2's per-interface, ordinary-overload-resolution design (Section "Why
explicit interface implementation removes the ambiguity," Amendment 2's
corrected generated-code shape) never required consumer code to write any
`using` for the generated types in the first place, `Configure()`/the
per-member configuration extensions are found by ordinary extension-method
lookup the moment their containing (`internal`, same-assembly) type is
anywhere in the compilation — no namespace import needed at all, by either
the package or the consumer. `Compono.TestDoubles` ships no global-using
declaration.

**Finding H — the void-member marker was missed by Amendment 3's own
accessibility fix.** Amendment 3 made `ReturnConfig<T>`/`ReturnConfigBuilder<T>`
usable across the core/consumer assembly boundary, but the "Generated code
shape" section's `ReturnConfig<Compono.Unit>` sketch (used for `void`
members) still describes `Unit` only as "the existing internal void-marker
shape, if Compono already has one; otherwise a small internal struct
introduced by this feature" — if introduced as `internal`, generated
consumer code referencing it hits exactly the `CS0122` Amendment 3 already
fixed for everything else, just missed for this one type.

**Corrected:** if core `Compono` doesn't already have a `Unit`-shaped type,
this feature introduces `public readonly struct Unit;` in core `Compono` —
public from the start, no separate accessibility fix needed later, applying
Amendment 3's own lesson (every type a generated consumer-assembly member
signature touches must be public) up front rather than rediscovering it a
fourth time.

**Finding I — return-shape diagnostics only covered parameters, not
returns.** "Diagnostics" above lists unsupported *parameter* shapes
(`ref`/`out`/`in`) but nothing for unsupported *return* shapes. A member
like `Span<byte> Read()` is ref-like and cannot close the unconstrained
generic `ReturnConfig<T>` at all (ref-like types can never be a generic
type argument); a by-ref-returning member (`ref int Current` on an
indexer/property) cannot be satisfied by returning a plain value from a
slot the way every other member shape in this design can. Left undiagnosed,
either shape would produce broken, non-compiling generated code rather than
a clean diagnostic deferring to the runtime-provider path.

**Corrected:** the unsupported-member-shape diagnostic list gains
ref-like, by-ref-returning, pointer, and function-pointer return shapes,
checked the same way the existing parameter-modifier check already is —
that leaf still defers to the ordinary runtime-provider path unchanged,
exactly like every other unsupported shape.

PLAN-0043 is updated in the same pass as this Amendment to reflect all four
corrections above.

## Links

- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) — the
  admitted problem and Gate A/Gate B result this ADR designs against; every
  Non-Goal recorded there is unchanged by this ADR.
- [ADR-0024](0024-public-provider-extensibility-model.md) — the
  `ICompositionValueProvider`/`AddTestDoubleProvider` extension point
  `GeneratedTestDoubleProvider` builds on directly, zero new engine
  mechanism.
- [ADR-0025](0025-compono-nsubstitute-package-design.md) — `Compono.NSubstitute`,
  the precedent this ADR's provider sizing, activation shape
  (`UseX()`), and non-goal discipline are modeled on; the package this
  feature's precedence rule explicitly orders against.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  the exact-requested-type shared-value storage this ADR's access-shape
  decision deliberately avoids needing to change.
- `docs/roadmap/future-packages.md` — updated alongside this ADR to record
  `Compono.TestDoubles` as the resulting package name.
