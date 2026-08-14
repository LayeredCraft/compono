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
