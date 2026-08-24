# [ADR-0049] Compono.TestDoubles: Per-Closed-Instantiation Configuration for Generic Methods Whose Return Type Depends on Their Own Type Parameter

**Status:** Accepted

**Date:** 2026-08-23

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

Dogfooding `Compono.TestDoubles` against `ncipollina/trivia-platform`'s
real test suite (a second, larger dogfood target than
`ncipollina/trivia-manager`, per this repo's own roadmap sequencing —
see [ADR-0042](0042-compono-owned-source-generated-test-doubles.md)
Amendment 2's classification policy, which already settled that a real,
evidenced gap is a roadmap candidate regardless of how narrow it looks in
isolation) surfaced a real, load-bearing shape ADR-0044 Requirement 2
explicitly declined to support:

```csharp
public interface IConversationalContextManager
{
    Task<ConversationContext?> GetCurrentContextAsync(IHandlerInput input, CancellationToken ct = default);
    Task<T?> GetContextDataAsync<T>(IHandlerInput input, string key, CancellationToken ct = default) where T : class;
    Task SetContextAsync(IHandlerInput input, ConversationContext context, CancellationToken ct = default);
    Task SetContextDataAsync<T>(IHandlerInput input, string key, T data, ContextScope scope = ..., CancellationToken ct = default);
    Task TransitionContextAsync(IHandlerInput input, ContextType newType, string? subContext = null, CancellationToken ct = default);
    // ...several more non-generic members
}
```

`GetContextDataAsync<T>`'s return type (`Task<T?>`) depends on the
method's own type parameter `T`. **Verified directly against the real repo**
(`grep -rn "GetContextDataAsync<" test/ | grep -oE "GetContextDataAsync<[A-Za-z0-9_]+>" | sort | uniq -c`,
excluding the interface/production-implementation declarations and the two
pass-through generic wrapper methods that just forward their own `<T>`),
real test call sites close `T` to **five distinct types**:

| Closed `T` | Test call sites |
|---|---|
| `ConversationContext` | 35 |
| `UpsellPayload` | 21 |
| `UserContextBase` | 28 |
| `CategorySelectionResponseModel` | 3 |
| `PurchaseFlowRepeatModel` | 2 |

This is genuine multi-`T` evidence — `Configure<ConversationContext>()` and
`Configure<UpsellPayload>()` on the same double instance need fully
independent state, which is exactly the capability this ADR designs.

**A related but distinct point, corrected from an earlier draft of this
ADR:** several `GetContextDataAsync<UserContextBase>()` call sites
configure *different return values* across different tests —
`AuthenticatedUserContext`, `GuestUserContext`, or `null` — but
`UserContextBase` is a single `abstract record`
(`GuestUserContext`/`AuthenticatedUserContext : UserContextBase`), so these
are **one closed instantiation** (`T = UserContextBase`) configured with
different polymorphic *values*, not multiple closed `T`s. That variation
needs no new mechanism — an ordinary `Returns(T? value)` already accepts
any value assignable to `T?`, the same as every non-generic member today.
It's real evidence that the *value* side needs no restriction, not evidence
for the bucket/multi-`T` mechanism itself — the `UpsellPayload`/
`ConversationContext`/etc. table above is what actually establishes that.

```csharp
// Two genuinely different closed T's, same double instance, independently configured:
contextManager.GetContextDataAsync<UserContextBase>(Arg.Any<IHandlerInput>(), "user", Arg.Any<CancellationToken>())
    .Returns(new AuthenticatedUserContext { Sub = sub });        // one closed T, a polymorphic value
contextManager.GetContextDataAsync<UpsellPayload>(Arg.Any<IHandlerInput>(), Attributes.UpsellPayload, Arg.Any<CancellationToken>())
    .Returns(payload);                                            // a genuinely different closed T
```

**This is not a bug report.** ADR-0044 Requirement 2 made an explicit,
evidence-based call at the time: support generic methods whose return type
is independent of their own type parameter (the `ILogger<T>.Log<TState>`
shape), and diagnose the return-type-dependent shape (`T Get<T>()`,
`Task<T> GetAsync<T>()`) as unsupported — reinforced in that ADR's own
Non-Goals as "no per-closed-generic-instantiation configuration." No real
consumer had surfaced a need for it at the time. trivia-platform is that
evidence now, and per ADR-0029's evidence-over-prediction discipline, that
reopens the question rather than settling it as a permanent boundary.

**Compounding effect on the rest of the interface.** Today, the generator
does not merely leave `GetContextDataAsync<T>` unconfigurable — its
presence causes `TestDoubleAnalyzer.Analyze` to return `Failure(...)` for
the **entire interface** (`TestDoubleAnalyzer.cs:410`), so
`TransitionContextAsync`, `GetCurrentContextAsync`, and every other
otherwise-perfectly-supportable member also get no generated double. This
ADR's chosen design (below) makes that consequence moot — once
`GetContextDataAsync<T>` itself is supported, it no longer triggers
`Failure()` at all, so the whole-interface question resolves as a
byproduct rather than needing its own separate fallback design (ADR-0045's
member-scoped-exclusion pattern was considered and is unnecessary here —
see "Considered Options").

**Pre-1.0 framing**, same as ADR-0048: `Compono.TestDoubles` is
intentionally still pre-1.0, and this ADR adds a capability rather than
breaking one — every currently-generated interface keeps generating
byte-for-byte identically (see "Consequence" under Requirement 2's
resolution below).

## Decision Drivers

- ADR-0042 Amendment 2's policy: a real, evidenced gap is a roadmap
  candidate regardless of how narrow it looks in isolation.
- [ADR-0001](0001-source-generation-first.md)'s no-reflection-by-default
  posture and this repo's AOT-safety precedent — "prove it, don't assume
  it," applied here as a real Native AOT publish-and-run spike before this
  ADR was drafted (see "Compiler/AOT Spike," below), the same discipline
  ADR-0048's overload-discriminator interaction section used.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
  evidence-over-prediction bias — scope this ADR to exactly the shape
  trivia-platform evidences (a single method-type-parameter, referenced
  only as the return type or its direct `Task`/`ValueTask` wrapper), not a
  speculative general solution for every way a return type could reference
  a type parameter.
- Reuse over invention: [ADR-0048](0048-testdoubles-argument-matching-and-call-verification.md)'s
  `Match<T>`/`CallVerifier` machinery already solves argument matching and
  call verification for ordinary (non-self-referencing) parameters —
  `GetContextDataAsync<T>`'s real parameters (`IHandlerInput`, `string`,
  `CancellationToken`) don't depend on `T` at all, so that machinery
  should apply directly, not be reinvented.

## Considered Options

### Scope: does this ADR also need to design whole-interface-vs-member-scoped fallback (ADR-0045's pattern)?

1. **No — supporting the member directly makes the question moot
   (chosen).** ADR-0045 introduced member-scoped exclusion specifically
   *because* the member itself stayed unconfigurable (a non-nullable
   return with no deterministic default) — the best it could do was stop
   that one member from poisoning its siblings. Here, the member becomes
   fully configurable, so it simply stops matching the `Failure()`
   condition at `TestDoubleAnalyzer.cs:410` — no fallback/default-value
   dispatch path is needed for it at all.
2. **Member-scoped exclusion only, leave the member itself
   unconfigurable.** This was this repo's own first instinct before the
   AOT spike (see the conversation this ADR originates from) — rejected
   explicitly: it satisfies "the interface generates" but not the actual
   evidenced need ("`GetContextDataAsync<UserContextBase>()` must return
   what the test configured"). Recorded here because it's the obvious
   smaller patch and worth naming as insufficient, not because it's a
   live option.

### Storage: how does a per-member, single-instance double class hold independently-configurable state for arbitrarily many closed `T`s?

The existing mechanism (`ReturnConfig<T>` as a mutable field, mutated
in-place via a `ref struct` `ReturnConfigBuilder<T>` wrapping `ref
ReturnConfig<T>`) assumes exactly one storage location per member, known
at codegen time. A closed-`T`-keyed member needs a storage location whose
*identity* varies at runtime (by whatever `T` a given test's `Configure<T>()`/
real call closes to) while its *shape* (a `ReturnConfig<Task<T?>>`, matcher
fields for the real parameters, a call log) stays fully knowable at codegen
time once `T` is chosen.

1. **`Dictionary<Type, object>` bucket keyed by `typeof(T)`, valued by a
   generator-emitted nested class generic in `T` (chosen).** Per eligible
   generic member, the generator emits:

   ```csharp
   internal sealed class __GetContextDataAsync_State<T> where T : class
   {
       internal Compono.ReturnConfig<Task<T?>> Config;
       internal Compono.Match<IHandlerInput>? Matcher_input;
       internal Compono.Match<string>? Matcher_key;
       internal Compono.Match<CancellationToken>? Matcher_cancellationToken;
       internal readonly List<(IHandlerInput input, string key, CancellationToken cancellationToken)> Calls = [];
       internal readonly object Lock = new();
   }

   internal readonly Dictionary<Type, object> __GetContextDataAsync_buckets = new();

   internal __GetContextDataAsync_State<T> __GetContextDataAsync_Bucket<T>() where T : class
   {
       lock (__GetContextDataAsync_buckets)
       {
           if (!__GetContextDataAsync_buckets.TryGetValue(typeof(T), out var boxed))
           {
               boxed = new __GetContextDataAsync_State<T>();
               __GetContextDataAsync_buckets[typeof(T)] = boxed;
           }

           return (__GetContextDataAsync_State<T>)boxed;
       }
   }
   ```

   The nested state class's own type parameter `T` lets the generator
   express `Task<T?>` (or whatever the member's real return shape is with
   `T` substituted) as an ordinary, fully-typed field — no boxing of the
   `ReturnConfig<T>` payload itself, no loss of the existing struct's
   shape. The *bucket* is `Dictionary<Type, object>` (heterogeneous across
   different closed `T`s, necessarily untyped at that outer layer), but
   every read/write immediately downcasts to the exact closed
   `__GetContextDataAsync_State<T>` the caller's own generic invocation
   already knows at compile time — the cast is never guessing, `T` is
   always syntactically present at both the `Configure<T>()`/`Verify<T>()`
   call site and the real interface call site. `typeof(T)` is a compile-time-
   safe runtime type token (not reflection in the `MakeGenericType`/
   `Activator`/`DynamicInvoke` sense) and is fully AOT/trim-safe as long as
   every closed `T` actually used is visible to the AOT compiler's static
   analysis — true here by construction, since C# requires writing the
   closed type argument literally at every call site.
2. **Box `ReturnConfig<T>` directly into the `Dictionary<Type, object>`
   value.** Rejected: `ReturnConfig<T>` is a mutable struct, and every
   existing write path (`ReturnConfigBuilder<T>.Returns`/`.Throws`)
   mutates it in place via `ref`. A boxed struct sitting in a
   `Dictionary<Type, object>` value slot cannot be mutated in place
   through the existing `ref`-based API without either
   `Unsafe.Unbox<T>(object)` (an advanced, easy-to-misuse JIT intrinsic
   this repo has no existing precedent for) or replacing every value on
   every write (defeating the call-log/matcher fields that need to
   persist independent of the `ReturnConfig<T>` mutation). Wrapping the
   struct in a small reference-type holder class (Option 1) sidesteps this
   entirely — a class field is always mutable in place through an
   ordinary reference, no `ref`/`Unsafe` needed anywhere.
3. **Reflection-based (`Activator.CreateInstance(typeof(...).MakeGenericType(t))`,
   or a `MethodInfo.MakeGenericMethod(t).Invoke(...)` dispatch layer).**
   Rejected outright — directly contradicts ADR-0001's no-reflection-by-
   default posture, and would be the first reflection-dependent code path
   anywhere in `Compono.TestDoubles`.
4. **A single shared, non-generic object graph keyed by `(Type, argument
   hash)` combining configuration and call-log storage into one
   `Dictionary` entry.** Rejected: no evidence motivates combining these
   (argument matching against the real, non-`T` parameters is unrelated to
   which `T` was closed to), and it would make the per-closed-T isolation
   this ADR requires (see "Requirement: independent state per closed `T`,"
   below) harder to reason about, not easier.

### Argument matching against the real (non-`T`) parameters

`GetContextDataAsync<T>`'s real parameters — `IHandlerInput`, `string`,
`CancellationToken` — don't reference `T` at all. Nothing about them
differs from an ordinary ADR-0048-eligible non-generic member's
parameters once a specific `T`'s bucket is selected.

1. **Reuse `Match<TParam>`/`Match.Any<TParam>()`/`Match.Is<TParam>(predicate)`
   directly, scoped per bucket (chosen).** The matcher fields live on the
   per-`T` nested state class (see storage design above), so
   `Configure<UserContextBase>()`'s matchers are completely independent of
   `Configure<UpsellPayload>()`'s — configuring one closed `T` cannot
   leak into or overwrite another's state. No new matching mechanism —
   `Match<T>`'s existing public `Matches(T)` surface (ADR-0048) is called
   exactly the same way today's non-generic eligible members call it.
2. **Skip argument matching for this shape; support only argument-
   independent `Configure<T>()`/`Verify<T>()`.** Rejected: real
   trivia-platform call sites configure `GetContextDataAsync<T>` by
   argument (e.g. matching a specific `key` string), and a bucket-scoped
   `Match<T>` reuse costs nothing extra once the bucket design (above) is
   already in place — there's no simplification this option would
   actually buy.

### Call verification per closed `T`

1. **`CallVerifier` reused unchanged, backed by the per-bucket call log's
   filtered count (chosen).** `contextManager.Verify().GetContextDataAsync<UserContextBase>(...)`
   and `contextManager.Verify().GetContextDataAsync<UpsellPayload>(...)`
   read from their own independent buckets — one closed `T`'s call count
   can never be confused with another's, by construction (they're
   different `Dictionary` entries). No new verification type; `CallVerifier`
   already takes a plain `(int observedCount, string memberDescription)` —
   the bucket-filtered count is computed the same way ADR-0048's existing
   per-member call-log filtering already works, just against the bucket's
   own `Calls` list instead of a member-level one.

### Scope boundary: which return-type shapes does this ADR actually cover?

Per ADR-0029's evidence discipline, this ADR covers exactly the shape
trivia-platform evidences and no more:

1. **A single method-type-parameter `T`, referenced only as the method's
   direct return type or as the sole type argument of `Task<T>`/`Task<T?>`/
   `ValueTask<T>`/`ValueTask<T?>` (chosen).** Covers
   `GetContextDataAsync<T>`'s real shape (`Task<T?>`) and its natural
   siblings (a synchronous `T Get<T>()`, `ValueTask<T>`) by the same
   mechanism — the nested state class's `ReturnConfig<TSlot>` field
   already generalizes to whichever of these shapes `TSlot` substitutes
   to, no additional design needed per shape.
2. **`T` nested deeper in the return type** (`Task<List<T>>`,
   `Task<Dictionary<string, T>>`, `Task<(T, int)>`, etc.). **Out of scope,
   unevidenced — not because the chosen mechanism is known to struggle
   with it.** The nested state class's `ReturnConfig<TSlot>` field is an
   ordinary generic field; nothing about the bucket/holder architecture
   stops a future instantiation from spelling `ReturnConfig<Task<List<T>>>`
   the same way this ADR spells `ReturnConfig<Task<T?>>`. The chosen
   mechanism may well generalize cleanly to this shape, but this ADR
   deliberately does not specify or promise that generalization — no
   current dogfood evidence requires it, and per ADR-0029's discipline,
   scope follows evidence rather than anticipating it. Stays
   whole-interface-rejecting via the existing `TypeReferencesOwnTypeParameter`
   check, unchanged, until real evidence motivates a follow-up ADR that
   actually designs and spikes it (not this one, by extrapolation).
3. **More than one method-type-parameter** (`Task<TResult?> Get<TKey,
   TResult>(TKey key)`). **Out of scope, unevidenced, for the same
   reason** — `GetContextDataAsync<T>` has exactly one. This looks like
   primarily a keying/identity expansion (a composite key — e.g. a
   `(Type, Type)` tuple, or nested dictionaries — in place of the single
   `typeof(T)` key this ADR uses) rather than a different storage
   architecture, but that is an observation, not a commitment this ADR is
   making. A future ADR extending this mechanism to multiple type
   parameters should not have to first undo a claim here that the single-
   parameter case was fundamentally different — there isn't one. Not
   designed against a hypothetical here regardless, per this ADR's own
   scope discipline (mirrors ADR-0048's identical choice not to design
   `InOrder` verification against zero evidence).

### A separate, deliberately untouched shape: `T` referenced in a parameter (`SetContextDataAsync<T>`)

`IConversationalContextManager.SetContextDataAsync<T>(IHandlerInput input,
string key, T data, ContextScope scope = ..., CancellationToken ct =
default)` has `T` in a **parameter** (`data`), not the return type. This is
a different, already-decided shape: ADR-0044 Requirement 2 already
supports it today (an argument-independent, non-generic `Configure()`
surface, the `ILogger<T>.Log<TState>` pattern) and ADR-0048 already
explicitly excludes argument-aware matching/verification for it (a
member-level call log cannot hold an open `T`). **This ADR makes no change
to that shape.**

**Corrected from an earlier draft of this ADR, which claimed
`SetContextDataAsync<T>`'s real trivia-platform usage was pure filler — it
is not.** Verified directly against the real repo
(`test/engine/unit/LayeredCraft.Alexa.TriviaEngine.Modules.Commerce.Tests/Interceptors/Request/UpsellEligibilityRequestInterceptorTests.cs`),
real call sites use `Received(1)`/`DidNotReceive()` with argument-aware
matching on the non-`T` parameters (`Arg.Is<string>(key)`,
`Arg.Is<ContextScope>(scope)`) **and** on the `T`-typed `data` parameter
itself, e.g. `Arg.Is<UpsellPayload>(payload => payload.ProductId == ... &&
payload.TriggerContext == ...)`. That last one is exactly the shape
ADR-0048 excluded — argument-aware matching against a parameter whose type
*is* the method's own open type parameter, which a member-level call log
cannot represent without something new.

**This is real, separate evidence this ADR does not attempt to resolve.**
Per this repo's evidence-classification discipline (ADR-0042 Amendment
2), it needs its own explicit design pass rather than being folded into
this ADR's storage mechanism speculatively — the two problems are
different in kind (this ADR's bucket keys by the method's own closed
return-position `T`; this new gap would need to record and match against
an open-`T`-typed *argument value*, a different storage/typing question
entirely). Recorded here as a named, classified gap for a follow-up ADR,
not designed against.

## Decision Outcome

Chosen option: the `Dictionary<Type, object>` bucket + generator-emitted
generic-in-`T` nested state class (storage), direct `Match<T>`/`CallVerifier`
reuse (matching/verification), scoped to a single method-type-parameter
referenced only as the return type or its direct `Task`/`ValueTask`
wrapper. Target API, matching trivia-platform's real call shape exactly:

```csharp
contextManager.Configure()
    .GetContextDataAsync<UserContextBase>(Match.Any<IHandlerInput>(), "user", Match.Any<CancellationToken>())
    .Returns(Task.FromResult<UserContextBase?>(authenticatedUserContext));

var result = await contextManager.GetContextDataAsync<UserContextBase>(input, "user", ct); // dispatches the configured value

contextManager.Verify()
    .GetContextDataAsync<UserContextBase>(Match.Any<IHandlerInput>(), "user", Match.Any<CancellationToken>())
    .Once();

// A different closed T, on the same double instance, is completely independent:
contextManager.Verify()
    .GetContextDataAsync<SomeOtherContext>(Match.Any<IHandlerInput>(), Match.Any<string>(), Match.Any<CancellationToken>())
    .Never();
```

### Invariant: identical public `Returns`/`Throws` ergonomics to the equivalent non-generic member; the bucket is never observable through the public API

Verified against the generator's own slot-type computation
(`TestDoubleMemberInfo.SlotTypeFullyQualifiedName => IsVoid ? "global::Compono.Unit"
: ReturnTypeFullyQualifiedName` — `Compono.Generators/Models/TestDoubleMemberInfo.cs:151`):
today's `ReturnConfigBuilder<T>.Returns(T value)` slot type is always the
member's **full** return type, never auto-unwrapped — an existing
non-generic `Task<string> GetNameAsync()` member requires
`.Returns(Task.FromResult("Ada"))`, not `.Returns("Ada")` (confirmed
directly in `test/Compono.TestDoubles.AotSmokeTest/Program.cs`'s existing,
already-shipped usage). This ADR's target API above follows that exact,
pre-existing convention (`.Returns(Task.FromResult<UserContextBase?>(...))`)
— it is not a new or different ergonomic for the generic case.

**The rule, stated explicitly:** a closed-generic-return member has the
same consumer-facing `Returns`/`Throws` semantics as the equivalent
non-generic member with that same closed return type. The
`Dictionary<Type, object>` bucket and the generic-in-`T` nested state class
are purely an internal storage-routing mechanism selected once per
`Configure<T>()`/`Verify<T>()`/real-call — nothing about them is
observable through the public `ReturnConfigBuilder<TSlot>`/`CallVerifier`
surface, which is exactly the same two types every other eligible member
already returns from `Configure()`/`Verify()`. This ADR does not redesign
async `Returns` ergonomics for anyone — generic or not.

### Compiler/AOT Spike

Before drafting this ADR, a hand-written double implementing this exact
design (not generator output — this shape isn't implemented yet) was
compiled, JIT-run, Native-AOT-published, and run as a native binary,
mirroring `test/Compono.TestDoubles.AotSmokeTest`'s existing
publish-and-run harness (`dotnet publish -c Release -f net10.0
-p:PublishAot=true`, real `Compono`/`Compono.TestDoubles` packages via the
project's local-feed pack script, not a `ProjectReference`). The spike
interface had **two** generic members, deliberately different in return
nullability, plus a `TransitionContextAsync` control member:

- `GetContextDataAsync<T> : Task<T?>` — nullable, matching
  `IConversationalContextManager`'s real shape exactly.
- `GetRequiredDataAsync<T> : Task<T>` — non-nullable, added specifically to
  exercise ADR-0045's *other* dispatch branch under the new bucket
  mechanism (see below — an earlier draft of this spike only implemented
  the nullable member and had its hand-written dispatch always throw when
  unconfigured, which doesn't actually match ADR-0045's real rule for a
  nullable return; this was caught and corrected before drafting this ADR,
  not after).

One run proved:

- Two closed `T`s on the nullable member (`UserContextRepro`,
  `GuestContextRepro`) configured with independent `Returns(...)` values
  and independent argument matchers — each dispatched its own value, never
  the other's.
- Independent `Verify().Once()` per closed `T`.
- **ADR-0045's deterministic-default branch, composed with the new bucket
  mechanism:** an unconfigured closed `T` on the nullable member
  (`GetContextDataAsync<string>`) returned `null`, not a throw — and an
  argument mismatch against a *correctly* configured `T` (wrong `key`)
  also fell through to `null`, not the configured value. Neither case
  leaked another `T`'s configured value.
- **ADR-0045's configuration-required branch, on the same bucket
  mechanism:** on the non-nullable member, both an unconfigured closed `T`
  and an argument mismatch against a correctly configured `T` correctly
  threw `TestDoubleNotConfiguredException`.
- `dotnet publish -c Release -f net10.0 -p:PublishAot=true` completed with
  **zero warnings** (no `IL2xxx`/`IL3xxx` trim warnings, no AOT
  diagnostics) and the resulting native binary ran and printed `PASS` with
  every assertion above holding.

The spike file was not committed (a throwaway proof, per this task's own
process) — its design is fully captured by the code block in "Decision
Outcome," above, which is what the real generator implementation will
produce.

### Unconfigured and argument-mismatch dispatch: ADR-0045/ADR-0048's existing rules apply unchanged, not a new generic-specific path

The spike's `null`-default and `TestDoubleNotConfiguredException` behaviors
are **not new semantics invented for this ADR** — they're
`ReturnConfig<TSlot>`'s existing `HasConfiguredValue`/`HasConfiguredException`
dispatch (ADR-0043) and ADR-0048's existing matcher-then-dispatch order,
applied to whichever bucket `__..._Bucket<T>()` selected. Once the bucket
lookup/cast (the only part of dispatch this ADR actually adds) resolves to
a concrete `__..._State<T>` instance, everything downstream — matcher
evaluation against that bucket's `Match<TParam>?` fields, the
configured-value/configured-exception/configuration-required-or-default
branch, call counting — is the same generated dispatch shape every other
eligible member already has, reading from the bucket's fields instead of
the member's own direct fields. No new "generic member" dispatch branch,
no new exception type, no new fallback rule.

`Task<T?> GetContextDataAsync<T>(...)`'s real return type is a nullable
reference type (`T?`, `where T : class`), so — same as any other
nullable-reference-returning member today — it has a real deterministic
default (`null`) and is **not** configuration-required by ADR-0045's own
rule; the spike's `GetContextDataAsync<T>` member proved exactly that
composes correctly with the bucket mechanism. The spike's second member
(`GetRequiredDataAsync<T> : Task<T>`, non-nullable) exists purely to prove
ADR-0045's *other* branch (configuration-required, throws) also composes
correctly — `IConversationalContextManager` itself has no non-nullable
generic-return member today, so that branch isn't exercised by the real
interface, only by the spike's own completeness check. Either disposition
— deterministic-default fallback or configuration-required — is decided by
the existing ADR-0045 rule applied to `TSlot` (here, `Task<T?>` or
`Task<T>` for whatever `T` the bucket closed to), completely unchanged by
this ADR.

### Requirement: independent state per closed `T`

A `Configure<T>()` call for one closed `T` must never affect another's
configured value, matchers, or call log — guaranteed structurally by the
bucket design (each closed `T` gets its own `__..._State<T>` instance;
there is no shared mutable state between buckets other than the outer
`Dictionary<Type, object>`'s own entries, which the `lock` in
`__..._Bucket<T>()` protects during lookup/creation only).

### State-holder lifetime and concurrency

- **`lock`-guarded bucket lookup/creation**, matching the existing
  per-member call-log locking pattern (ADR-0048) rather than introducing a
  new concurrency primitive. A plain `Dictionary<Type, object>` under a
  `lock`, not `ConcurrentDictionary<Type, object>` — this repo has no
  existing precedent for `ConcurrentDictionary` and ADR-0048's own call
  logs use plain `lock`-guarded `List<T>`s, so this mirrors established
  practice rather than introducing a new one.
- **Allocated lazily on first use of a given closed `T`** (`Configure<T>()`,
  `Verify<T>()`, or the real interface call, whichever happens first) —
  not pre-allocated for every closed `T` a member's declared constraints
  might permit (unbounded, and unknowable ahead of time regardless).
- **Repeated use of the same closed `T`** reuses the same bucket instance
  — `Configure<T>()` called twice for the same `T` is last-configuration-
  wins, identical to every other `ReturnConfig<T>` write today (ADR-0043
  Amendment 7).
- **No new disposal ownership.** Nothing stored in a bucket is
  `IDisposable`-tracked or owned by `Compono.TestDoubles` — same as every
  other configured value today (a consumer-supplied `Returns(...)` value's
  lifetime is the consumer's concern).
- **Configuration concurrent with invocation remains unsupported**,
  matching every other `Compono.TestDoubles` member today — this ADR adds
  no new thread-safety guarantee beyond "safe to look up/create a bucket
  concurrently," not "safe to `Configure()` and dispatch the same closed
  `T` from different threads at the same time."

### Invariant: `Dictionary<Type, object>` is a runtime identity/index only — it never becomes an erased mocking runtime

Stated explicitly, as a standing constraint on every future change to this
mechanism, not just its first implementation:

- `Dictionary<Type, object>` exists for exactly one purpose — mapping a
  runtime `Type` token to the one `__..._State<T>` instance that closed
  `T` owns. It stores no configuration, matcher, or call-log state
  directly; those all live as strongly-typed fields on the state class
  itself.
- Every read or write of configuration, matcher, return, or call-log state
  happens through the state class's own strongly-typed fields, reached via
  an ordinary generic-type cast immediately after the bucket lookup — never
  through the `object`-typed bucket value itself, and never boxed/unboxed
  beyond that one cast.
- **No `MakeGenericType`, no `Activator.CreateInstance`, no
  `MethodInfo.Invoke`/`DynamicInvoke`, no expression-tree compilation, and
  no `Unsafe`-based unboxing anywhere in this mechanism** — every closed
  `T` this mechanism ever touches is written out literally at some real
  call site in consumer code, which is what keeps the whole design
  reflection-free and AOT-safe (the Compiler/AOT Spike above is the proof,
  not just the design intent). Any future change to this mechanism that
  would require one of these to reach a closed `T` it can't otherwise see
  is a different mechanism and needs its own ADR, not an extension of this
  one.

### Positive Consequences

- Closes the real trivia-platform gap this ADR was authored to answer:
  `IConversationalContextManager` (and any interface shaped like it) can
  move from `Compono.NSubstitute` to `Compono.TestDoubles` without losing
  real test behavior.
- The whole-interface-rejection consequence resolves as a byproduct — no
  separate ADR-0045-style fallback design was needed (see "Considered
  Options," Scope).
- Zero change to any currently-generated interface's output — this is a
  pure capability addition, not a breaking or behavior-altering change to
  anything ADR-0044/ADR-0045/ADR-0048 already ship.
- Proven, not assumed: the storage mechanism survived a real Native AOT
  publish-and-run before this ADR was written, not just a paper design.

### Negative Consequences

- **New per-member runtime state shape** (a nested generic class plus a
  `Dictionary<Type, object>` bucket) — more generated surface per eligible
  member than any other shape this repo generates today. Mitigated by
  scoping tightly (single type parameter, direct-or-`Task`/`ValueTask`-
  wrapped return only) rather than building a fully general mechanism.
  Judged worth it given generator code, not consumer-authored code — the
  complexity is invisible to whoever writes `Configure<T>().Returns(...)`.
  - This is a decision that touches previously-decided ADR-0044/ADR-0048
  territory, not a pure extension — `docs/adr/README.md`'s index should
  record this as evidence-based reopening, and ADR-0044 gets a new dated
  Amendment (see "Links") recording exactly what changed and why, per this
  repo's own Amendment mechanic (ADR-0042 Amendment 2's own precedent).
- Deferred, evidence-gated boundaries (deeper-nested `T`, multi-type-
  parameter self-referencing returns) mean a future real-world interface
  could still hit an unsupported shape and need its own follow-up ADR —
  named explicitly in "Considered Options" rather than silently left as an
  unexplained gap.

### Amendment 1 (2026-08-23) — `T?` requires `T` constrained to a reference type

Codex review, PR #107 round 8, flagged that `Task<T?> Get<T>() where T :
struct` (likewise `ValueTask<T?>`) reports `CMP0031`, not the
`Configure<T>()`/`Verify<T>()` surface — because for a value-type `T`, C#
represents `T?` as the distinct generic type `System.Nullable<T>`, not as
`T` with a nullable *annotation* (nullable annotations apply only to
reference types). The eligibility check's `T?`/`Task<T?>`/`ValueTask<T?>`
recognition compares the return position directly against the method's
own `T` symbol, which a `Nullable<T>`-wrapped value never equals — so this
falls through to the same whole-interface `CMP0031` every other
unrecognized shape gets, safely, not broken generated code.

This was never actually evidenced: every real trivia-platform call site
this ADR's own evidence table cites (`GetContextDataAsync<T>` and its
siblings) is `where T : class`. The "Scope boundary" section above and
this ADR's target API were written using `T?` loosely, without stating
the reference-type-only precondition explicitly — clarified here rather
than treated as a design gap to close: per ADR-0029's evidence discipline,
recognizing `Nullable<T>` would be a genuinely new, unevidenced capability
(a distinct runtime representation, not just a wider pattern match), not
a bug fix to the shape actually designed and spiked. `docs/packages/compono-testdoubles.md`
and `docs/reference/diagnostics.md` corrected to state the constraint
explicitly. A value-type-constrained self-referencing `T?`/`Task<T?>`/
`ValueTask<T?>` return remains a named, out-of-scope shape for a future
ADR if real evidence ever surfaces it — the same disposition already
applied to deeper nesting and multi-type-parameter returns above.

## Links

- [ADR-0044](0044-compono-testdoubles-v2-overloads-generics-verification.md) —
  Requirement 2, whose "generic method whose return type depends on its
  own type parameter... stays diagnosed and unsupported" and "no per-
  closed-generic-instantiation configuration" Non-Goal this ADR reopens
  and supersedes for the specific evidenced shape above (single type
  parameter, direct-or-wrapped return only). Everything else Requirement 2
  decided — generic methods independent of their own type parameter,
  `ReturnConfig<T>`'s unchanged shape, the discriminator-arity rules — is
  unaffected. See that ADR's new Amendment 19.
- [ADR-0048](0048-testdoubles-argument-matching-and-call-verification.md) —
  the `Match<T>`/`CallVerifier` machinery this ADR reuses directly rather
  than reinventing; also the precedent for scoping a capability tightly to
  real evidence and naming the deferred boundary explicitly rather than
  guessing at general support.
- [ADR-0045](0045-testdoubles-configuration-required-members.md) — the
  member-scoped-exclusion pattern considered and found unnecessary here
  (see "Considered Options," Scope) because this ADR makes the member
  itself configurable rather than merely non-poisoning.
- [ADR-0043](0043-compono-generated-test-doubles-design.md) — the v1
  design (`ReturnConfig<T>`/`ReturnConfigBuilder<T>`) this ADR extends
  with a new storage shape for exactly the closed-generic case; v1's
  existing non-generic single-slot-per-member design is untouched.
- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) Amendment
  2 — the evidence-reopens-Non-Goals policy this ADR is a direct
  application of.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the evidence-over-prediction discipline behind this ADR's scope
  boundaries (single type parameter, direct-or-wrapped return only,
  `SetContextDataAsync<T>` left untouched).
- [ADR-0001](0001-source-generation-first.md) — the no-reflection/AOT-
  verification standard this ADR's spike satisfies.
- `test/Compono.TestDoubles.AotSmokeTest` — the existing publish-and-run
  harness this ADR's pre-drafting spike mirrored; implementation should
  add a real (generator-driven, not hand-written) case here once this ADR
  is `Accepted`.
- [PLAN-0049](../plans/0049-testdoubles-generic-return-closed-instantiation-configuration-impl-plan.md) —
  the implementation plan for this ADR.
