# [ADR-0044] Compono.TestDoubles v2: Overloaded Members, Generic Methods, Minimal Call Verification

**Status:** Accepted

**Date:** 2026-08-14

**Decision Makers:** Nick Cipollina, Claude (design deep dive)

## Context

[ADR-0043](0043-compono-generated-test-doubles-design.md) shipped
`Compono.TestDoubles` v1 (PLAN-0043, `Done`): source-generated, AOT-safe,
interface-only test doubles with member-level, argument-independent
`Returns`/`Throws` configuration. Three shapes were explicitly excluded
from v1 and diagnosed at compile time rather than emitted: overloaded
members (`CMP0022`), generic methods (`CMP0021`), and — per
[ADR-0042](0042-compono-owned-source-generated-test-doubles.md)'s own
Non-Goals — any form of call recording or verification.

A real dogfooding attempt (migrating `lightsaber-skill`'s test suite from
`Compono.NSubstitute` to `Compono.TestDoubles`, requested directly by the
repo owner once the v1 package reached NuGet preview) found the provider
swap itself trivial — the repo already composes through
`[Compose<NSubstituteProfile>]` — but v1's supported-shape boundary
rejects the two interfaces that dominate the suite's substitution surface:

- **`IResponseBuilder`** — rejected outright: it declares
  `Speak(string?)`/`Speak(params ISsml[])` and
  `Reprompt(string?)`/`Reprompt(params ISsml[])`, both overloaded.
- **`ILogger<T>`** — rejected outright: `Log<TState>(...)` and
  `BeginScope<TState>(...)` are both generic methods. `ILogger<T>` appears
  in nearly every composed test in the suite, so even a fully-migratable
  dependency sitting next to it in the same test parameter list buys
  nothing — the test still needs `Compono.NSubstitute` for the logger,
  meaning both providers run side by side with no dependency reduction.
- **`IAmazonS3`** — rejected outright: AWS SDK client interfaces are built
  almost entirely from overload sets.
- **Two real assertions** (`LambdaHandlerTests.cs`) depend on
  `mockMediator.Received(1).Send(request)` — a hard blocker regardless of
  interface shape, since v1 has no verification API at all.

Of roughly 40 NSubstitute call sites in the suite, only a small minority
(`ISkillMediator`, `IOptions<T>`, `ILambdaContext`) would generate cleanly
under v1 today, and every one of them is used alongside a rejected
interface in the same test — so v1 cannot materially reduce the suite's
NSubstitute dependency. This is real, evidenced Gate-B-style evidence
(per [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
evidence-over-prediction bias) for exactly three next capabilities:
overloaded-member support, generic-method support, and minimal call-count
verification. This ADR is the deep-design pass for those three, deliberately
scoped to what the evidence supports — not a general pivot toward mocking-
framework feature parity.

## Decision Drivers

- **Every ADR-0042/ADR-0043 driver still applies unchanged**: no
  cross-generator dependency, no reflection/`Activator.CreateInstance`/
  `MakeGenericMethod`/expression-tree compilation, Native AOT/trimming
  safety, explicit-over-implicit activation, `Compono.NSubstitute` not
  deprecated or replaced, Compono integration first.
- **Evidence discipline.** Every capability added here traces to a
  specific blocker the dogfooding pass actually hit. Argument matchers
  (`Arg.Any<T>()`/`Arg.Is<T>()`) were not shown to be necessary and stay
  out — see Non-Goals.
- **No new per-member storage shape unless the evidence demands it.** v1's
  "one `ReturnConfig<T>` slot per member, no dictionary, no boxing beyond
  what the member's own return type needs" property
  ([ADR-0043](0043-compono-generated-test-doubles-design.md)'s "Generated
  code shape") is worth preserving; a design that needs a
  `Dictionary<Type, ReturnConfig<T>>`-per-closed-generic-instantiation
  shape to support one member's most theoretical case is disqualified in
  favor of a narrower, evidence-matching alternative if one exists.
- **The double must always compile.** Explicit interface implementation
  requires every declared member of the interface (and its full transitive
  base-interface closure, per Amendment 11 Finding Z) to be implemented,
  full stop — there is no partial `: IFoo` in C#. Any design that gives a
  subset of an interface's shapes richer treatment must still produce a
  body for every other member, or fall back to today's whole-interface
  rejection for that member.
- **Existing v1 shipped behavior doesn't change silently.** `Compono.TestDoubles`
  is a released package (NuGet preview); this pass adds capability, it
  doesn't rewrite already-shipped `Returns`/`Throws`/default-value
  semantics without saying so explicitly (see "Async/Throws semantics" in
  Decision Outcome).

## Considered Options

### Requirement 1 — overloaded members

1. **Preserve real parameter types on the configuration surface as pure,
   value-ignored overload discriminators** (`Configure().Speak(string.Empty).Returns(...)`
   vs. `Configure().Speak(Array.Empty<ISsml>()).Returns(...)`) — one
   generated configuration extension per overload, signature matching the
   real overload's parameter *types* exactly, argument values discarded
   (never stored, never matched against). Ordinary C# overload resolution
   — the same mechanism already doing the harder job of resolving the real
   interface members — picks the right extension.
2. **Overload-specific named members** (`Configure().SpeakString()`,
   `Configure().SpeakSsml()`) — a generated suffix derived from each
   overload's parameter-type shape.
3. **Generated member handles** (`Configure().Speak.String`,
   `Configure().Speak.Ssml`) — a nested per-member object exposing one
   property per overload.
4. **Positional/ordinal discriminators** (`Configure().Speak_1()`,
   `Configure().Speak_2()`) — numbered by declaration order.

### Requirement 2 — generic methods

1. **Support only generic methods whose return type does not depend on
   the method's own type parameter(s)** (anywhere in the return type's
   syntax tree, not just as the direct return type) — the explicit
   interface implementation stays generic (type parameters and constraint
   clauses copied verbatim from the interface), but the backing
   `ReturnConfig<T>` slot and its configuration extension are ordinary,
   non-generic, member-level, exactly like v1's existing non-generic
   members — because the slot's `T` is concrete regardless of what the
   caller closes the method's own type parameter to. A generic method
   whose return type *does* depend on its own type parameter (`T Get<T>()`,
   `Task<T> GetAsync<T>()`) stays diagnosed and unsupported.
2. **Per-closed-generic-instantiation storage** — a
   `Dictionary<Type, ReturnConfig<T>>`-shaped (or similar) runtime lookup
   keyed by the closed type argument(s) actually observed, giving every
   generic method (including return-type-dependent ones) its own
   independently configurable behavior per closed `T`.
3. **Diagnose all generic methods as unsupported, unchanged from v1** — do
   nothing.

### Requirement 3 — minimal call verification

1. **A dedicated `Verify()` bridge, parallel to `Configure()`** — a second
   generator-emitted downcast extension returning a distinct wrapper type
   (not the double itself, to avoid extension-method-resolution ambiguity
   with `Configure()`'s own per-member surface), whose per-member handles
   expose `.Once()`/`.Never()`/`.Exactly(n)` against an invocation counter
   folded into the existing per-member `ReturnConfig<T>` slot.
2. **Fold verification into `ReturnConfigBuilder<T>`** — expose a
   `CallCount` property directly off the same handle `Configure().Member()`
   already returns, verified with ordinary `Assert.Equal(...)` rather than
   a dedicated verification API.
3. **A full `Received()`-equivalent** — argument-aware call recording,
   sequence/order verification, `ReceivedCalls()`-style enumeration.

## Decision Outcome

**Chosen: Option 1 for overloads, Option 1 for generic methods, Option 1
for verification.** All three keep the "one small, concrete,
per-member(-overload) `ReturnConfig<T>`-shaped slot, no dictionary, no
boxing, no reflection" architecture ADR-0043 established, extended along
exactly the axis the dogfooding evidence demands and no further.

### Requirement 1 — overloaded members

**Per-overload identity, not per-member-name identity.** Today's
`TestDoubleAnalyzer` rejects an entire member *name* the moment it's
declared more than once in the interface's closure
(`duplicateConfigurationMemberNames`, feeding `CMP0022`). v2 replaces
whole-name rejection with **per-overload** analysis: each overload gets
its own `ReturnConfig<T>` field, keyed by a deterministic hash of its full
parameter-type list (reusing `TestDoubleIdentifierNaming`'s existing
identifier-safe-sanitizer + FNV-1a-hash convention — a sibling application
of the exact same tool already used for collision-safe type names, not a
new naming scheme), and its own configuration extension whose parameter
*types* mirror the real overload exactly:

```csharp
// Two overloads of Speak on IResponseBuilder - each gets its own slot and extension.
internal sealed class IResponseBuilder_a1b2c3d4_Double : IResponseBuilder
{
    internal global::Compono.ReturnConfig<global::IResponseBuilder> __speak_9f8e;   // Speak(string?)
    internal global::Compono.ReturnConfig<global::IResponseBuilder> __speak_2c1d;   // Speak(params ISsml[])

    IResponseBuilder IResponseBuilder.Speak(string? text) =>
        __speak_9f8e.HasConfiguredException ? throw __speak_9f8e.ConfiguredException
        : __speak_9f8e.HasConfiguredValue ? __speak_9f8e.ConfiguredValue
        : this;

    IResponseBuilder IResponseBuilder.Speak(params ISsml[] ssml) =>
        __speak_2c1d.HasConfiguredException ? throw __speak_2c1d.ConfiguredException
        : __speak_2c1d.HasConfiguredValue ? __speak_2c1d.ConfiguredValue
        : this;
}

internal static class IResponseBuilder_a1b2c3d4_DoubleConfiguration
{
    // Parameter value is a pure discriminator - never read, never stored.
    public static global::Compono.ReturnConfigBuilder<global::IResponseBuilder> Speak(
        this global::IResponseBuilder_a1b2c3d4_Double self, string? text) => new(ref self.__speak_9f8e);

    public static global::Compono.ReturnConfigBuilder<global::IResponseBuilder> Speak(
        this global::IResponseBuilder_a1b2c3d4_Double self, params global::ISsml[] ssml) => new(ref self.__speak_2c1d);
}
```

```csharp
responseBuilder.Configure().Speak(string.Empty).Returns(responseBuilder);
responseBuilder.Configure().Speak(Array.Empty<ISsml>()).Returns(responseBuilder);
// Invoking one overload's real behavior never consumes the other's configuration -
// they're two distinct fields, distinguished at compile time by C#'s own overload
// resolution, exactly the same resolution that already picks the right *real* overload.
```

**Why Option 1 over 2/3/4:** Option 1 needs no naming heuristic at all —
the discriminator *is* the real parameter type, which is already
guaranteed unique per overload by the C# language itself (two overloads
with identical parameter types can't coexist in the same interface).
Options 2 and 3 both need to derive a human-readable suffix from an
arbitrary parameter type (`ISsml[]` → `Ssml`? `IReadOnlyList<ISsml>` →
`ReadOnlyListOfSsml`?) — a genuinely harder, more fragile problem than the
one it's solving, and unpredictable for a consumer trying to guess the
generated member name without reading generated source. Option 4 is
declaration-order-dependent — reordering members in the interface (a
routine, semantically-inert refactor) silently changes which
`Configure().Speak_1()` call configures which overload, a footgun v1's own
architecture never has anywhere else. Option 1 is also the smallest actual
diff from v1: it removes the "zero-argument, argument-independent"
constraint only from the *discriminator* parameters used to pick the right
generated extension, while argument-independence itself is fully
preserved *within* an overload (configuring `Speak(string.Empty)` applies
to every real call to `Speak(string?)`, regardless of what string is
actually passed at the real call site) — not a new matcher subsystem, an
extension of the existing member-level rule down one level of granularity.

**Overload-set-internal partial support.** Because interface conformance
already forces the double to implement every overload's dispatch body
regardless of whether that overload gets a `Configure()` extension (same
"give it a body either way" logic v1 already applies to any ordinary
unconfigured member via `TestDoubleDefaults`), an overload whose own shape
is independently unsupported (a `ref`/`out`/`in` parameter, a pointer
parameter, a return type with no deterministic default) gets a
deterministic-default dispatch body but **no** `Configure()` extension for
that specific overload — diagnosed informationally, same severity as every
other unsupported-shape diagnostic — **without** rejecting its sibling
overloads or the rest of the interface. This is a genuine, if narrow,
policy change from v1 (which rejects the whole interface the instant any
member anywhere is unsupported): it's adopted here specifically because
the "give it a body, no Configure() surface" mechanism already exists for
every ordinary member and costs nothing new to apply per-overload instead
of per-interface. It is **not** extended to whole different member
*kinds* (indexers, events) — see "Diagnostics revisit" below and the
explicitly-rejected "interface-level partial support" option.

### Requirement 2 — generic methods

**Scope: generic methods whose return type doesn't reference their own
type parameter(s).** This directly and fully covers the motivating
evidence — `ILogger<T>`'s `void Log<TState>(LogLevel, EventId, TState,
Exception?, Func<TState, Exception?, string>)` and `IDisposable?
BeginScope<TState>(TState state)` — neither returns `TState` or anything
built from it. The explicit interface implementation stays generic,
copying the interface's own type parameters, variance, and constraint
clauses verbatim (mechanical text propagation through the same
`SymbolDisplay`-based emission every other type reference already uses,
extended to also emit `where T : ...` clauses — no new design surface).
The backing slot and its configuration extension are **ordinary,
non-generic, member-level** — because the slot's `T` (`IDisposable?` for
`BeginScope`, `Compono.Unit` for `Log`) is fixed regardless of what the
caller closes `TState` to:

```csharp
IDisposable? ILogger.BeginScope<TState>(TState state)
    where TState : notnull =>
    __beginScope.HasConfiguredException ? throw __beginScope.ConfiguredException
    : __beginScope.HasConfiguredValue ? __beginScope.ConfiguredValue
    : default;

// Configuration extension is NOT generic - TState never reaches the slot's own type.
public static global::Compono.ReturnConfigBuilder<global::System.IDisposable?> BeginScope(
    this global::ILogger_a1b2c3d4_Double self) => new(ref self.__beginScope);
```

```csharp
logger.Configure().BeginScope().Returns(myScope);
// Applies regardless of what TState the real caller closes BeginScope<TState> to -
// the same "member-level, don't-care-about-the-call-site-specifics" philosophy v1
// already applies to ordinary parameters, extended to the method's own type parameter.
```

Answering the specific questions this ADR was asked to settle:

- **Different behavior per closed `TState`?** Not needed and not
  supported — the covered case (return type independent of the type
  parameter) has no meaningful "behavior varies by `TState`" scenario:
  `BeginScope`'s configured return is the same `IDisposable?` no matter
  what `TState` the caller passes.
- **A generated static generic state holder?** Considered and rejected —
  it's Requirement-2 Option 2 above, and only earns its cost for a shape
  (return type depending on the type parameter) the evidence never showed
  a need for. Revisit only against new evidence.
- **AOT/closed-generic-instantiation concerns?** None beyond what already
  exists: the method itself stays generic (an ordinary generic interface
  member, something the CLR/AOT compiler already handles for the real
  interface regardless of Compono), but the *slot* type is always
  concrete — no new closed-generic-instantiation surface is introduced by
  this feature at all.
- **Constraints, multiple type parameters, nullable annotations on type
  parameters?** All mechanical: copy every constraint clause and every
  type parameter as declared; the existing `NullableAwareFullyQualifiedFormat`
  emission already preserves nullable annotations on ordinary types and
  extends the same way to type-parameter-referencing text. No arity
  special-casing.
- **What stays unsupported?** A generic method whose return type
  references its own type parameter anywhere in its syntax tree
  (`T Get<T>()`, `Task<T> GetAsync<T>()`, `IEnumerable<T> Filter<T>()`) —
  diagnosed, same fallback-to-runtime-provider pattern as every other
  unsupported shape. Parameters referencing the type parameter (`TState
  state` in both `ILogger<T>` methods) are always fine regardless — v1's
  argument-independence already means no parameter value is ever stored,
  generic or not.

### Requirement 3 — minimal call verification

**A `Verify()` bridge, parallel to and independent from `Configure()`.**
`Configure()` and `Verify()` cannot both return the bare double type — two
extension methods with the same name and receiver type
(`<Hash>_DoubleConfiguration.Speak(this <Hash>_Double)` vs. a hypothetical
`<Hash>_DoubleVerification.Speak(this <Hash>_Double)`) would be ambiguous
(`CS0121`) the moment both existed on the same receiver. `Verify()`
therefore returns a small **distinct wrapper struct** (not the double
itself), so `.Member()` resolves unambiguously to the verification
extension set instead of the configuration one:

```csharp
internal static class IMediator_a1b2c3d4_VerifyExtension
{
    public static global::IMediator_a1b2c3d4_DoubleVerifier Verify(this global::IMediator mediator) =>
        mediator as global::IMediator_a1b2c3d4_Double is { } d
            ? new(d)
            : throw new InvalidOperationException(/* same cast-failure message shape as Configure() */);
}

internal readonly struct IMediator_a1b2c3d4_DoubleVerifier(global::IMediator_a1b2c3d4_Double instance)
{
    internal global::IMediator_a1b2c3d4_Double Instance { get; } = instance;
}

internal static class IMediator_a1b2c3d4_DoubleVerification
{
    public static global::Compono.CallVerifier Send(this global::IMediator_a1b2c3d4_DoubleVerifier self) =>
        new(self.Instance.__send.CallCount);
}
```

```csharp
mediator.Verify().Send().Once();     // exactly 1 call
mediator.Verify().Send().Never();    // exactly 0 calls
mediator.Verify().Send().Exactly(3); // exactly 3 calls
```

**Storage: extend `ReturnConfig<T>` with a call counter, don't add a
second per-member field.** `ReturnConfig<T>` already exists once per
member (or, per Requirement 1, once per overload) — adding an `internal
int CallCount` field there (mutated same-assembly, generated dispatch
code lives in the same file as the slot) plus a `public readonly int
ConfiguredCallCount` accessor for `Compono.CallVerifier` (a different,
core-`Compono` assembly) to read reuses the exact "internal write surface,
public read surface" split Amendment 3 already established for
`HasValue`/`Value`/`Exception`, rather than introducing a second
per-member field or a new struct shape:

```csharp
Task IMediator.Send(Request request)
{
    global::System.Threading.Interlocked.Increment(ref __send.CallCount);
    return __send.HasConfiguredException ? throw __send.ConfiguredException
        : __send.HasConfiguredValue ? __send.ConfiguredValue
        : global::System.Threading.Tasks.Task.CompletedTask;
}
```

`Compono.CallVerifier` (core `Compono`, public, a small readonly struct
wrapping the observed count) is:

```csharp
public readonly struct CallVerifier(int observedCount, string memberDescription)
{
    public void Never() => Exactly(0);
    public void Once() => Exactly(1);

    public void Exactly(int times)
    {
        if (observedCount != times)
            throw new TestDoubleVerificationException(
                $"Expected exactly {times} call(s) to {memberDescription}, but received {observedCount}.");
    }
}
```

`TestDoubleVerificationException` is a plain `Exception` subtype in core
`Compono` (matching `CompositionException`/`CompositionConfigurationException`'s
existing plain-exception convention — core `Compono` cannot reference
xUnit/TUnit/AwesomeAssertions to throw *their* assertion type, the same
"no reference from core to an integration package" rule
`design-decisions.md` rule 3 already enforces elsewhere).

**Deliberately minimal, matching the explicit instruction:** `Never`/
`Once`/`Exactly(n)` only — no `AtLeast`/`AtMost`, no argument-aware
recording, no call-order verification, no `ReceivedCalls()`-style
enumeration, no strict mode. `Interlocked.Increment` on a plain `int`
field is the cheapest possible thread-safe counter — no allocation per
call, no dictionary, matching the "don't allocate just to support
`Once()`" instruction directly.

**Overload interaction:** verification reuses Requirement 1's exact
per-overload discriminator mechanism — `Verify().Speak(string.Empty)`
selects the same overload-specific slot `Configure().Speak(string.Empty)`
would, per the explicit instruction to reuse rather than invent a second
selection model.

### Async/Throws semantics — settled, not changed

v1's already-shipped dispatch pattern throws **synchronously**, at the
call site, even for `Task<T>`/`ValueTask<T>`-returning members
(`__findAsync.HasConfiguredException ? throw __findAsync.ConfiguredException : ...`
— a real `throw` inside the method body, never `Task.FromException<T>(...)`).
This means `await repository.FindAsync()` faults at the *call*, not at the
`await`, which is a real, if subtle, divergence from the idiomatic
async-fault convention most hand-written async APIs and NSubstitute's own
`Returns(Task.FromException<T>(...))` pattern both follow. **Decided: v2
does not change this.** It is already-shipped, released behavior for
existing `Compono.TestDoubles` consumers; changing it now would be a
breaking behavior change to a public package with no dogfooding evidence
requesting it. This ADR exists to close the ambiguity the requester flagged
(the semantics were implicit in code, never stated in prose), not to
revisit the decision — recorded here explicitly so it isn't accidentally
"fixed" into a breaking change during v2 implementation.

### Diagnostics revisit

| Shape | Disposition |
|---|---|
| Overloaded members | **Now conditionally supported**, per-overload (Requirement 1). `CMP0022` narrows to firing only on an individual overload whose own shape is independently unsupported, not the whole member name. |
| Generic methods, return type independent of type parameter | **Now supported** (Requirement 2). |
| Generic methods, return type depends on type parameter | **Still unsupported** — carved out of `CMP0021`'s existing scope into its own diagnostic (exact code assigned during implementation, next available after `CMP0028`), so the message can name the specific reason (open generic return) rather than the generic blanket "unsupported member kind." |
| Indexers, events, static abstract members | **Still unsupported, unchanged, interface-level rejection** — see "Rejected: full interface-level partial support" below. |
| `ref`/`out`/`in`, pointer/function-pointer parameters or returns, non-nullable-reference returns with no default | **Still unsupported.** Under Requirement 1, now diagnosed at **overload** granularity when the shape appears on one overload among several, instead of always rejecting the whole member name. |
| Set-only properties, `Configure`-name collision, `object`-member collision, inaccessible interfaces | **Unchanged from v1**, no interaction with this ADR's scope. |

**Rejected: full interface-level partial support** (i.e., extending
Requirement 1's per-overload fallback to indexers/events/every remaining
unsupported category, so an interface with one indexer still gets a
double for everything else). Considered explicitly, per the requester's
own instruction not to change this policy casually. Structurally
possible in principle (C# lets you implement an indexer with a trivial
default-returning getter and no-op setter, an event with no-op add/remove)
but real, separable, non-trivial emission work for shapes the dogfooding
evidence never showed blocking anything — `IAmazonS3`'s blockers are
overloads (now fixed by Requirement 1) and, structurally, AWS SDK client
interfaces do not commonly declare indexers or events, so this gap is
unlikely to matter for the evidence actually in hand. Deferred to a future
evidence-gated pass if a real interface is found where a single indexer/
event blocks an otherwise-supportable interface — not bundled into v2
speculatively, matching this repo's evidence-over-prediction bias
([ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)).

### AOT and performance

No new AOT-risk surface: `ReturnConfig<T>`/`ReturnConfigBuilder<T>` stay
exactly as generic as before (per-overload/per-member instantiation is the
same shape v1 already exercises many times per interface); generic-method
support keeps the method itself generic (already something the CLR/AOT
toolchain handles for any interface, with or without Compono) while
storage stays concrete; verification adds one `int` field and one
`Interlocked.Increment` per member, no new type parameters anywhere. The
implementing plan's AOT phase must still **prove**, not assume, this via a
real `dotnet publish -p:PublishAot=true` run exercising all three new
shapes together (an overloaded interface, a covered generic method, and a
verified call) — per this repo's "prove it, don't assume it" standard
([ADR-0001](0001-source-generation-first.md), `Compono.TUnit`'s PLAN-0040
precedent, PLAN-0043's own AOT smoke tests).

Benchmarks are warranted only where they check a real risk: verification's
`Interlocked.Increment` overhead per call, and overload-dispatch overhead
(does adding N sibling fields/branches per overloaded member meaningfully
change dispatch cost) — not a general "beat NSubstitute" exercise, matching
this repo's existing benchmark-suite discipline
([ADR-0034](0034-benchmark-suite-strategy-and-redesign.md)).

### Non-Goals (carried forward and new)

Everything ADR-0042/ADR-0043 already excluded stays excluded except the
three capabilities this ADR explicitly adds:

- **Argument matchers remain out of scope** (`Arg.Any<T>()`/`Arg.Is<T>()`
  equivalents) — the dogfooding evidence never showed a need for
  argument-specific configuration or verification; every overload-
  discriminator parameter introduced by Requirement 1 is a pure,
  value-ignored compile-time selector, never a runtime match.
- **No call-order verification, no argument-aware call recording, no
  `ReceivedCalls()`-style enumeration, no strict mode** — Requirement 3 is
  deliberately `Never`/`Once`/`Exactly(n)` only.
- **No per-closed-generic-instantiation configuration** — Requirement 2's
  scope stops at "return type independent of the method's own type
  parameters."
- **No class/protected-member/static-abstract-member support** — unchanged
  from ADR-0042.
- **No interface-level partial support for indexers/events/other
  remaining unsupported categories** — see "Rejected" above; a
  deliberately separate, evidence-gated future question.
- **Not a general-purpose mocking framework** — the same durable Non-Goal
  ADR-0042 recorded, still true after this extension: three narrow,
  evidence-driven additions, not a pivot toward NSubstitute/Moq feature
  parity.

### Positive Consequences

- Directly closes the two blockers that actually stopped the
  `lightsaber-skill` migration (`IResponseBuilder`'s overloads,
  `ILogger<T>`'s generic methods) and the two `Received(1)` assertions,
  giving the re-dogfood phase (PLAN-0044 Phase 5) a real chance at showing
  material improvement rather than incremental.
- Every new mechanism is a narrow, same-shape extension of an
  already-proven pattern (per-slot fields, internal-write/public-read
  accessor split, generator-emitted per-interface bridge types) — no new
  subsystem, no reflection, no new core engine mechanism in
  `CompositionScope`/`ICompositionValueProvider`.
- Per-overload (not whole-member-name) and per-generic-shape (not
  blanket-generic) rejection granularity means a future interface with
  *some* unsupported shapes still gets partial, real configuration
  coverage for everything else in it, rather than v1's current
  all-or-nothing per interface.

### Negative Consequences

- Two independent bridge types (`Configure()`/`Verify()`) per interface,
  each with its own per-member extension class, roughly doubles this
  feature's generated-code volume for any interface that uses
  verification — accepted, matching `Compono.XunitV3`/`Compono.TUnit`'s
  own precedent that real generated-code volume is an expected cost of
  this architecture, not a regression.
- Per-overload field/branch multiplication means an interface with many
  overloads of the same member (rare, but real for something like
  `IAmazonS3`) generates proportionally more code than v1's flat
  one-field-per-member-name model — accepted, bounded by the interface's
  own real shape, not amplified by this design.
- Full interface-level partial support (indexers/events/etc.) stays
  unresolved — `IAmazonS3` and any interface like it may still hit a
  residual unsupported shape even after this ADR ships, unless the
  specific overload-heavy blockers this ADR fixes turn out to be the only
  ones present. Accepted as a deliberate scope boundary, revisit only on
  new evidence.

## Pros and Cons of the Options

### Overloads — typed discriminators (chosen)

- Good, because the discriminator is the real, already-unique-by-language-rule
  parameter type — no naming heuristic to invent or get wrong.
- Good, because it's the smallest actual change from v1's existing
  per-member-slot architecture: argument-independence is preserved within
  each overload, only the overload-*selection* step gains type-driven
  discrimination.
- Bad, because a consumer must supply *some* value of the right type at
  the call site purely to select an overload, even though that value is
  discarded — a small, real ergonomics cost, accepted as clearly smaller
  than options 2-4's costs.

### Overloads — named/suffix members

- Good, because the generated member name is descriptive without needing
  a dummy argument.
- Bad, because deriving a readable, collision-free suffix from an
  arbitrary parameter-type shape is a harder, more fragile problem than
  the one it replaces, and produces unpredictable names a consumer can't
  guess without reading generated source.

### Overloads — member handles

- Good, because it groups all of a member's overloads under one
  discoverable root (`Configure().Speak.*`).
- Bad, because it still needs the same naming heuristic as the previous
  option, just moved one level deeper, plus a new per-member wrapper type
  this design otherwise has no need for.

### Overloads — ordinal discriminators

- Good, because it needs no naming heuristic and no dummy argument value.
- Bad, because declaration order in the interface is not semantically
  stable — a routine reorder silently reassigns which generated member
  configures which real overload, a footgun nothing else in this
  architecture has.

### Generic methods — return-type-independent only (chosen)

- Good, because it fully covers the actual motivating evidence
  (`ILogger<T>`) with zero new storage mechanism — the configuration
  surface stays exactly as simple as an ordinary non-generic member's.
- Good, because it introduces no new AOT-risk surface: the slot's type is
  always concrete.
- Bad, because a genuinely generic-returning method (`T Get<T>()`) stays
  unsupported — accepted, no evidence currently requires it.

### Generic methods — per-closed-instantiation storage

- Good, because it would cover every generic method shape, not just the
  return-type-independent subset.
- Bad, because it needs a dictionary-shaped runtime lookup per closed type
  argument, reintroducing exactly the "no dictionary/lookup" cost ADR-0043's
  own generated-code-shape section deliberately avoided, for a case the
  evidence never showed was needed.

### Verification — dedicated `Verify()` bridge (chosen)

- Good, because it reads close to NSubstitute's own `Received()` idiom,
  lowering real migration friction — the explicit goal behind adding
  verification at all.
- Good, because a distinct wrapper return type avoids `Configure()`/`Verify()`
  extension-method-name collision cleanly, with no new engine mechanism.
- Bad, because it's a second bridge type and a second per-member extension
  class per interface — real, bounded generated-code cost, accepted per
  Negative Consequences above.

### Verification — `CallCount` on `ReturnConfigBuilder<T>`

- Good, because it needs no second bridge type at all — reuses `Configure()`
  entirely.
- Bad, because it reads awkwardly against ordinary assertion syntax
  (`Assert.Equal(1, mediator.Configure().Send().CallCount)` mixes
  "configure" vocabulary into what's conceptually a read/assert
  operation) and doesn't generalize cleanly to `Exactly(n)`'s own
  throw-with-message ergonomics without inventing assertion-framework-
  specific behavior in a core-`Compono` type.

## Amendment 1 (2026-08-14): overloaded generic methods, and a correction to overload-partial-support's return-shape claim

Requester review of this ADR (before any implementation code existed) asked
for the combined overload+generic case to be explicitly resolved — an
interaction Requirements 1 and 2's Decision Outcome text above designed
independently, and which can genuinely conflict:

```csharp
void Process<T>(T value);
void Process<T>(IEnumerable<T> values);
```

Requirement 1 needs each overload's configuration extension to carry a
discriminator parameter matching the real overload's parameter type.
Requirement 2 decided a supported generic method's configuration extension
stays non-generic, because the slot's type is fixed regardless of the
method's own type parameter. Here, the discriminator parameter types
(`T`, `IEnumerable<T>`) reference `Process`'s own type parameter — which a
non-generic extension has no way to spell. The original Decision Outcome
text above is left exactly as written, per `design-decisions.md`'s
immutability rule — this Amendment resolves the interaction those two
sections didn't individually anticipate, without changing either one's
own decision.

**Decided: the configuration (and verification) extension for an
overloaded generic member becomes generic itself, reusing the overload's
own type parameters and constraint clauses verbatim — purely as a
compile-time overload-selection mechanism. The backing slot's type stays
fixed per Requirement 2's existing rule (return type independent of the
method's own type parameter), so every closed `T` still shares exactly
one slot per overload — no per-closed-generic storage is introduced.**
This is not a third mechanism: it is Requirement 1's per-overload
discriminator rule and Requirement 2's generic-constraint-propagation rule
composed together, each exactly as already decided, applied to the same
member at once.

```csharp
internal sealed class IWidget_a1b2c3d4_Double : IWidget
{
    internal global::Compono.ReturnConfig<global::Compono.Unit> __process_9f8e; // Process<T>(T value)
    internal global::Compono.ReturnConfig<global::Compono.Unit> __process_2c1d; // Process<T>(IEnumerable<T> values)

    void IWidget.Process<T>(T value)
    {
        global::System.Threading.Interlocked.Increment(ref __process_9f8e.CallCount);
        if (__process_9f8e.HasConfiguredException) throw __process_9f8e.ConfiguredException;
    }

    void IWidget.Process<T>(global::System.Collections.Generic.IEnumerable<T> values)
    {
        global::System.Threading.Interlocked.Increment(ref __process_2c1d.CallCount);
        if (__process_2c1d.HasConfiguredException) throw __process_2c1d.ConfiguredException;
    }
}

internal static class IWidget_a1b2c3d4_DoubleConfiguration
{
    // Generic purely for overload selection - T is never stored anywhere, matching
    // Requirement 2's "slot type independent of the method's own type parameter" rule
    // exactly; T is inferred from the discriminator argument the same way it would be
    // inferred at a real call site against the actual interface member.
    public static global::Compono.ReturnConfigBuilder<global::Compono.Unit> Process<T>(
        this global::IWidget_a1b2c3d4_Double self, T value) => new(ref self.__process_9f8e);

    public static global::Compono.ReturnConfigBuilder<global::Compono.Unit> Process<T>(
        this global::IWidget_a1b2c3d4_Double self, global::System.Collections.Generic.IEnumerable<T> values) =>
        new(ref self.__process_2c1d);
}
```

```csharp
widget.Configure().Process(0).Throws(new InvalidOperationException());        // T inferred int -> T-value overload
widget.Configure().Process(Array.Empty<string>()).Returns(default);           // T inferred string -> IEnumerable<T> overload
widget.Configure().Process<string>(someIEnumerableOfString).Returns(default); // explicit type argument, same as a real call site
```

**Why this stays correct without new machinery:** because the
discriminator's parameter type (and constraints) are copied verbatim from
the real overload — exactly Requirement 1's existing rule — C#'s own
overload-resolution "betterness" rules pick between `Process<T>(T)` and
`Process<T>(IEnumerable<T>)` identically to how they'd resolve a real call
to the interface member with the same argument. The discriminator can
never disagree with which real overload a consumer is actually exercising,
by construction — not a property this Amendment has to separately prove
correct case by case. No new AOT-risk surface either: the extension is an
ordinary static generic method, the same shape as any BCL generic
extension method; the slot type stays concrete.

**Scope boundary — both requirements' existing constraints still apply
independently, per overload, unchanged:**

- An overload whose return type *does* depend on its own type parameter
  (`T Get<T>(T seed)`) stays diagnosed and unsupported under Requirement
  2's existing rule, checked per-overload — it does not block a sibling
  overload of the same name that satisfies Requirement 2
  (`void Reset<T>(T value)`, say), per Requirement 1's own overload-set-
  internal partial support.
- This combination is only reachable when the member name is actually
  overloaded (≥2 members sharing the name); a solo generic method keeps
  Requirement 2's original zero-argument, non-generic configuration
  extension unchanged — the generic discriminator extension described
  here only appears when overload disambiguation genuinely needs it.
- No new diagnostic category is introduced for the combined case itself —
  a discriminator signature collision between two overloads can't arise
  for shapes the C# language itself would already accept as distinct
  overloads (identical erased parameter-type sequences aren't legal
  overloads to begin with), so nothing here needed inventing a new
  rejection rule.

**Correction — "return type with no deterministic default" does not
belong in the overload-partial-support list.** Requirement 1's Decision
Outcome text lists "a return type with no deterministic default" alongside
`ref`/`out`/`in` and pointer parameters as shapes that "get a
deterministic-default dispatch body" when only one overload among several
has the unsupported shape. That's correct for `ref`/`out`/`in` and pointer
parameters (the body can simply not touch the parameter meaningfully,
or — for `out` — assign it a default, and still return the member's own
deterministic default) but **wrong** for a return type with no
deterministic default: by definition, there is no value to construct a
body around at all — the same reason ADR-0043 Amendment 5 Finding K
decided **diagnose and reject** for this shape in the first place. Caught
while working through this Amendment's own analysis, not a new finding
about overloads specifically — it was always true, just mis-stated.
**Corrected:** overload-set-internal partial support applies only to
shapes where a trivial fallback body is actually constructible
(`ref`/`out`/`in`, pointer/function-pointer parameters). An overload
returning a type with no deterministic default has no constructible body
at any granularity and triggers today's existing whole-interface
rejection outcome, unchanged from v1 — not a new decision, a correction to
this ADR's own mis-statement.

PLAN-0044 is updated in the same pass as this Amendment: a new Phase 1
task covering the combined overload+generic interaction with its own
`Verify()`-snapshot test (so Phases 0 and 1 don't each pass independently
while their combination produces invalid generated code), and Phase 0's
overload-partial-support task text corrected to match the fix above.

## Amendment 2 (2026-08-14): cross-assembly counter mutation, explicit-implementation constraint redeclaration, generic-arity overload identity, `Verify()` collision diagnostic

A Codex review pass against this ADR's original push (before any
implementation code existed) caught four real defects — two P1, two P2.
All prior text (the original Decision Outcome and Amendment 1) is left
exactly as written, per `design-decisions.md`'s immutability rule; this
Amendment corrects the affected sketches only.

**Finding 1 (P1) — `CallCount` is unwritable from generated dispatch
code.** Requirement 3's sketch adds `internal int CallCount` to
`ReturnConfig<T>` (core `Compono`) and has generated dispatch code (the
consumer's own assembly) call `Interlocked.Increment(ref __send.CallCount)`
directly. `internal` doesn't cross assembly boundaries — the exact same
class of defect ADR-0043 Amendment 3 Finding A already found and fixed
for `Value`/`Exception`, and Amendment 8 Finding S found again for a
property setter that reached for the internal fields directly instead of
routing through the public surface. This sketch made the identical
mistake for the call counter.

**Corrected:** `CallCount` stays `internal`, but `ReturnConfig<T>` itself
gains a `public` instance method to mutate it — an instance method
declared *inside* `ReturnConfig<T>` has ordinary access to its own type's
`internal` members regardless of which assembly calls it, the same
"public write surface over private state" shape `ReturnConfigBuilder<T>`
already established, just as a method on the struct itself instead of a
separate builder type (no `ref`-struct indirection needed here, since the
call site already holds the field by reference through ordinary struct
field access):

```csharp
public struct ReturnConfig<T>
{
    internal bool HasValue;
    internal T? Value;
    internal Exception? Exception;
    internal int CallCount;

    public readonly int ConfiguredCallCount => CallCount;

    /// <summary>Thread-safe call-count increment, callable from generated dispatch code in any assembly.</summary>
    public void RecordCall() => global::System.Threading.Interlocked.Increment(ref CallCount);

    // ... existing HasConfiguredValue/HasConfiguredException/ConfiguredValue/ConfiguredException unchanged
}
```

```csharp
Task IMediator.Send(Request request)
{
    __send.RecordCall();
    return __send.HasConfiguredException ? throw __send.ConfiguredException
        : __send.HasConfiguredValue ? __send.ConfiguredValue
        : global::System.Threading.Tasks.Task.CompletedTask;
}
```

**Finding 2 (P1) — an explicit interface implementation cannot redeclare
inherited generic constraints.** Requirement 2's `BeginScope` sample
writes `where TState : notnull` directly on the explicit interface
implementation. C# constraints for an explicit interface implementation
are always inherited automatically from the interface declaration and
**cannot** be restated — doing so is `CS0460`. The original sample does
not compile.

**Corrected:** the explicit interface implementation never emits a
`where` clause, for any member, under any of this ADR's shapes — the
constraint still applies (inherited, enforced by the compiler and CLR
exactly as if written), it's simply never spelled out in generated source:

```csharp
IDisposable? ILogger.BeginScope<TState>(TState state) =>
    __beginScope.HasConfiguredException ? throw __beginScope.ConfiguredException
    : __beginScope.HasConfiguredValue ? __beginScope.ConfiguredValue
    : default;
```

This affects only the explicit interface implementation. It does **not**
affect Requirement 2's own non-generic configuration extension (no type
parameter, nothing to constrain) or Amendment 1's overloaded-generic
configuration extension (`Process<T>(this ..., T value)`) — that extension
is an ordinary, standalone generic method, not an interface
implementation, so it both *can* and *must* declare its own `where`
clauses explicitly to stay type-safe; Amendment 1's own sample never
happened to show a constrained example, so it wasn't wrong, just
untested against this exact question. **Rule, stated once for
implementation:** copy constraint clauses verbatim onto a generated
generic *extension* method; never emit them on a generated explicit
interface implementation, generic or not.

**Finding 3 (P2) — overload identity must include generic arity, not
just parameter types.** C# permits overloading purely by generic arity —
`int M()` and `int M<T>()` are distinct, legal overloads even though both
have an empty parameter list. Both are independently supported under this
ADR (`M<T>()`'s return doesn't depend on `T`), but hashing discriminator
identity from parameter types alone (Requirement 1's original scheme)
gives them the same empty-list identity — colliding backing fields and
colliding zero-argument configuration extensions.

**Corrected:** the discriminator hash's input is the member's full
signature shape — parameter types **and** generic arity (type-parameter
count) — not parameter types alone. No new call-site ambiguity results
once identity is fixed: `Configure().M()` (no explicit type argument) is
only ever a candidate call for the non-generic `M()` — `M<T>()` has
nothing to infer `T` from and requires an explicit type argument
(`Configure().M<int>()`) at any call site, real or generated, exactly
mirroring how a caller would have to invoke the real interface member
itself. No new diagnostic is needed for this shape once the identity
scheme is corrected — the ambiguity Codex flagged was a generator-internal
naming collision, not a real consumer-facing one.

**Finding 4 (P2) — no collision diagnostic exists for an interface that
declares its own `Verify` member.** ADR-0043 Amendment 3 Finding E
diagnoses an interface whose own member would shadow the generated
`Configure()` bridge (instance-member lookup always wins over extension
resolution). Requirement 3 introduces a second bridge, `Verify()`, with
the identical shadowing exposure, but no symmetric check was added.

**Corrected:** the existing `Configure`-collision check generalizes to a
small reserved-name set (`Configure`, `Verify`) using the exact same
zero-argument-applicability logic already established (Amendment 3
Finding E, refined by PR #83 review round 2's arity/applicability fix) —
not a new mechanism, not a new diagnostic code, the existing one's scope
widens to cover both bridge names.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's
identity-hash task now includes generic arity in the hash input from the
start (even though it has no observable effect until Phase 1 ships any
generic-method support) specifically so Phase 1 never has to change an
already-shipped Phase-0 naming/hint-name scheme out from under early
adopters; Phase 1's constraint-propagation task is scoped to generated
generic *extension* methods only, explicit interface implementations
never receive constraint clauses; Phase 2 gains the `RecordCall()`
bridge method task in place of a raw field increment, and a task for the
generalized `Configure`/`Verify` collision check.

## Amendment 3 (2026-08-14): verifier read-path and constructor-arity fixes, ref-kind in overload identity, closure-wide collision detection preserved

A fresh Codex review pass against Amendment 2's own fixes caught four more
real defects — two P1, two P2, three of them in code Amendment 2 itself
introduced or left untouched. All prior text is left exactly as written,
per `design-decisions.md`'s immutability rule; this Amendment corrects the
affected sketches only.

**Finding 5 (P1) — the `Verify()` extension reads `CallCount` directly,
the same cross-assembly defect Amendment 2 only fixed for the write
side.** Amendment 2's `RecordCall()` fix repaired generated *dispatch*
code's write access to the counter, but Requirement 3's own `Verify()`
extension sample — untouched by that Amendment — still reads
`self.Instance.__send.CallCount` directly, the `internal` field, from
generated code in the consumer assembly. Same defect class, different
call site, missed because Amendment 2 was scoped to the write path that
prompted it.

**Finding 6 (P1) — the generated `Verify()` extension supplies the wrong
number of constructor arguments.** `CallVerifier`'s decided shape takes
`(int observedCount, string memberDescription)`, but the sample calls
`new(self.Instance.__send.CallCount)` with one argument — every generated
verification member as sketched fails to compile, independent of Finding
5's accessibility problem.

**Corrected together** (both defects are on the same generated line):

```csharp
internal static class IMediator_a1b2c3d4_DoubleVerification
{
    public static global::Compono.CallVerifier Send(this global::IMediator_a1b2c3d4_DoubleVerifier self) =>
        new(self.Instance.__send.ConfiguredCallCount, "IMediator.Send");
}
```

The member-description string is a compile-time literal the generator
already has everything it needs to produce (the declaring interface's
display name plus the member's own name, the same text already used in
this feature's various cast-failure exception messages) — no new data
flows into the emitter to support it.

**Finding 7 (P2) — parameter ref-kind is missing from overload identity,
the same gap class as Amendment 2 Finding 3's generic-arity fix.**
`void M(int value)` and `void M(ref int value)` are a legal C# overload
pair with identical parameter *types* and identical generic arity (zero,
for both) — Amendment 2's corrected hash still collapses them to the same
identity. This matters even though the `ref` overload never gets a
`Configure()` extension (Requirement 1's existing per-parameter
`ref`/`out`/`in` exclusion still applies) — PLAN-0044's own Phase 0 task
commits to emitting a `ReturnConfig<T>` field for *every* overload
uniformly, supported or not, so both overloads still need distinct field
identities even though only one gets a configuration surface.

**Corrected:** the discriminator hash's input is the member's full
signature shape — parameter types, each parameter's `RefKind`, **and**
generic arity (Amendment 2 Finding 3) — stated together here as the
complete, final input, so no fourth axis gets discovered piecemeal later.

**Finding 8 (P2) — the rewritten duplicate-name check's own justification
overclaims, and risks weakening real, already-covered collision
detection.** Requirement 1's Decision Outcome text describes the new
per-overload check as flagging "a genuine ambiguity (identical
parameter-type signature, which C# itself can't produce, so effectively
unreachable)." That parenthetical is wrong: identical signatures **are**
reachable — not within one interface's own declared overload set (where
the compiler does enforce uniqueness), but **across the transitive
base-interface closure**, when two unrelated base interfaces each
independently declare a same-named, same-shaped member (a diamond).
`test/Compono.Generators.Tests`' existing `TestDoubleVerifyTests.DiamondInheritedSameNameProperty_ReportsOverloadedDiagnostic`
already covers exactly this scenario for properties — real, not
hypothetical, and the same shape is equally possible for methods, not
just properties.

**Decided: the underlying mechanism was always correct, only the prose
justifying it was wrong.** Grouping by full signature identity
(name + parameter types + ref-kinds + generic arity, per Finding 7 above)
**across the whole closure** (not per declaring interface — the existing
`duplicateConfigurationMemberNames` pre-pass already iterates
`closure.SelectMany(i => i.GetMembers())`, and the corrected identity
scheme must keep doing so) still correctly flags a diamond collision: two
distinct symbols from different declaring interfaces that happen to
produce the same discriminator identity fail this check exactly like two
literal duplicate declarations would, while two genuinely different
real overloads within one interface never share a full-signature identity
to begin with. Nothing about Requirement 1's actual mechanism needed to
change — only the mis-stated "effectively unreachable" characterization,
corrected here so an implementer doesn't read it as license to weaken or
skip closure-wide grouping.

**One real, intentional policy change, distinct from the above
correction:** under v1, this collision (`CMP0022`) rejected the *whole
interface*. Under Requirement 1's already-decided overload-set-internal
partial support, a collision now costs `Configure()`/`Verify()` surface
only for the specific colliding identity — both colliding members still
get explicit-interface-implementation dispatch bodies (qualified against
their own declaring interface, per ADR-0043 Amendment 11 Finding Z, no
new mechanism), and the rest of the interface generates normally. This is
a real improvement over v1's blanket rejection for this scenario, not a
regression — but it does mean `DiamondInheritedSameNameProperty_ReportsOverloadedDiagnostic`'s
existing assertion (the whole interface falls back) needs updating during
Phase 0 implementation to match the new scoped outcome; noted as a task
correction below, not a new design question.

PLAN-0044 is updated in the same pass as this Amendment: Phase 2's
`Verify()`-extension task now specifies the corrected `ConfiguredCallCount`
read plus the two-argument `CallVerifier` construction; Phase 0's identity-
hash task folds in parameter ref-kind alongside generic arity; Phase 0's
duplicate-name task is corrected to state the check runs across the full
closure (not per-interface) and must keep passing the existing diamond
property test (adapted to the new scoped-not-whole-interface outcome), not
treat cross-interface collisions as unreachable.

## Amendment 4 (2026-08-14): a backing field exists only where a configuration surface does, and a stale plan-text sync fix

A third Codex review pass, against PLAN-0044's own task text rather than
this ADR's, caught one real design gap this ADR left unstated, and one
place PLAN-0044 failed to propagate an already-decided fix everywhere it
applied. All prior text is left exactly as written, per
`design-decisions.md`'s immutability rule; this Amendment fills the gap
and records the correction.

**Finding 9 (P1) — this ADR never decided whether an overload without a
`Configure()`/`Verify()` surface still gets a backing `ReturnConfig<T>`
field.** Requirement 1's "overload-set-internal partial support" says an
unsupported overload "gets a deterministic-default dispatch body" but
never says whether that body is backed by a field. PLAN-0044's own task
text filled the gap on its own, unreviewed, with "emit one `ReturnConfig<T>`
field ... per overload" — unconditionally. Amendment 3 Finding 8 then
established that a diamond-inherited collision withholds the
`Configure()`/`Verify()` surface for **both** colliding members without
rejecting the interface — but if each of those two members still gets its
own field under the plan's unconditional reading, both fields resolve to
the *same* discriminator identity (same name, same signature — that's
the definition of the collision) and the generated class declares the
same field name twice: `CS0102`, uncompilable code, for a scenario this
ADR's own worked example (the diamond property test) is supposed to keep
generating successfully.

**Decided: a backing field exists if and only if the member also gets a
`Configure()` extension.** A member with no configuration surface —
whether because its own shape is unsupported (`ref`/`out`/`in`, pointer)
or because it collides with another member's identity (diamond) — needs
no state at all: its dispatch body evaluates the plain deterministic-
default expression inline (the same expression `TestDoubleDefaults`
already computes for any unconfigured member), with no field, no
`RecordCall()`, nothing to collide on. This isn't a new mechanism; it's
narrowing an unstated assumption (every overload gets a field,
regardless of whether anything could ever configure or observe it) down
to what the architecture actually needs a field *for*. It also resolves
Finding 9 by construction rather than by adding a "shared slot" special
case: two colliding diamond members simply both end up in the
no-field, inline-default bucket, exactly like an unsupported-shape
overload already does — no duplicate declaration is possible because
neither declares anything.

**Finding 10 (documentation-sync, not a new decision) — PLAN-0044's Phase
1 emitter task still said "type parameters + constraints" for the
explicit interface implementation**, two tasks below the correctly-worded
constraint-propagation task Amendment 2 Finding 2 already fixed. Amendment
2 decided the rule; this specific task bullet was simply never updated to
match it, leaving a live contradiction pointing an implementer straight
at the `CS0460` this ADR already ruled out. No new design content — a
plan-text correction, recorded here only because it's the kind of leftover
inconsistency worth naming explicitly so it doesn't get treated as a
second, competing decision during implementation.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's field-
emission task is now conditioned on the member actually getting a
`Configure()` extension, and Phase 1's emitter task drops "constraints"
from the explicit-implementation half of its own sentence.

## Amendment 5 (2026-08-14): canonical generic-parameter identity, pointer parameters removed from the fallback-body bucket, conditional `RecordCall()`

A fourth Codex review pass caught three more real defects, all P1, two
against this ADR's own original Decision Outcome text and one a leftover
plan-text contradiction from Amendment 4's own fix. All prior text is
left exactly as written, per `design-decisions.md`'s immutability rule;
this Amendment corrects the affected sketches only.

**Finding 11 — inherited generic overloads with differently-named type
parameters would evade diamond-collision detection.** `IA.M<T>(T)` and
`IB.M<U>(U)` are the same signature under C#'s own rules (type-parameter
*names* aren't part of a method's identity, only their ordinal position
is), but the discriminator hash described so far serializes each
parameter's *displayed* type — which renders a type-parameter reference
using its own declared name (`"T"` vs `"U"`). Two structurally identical
inherited generic overloads would hash differently, missing the diamond
collision Amendment 3 Finding 8 already established the check must catch,
and the generator would emit two configuration extensions with genuinely
identical real signatures — `CS0111`, a duplicate-declaration compile
error, not a diagnosed fallback.

**Corrected:** before hashing, every reference to one of the *method's
own* type parameters is replaced with a position-based canonical token
(the type parameter's ordinal index among the method's own type
parameters — the same "name doesn't matter, position does" identity rule
the CLR's own metadata encoding already uses for method-level generics),
not its declared name. `IA.M<T>(T)` and `IB.M<U>(U)` both canonicalize to
the same identity (`M`, one type parameter, parameter list `[!0]`),
correctly triggering the diamond check.

**Finding 12 — pointer/function-pointer *parameter* shapes cannot
actually get a fallback body without emitting `unsafe`, which this
feature has never decided to do.** The original Decision Outcome text
above lists "a pointer parameter" alongside `ref`/`out`/`in` as shapes
with a "constructible fallback body." That's wrong: any method whose
signature contains a pointer or function-pointer-typed parameter must
itself be declared `unsafe` — a C# requirement regardless of whether the
body ever touches the parameter — and this feature has never emitted
`unsafe` generated code, nor decided to require a consumer's project to
set `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (a real, consumer-
visible project-wide setting this feature has no business silently
requiring). This also directly contradicts already-`Accepted`,
already-shipped v1 behavior: [ADR-0043 Amendment 10 Finding Y](0043-compono-generated-test-doubles-design.md#amendment-10-2026-08-13-set-only-properties-diagnosed-parameter-names-escaped-unsafe-parameter-shapes-diagnosed)
already decided pointer/function-pointer parameters get **no** fallback
and defer to the ordinary runtime-provider path (whole-interface,
matching the return-side disposition) — this ADR's original text
introduced a new claim that silently reversed a shipped v1 decision
without saying so.

**Corrected: pointer/function-pointer parameter shapes are removed from
the fallback-body bucket entirely, restoring v1's existing disposition
unchanged.** Only `ref`/`out`/`in` parameters remain in the "constructible
fallback body" bucket — none of them require `unsafe`, a plain assignment
or an ignored-parameter body is always legal C#. An overload with a
pointer/function-pointer parameter has no constructible body under this
feature's "never emit `unsafe`" boundary (stated explicitly here for the
first time, though implicit in every prior design) and triggers today's
existing whole-interface rejection, the same bucket a non-nullable-no-
default return already occupies.

**Finding 13 (plan-text contradiction, not a new decision) —
Requirement 3's `RecordCall()` call is unconditional in PLAN-0044's own
task text, but Amendment 4 Finding 9 already decided a member with no
`Configure()` surface gets no backing field at all.** `__member.RecordCall()`
on a field that was never emitted doesn't compile — the same "an edit
wasn't propagated to every task bullet it touches" pattern as Amendment 4
Finding 10.

**Corrected:** `RecordCall()` is emitted only for a member/overload that
has a backing `ReturnConfig<T>` field — exactly the members that have a
`Configure()` extension, per Amendment 4's own rule. A member with no
configuration surface has no `Verify()` surface either (Amendment 1: it
reuses the same discriminator mechanism), so nothing could ever read a
count for it anyway — no observable capability is lost by not counting
calls to it.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's
identity-hash task canonicalizes generic-parameter references by ordinal
before hashing (defensively, same "get it right before Phase 1 makes it
observable" reasoning as Amendment 2/3's arity and ref-kind fixes);
Phase 0's overload-partial-support task drops pointer/function-pointer
parameters from the fallback-body bucket, restoring v1's existing
whole-interface-rejection disposition for that shape unchanged; Phase 2's
`RecordCall()` task is conditioned on the backing field's existence.

## Amendment 6 (2026-08-14): nullable annotations excluded from overload identity, unconstrained nullable type-parameter shapes diagnosed rather than guessed at

A fifth Codex review pass caught two more real defects in this ADR's own
identity/constraint reasoning, plus one process gap addressed in
PLAN-0044 only (see that plan's own Notes — a pure test-sequencing
concern has no ADR decision content per `design-decisions.md`'s "keep the
ADR itself to decision content" rule, so it's not repeated here). All
prior text is left exactly as written, per the immutability rule already
followed five times above.

**Finding 14 — nullable-reference annotation is not part of a C# method
signature and must be excluded from discriminator identity, same family
as Amendment 5 Finding 11's generic-parameter-name fix.** `IA.M(string)`
and `IB.M(string?)` can coexist across two unrelated base interfaces (a
diamond) because nullable annotations are compiler-tracked metadata, not
part of the CLR signature the compiler uses to detect duplicate
declarations. Hashing each parameter's *displayed* type (which includes
the `?`, per this feature's own `NullableAwareFullyQualifiedFormat`, used
deliberately for emitted *code* to avoid spurious warnings) would treat
these as different identities, missing the diamond collision and emitting
two configuration extensions the compiler considers genuinely identical —
`CS0111`.

**Corrected:** the discriminator hash strips nullable-reference annotations
(and any other decoration that isn't part of true signature identity) from
each parameter type before hashing — a third canonicalization step
alongside Amendment 2/3's ref-kind-and-arity fix and Amendment 5's
generic-parameter-ordinal fix. `NullableAwareFullyQualifiedFormat` stays
exactly as-is for *emitted code text* (nothing about how source is
generated changes) — this correction is scoped entirely to the hash
*input*, a separate concern from what gets printed into the `.g.cs` file.

**Finding 15 — an unconstrained generic type parameter used as `T?` in a
parameter can require a C# 9+ "default constraint"
(`where T : default`/permitted `class?`/`struct?` forms) on the explicit
implementation, an exception to the otherwise-blanket "never emit a
`where` clause" rule Amendment 2 Finding 2 established.** This is real —
C# added the `default` constraint specifically so an override or explicit
interface implementation can disambiguate an inherited, unconstrained
`T?`'s oblivious reference-or-value-type meaning, an exception folded
into the same constraint-inheritance family `CS0460` governs. It does
**not** affect this ADR's own motivating shape — the real
`ILogger<T>.Log<TState>` is unconstrained but its own parameter is plain
`TState`, never `TState?`, so the concrete evidence this ADR was built
from never exercises this corner.

**Decided: diagnose and exclude, not attempt to reproduce.** Correctly
modeling exactly when a `default`/`class?`/`struct?` constraint is
*required* (as opposed to merely permitted, or unnecessary because the
compiler can infer it) is real, deep C# nullable-feature surface this ADR
has no verified, checkable answer for — and getting it wrong risks
emitting generated code that fails to compile in exactly the silent,
narrow way this whole review has been catching one case at a time.
Consistent with this ADR's own established pattern for a genuinely
uncertain corner (the overload+generic interaction's own three-way
framing: clean representation, narrower subset, or a diagnostic) and with
`design-decisions.md`'s "no reflection/no invented complexity for a case
the evidence doesn't require" posture: a generic method with a parameter
(or, for symmetry, the method's own declaration) using `T?` on an
unconstrained type parameter is diagnosed and excluded, deferring to the
ordinary runtime-provider path — the same disposition every other
narrowly-scoped-out shape in this ADR already gets. Revisit only if real
evidence (a future dogfooding pass hitting this exact shape) justifies
the added complexity of modeling the constraint correctly.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's
identity-hash task adds nullable-annotation stripping as a third
canonicalization step; Phase 1's constraint-propagation task gains the
unconstrained-`T?`-parameter exclusion as a new diagnosed shape, distinct
from (and narrower than) the return-type-dependency exclusion already
established.

## Amendment 7 (2026-08-14): `dynamic`/`object` excluded from overload identity

A sixth Codex review pass caught one more real defect in this ADR's
identity-canonicalization reasoning (a design-content fix, recorded here)
and one plan-only sequencing bug (fixed in PLAN-0044 directly, per the
same content-separation judgment as Amendment 6's process finding — no
ADR content changes as a result, not repeated here). All prior text is
left exactly as written, per the immutability rule already followed six
times above.

**Finding 17 — `dynamic` and `object` are the same signature at the CLR
level, the same excluded-decoration family as Amendment 6 Finding 14's
nullable-annotation fix.** `dynamic` has no runtime representation of its
own — it erases to `System.Object` decorated with a compile-time-only
`DynamicAttribute`, exactly the same "compiler metadata, not real
signature" shape nullable annotations have. `IA.M(dynamic)` and
`IB.M(object)` can coexist across a diamond of two base interfaces for
the same reason `IA.M(string)`/`IB.M(string?)` can — but the discriminator
hash, as corrected through Amendment 6, still serializes `dynamic` and
`object` as different displayed types, missing the collision and risking
the same `CS0111` duplicate-extension failure.

**Corrected:** the discriminator hash's canonicalization step (already
stripping nullable annotations and generic-parameter names, per Amendment
5/6) also normalizes `dynamic` to `object` before hashing — a fourth, and
likely final, instance of the same underlying principle: any type
decoration that C#'s own compiler treats as non-signature-affecting must
be excluded from discriminator identity, not just the two instances
found so far. Implementation should treat this list (nullable annotation,
generic-parameter naming, `dynamic`/`object`) as illustrative of the
*principle*, not necessarily exhaustive — the identity scheme's job is
"match what the C# compiler considers the same signature," and Phase 0's
own test suite should include a case for each decoration found here
rather than assuming no fourth one exists.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's
identity-hash task gains the `dynamic`→`object` normalization step, and a
`Verify()`-test covering the inherited-diamond case (`IA.M(dynamic)`/
`IB.M(object)`) alongside the existing nullable and generic-parameter
diamond cases.

## Amendment 8 (2026-08-14): identity canonicalization generalized, constrained-nullable type parameters partially unblocked, `out` parameters definitely assigned in fallback bodies

A seventh Codex review pass caught a third consecutive instance of the
identity-canonicalization gap (Amendments 5-7's own pattern), plus two
new, unrelated defects. All prior text is left exactly as written, per
the immutability rule already followed seven times above.

**Finding 19 — tuple element names are the fourth non-signature-affecting
decoration found in three consecutive review rounds; the hash is
corrected once, structurally, instead of patched a fifth time.** Tuple
element names (`(int X, int Y)` vs `(int A, int B)`) are compiler
metadata (a `TupleElementNamesAttribute`), not part of a tuple's real
underlying `System.ValueTuple<...>` signature — the same
"compiler-tracked, not CLR-real" shape as Amendments 5, 6, and 7's
generic-parameter-name, nullable-annotation, and `dynamic` findings.
Patching a fifth ad hoc special case would very likely not be the last
one (`params` is a fourth real candidate: `void M(int[] a)` and
`void M(params int[] a)` are also the same signature, differing only in a
calling-convention attribute — not independently confirmed as a live
Codex finding, but the same shape, offered here as evidence the pattern
isn't closed).

**Decided: the discriminator hash's canonicalization step is redefined as
one recursive type-transform, not an enumerated list of exceptions.**
Before hashing, every parameter type is walked — through generic type
arguments, array element types, and tuple element types, at every nesting
level, not just the top level — applying: strip nullable-reference
annotation; replace `dynamic` with `object`; replace a named tuple with
its underlying `ValueTuple<...>` form; replace a reference to the
member's own type parameter with its ordinal-position token (Amendment
5's fix, restated as one case of this same principle). This closes the
whole class at once — `IEnumerable<(int X, int Y)>` vs
`IEnumerable<(int A, int B)>`, or `List<string?>` vs `List<string>`
nested inside a larger generic parameter type, are now covered by the
same recursive pass rather than needing their own future Amendment.
**Explicitly stated as an open principle, not a closed enumeration:** the
rule is "exclude anything the C# compiler itself doesn't treat as
signature-affecting," and implementation should treat every case found so
far (generic-parameter naming, nullable annotation, `dynamic`, tuple
names) as illustrative, testing each with its own diamond-collision case,
rather than assuming a fifth can't exist.

**Finding 18 — a *constrained* nullable type parameter (`M<T>(T? value)
where T : class` or `where T : struct`) needs its constraint restated on
the explicit implementation, unlike the fully unconstrained case Amendment
6 Finding 15 already excludes.** C#'s narrow exception to `CS0460`
specifically permits (and here, requires) restating exactly one of the
`class`/`struct`/`notnull`/`unmanaged` keyword-only constraints — never a
base-type or interface constraint — when a type parameter's own `T?`
usage needs disambiguating and the interface's own declaration already
constrains it to one of those forms.

**Decided: unblock this narrower, mechanically verifiable case; the fully
unconstrained case (Amendment 6 Finding 15) stays excluded.** Unlike
`where T : default` for the unconstrained case — genuinely deep surface
this ADR still has no confidently-verified answer for — restating a
`class`/`struct`/`notnull`/`unmanaged` keyword the interface *already
declares*, verbatim and alone (no other constraint ever gets restated,
matching every other rule in this ADR), is a small, mechanical, low-risk
operation directly grounded in a documented C# 9+ feature. **Corrected
rule:** the explicit interface implementation emits no `where` clause at
all *except* this one narrow case — when the interface's own declared
constraint for a type parameter is exactly `class`, `struct`, `notnull`,
or `unmanaged`, and that type parameter appears as `T?` anywhere in the
member's signature, restate that single keyword on the explicit
implementation. Every other constrained-generic-method shape (including
still-unconstrained `T?`) keeps Amendment 6 Finding 15's existing
diagnose-and-exclude disposition, unchanged.

**Finding 20 — a fallback dispatch body for an `out`-parameter overload
never assigns the `out` parameter, which C# requires on every return
path (`CS0177`).** Requirement 1's overload-partial-support text
describes `ref`/`out`/`in` parameters as getting "a deterministic-default
dispatch body" without specifying what that body actually does with an
`out` parameter — a real, plain gap (not a subtle corner case; `out`
parameters have always required definite assignment).

**Corrected:** for an `out` parameter, the fallback body assigns it
`TestDoubleDefaults`'s own deterministic-default expression for that
parameter's type — the same lookup already used for return types, not new
logic. **If that lookup fails for even one `out` parameter** (the
parameter's own type has no deterministic default, the identical
condition that already excludes a return type from the fallback bucket),
**the whole overload has no constructible body**, joining the existing
"return type with no deterministic default" bucket and triggering the
same whole-interface-rejection disposition — not a silent `default`
assignment that could violate the parameter's own non-nullable contract.
`ref`/`in` parameters need no such handling — they're never required to
be written, so their own types never gate fallback-body constructibility.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's
identity-hash task is rewritten around the recursive canonicalization
principle (with a tuple-name diamond test) rather than listing a fifth
special case; Phase 1's constraint-propagation task gains the narrow
constrained-nullable-type-parameter exception; Phase 0's overload-partial-
support task gains the `out`-parameter definite-assignment requirement and
its own no-deterministic-default exclusion, plus a packaged-smoke-test
case for a mixed overload set containing a non-default-assignable `out`
parameter.

## Amendment 9 (2026-08-14): withdraw the constrained-nullable-constraint exception; corrected overload-selection example

An eighth Codex review pass caught a real problem with Amendment 8's own
Finding 18 fix, a direct consequence of that same problem in the plan,
and a genuine correctness error in Amendment 1's own worked example. All
prior text is left exactly as written, per the immutability rule already
followed eight times above — this Amendment **withdraws** part of
Amendment 8's fix rather than further narrowing it, which the rule
permits (a later Amendment correcting an earlier one, not editing it).

**Finding 21 — the exact set of constraint keywords permitted on an
explicit interface implementation for nullable disambiguation is genuinely
uncertain, and Amendment 8's `class`/`struct`/`notnull`/`unmanaged` list
is very likely wrong.** Codex's own finding here disputes part of what
Codex itself suggested in the finding Amendment 8 fixed: `class` and
`struct` may be legitimately restatable for this purpose, but `notnull`
and `unmanaged` are not part of the same permitted set. Cross-checking
this against Amendment 6 Finding 15's own reasoning (`where T : default`
for the unconstrained case) exposes the real problem: this is deep,
easily-misremembered C# nullable-generics surface, and Amendment 8 made
exactly the mistake Amendment 6 explicitly declined to make for the
unconstrained case — guessing at specific constraint syntax without a
verified answer. Two rounds of review disagreeing with each other about
the precise permitted keyword set is itself strong evidence that guessing
further (e.g. trimming the list to just `class`/`struct`) risks a third
wrong guess rather than a correct one.

**Decided: withdraw Amendment 8 Finding 18 entirely, not narrow it.**
Every type parameter used as `T?` in a generic method's own signature —
constrained or unconstrained, regardless of which constraint — is
diagnosed and excluded, unifying with Amendment 6 Finding 15's existing
disposition for the unconstrained case rather than carving out a
special-cased exception this ADR cannot verify compiles. This costs a
small amount of additional excluded surface (a constrained `T?` method
that might genuinely be supportable with the exact right syntax) in
exchange for never emitting generated code on an unverified guess — the
same trade this ADR already made once, deliberately, for the harder case.
Revisit only with a verified answer (a real compiler check during
implementation, or authoritative confirmation of the exact permitted
form), not a third review-round guess.

**Finding 22 (moot as a direct consequence of Finding 21, not a separate
fix) — a Codex finding that PLAN-0044's emitter task never synchronized
Amendment 8's exception.** Withdrawing the exception (Finding 21, above)
removes what that task would have needed to synchronize in the first
place — there is no longer any constraint-restatement exception for the
explicit interface implementation to carry forward. PLAN-0044's emitter
task text is corrected to match the withdrawal, not to add the sync fix
Finding 22 originally asked for.

**Finding 23 — Requirement 1/Amendment 1's own worked example incorrectly
claims implicit type inference selects the `IEnumerable<T>` overload.**
The example (`Configure().Process(Array.Empty<string>()).Returns(default)`,
commented "T inferred string -> IEnumerable<T> overload") is wrong: C#
overload-resolution betterness prefers an **identity conversion** over an
**implicit reference conversion** — `Array.Empty<string>()` (type
`string[]`) has an identity conversion to the unconstrained `T value`
parameter (`T = string[]`) but only a reference conversion to
`IEnumerable<T> values` (`T = string`), so the `T`-value overload wins,
not the enumerable one. This isn't a one-off bad example choice: an
unconstrained `T` parameter's identity conversion is available for
**every** possible argument, so implicit inference can **never** select
the `IEnumerable<T>` overload over a sibling `T`-value overload for this
shape — a real, structural fact about this specific overload pair,
already true of the real interface member itself (Compono's discriminator
selection isn't introducing a new ambiguity, only inheriting an existing
one), not previously stated plainly.

**Corrected example:**

```csharp
widget.Configure().Process(0).Throws(new InvalidOperationException());        // T inferred int -> only Process<T>(T) is applicable
widget.Configure().Process<string>(Array.Empty<string>()).Returns(default);   // explicit <string> required - implicit inference can
                                                                               // never select Process<T>(IEnumerable<T>) here: the
                                                                               // T-value overload's identity conversion always wins
                                                                               // when both are applicable, for any argument
```

The third line from the original sample (explicit type argument on a
placeholder variable) already demonstrated the correct form — it's kept,
now consistent with the corrected second line rather than contradicting
it. No design/mechanism content changes: the discriminator selection
mechanism (Requirement 1) was always correct; only this illustrative
example and its accompanying claim were wrong.

PLAN-0044 is updated in the same pass as this Amendment: Phase 1's
diagnostic task and constraint-propagation task both drop the withdrawn
constrained-nullable exception, reverting to "diagnose and exclude any
`T?`-using type parameter, constrained or not."

## Amendment 10 (2026-08-14): native-integer aliases normalized, confirming Amendment 8's identity principle rather than reopening it

A ninth Codex review pass found `nint`/`nuint` (C# 9+ native-sized integer
types, compiler sugar for `System.IntPtr`/`System.UIntPtr` at the CLR
level — the same "source-level spelling, same real signature" shape as
every case Amendment 8 already generalized around) missing from the
discriminator hash's canonicalization transform. All prior text is left
exactly as written, per the immutability rule already followed nine times
above.

This is not a new gap in the *principle* Amendment 8 established — it's
exactly the kind of case that Amendment already said not to assume
couldn't exist ("test for each found so far, but don't assume a fifth
can't exist"). **Corrected:** the recursive canonicalization transform
gains a fifth concrete step — normalize `nint`/`nuint` to
`System.IntPtr`/`System.UIntPtr` — alongside nullable-annotation
stripping, `dynamic`→`object`, tuple-name stripping, and generic-parameter
ordinal canonicalization. PLAN-0044 is updated in the same pass: Phase
0's identity-hash task gains this fifth step and its own diamond test
(`IA.M(nint)`/`IB.M(System.IntPtr)`).

## Amendment 11 (2026-08-14): `object`-member collision re-evaluated against the generated discriminator signature, not just the member's name

A tenth Codex review pass caught a real over-broadening gap in v1's
existing `object`-member collision check, now that Requirement 1 gives
overloaded members typed (non-zero-argument) discriminator extensions.
All prior text is left exactly as written, per the immutability rule
already followed ten times above.

**Finding — v1's blanket "`ToString`/`GetHashCode`/`GetType` always
collides" check no longer holds once an overload's discriminator
extension takes real parameters.** ADR-0043 Amendment 6 Finding N
decided this check compares the *generated* extension's signature against
`object`'s own members, correctly noting the generated extension was
*always* zero-argument in v1 (argument-independent configuration, no
overloads existed yet) — so checking by name alone was equivalent to
checking by signature. Requirement 1 breaks that equivalence: an
overloaded member like `string ToString(int format)` now generates a
typed, one-argument discriminator extension (`ToString(this <Double>
self, int format)`), which is **not applicable** to a zero-argument call
and therefore does not collide with `object.ToString()` at all — but the
existing `TestDoubleAnalyzer` branch still rejects it unconditionally by
name, needlessly falling this otherwise-fully-supportable overload (and,
per Requirement 1's whole-interface-vs-overload-scoped design, potentially
its whole interface) back to the runtime-provider path.

**Verified against this repo's own established, compile-spike-confirmed
precedent, not assumed:** `ADR-0043`'s own implementation history (PR #83
review round 2, recorded in PLAN-0043) already proved directly — via a
real compile spike, not inference from the C# spec — that "C# only falls
back to extension-method resolution when ordinary member lookup finds no
*applicable* candidate, not merely 'no candidate with this name'": an
interface's own `Configure(int mode)` does not shadow a zero-argument
`Configure()` extension, because the one-argument real member isn't
applicable to a zero-argument call. The same rule, applied in the other
direction, settles this finding: `object.ToString()` (zero-argument) does
not shadow a one-argument generated `ToString(int)` extension, because
the *zero-argument real member* isn't applicable to a call this specific
overload's own discriminator extension expects.

**Corrected:** the `object`-member collision check evaluates the
*generated discriminator extension's own applicability to a zero-argument
call* — reusing the same `IsApplicableToZeroArguments`-shaped logic the
`Configure`-collision check already applies (every parameter optional, or
a trailing `params`) — rather than checking the member's bare name. A
non-overloaded member (still always a zero-argument extension, Amendment
2 Finding 4's argument-independence rule unchanged) keeps today's exact
v1 behavior — always collides when named `ToString`/`GetHashCode`/
`GetType`, no regression there. An overloaded member whose discriminator
extension takes required, non-optional parameters is **not** applicable
to a zero-argument call and therefore does **not** collide — genuinely
widening supported surface for exactly the shape this ADR's own Requirement
1 introduced, not merely fixing a defect.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's
object-collision handling (already an existing `TestDoubleAnalyzer`
check, not previously called out as its own plan task) gains an explicit
task reusing the applicability-check pattern, with a `ToString(int
format)`-shaped overload test proving the corrected, narrower collision
scope.

## Amendment 12 (2026-08-14): `object`-collision withheld only for a truly zero-parameter discriminator; stale phase cross-references corrected

An eleventh Codex review pass caught a real over-narrowing in Amendment
11's own fix, plus two stale cross-references to earlier plan structure.
All prior text is left exactly as written, per the immutability rule
already followed eleven times above.

**Finding — Amendment 11's `IsApplicableToZeroArguments` reuse
over-withholds for a `params`/all-optional-parameter overload.** A member
like `ToString(params object[] values)` generates a discriminator
extension that *is* applicable to a zero-argument call (that's exactly
what `params` means) — but it's *also* applicable to any non-zero-argument
call (`Configure().ToString(Array.Empty<object>())`), and only the bare,
argument-omitted spelling is actually shadowed by `object.ToString()`.
Amendment 11's rule ("applicable to zero arguments → withhold the whole
overload's surface") withholds the entire `Configure()`/`Verify()`
surface for this overload, even though a fully working, unambiguous
non-empty-argument spelling remains reachable — a real, if narrow,
over-rejection Amendment 11 didn't anticipate because it reused an
existing helper (`IsApplicableToZeroArguments`) built for a different
question (Configure-name collision, where *any* zero-argument
applicability is disqualifying) without checking whether the same
threshold was correct here too.

**Corrected: withhold the `object`-collision surface only when the
discriminator extension has genuinely zero parameters** — no required, no
optional, no `params` — not merely "applicable to a zero-argument call."
`object.ToString()`/`GetHashCode()`/`GetType()` are all fixed-arity,
zero-parameter methods with no overloads of their own; the *only* way an
interface member's generated extension can be entirely, unconditionally
shadowed by one of them is if the extension itself can *only* ever be
called with zero arguments — the exact case a non-overloaded member
already is (Amendment 2 Finding 4's argument-independence, unchanged), and
the only case v1 itself ever had to consider. A `params`/all-optional
overload keeps its full surface; a consumer simply can't reach it via the
bare zero-argument spelling, which resolves to `object`'s own member
instead — a real but ordinary C# ambiguity a consumer would already hit
calling the real interface member the same way, not something Compono
needs to diagnose specially. This check is simpler than Amendment 11's
own (`Parameters.Length == 0`, not the multi-branch applicability check),
not just more correct.

**Two stale cross-references, corrected without new decision content**
(the underlying facts were already decided by earlier Amendments; only
the pointers to them had gone stale):

- This ADR's own Links section still says
  `docs/packages/compono-testdoubles.md`/`skills/compono/references/testdoubles.md`
  are "updated once implemented (PLAN-0044 Phase 4)" — stale since the
  packaged-verification/documentation-scheduling process fix (recorded in
  PLAN-0044 directly, not as an ADR Amendment, per that fix's own content-
  separation reasoning) moved each phase's own docs into Phases 0-2, with
  Phase 4 reduced to a cross-cutting consistency pass. The doc updates
  happen where the behavior ships, not in one later phase.
- PLAN-0044's own Critical Files section still described
  `TestDoubleEmitter.cs`/`TestDouble.scriban` as producing "`Interlocked.Increment`
  dispatch" — stale since Amendment 2 Finding 1 routed the counter
  increment through the public `RecordCall()` bridge instead, to fix the
  exact cross-assembly accessibility failure a raw `Interlocked.Increment(ref
  __member.CallCount)` would hit.

PLAN-0044 is updated in the same pass as this Amendment: Phase 0's
`object`-collision task is corrected to the zero-parameter-only rule, with
a `params`-shaped-object-named-member test proving the overload keeps its
surface; Critical Files now says `RecordCall()`.

## Amendment 13 (2026-08-14): a return-type-dependent generic method has no constructible body and triggers whole-interface rejection, not member-scoped exclusion

A twelfth Codex review pass caught a genuine inconsistency in Amendment
1's own "Scope boundary" illustration — the original Decision Outcome and
every other Amendment's text is left exactly as written, per the
immutability rule already followed twelve times above; this Amendment
corrects Amendment 1's own example.

**Finding — Amendment 1 claims a return-type-dependent generic method
(`T Get<T>(T seed)`) doesn't block a differently-named sibling
(`void Reset<T>(T value)`), but this both misuses "sibling overload" (the
two methods don't share a name, so they were never overloads of each
other to begin with) and, more substantively, contradicts this ADR's own
already-established rule for the underlying condition.** A return-type-
dependent generic method has no constructible body **at all** — the exact
same root cause ("no way to fix a concrete slot type/deterministic
default") as a non-nullable-reference return with no default (Amendment
1's own correction: "no constructible body at any granularity... triggers
today's existing whole-interface rejection, unchanged from v1") and a
pointer-typed parameter (Amendment 5 Finding 12, for the same reason).
Amendment 1's illustration treated `Get<T>(T seed)`'s exclusion as if it
belonged to the *other* bucket — an overload with a constructible fallback
body that just lacks a `Configure()` extension — without checking which
bucket it actually falls in.

**Corrected: a return-type-dependent generic method is in the
no-constructible-body bucket, and its whole interface falls back to the
runtime-provider path — the same disposition every other no-constructible-
body shape already gets, not a new one.** This does not touch the ADR's
actual evidence or motivating case at all: `ILogger<T>`'s own two methods
(`Log<TState>`, `BeginScope<TState>`) neither has a return type depending
on its own type parameter, so `ILogger<T>` remains fully supported
regardless of this correction — the mis-scoped claim was only ever in an
illustrative, made-up example, never in anything the real evidence
required. **What Amendment 1's "overload-set-internal partial support"
genuinely still applies to, unchanged:** an *overload* (same name, same
member) with a `ref`/`out`/`in` parameter, which *does* have a
constructible fallback body — that case, and only that case, keeps its
sibling overloads and the rest of the interface generating normally.

PLAN-0044 needs no change from this Amendment — it never repeated the
mis-scoped illustration, only the ADR's own "Scope boundary" bullet did.

## Amendment 14 (2026-08-14): generic members are never implicit zero-argument candidates, closing two related collision gaps at once; `Equals` added to the object-collision check

A thirteenth Codex review pass caught two related gaps in this ADR's
collision-detection logic, both stemming from the same missing
consideration: once Requirement 2 admits generic interface members for
the first time, both the `object`-member collision check (Amendment 11/12)
and the `Configure`/`Verify` bridge-name collision check (ADR-0043
Amendment 3 Finding E, generalized by this ADR's Amendment 2 Finding 4)
need to account for **type inference**, not just parameter count. All
prior text is left exactly as written, per the immutability rule already
followed thirteen times above.

**The shared root cause.** Both checks ultimately ask "is this real
member applicable to a call with no explicit type arguments and (for the
`object`-collision check) some small fixed number of value arguments?" —
and both currently answer that question using `IsApplicableToZeroArguments`-
shaped logic that only inspects parameter optionality, never generic
arity. A generic method's own type parameters can only be resolved by
inference from **supplied, non-omitted** value arguments (or by explicit
type arguments at the call site) — never from nothing. A generic member
with no value parameters to infer from, or with only optional/`params`
parameters a caller could omit, is therefore **never** a valid implicit
(no-explicit-type-argument) zero/near-zero-argument candidate, regardless
of what a parameter-optionality-only check concludes.

**Finding (`object`-collision) — an overloaded, generic `ToString<T>()`
(no value parameters) is wrongly withheld.** Amendment 12's corrected rule
("collide only when the discriminator has genuinely zero parameters") did
not also check generic arity. `object.ToString()` has zero type
parameters; a call `Configure().ToString<int>()` supplies an explicit
type argument, which `object.ToString()` cannot match (arity mismatch) —
member lookup finds no applicable instance candidate, and extension
search proceeds normally, reaching the generated discriminator. Amendment
12's check, as stated, still withholds the surface for this case, an
unnecessary loss of otherwise-real support.

**Finding (`object`-collision) — `Equals` was never added to the checked
name set, but overloaded discriminators can now collide with it too.**
ADR-0043 Amendment 6 Finding N correctly excluded `Equals` from v1's
collision check, because `object.Equals(object)` is one-argument and v1's
extensions were always zero-argument — no collision was ever possible.
Requirement 1 changes this: an overloaded, **non-generic** member like
`Equals(int format)` generates a one-argument discriminator, and
`object.Equals(object)` is applicable to **any** single-argument call
(every type has a reference or boxing conversion to `object`) — with no
escape hatch, since a non-generic method has no explicit-type-argument
form to disambiguate with. This is a real, previously-nonexistent
collision risk this ADR's own overload work introduced without updating
the object-member name list to match.

**Finding (`Configure`/`Verify` bridge collision) — a generic
`Configure<T>()`/`Verify<T>()` interface member is wrongly treated as
colliding with the bridge.** The existing `IsApplicableToZeroArguments`
helper (ADR-0043, refined by PR #83 review rounds 2 and 4) only checks
parameter optionality; it has no reason today to consider generic arity,
because v1 never emitted or analyzed generic interface members. Once
Requirement 2 admits them, a zero-value-parameter generic member like
`Configure<T>()` has nothing for the compiler to infer `T` from at a bare
`Configure()` call site — type inference fails, the candidate is excluded,
and (per this repo's own compile-spike-verified "applicability, not
name-existence" precedent, PLAN-0043 PR #83 review round 2) extension
search proceeds normally, reaching the `Configure()`/`Verify()` bridge
successfully. The existing helper, unchanged, incorrectly reports this as
"applicable to zero arguments" and rejects the interface.

**Corrected, once, for all three checks together:** `IsApplicableToZeroArguments`
(and the `object`-collision check, which adopts the identical rule)
returns `false` immediately for any generic member (`IsGenericMethod`)
whose type parameters aren't all inferable from its own **required**
(non-optional, non-`params`) value parameters — in practice, for this
feature's purposes, any generic member with zero required value
parameters is never an implicit zero-argument candidate, full stop,
regardless of what its optional/`params` parameters might otherwise
suggest. A generic member therefore only ever collides with the bridge or
with an `object` member when called with **explicit** type arguments
matching that member's own arity — which the bridge/`object`-collision
check doesn't need to model at all, since the generated discriminator
extension (also generic, per Amendment 1) is exactly what such an
explicit-type-argument call reaches; there is no remaining case where a
generic member genuinely, unconditionally shadows the generated surface
the way a non-generic zero-arg member does.

PLAN-0044 is updated in the same pass as this Amendment: the shared
`IsApplicableToZeroArguments`-generalization task is added to Phase 0
(used by both collision checks), `Equals` is added to the object-member
collision name list with its own test, and a generic `Configure<T>()`/
`ToString<T>()` non-collision test is added to Phase 1 (once generic
methods exist to construct the case with).

## Amendment 15 (2026-08-14): pre-implementation design-review loop closed

Fourteen review rounds against this ADR (Amendments 1-14, plus several
plan-only process/wording fixes) surfaced real, load-bearing defects
every round — but the shape of what they found shifted over that span,
the same way it did during ADR-0043's own pre-implementation review
(that ADR's Amendment 11 recorded the identical transition, at a similar
round count): Amendments 1-4 caught structural problems (cross-assembly
field/counter accessibility, `CS0460`/`CS0111`/`CS0214`-class compile
failures in the core generated-code shapes, a genuine field-emission gap,
phase-sequencing contradictions). Amendments 5-14 narrowed steadily into
edge-case corrections against helper logic and canonicalization rules
that don't exist as compiled code yet — real, worth fixing, but
individually smaller and increasingly about *how precisely* an
already-decided mechanism handles one more C# corner case, not *whether*
the mechanism itself is sound.

**Confirmed directly with the requester: this is the same signal
ADR-0043 hit, and the same response applies.** Further refinement
continues during actual implementation instead — `tasks/implement.md`'s
own build/test/PR-review cycle surfaces and resolves remaining gaps
empirically against real generated code and a real compiler, rather than
this text-review cycle continuing indefinitely against a design that
doesn't compile anything yet. PLAN-0044's own phased structure (each
generator-facing phase gated on a real packaged-consumer smoke test,
added specifically because this review kept finding cross-assembly
compile failures a design-only review can't fully rule out) is the
concrete mechanism that carries this discipline into implementation.

This closes the pure pre-implementation design-review loop for ADR-0044.
PLAN-0044 (`Not Started`) is ready; implementation begins with Phase 0
once explicitly requested.

## Amendment 16 (2026-08-14): Amendment 14's escape hatch requires the extension itself to be generic; `Equals` collision requires real object-convertibility

The outstanding Codex review requested before Amendment 15 closed the
loop landed two more real findings against Amendment 14's own fix — both
confirmed before implementation begins, consistent with treating this as
the genuine last pre-implementation round. All prior text is left exactly
as written, per the immutability rule already followed fifteen times
above.

**Finding — Amendment 14's "a generic member never collides" rule
checked the wrong thing: the *real interface member's* genericity, not
the *generated discriminator extension's*.** The escape hatch Amendment
14 relies on (an explicit type argument disambiguates arity against a
non-generic `object` member) only exists if the **extension itself**
accepts a type argument. For an **overloaded** generic method, Amendment
1 makes the extension generic too — the escape hatch is real. For a
**solo** (non-overloaded) generic method, Requirement 2's original,
unchanged design keeps the extension non-generic and zero-argument
(member-level, argument-independent, matching every other solo member) —
there is no escape hatch, because the extension was never given one. A
solo `ToString<T>()` under Amendment 14's rule as written would neither
collide (the real method is generic) nor actually be reachable
(`Configure().ToString<int>()` fails — the non-generic extension doesn't
accept a type argument at all) nor fall back to `object.ToString()`
cleanly — a genuinely broken, unreachable-either-way state, worse than
the collision it was meant to avoid.

**Corrected:** the escape-hatch exception applies only when the
*generated discriminator extension* is itself generic — in practice,
only the overloaded-generic case (Amendment 1). A solo generic method
sharing a name with `ToString`/`GetHashCode`/`GetType`/`Equals` keeps
v1's original disposition unchanged: its (non-generic, zero-argument)
extension collides exactly like any other zero-parameter member, and is
diagnosed accordingly — Amendment 14's fix never actually applied to this
case in the first place, once stated correctly.

**Finding — not every type converts to `object`; ref-like types
(`Span<T>`, other `ref struct`s) categorically don't, and Amendment 14's
`Equals` collision claim assumed otherwise.** `object.Equals(object)` is
only applicable to a call whose argument actually has a conversion (
reference or boxing) to `object` — a ref-like type has neither (the CLR
forbids boxing a `ref struct`, by design, the same restriction that
already makes a ref-like *return* type diagnosed-and-excluded elsewhere
in this ADR). An overload like `Equals(Span<int> value)` is therefore
**not** shadowed by `object.Equals(object)` at all — the generated
discriminator extension remains fully reachable, and Amendment 14's
blanket "any non-generic one-argument overload named `Equals` collides"
claim over-rejects this shape. (Pointer-typed parameters have the same
non-convertibility property, but are already excluded for an unrelated
reason — Amendment 5 Finding 12's `unsafe`-context rule — before this
check would ever run, so they don't need a second exclusion here.)

**Corrected:** the `Equals` collision check additionally verifies the
discriminator's parameter type is not ref-like
(`!parameterType.IsRefLikeType`, the same property `TestDoubleAnalyzer`
already uses for the existing ref-like-return-type check) before
concluding a collision. A ref-like-typed `Equals` parameter keeps its
`Configure()`/`Verify()` surface.

PLAN-0044 is updated in the same pass as this Amendment: Phase 1's
generic-method task clarifies the escape hatch is extension-genericity-
gated, not method-genericity-gated, with a solo-generic-`ToString<T>()`
still-collides test; Phase 0's `Equals` collision task gains the
ref-like-parameter exclusion and its own non-colliding test
(`Equals(Span<int> value)`).

This is the genuine close of the pre-implementation design-review loop —
confirmed directly with the requester, who is treating this as the last
round before Phase 0 implementation begins.

## Links

- [ADR-0043](0043-compono-generated-test-doubles-design.md) — the v1
  design this ADR extends; not superseded, ADR-0043's own decisions
  (source-generation-first, interface-only, argument-independent
  configuration outside overload discriminators, explicit two-gate
  activation) remain in force.
- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) — the
  admitted problem and Non-Goals this ADR's own Non-Goals extend, not
  replace.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the evidence-over-prediction bias this ADR's scoping (and its explicit
  rejection of full interface-level partial support) follows directly.
- [ADR-0024](0024-public-provider-extensibility-model.md) — the
  `ICompositionValueProvider` extension point this ADR makes zero changes
  to; `GeneratedTestDoubleProvider`'s existing shape is untouched.
- [ADR-0001](0001-source-generation-first.md) — the "prove it, don't
  assume it" AOT-verification standard PLAN-0044's AOT phase must meet.
- [ADR-0034](0034-benchmark-suite-strategy-and-redesign.md) — the
  benchmark discipline this ADR's "Performance" section follows (targeted
  risk checks, not a general competitive benchmark).
- `docs/packages/compono-testdoubles.md`, `skills/compono/references/testdoubles.md` —
  updated once implemented (PLAN-0044 Phase 4), not by this ADR directly.
