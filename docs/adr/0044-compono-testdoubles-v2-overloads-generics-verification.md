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
