# [ADR-0024] Public Provider Extensibility Model

**Status:** Accepted

**Date:** 2026-07-31

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

`docs/architecture.md`'s Resolution Pipeline reserves stage 5 (semantic value
providers) and stage 6 (test-double providers) for exactly this purpose, and both
have sat empty since Milestone 2 — wired end-to-end in `CompositionContext`
(`_semanticProviders`/`_testDoubleProviders` fields, `TryProviders` dispatch,
`PipelineStage.SemanticProvider`/`TestDoubleProvider` trace entries) but never fed
by anything, because nothing outside `Compono` itself has ever needed to populate
them. `docs/mvp.md`'s Milestone 3 section and `docs/architecture.md`'s own Open
Architectural Decisions list both record the same explicit deferral: Milestone 3's
own scope (registrations, profiles, type/member rules) needs no public provider
contract at all — [ADR-0020](0020-composition-configuration-rules.md) compiles
`.For<T>()` rules into small, *internal*, Compono-authored `ICompositionProvider`
implementations, so a user never implements that interface directly. Milestone 5
(`Compono.NSubstitute`) is the first real consumer that needs open-ended,
pattern-matching logic a closed-set rule can't express ("any interface type," "any
unsealed abstract class") — the deferred question this ADR now settles.

The engine's own `ICompositionProvider` (`src/Compono/ICompositionProvider.cs`) is
deliberately `internal`: its `TryCompose(in CompositionRequest, ICompositionContext)`
signature takes the engine's internal `CompositionRequest` (a path, an `IsShared`
flag, and other pipeline plumbing no integration author should need to know about
or could construct correctly without reflection). Making that interface `public`
outright — the most literal reading of "add a provider extension point" — would
either force `CompositionRequest`/`CompositionPath`/`PathSegment` to become public
too (a large, ongoing-maintenance-cost surface, and exactly the "avoid exposing
source-generator/engine implementation details" rule `docs/public-api.md`'s API
Design Rules already state), or leave external implementors constructing a request
some other, undocumented way. This ADR designs a smaller, decoupled public contract
instead — mirroring the split [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)
already established between the public, compact `CompositionRequestDescriptor`
generated code passes and the internal, path-expanded `CompositionRequest` the
engine actually operates on.

This ADR is scoped to the **mechanism** — the public contract, registration
surface, stage placement, ordering, diagnostics identity, and failure semantics
shared by any future stage-5/6 integration. [ADR-0025](0025-compono-nsubstitute-package-design.md)
is the first real consumer built on top of it (`Compono.NSubstitute`'s own
substitution rules, options, and package-specific diagnostics). Splitting the two
mirrors [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)/
[ADR-0022](0022-compono-xunit-package-design.md)'s precedent: a core-engine-extension
ADR, and a package-design ADR that consumes it.

## Decision Drivers

- `design-decisions.md` rule 3: the core `Compono` package must never reference or
  know about an integration package. The contract this ADR designs has to be
  something `Compono.NSubstitute` (and, unbuilt but validated against here,
  `Compono.Bogus`) can implement **from the outside**, with zero core-to-integration
  reference in either direction beyond the ordinary integration-depends-on-core
  arrow already established.
- `docs/architecture.md`'s Resolution Pipeline: stage order is fixed product
  contract, "not configurable — not by users or by providers reordering
  themselves." Any design that lets a provider see or influence "what happens
  next" beyond its own `NotHandled`/`Handled` answer (an AutoFixture-`ISpecimenBuilder`-
  style recursive "keep asking further builders" hook, or an ASP.NET-middleware-style
  `next()` delegate) would hand a provider implicit control over pipeline ordering —
  explicitly out of bounds per the task that produced this ADR ("avoid designing
  something that resembles AutoFixture's `ISpecimenBuilder` or a general middleware
  pipeline unless those designs prove objectively superior" — no such proof arose
  during this design).
- `docs/public-api.md`'s API Design Rules: minimal public surface, one obvious way
  to do a thing, avoid exposing engine internals. The smallest contract that lets
  NSubstitute (and, by construction, Bogus) express "any interface type"/"any
  member named `Email`" pattern-matching is the actual target, not the largest one
  that could theoretically be useful.
- Diagnostics (`docs/architecture.md`'s Diagnostics section, `ProviderAttempt.Provider`
  per [ADR-0016](0016-provider-identity-restored-in-provider-attempt.md)): a
  composition failure must still be able to name the concrete provider type that
  was tried, exactly as it does for stage 4/7's internal providers today — this
  can't regress just because the provider now originates outside the assembly.
- `ADR-0001`'s no-reflection-by-default rule governs `Compono` core's own
  construction path; it does not (and per this design task's own framing, should
  not) reach into what a third-party library like NSubstitute does internally to
  do its own job. The core contract itself must stay reflection-free to implement
  and to invoke; what a specific provider implementation does inside its own
  `TryProvide` body is that provider's own concern.
- Stateless-provider expectation: `Composer`/`CompositionConfiguration` are
  immutable and reused across every `Create<T>()`/`CreateMany<T>()`/`CreateRow`
  call a composer ever serves, including concurrent calls (xUnit v3 supports
  parallel test execution). A provider instance is constructed once and invoked
  repeatedly, potentially concurrently — its `TryProvide` must not depend on or
  mutate per-call instance state.

## Considered Options

### Provider contract shape

1. **A new, minimal public interface** (`ICompositionValueProvider`) with its own
   public request/result value types, decoupled from the engine's internal
   `ICompositionProvider`/`CompositionRequest`/`CompositionResult` — compiled, at
   `CompositionBuilder.Build()` time, into a small internal adapter registered into
   stage 5/6's existing `ICompositionProvider` list, the same "public data compiles
   into an internal provider" shape [ADR-0020](0020-composition-configuration-rules.md)
   already established for `.For<T>()` rules.
2. **Delegate-based registration only** — `builder.AddTestDoubleProvider(Func<CompositionProviderRequest, ICompositionContext, CompositionProviderResult> tryProvide)`,
   no interface at all.
3. **Make the engine's own `ICompositionProvider` (and enough of `CompositionRequest`/
   `CompositionResult` to support it) public directly.**
4. **A middleware/chain-of-responsibility contract** — `TryProvide(request, context, next)`,
   where a provider can call `next()` to continue past itself, AutoFixture-`ISpecimenBuilder`-style.

### Stage placement and registration

1. **One shared interface, two stage-scoped registration methods** —
   `ICompositionValueProvider` is not stage-specific; `builder.AddSemanticProvider(provider)`
   and `builder.AddTestDoubleProvider(provider)` are what decide which pipeline
   stage (5 or 6) a given instance lands in. An integration's own `UseX()` extension
   method calls whichever registration method matches its own package's purpose.
2. **Two marker sub-interfaces** — `ISemanticValueProvider : ICompositionValueProvider`,
   `ITestDoubleProvider : ICompositionValueProvider`, with a single
   `builder.AddProvider(...)` overloaded/dispatched by static type.

### Diagnostic identity through the adapter

1. **Add a `ProviderType` member to the internal `ICompositionProvider` interface**,
   default-implemented as `=> GetType()`; the internal adapter wrapping a public
   provider overrides it to report the wrapped instance's own concrete type. Every
   trace/diagnostic call site that currently reads `candidate.GetType()` reads
   `candidate.ProviderType` instead.
2. **Leave `ProviderAttempt.Provider` reporting the adapter's own type** (e.g. a
   generic `PublicProviderAdapter`) for every public-provider attempt, accepting
   the diagnostic regression.

## Decision Outcome

**Chosen: Option 1 for the contract shape, Option 1 for stage placement, Option 1
for diagnostic identity** — confirmed directly with the user (a genuine fork
between a named public interface and delegate-based registration was raised and
resolved in favor of the interface, for exactly the reasons Option 2's Pros/Cons
below state).

**What this ADR actually commits to, versus what's shown for illustration.**
Per this ADR's own Alpha Compatibility Policy (below) and explicit user direction
during review: `Compono` is intentionally alpha until the MVP milestones
(`docs/mvp.md`) complete, and this repo already has a process for revising a
shipped public contract when real evidence calls for it (see that section). This
ADR should not — and does not — over-design the provider contract to protect
against every hypothetical future metadata need. The architectural guarantees this
ADR actually commits to are:

- A **named public interface** is the authoring shape for a stage-5/6 provider
  (not a bare delegate).
- A **small request/result contract**, decoupled from the engine's own internal
  request/path types — a provider author never sees `CompositionRequest`,
  `CompositionPath`, or `PathSegment`.
- **Deterministic stage placement and registration order** — which stage (5 or 6)
  a provider participates in is explicit at registration time, and multiple
  providers in the same stage are tried in registration order.
- **Isolation from internal engine request/path types** — the public contract
  never leaks an internal engine type into a provider author's code.
- **Diagnostics identify the logical provider** that produced or attempted a
  result, not an opaque wrapper/adapter type.
- **Immutable, reusable provider registration** — a provider is constructed once
  at configuration time and safe to invoke repeatedly (including concurrently)
  for the lifetime of the `Composer` it's registered on.
- **`NotHandled` versus `Handled` semantics** — a provider can decline or produce
  a value; it cannot report a stronger "failure" the way a context-owned
  authoritative stage can.

Everything else in this ADR's code samples below — the exact type names
`CompositionProviderRequest`/`CompositionProviderResult`, the internal
`PublicProviderAdapter` class name, the `ProviderType` default-interface-member
mechanism, `AddSemanticProvider`/`AddTestDoubleProvider` as two methods rather than
one overloaded/stage-inferred method, and every other dispatch micro-detail — is an
**illustrative implementation choice**, useful to prove the design is buildable and
to give `implement.md` a concrete starting point, not a separate guarantee this ADR
locks in. Milestone 6 (Bogus) or Milestone 7 (dogfooding) surfacing a real need to
reshape any of it is expected, not a process failure — see the Alpha Compatibility
Policy below for how that gets handled if it happens.

### The public contract

```csharp
namespace Compono;

/// <summary>
/// A composition request, as seen by a public <see cref="ICompositionValueProvider"/> — decoupled
/// from the engine's own internal request shape (no path, no shared-scope flag, no pipeline
/// plumbing a provider author has no legitimate use for).
/// </summary>
public readonly struct CompositionProviderRequest
{
    /// <summary>The requested CLR type.</summary>
    public Type RequestedType { get; }

    /// <summary>
    /// The type whose constructor/required member declares this request, or
    /// <see langword="null"/> for a request with no member identity (a collection element, a
    /// manual resolve, or the composition root itself) — the same field/same semantics
    /// <c>CompositionRequestDescriptor.DeclaringType</c> already carries, per
    /// <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </summary>
    public Type? DeclaringType { get; }

    /// <summary>
    /// The declaring constructor parameter/required member/test-method-parameter's own name, for
    /// diagnostic display and name-based provider matching (e.g. a future <c>Compono.Bogus</c>
    /// member-name convention) — <see langword="null"/> for a request with no name of its own
    /// (a collection element, dictionary key/value, or manual resolve).
    /// </summary>
    public string? Name { get; }

    /// <summary>Whether the requesting parameter or member is nullable-annotated.</summary>
    public Nullability Nullability { get; }
}

/// <summary>
/// What an <see cref="ICompositionValueProvider"/> reports for one <see cref="CompositionProviderRequest"/>.
/// </summary>
/// <remarks>
/// Deliberately only two cases, mirroring the engine's own internal <c>CompositionResult</c>
/// contract for ordinary providers: a public provider can report that it doesn't apply, or that it
/// produced a value — never a stronger "failure." An unhandled exception a provider's own
/// <see cref="ICompositionValueProvider.TryProvide"/> implementation throws propagates uncaught,
/// exactly like an internal stage-4/7 provider's exception does today — see this ADR's Provider
/// Failure Semantics section.
/// </remarks>
public readonly struct CompositionProviderResult
{
    /// <summary>The provider does not handle this request.</summary>
    public static CompositionProviderResult NotHandled => default;

    /// <summary>The provider produced <paramref name="value"/> for this request.</summary>
    public static CompositionProviderResult Handled(object? value) => new(isHandled: true, value);

    // IsHandled/Value are internal - a public provider constructs a result only through the two
    // factory members above, never by inspecting or round-tripping one it didn't just receive.
    internal bool IsHandled { get; }
    internal object? Value { get; }

    private CompositionProviderResult(bool isHandled, object? value)
    {
        IsHandled = isHandled;
        Value = value;
    }
}

/// <summary>
/// A public extension point for pipeline stage 5 (semantic value providers) or stage 6
/// (test-double providers) — open-ended, pattern-matching composition logic a closed-set
/// <c>.For&lt;T&gt;()</c> rule can't express ("any interface type," "any member named <c>Email</c>").
/// Registered via <see cref="CompositionBuilder.AddSemanticProvider"/> or
/// <see cref="CompositionBuilder.AddTestDoubleProvider"/> — which method an integration's own
/// <c>UseX()</c> extension calls decides which stage a given instance participates in; the
/// interface itself is not stage-specific.
/// </summary>
/// <remarks>
/// An implementation must be safe to invoke repeatedly, including concurrently, once constructed —
/// a <see cref="Composer"/>'s configuration (and every provider registered into it) is immutable and
/// reused across every composition call it ever serves, exactly like every other builder-compiled
/// piece of configuration (a <c>.For&lt;T&gt;()</c> rule, a registration factory).
/// </remarks>
public interface ICompositionValueProvider
{
    /// <summary>
    /// Attempts to produce a value for <paramref name="request"/>. Returns
    /// <see cref="CompositionProviderResult.NotHandled"/> for any request this provider doesn't
    /// apply to, so a later provider or pipeline stage still gets a chance — never throws for an
    /// expected non-match.
    /// </summary>
    /// <param name="request">The request to attempt.</param>
    /// <param name="context">
    /// The active composition context - a provider may call <c>context.Resolve&lt;T&gt;()</c> to
    /// compose part of its value from a nested request, exactly as an internal provider already
    /// may (<c>docs/architecture.md</c>'s Providers section).
    /// </param>
    CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context);
}
```

### Registration and compilation

```csharp
public sealed class CompositionBuilder
{
    public CompositionBuilder AddSemanticProvider(ICompositionValueProvider provider);
    public CompositionBuilder AddTestDoubleProvider(ICompositionValueProvider provider);
}
```

Both accumulate into an ordered list (registration order — the same "provider order
within an extensible stage is registration order" rule stage 4/7 already follow;
no priority/specificity system, matching `docs/architecture.md`'s existing "no
richer ordering rule exists yet because none has been needed" reasoning applied to
a new stage rather than invented for one). At `Build()` time, each accumulated
`ICompositionValueProvider` is wrapped, in order, into an internal
`PublicProviderAdapter : ICompositionProvider` and placed into
`CompositionConfiguration.SemanticProviders`/`TestDoubleProviders`
(`IReadOnlyList<ICompositionProvider>`, mirroring the existing `Rules` field's
shape exactly). `Composer.Create<T>`/`CreateMany<T>`/`CreateRow` thread these two
new lists into `CompositionContext`'s already-existing `_semanticProviders`/
`_testDoubleProviders` constructor parameters — both fields have been wired since
Milestone 2 and have simply never been fed anything until now; no pipeline-dispatch
code changes at all, only what's constructed and passed in.

```csharp
internal sealed class PublicProviderAdapter : ICompositionProvider
{
    private readonly ICompositionValueProvider _inner;

    internal PublicProviderAdapter(ICompositionValueProvider inner) => _inner = inner;

    public Type ProviderType => _inner.GetType();

    public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
    {
        var publicRequest = new CompositionProviderRequest(
            request.RequestedType, request.DeclaringType, NameOf(request.Path.Segment), request.Nullability);

        var result = _inner.TryProvide(in publicRequest, context);

        return result.IsHandled
            ? new CompositionResult.Success(result.Value)
            : CompositionResult.NotHandled.Instance;
    }
}
```

This is the exact shape [ADR-0020](0020-composition-configuration-rules.md)
already established for value rules ("compile into internal, Compono-authored
`ICompositionProvider` instances — a user never implements `ICompositionProvider`
directly"), applied to a second public entry point into the same stage-4-adjacent
compilation step, not a new mechanism.

### Diagnostic identity: `ProviderType` on `ICompositionProvider`

The internal `ICompositionProvider` interface gains one default-implemented member:

```csharp
internal interface ICompositionProvider
{
    Type ProviderType => GetType();

    CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context);
}
```

Every existing internal provider (`PrimitiveValueProvider`, `TypeRuleProvider`,
etc.) gets the default `GetType()` behavior for free, unchanged from today.
`PublicProviderAdapter` is the only implementation that overrides it, reporting the
*wrapped* provider's own concrete type (e.g. `NSubstituteProvider`), not the
adapter's own type. `CompositionContext.TryProviders` (and the one other read site,
`InvokeFactory`'s `provider` parameter passthrough) read `candidate.ProviderType`
instead of `candidate.GetType()` — a one-line internal change per call site.
`ProviderAttempt.Provider` therefore continues to name the real, meaningful
provider type for a public-provider attempt exactly as it already does for stage
4/7's internal ones — no diagnostic regression, and no public API change
(`ProviderAttempt` itself, and `ICompositionProvider`, both stay exactly as
`public`/`internal` as they already are).

### Provider Failure Semantics

An `ICompositionValueProvider` can only report `NotHandled` or `Handled(value)` —
never a stronger "failure" result, matching every other ordinary provider in this
engine (`docs/architecture.md`'s Providers section: "Ordinary providers cannot
report Failure"). If a provider's own `TryProvide` implementation throws — a bug in
the provider, or a third-party library it wraps throwing internally for an
input it superficially claimed but couldn't actually satisfy — that exception
propagates uncaught out of `Create<T>()`/`CreateMany<T>()`/`CreateRow`'s call,
exactly as an internal stage-4/7 provider's own thrown exception already does today
(`CompositionContext.TryProviders` calls `candidate.TryCompose(...)` with no
surrounding `try`/`catch` at all). This is a deliberate consistency choice, not a
gap newly introduced by public providers: stage 3's exact-registration factories
are the one place a thrown exception is caught and converted into an authoritative,
diagnostic-carrying `CompositionException`
([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md))
precisely *because* stage 3 is a context-owned authoritative stage; stages 4-7 are
not, and have never wrapped provider exceptions. A well-behaved provider (per this
interface's own contract, restated in its XML doc) should proactively check what it
can statically determine before doing any real work, and return `NotHandled` for
anything it can't actually satisfy, rather than attempting the work and letting a
third-party exception surface raw — [ADR-0025](0025-compono-nsubstitute-package-design.md)
follows exactly this pattern for `NSubstituteProvider`.

### Interaction with existing stages and mechanisms

None of these required any new engine mechanism — each is an existing one, simply
reachable from a new direction:

- **Stage order.** Unchanged, fixed, per `docs/architecture.md`. Stage 5/6 sit
  between stage 4 (configuration rules) and stage 7 (built-in providers), exactly
  where they've always been reserved. A registration/rule/prior-stage match always
  wins over a public provider for the same request; a public provider always gets
  first refusal over stage 7/8's built-in/generated fallback.
- **Shared substitute reuse / composition scopes.** No new mechanism — stage 2
  (shared/scoped values, [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
  already runs before stage 5/6 and already stores *any* stage's successful result
  when the request `IsShared`. A `[Shared] IRepository` parameter that a test-double
  provider satisfies is stored and reused exactly like a shared registration or
  built-in value would be — `docs/mvp.md`'s "Shared substitute reuse" Milestone 5
  scope item is fully satisfied by the existing engine, zero new work.
- **Deterministic seeds / composition path.** A provider that calls
  `context.Resolve<T>()` for a nested value forks the random stream and path
  exactly like any other nested request (existing behavior, unchanged). A provider
  that produces a leaf value directly (M5's `NSubstituteProvider`, per
  [ADR-0025](0025-compono-nsubstitute-package-design.md)'s explicit no-recursive-
  configuration non-goal) never touches randomness at all.
- **Recursion.** Not a new concern — the active-construction-frame recursion check
  ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
  is pushed only around generated-plan dispatch (stage 8), not stage 5/6. A public
  provider that never calls `context.Resolve<T>()` (M5's provider) can't
  participate in a construction cycle at all; one that does is subject to the exact
  same cycle detection any other nested request already is.
- **Open generic behavior.** Does not arise: `CompositionProviderRequest.RequestedType`
  is always a closed, concrete runtime `Type` (composition requests are never for
  an open generic definition), so a provider never has to special-case one. This is
  unrelated to core's separate, already-deferred "open generic *registrations*"
  non-goal (`docs/mvp.md`).
- **`IServiceProvider` fallback.** Unaffected — stage 3 (exact registrations, then
  the configured `IServiceProvider` fallback,
  [ADR-0019](0019-registrations-and-service-provider-injection.md)) runs entirely
  before stage 4/5/6, so a consumer's own DI container or exact registration always
  takes precedence over a test-double/semantic provider for the same type, with no
  new interaction to design.
- **Caching.** None needed beyond what already exists. A provider's own match
  decision (e.g. `RequestedType.IsInterface`) is cheap enough to recompute per
  request; value *reuse* is already scope's job (above), not the provider's.

### Package Boundaries and Reflection Boundary

`ICompositionValueProvider`/`CompositionProviderRequest`/`CompositionProviderResult`
are defined in core `Compono`, `public`, with zero dependency on any integration
package — an integration package implements the interface from the outside,
matching the existing dependency-diagram arrow direction
(`docs/architecture.md`'s Package Dependency Diagram: every arrow points *into*
`Compono`, never out). The public contract itself requires and performs zero
reflection to implement or invoke (an ordinary interface method call through
`PublicProviderAdapter`). What a specific provider implementation does *inside* its
own `TryProvide` body — NSubstitute's own `Substitute.For<T>()` internally uses
Castle DynamicProxy/IL emission, which is reflection- and codegen-heavy and not
Native-AOT-safe — is entirely that provider's own concern, confined to its own
integration package. This is the explicit framing this design task was given:
"treat reflection required by NSubstitute itself as an integration concern, not a
relaxation of the core runtime guarantees," and it falls out of the design directly
rather than needing a special carve-out — `docs/mvp.md`'s existing "Native AOT
certification" non-goal already covers the consequence for a consumer that opts
into `UseNSubstitute()`.

### Performance Characteristics

No new allocation or dispatch cost on the pipeline's existing hot path beyond what
stage 4/7 already pay for their own ordered-provider lists: `TryProviders`'
existing loop, `CompositionTraceBuffer`'s existing per-attempt recording, and
`PublicProviderAdapter`'s own `TryCompose` (one struct construction for
`CompositionProviderRequest`, one interface dispatch to `TryProvide`) are all
already-established costs of the same shape stage 4's `TypeRuleProvider`/
`MemberRuleProvider` already pay. `PublicProviderAdapter` instances themselves are
constructed once per registered provider, at `Build()` time — not per request, per
`Create<T>()` call, or per composition. Whatever a specific provider's own
`TryProvide` implementation allocates (e.g. NSubstitute's own proxy-generation
cost) is that provider's own cost, orthogonal to this design; `Compono.Benchmarks`'
existing `ResolutionBenchmarks` infrastructure is the right place to add a
representative test-double-composed benchmark once `Compono.NSubstitute` ships
(a `PLAN-0005` task, not a design decision).

### Validated against Bogus (M6), not designed for it

To confirm this contract doesn't need reshaping the moment a second consumer shows
up, it was sketched (not built) against Milestone 6's own scope
(`docs/mvp.md`: "Conservative member-name conventions," "Explicit member rules"):

```csharp
// Compono.Bogus, sketch only — not part of this milestone's scope.
internal sealed class BogusMemberNameProvider : ICompositionValueProvider
{
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
        request switch
        {
            { RequestedType.FullName: "System.String", Name: "FirstName" } =>
                CompositionProviderResult.Handled(_faker.Name.FirstName()),
            { RequestedType.FullName: "System.String", Name: "Email" } =>
                CompositionProviderResult.Handled(_faker.Internet.Email()),
            _ => CompositionProviderResult.NotHandled,
        };
}
```

`CompositionProviderRequest.Name`/`DeclaringType` (present, but unused by M5's own
`NSubstituteProvider`) are exactly what this needs — no interface change
anticipated for Milestone 6 based on this sketch. Not a commitment that Bogus's
actual design will look like this; a real Milestone 6 design dive still owns that
decision, per this repo's own process.

### Alpha Compatibility Policy

`ICompositionValueProvider`/`CompositionProviderRequest`/`CompositionProviderResult`
are `public` — but `Compono` as a whole is still alpha, pre-`docs/mvp.md`-completion
software, and this contract is no exception to that:

- **Provisional, not frozen.** This contract is public so `Compono.NSubstitute`
  can build against it and so a consumer could write a custom provider today, not
  because it's guaranteed to be byte-for-byte stable through the rest of the MVP.
- **Milestone 6 and Milestone 7 are expected to validate it further.** This ADR's
  own Bogus sketch (below) is a lightweight check that the contract isn't
  accidentally NSubstitute-shaped, not proof it's the final shape — a real
  Milestone 6 design dive, and Milestone 7's dogfooding pass, are the actual tests
  of whether `CompositionProviderRequest`'s fields, `ICompositionValueProvider`'s
  method shape, or the two-registration-method split hold up against real use.
- **Breaking changes are allowed when justified by real integration or dogfooding
  evidence** — a concrete need Milestone 6/7 (or `Compono.NSubstitute` itself)
  surfaces, not a hypothetical one imagined ahead of time. This ADR deliberately
  does not add abstraction, versioning indirection, or extra fields "just in case"
  to avoid ever having to make such a change during alpha — see the framing note
  under Decision Outcome above.
- **Any such change must be explicitly documented, never silently introduced.**
  Concretely, during alpha:
  - The ADR that introduced the contract (this one, or [ADR-0025](0025-compono-nsubstitute-package-design.md)
    for the NSubstitute-specific surface) gets a dated `## Amendment N (YYYY-MM-DD)`
    section recording what changed and why, per `design-decisions.md`'s existing
    Amendment mechanic — never a silent edit to this ADR's original Decision
    Outcome text.
  - `docs/public-api.md`'s Provider Extensibility/NSubstitute Integration sections
    and the affected plan's Notes are updated in the same PR.
  - The PR itself is labeled and categorized per this repo's existing Release
    Drafter breaking-change convention — see `PLAN-0005`'s Breaking-Change
    Handling During Alpha section for the mechanics, which apply to any future PR
    that revises this contract after it first ships, not only to
    `Compono.NSubstitute`'s own initial implementation.

### Positive Consequences

- The smallest public surface that lets NSubstitute (and, by validated sketch,
  Bogus) express open-ended pattern matching: one interface, one method, two small
  value types, two builder registration methods.
- No engine-internal type (`CompositionRequest`, `CompositionPath`, `PathSegment`,
  the internal `CompositionResult`) becomes public; the internal/public split this
  repo already uses for descriptors ([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md))
  is extended, not abandoned.
- Diagnostics keep real provider-type identity through the adapter, with no public
  API cost (`ProviderType` is an internal-only addition).
- Zero new pipeline mechanism: stage order, shared-scope reuse, recursion
  detection, the `IServiceProvider` fallback, and collection dispatch are all
  reused completely unchanged — this milestone fills in two already-reserved,
  already-wired, always-empty stages rather than inventing new plumbing.
- Explicitly does not resemble `ISpecimenBuilder`/a middleware pipeline: a provider
  answers exactly one request with exactly one of two outcomes and has no way to
  see, skip, or reorder any other stage or provider.

### Negative Consequences

- Two small new public value types (`CompositionProviderRequest`,
  `CompositionProviderResult`) plus one new public interface add to the public
  surface `docs/public-api.md` asks to keep minimal — accepted as the necessary
  cost of a real, decoupled public extension point; the alternative (Option 3)
  costs strictly more surface for a worse encapsulation outcome.
- A provider that throws is not wrapped into a diagnostic-carrying
  `CompositionException` the way a stage-3 registration factory's exception is —
  an accepted, pre-existing asymmetry (stages 4-7 have never wrapped provider
  exceptions), not a new gap, but worth revisiting if it proves painful once
  `Compono.NSubstitute` ships real usage.
- `ICompositionProvider.ProviderType`'s default-interface-member addition is a
  binary-compatible but still real shape change to an `internal` interface — no
  external consumer can observe it (the interface is `internal`), but it's the
  first default-interface-member this codebase has used; noted as a small style
  precedent, not a risk.

## Pros and Cons of the Options

### Contract shape — new minimal public interface (chosen)

- Good, because it keeps every engine-internal type internal.
- Good, because it reuses this repo's own established descriptor/request-split
  precedent (ADR-0010) rather than inventing a new pattern.
- Good, because it preserves diagnostic identity through a small, internal-only
  adapter addition.
- Bad, because it's two new public value types plus one interface, more surface
  than a delegate-only design — accepted, see Decision Outcome.

### Contract shape — delegate-based registration only

- Good, because it's the smallest possible public surface: one method parameter
  type, zero new interfaces to learn.
- Good, because a trivial custom provider needs no class declaration at all.
- Bad, because a captured lambda has no meaningful `GetType()`/type identity for
  `ProviderAttempt.Provider` — the exact diagnostic-naming problem
  [ADR-0016](0016-provider-identity-restored-in-provider-attempt.md) already
  solved for internal providers would regress for every public one, the single
  biggest reason this option was rejected (confirmed directly with the user).
- Bad, because `NSubstituteProvider`/a future `BogusProvider` are reusable
  integration components with their own configuration, diagnostics, and lifecycle
  concerns (per the user's own framing) — a named, independently unit-testable,
  independently documented type fits that better than an inline lambda a package's
  `UseX()` extension method would otherwise have to construct and hand over.
- A delegate-based convenience overload may still be added later for a lightweight
  ad hoc custom provider, adapting to `ICompositionValueProvider` internally rather
  than defining the core extensibility model itself — noted as a Deferred Decision
  below, not designed here.

### Contract shape — make the engine's own `ICompositionProvider` public

- Bad, because it forces `CompositionRequest` (and transitively `CompositionPath`,
  `PathSegment`, `IsShared`) to become public too, or leaves external implementors
  with no documented way to construct a request at all.
- Bad, because it couples every future internal pipeline refactor to a public
  compatibility promise it doesn't need to make — the exact "avoid exposing
  source-generator/engine implementation details" rule this repo's API Design
  Rules already state.
- Bad, because `docs/public-api.md`'s minimal-surface goal is directly opposed by
  exposing the engine's own richer internal request/result shapes wholesale.

### Contract shape — middleware/chain-of-responsibility (`next()`)

- Bad, because it lets a provider observe and influence what happens after its own
  decision, which conflicts directly with `docs/architecture.md`'s explicit "stage
  order... not configurable, by users or by providers reordering themselves" rule.
- Bad, because it's exactly the shape this design task was told to avoid absent an
  objective advantage — none was found; the existing ordered-list-of-independent-
  candidates dispatch (`TryProviders`) already achieves composability across
  multiple registered providers without any provider needing visibility beyond its
  own match/no-match decision.

## Amendment 1 (2026-07-31): Nested resolution and self-recursion, fixed before any external consumer existed

PR #28 review (Codex, two P1 findings against Phase 0's implementation of this
ADR, both against the same root cause) caught a real gap this ADR's own Decision
Outcome text glossed over: `ICompositionValueProvider.TryProvide`'s XML doc — and
this ADR's own "Interaction with existing stages and mechanisms" section — state
that a provider "may call `context.Resolve<T>()` to compose part of its value from
a nested request, exactly as an internal provider already may." That's true for an
internal `TypeRuleProvider`/`MemberRuleProvider`, but was **not** true for a public
provider as originally implemented, for a reason this ADR didn't anticipate:

1. **The descriptor-less `context.Resolve<T>()` overload threw unconditionally.**
   That overload requires an active manual-resolve frame
   ([ADR-0019](0019-registrations-and-service-provider-injection.md)), pushed only
   by `CompositionContext.InvokeFactory` — which `PublicProviderAdapter.TryCompose`
   never called; it invoked the wrapped provider's `TryProvide` directly. A
   provider following this ADR's own documented contract hit
   `InvalidOperationException` every time.
2. **A provider recursing on its own requested type (via the descriptor-*based*
   overload, which needs no frame) had no cycle protection at all and ran until
   `StackOverflowException`.** An internal `TypeRuleProvider`/`MemberRuleProvider`
   avoids this because `InvokeFactory`'s reentrance guard is keyed on **factory
   delegate identity** — safe only because each of those provider instances is
   compiled 1:1 for exactly one type, so "same delegate re-entered" and "same type
   re-entered" are equivalent by construction. A public `ICompositionValueProvider`
   instance has no such 1:1 guarantee (one instance can legitimately handle many
   types, e.g. "any interface"), so that same keying would either miss real cycles
   or — worse — wrongly block a provider composing an unrelated type as one of its
   own legitimate nested dependencies.

**Fix:** a new internal `CompositionContext.InvokeProvider` method, structurally
parallel to `InvokeFactory` but distinct from it in two ways: reentrance is keyed
on **`(provider instance, requested type)`**, not delegate identity alone; and it
never catches or wraps an exception `TryProvide` throws — `InvokeFactory`'s
blanket `catch (Exception ex)` block is deliberately not reused, since this ADR's
own Provider Failure Semantics section commits to a public provider's thrown
exception propagating uncaught, and reusing `InvokeFactory` wholesale would have
silently reversed that decision (a rejected alternative, considered explicitly
during the fix rather than applied by default — the reentrance-guard's own
diagnosed `CompositionException` is a different concern from wrapping the
provider's *own* thrown exception, and stays intact under this fix). Every
architectural guarantee this ADR's Decision Outcome commits to (named interface,
decoupled request/result contract, stage placement/order, diagnostics identity,
immutable/reusable registration, `NotHandled`/`Handled` semantics) is unchanged by
this fix — `PublicProviderAdapter`'s exact internal shape was already documented
as illustrative, not committed, per this ADR's own framing note under Decision
Outcome.

Fixed entirely within Milestone 5 Phase 0, before `Compono.NSubstitute` (the
ADR's first real external consumer) exists — no external code has ever observed
the broken behavior. Regression coverage:
`Provider_CanComposeANestedValue_ViaTheDescriptorLessResolveOverload` (finding 1,
plus proves the `(provider, type)` key doesn't over-block a legitimate nested
different-type request through the same instance) and
`Provider_RecursivelyResolvingItsOwnRequestedType_ThrowsADiagnosedException_NotStackOverflow`
(finding 2), both in `CompositionValueProviderTests.cs`; the pre-existing
`ThrownException_FromAPublicProvider_PropagatesUncaught` test continues to pass
unchanged, confirming the fix didn't reverse the Provider Failure Semantics
decision.

## Links

- [docs/mvp.md](../mvp.md) — Milestone 5 scope, and Milestone 3's explicit
  deferral of this exact question
- [docs/architecture.md](../architecture.md) — Resolution Pipeline stages 5/6,
  Providers section, Open Architectural Decisions' deferred-extensibility entry
  (resolved by this ADR)
- [docs/public-api.md](../public-api.md) — API Design Rules (minimal public
  surface, avoid exposing engine internals), NSubstitute/Bogus Integration sketches
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) — the
  public-descriptor/internal-request split this ADR's public-request/internal-
  request split mirrors; the `Failure`-is-authoritative-only rule this ADR's
  provider failure semantics follow
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  the shared-scope/recursion mechanisms this ADR reuses unchanged
- [ADR-0016](0016-provider-identity-restored-in-provider-attempt.md) — the
  provider-identity diagnostics contract this ADR's `ProviderType` addition
  preserves through the new public-provider adapter
- [ADR-0019](0019-registrations-and-service-provider-injection.md) — the stage-3
  fallback this ADR's stage-5/6 placement stays subordinate to
- [ADR-0020](0020-composition-configuration-rules.md) — the "compile public
  builder data into an internal `ICompositionProvider`" precedent this ADR reuses
  for public providers instead of value rules
- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)/
  [ADR-0022](0022-compono-xunit-package-design.md) — the core-extension-ADR +
  package-design-ADR split this ADR/ADR-0025 mirror
- [ADR-0025](0025-compono-nsubstitute-package-design.md) — `Compono.NSubstitute`,
  the first real consumer of this contract
