# [ADR-0048] Compono.TestDoubles: Argument Matching and Argument-Aware Call Verification

**Status:** Accepted

**Date:** 2026-08-21

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

Dogfooding `Compono.TestDoubles` against `ncipollina/trivia-manager`'s real
test suite (`docs/adr/0002-staged-migration-to-compono.md`/
`docs/plans/0002-staged-compono-migration.md` in that repo, Stage 3)
surfaced a real, evidenced gap, refined across two design passes before
implementation:

- **Argument-matched response configuration is real, but simpler than
  first assumed.** Every real site configures exactly one response per
  member per test, guarded by an equality/predicate check on one or more
  arguments (the rest wildcarded) — e.g. a repository substitute returns a
  specific player only when called with that player's own `cognitoSub`.
  No real site configures two different responses on the same
  member/instance differentiated by arguments.
- **Argument-filtered call verification is heavily evidenced** — 19 real
  `Match.Is<T>(predicate)` sites, all inside `Received(1)`/`DidNotReceive()`,
  across nearly every substituted domain interface.
- **Call-order verification has zero real evidence** — a direct search
  found no `Received.InOrder`-equivalent call site anywhere in the repo.
  Stays a Non-Goal (see below).
- **None of the 19 `Match.Is` sites targets an overloaded member** —
  verified against the real production interface declarations
  (`IPlayerRepository`, `IModerationRepository`, `IGrammarManager`, etc.),
  not inferred. This turns out to matter architecturally, not just as a
  scope note — see "Overload-discriminator interaction" below.

[ADR-0042](0042-compono-owned-source-generated-test-doubles.md)'s
Amendment 2 already settled the *classification* question this evidence
raises: a real, evidenced `Compono.NSubstitute`-vs-`Compono.TestDoubles`
gap is a roadmap candidate regardless of how narrow it looks in isolation.

**Pre-1.0 framing.** `Compono.TestDoubles` has shipped in preview but is
intentionally still pre-1.0. Compatibility is treated as "preserve
existing behavior when it does not materially damage the better API," not
"preview semantics may never change." In practice, this ADR ends up
needing none of that latitude — see "Overload-discriminator interaction":
the design that survived a real compiler spike doesn't touch ADR-0044's
existing surface at all.

## Decision Drivers

- ADR-0042 Amendment 2's corrected policy: a real, evidenced
  `Compono.NSubstitute`-vs-`Compono.TestDoubles` gap is a roadmap
  candidate regardless of how narrow it looks in isolation.
- Pre-1.0 permission to improve the API the evidence actually supports —
  not permission to speculatively clone the rest of NSubstitute, and not
  a license to skip proving a design choice that looks obviously safe.
  "Do not assume, run the compiler" applied directly to this ADR's own
  first draft and changed its architecture.
- [ADR-0001](0001-source-generation-first.md)'s no-reflection-by-default
  posture and this repo's AOT-safety precedent.
- Simplicity over generality where the evidence doesn't demand generality:
  a single configured response per member (not a chain), and a scope
  boundary (non-overloaded members only) rather than a combinatorial
  matcher/discriminator unification.

## Considered Options

### Requirement scope

1. **The two evidenced capabilities: argument-matched response
   configuration, argument-filtered call verification.** Matches the real
   inventory above.
2. **All three originally drafted capabilities, including call-order
   verification.** Rejected: zero real call sites; designing `InOrder`
   semantics now would mean inventing behavior against a hypothetical
   example. Deferred to its own future ADR if real evidence ever emerges.

### Overload-discriminator interaction — resolved by a real compiler spike, not by argument

ADR-0044 Requirement 1 made every `Configure()`/`Verify()` argument on an
overloaded member a **pure, value-ignored overload discriminator**. This
ADR's first draft proposed giving every generated parameter (overloaded or
not) type `Compono.Match<T>` with an implicit `T -> Match<T>` equality-matcher
conversion, reasoning that overload *selection* (compile-time, by
parameter type) and argument *matching* (runtime, by value) were
orthogonal concerns that wouldn't interfere with each other.

**That reasoning was wrong, and a real compiler spike proved it before any
code was written.** A small standalone project defined six representative
overload-parameter-type families, each as both a plain (unwrapped)
overload pair and an `Match<T>`-wrapped pair, and attempted to compile a
call through the implicit conversion for each:

| Family | Plain (baseline) | `Match<T>`-wrapped |
|---|---|---|
| `M(string)` / `M(object)` | unambiguous | compiles (resolves to `Match<string>`) |
| `M(IEnumerable<string>)` / `M(string[])` | unambiguous | **`CS0121` ambiguous** |
| `M(Base)` / `M(Derived)` | unambiguous | **`CS0121` ambiguous** |
| `M(int)` / `M(long)` | unambiguous | **`CS0121` ambiguous** |
| `M(string?)` / `M(object?)` | unambiguous | compiles |

Three of five realistic overload-parameter-type families fail to compile
under the wrapped design, and the two that compile aren't structurally
distinguishable in advance from the three that don't (both are
"identity-conversion-to-one-candidate vs. standard-conversion-to-the-
other," yet the compiler's tie-break resolves them differently) — there is
no reliable per-family rule to design around. Per this ADR's own governing
instruction, the response to an unreliable spike is to change the API
shape, not patch individual overload families.

1. **Scope the `Match<T>` mechanism to non-overloaded members only
   (chosen).** When a member has exactly one real overload, there is no
   competing candidate for the compiler to be ambiguous against — verified
   with the same spike project: a real multi-parameter, non-overloaded
   member with mixed literal/`Match.Any`/`Match.Is` compiled and dispatched
   correctly on the first attempt. An overloaded member keeps ADR-0044's
   exact discriminator-only signature, completely untouched. This is not
   a compatibility compromise — it's the only design proven to compile
   reliably, and it happens to align exactly with the evidence (zero real
   trivia-manager argument-matching site targets an overloaded member).
2. **Give overloaded members a second, separately-named matching surface**
   (e.g. `ConfigureMatching().Speak(...)`). Rejected: no real evidence any
   overloaded member needs argument matching, and it reintroduces exactly
   the "second, parallel API surface" this ADR otherwise avoids, for a
   capability nothing requires yet.
3. **Keep the original wrapped-everywhere design and special-case the
   ambiguous families.** Rejected per this ADR's own governing
   instruction — an unreliable mechanism isn't fixed by patching the
   specific families a spike happened to catch; a real interface not in
   the spike's sample could hit the same failure with no advance warning.

**Consequence: this ADR does not modify, break, or supersede any part of
ADR-0044's existing generated surface.** Every overloaded member — the
scenario ADR-0044 Requirement 1 designed for — generates identically
before and after this ADR. See ADR-0044 Amendment 18.

### Response-selection model

1. **Single slot, one configured response per member (chosen).** New
   per-parameter matcher storage (see "Field ownership" below) sits
   alongside the existing `ReturnConfig<T>` slot; a second `Configure()`
   call on the same member overwrites the matchers and value/exception,
   exactly like today's single-value overwrite. Matches the evidence
   exactly — no real site needs more than one configured response per
   member.
2. **Ordered, append-only response chain, last-match-wins.** Rejected:
   unevidenced, and introduces a real new failure mode (overlapping
   matchers silently shadowing each other by registration order) a
   single-slot design doesn't have.

### Field ownership — `ReturnConfig<T>` is unchanged

`ReturnConfig<T>` (`HasConfiguredValue`/`ConfiguredValue`/
`HasConfiguredException`/`ConfiguredException`/`CallCount`) gets **no new
members**. Matcher storage and the call log are separate, per-member
**generated fields on the double class itself**, sitting next to the
existing `ReturnConfig<T>` field — not inside it, and not a single
arity-generic core type (a member's real parameter types vary in count and
shape; a core type can't statically hold an arbitrary tuple of them
without boxing or reflection). Concretely, per eligible member:

- The existing `ReturnConfig<T>` field, unchanged.
- One `Match<TParam>?` field per real parameter — **not** an extracted
  `Func<TParam, bool>?`. Generated code outside the `Compono` assembly can
  only reach a *public* member of `Match<T>` (this is the same
  cross-assembly-accessibility class of defect ADR-0044 already had to
  solve for `ReturnConfig<T>`'s own internal/public field split — see its
  Amendment 3), so the field stores the whole `Match<TParam>` value and
  dispatch calls its public `Matches(TParam)` (see "`Match<T>`'s shape"
  below) rather than reading out a delegate. `Match<T>` being a
  `readonly struct` means "no matcher configured for this parameter" needs
  its own representable state distinct from any real configured matcher
  (including `Match.Any<T>()`, which is itself a valid, deliberately-chosen
  matcher, not the same thing as "nothing was configured") — the
  System.Nullable`1` wrapper (`Match<TParam>?`) gives that for free: `null`
  means unconfigured, `HasValue` with any `Match<TParam>` (including one
  built by `Match.Any<TParam>()`) means configured. Dispatch treats both
  "unconfigured" and "configured via `Match.Any`" identically as
  always-matching, which is the correct behavior in both cases — they only
  differ in whether a `Configure()`/`Verify()` call happened at all, which
  dispatch doesn't need to distinguish.
- A `lock`-guarded call log: a generated `List<(T1, T2, ...)>` (or
  equivalent per-member tuple/record shape) appended to on every
  invocation, only for members eligible for argument-aware behavior.

### `Match<T>`'s shape — public `Matches`, no public delegate, no closure for the common cases

`Match<T>` exposes exactly one public operation generated code needs:

```csharp
public readonly struct Match<T>
{
    private enum Kind : byte { Equality, Any, Predicate }

    private readonly Kind _kind;
    private readonly T? _value;              // used only when Kind == Equality
    private readonly Func<T, bool>? _predicate; // used only when Kind == Predicate

    private Match(Kind kind, T? value, Func<T, bool>? predicate)
    { _kind = kind; _value = value; _predicate = predicate; }

    public static implicit operator Match<T>(T value) => new(Kind.Equality, value, null);

    public static Match<T> Any() => new(Kind.Any, default, null);
    public static Match<T> Is(Func<T, bool> predicate) => new(Kind.Predicate, default, predicate);

    /// <summary>The one operation generated dispatch/verification code calls.</summary>
    public bool Matches(T value) => _kind switch
    {
        Kind.Any => true,
        Kind.Equality => EqualityComparer<T>.Default.Equals(_value!, value),
        Kind.Predicate => _predicate!(value),
        _ => false
    };
}
```

No `Predicate`/delegate accessor is public — `Matches(T)` is the entire
generated-code-facing surface, so `Match<T>`'s internal representation (this
three-case `Kind` shape, or any future alternative) stays free to change
without becoming a breaking change to generated output's compile-time
dependency on `Match<T>`. This also fixes an efficiency claim the first
draft got wrong: a **literal** argument (`Configure().Member(player.CognitoSub!)`,
by far the common case in real trivia-manager call sites) no longer
allocates a closure at all — it's `Kind.Equality` with the value stored
directly, compared via `EqualityComparer<T>.Default` inside `Matches`, the
same allocation profile v1/v2 already has for any other configuration
value. `Match.Is<T>(predicate)` still captures whatever closure its own
caller-supplied lambda naturally does — that cost is the caller's, not
something this design adds on top of it. `Match.Any<T>()` allocates nothing
beyond the struct itself.

### Call-recording storage

1. **Per-member, generator-emitted strongly-typed call log (chosen).**
   Real, unboxed parameter types, no reflection — consistent with every
   prior `Compono.TestDoubles` decision.
2. **Boxed `object[]` argument storage with runtime `Func<object[], bool>`
   matchers.** Rejected: reintroduces boxing and loses compile-time
   argument-type safety, against this repo's established AOT-safety
   precedent.

### Generic methods (`ILogger<TState>.Log<TState>(...)`, ADR-0044's own canonical example)

A per-member call log cannot hold `TState` — it exists only per closed
invocation, not per member declaration. trivia-manager's own `ILogger<T>`
usage is exclusively pure filler (`Composer.Create(...).Create<ILogger<T>>()`
for constructor-guard tests) — never configured, never verified.

1. **Scope argument-awareness to members with no unclosed method-type-
   parameter in their real parameter types (chosen).** Exactly parallel to
   the overload scoping above — the same underlying condition ("can a
   single, unambiguous, fully-closed-type extension signature exist for
   this member") governs both. A generic method whose parameters reference
   its own type parameter keeps exactly its current v1/v2 shape:
   argument-independent dispatch, `CallCount`-only `Verify()`, no
   `Configure()`-side matcher, no call log. **This ADR cannot make any
   ADR-0044-supported generic method stop generating** — a scoped-out
   member's codegen path is byte-for-byte what it is today, not a new path
   that happens to produce the same result.
2. **A specialized erased/boxed recording path for method-generic
   parameters only.** Rejected: no real evidence justifies the added
   complexity for a capability nothing in trivia-manager needs.
3. **Per-closed-generic-instantiation storage.** Rejected for the same
   reason ADR-0044 Requirement 2 already rejected it.

### Matching API shape

1. **The eligible member's `Verify()` extension takes the same `Match<T>`-
   per-parameter shape as `Configure()`, and returns the existing,
   unchanged `CallVerifier` directly (chosen).** No `.Matching(...)` step,
   no second mechanism: the generated extension counts call-log entries
   satisfying every matcher and constructs `CallVerifier(filteredCount,
   description)` — the exact same terminal type `Never`/`Once`/
   `Exactly(n)` already use for the unfiltered case. `CallVerifier` never
   needs access to the call log at all.
2. **A `.Matching(predicate)` step between `Verify().Member()` and
   `.Once()`.** Rejected: requires `CallVerifier` (or an intermediate
   wrapper) to retain access to the call log after construction,
   reopening exactly the architectural question this ADR's review caught
   — "`CallVerifier` cannot perform matching after construction because it
   no longer has access to the call log." Avoided entirely by folding
   matching into the same per-member extension that already knows where
   the log lives.

## Decision Outcome

Chosen: requirement scope 1 (two capabilities), `Match<T>` scoped to
non-overloaded, non-open-generic-parameter members only, single-slot
response model, matcher/call-log fields generated on the double class
(not inside `ReturnConfig<T>`), matching folded directly into `Verify()`'s
existing per-member extension shape.

### Generated C# — representative shapes

```csharp
// A. Ordinary, non-overloaded, has parameters - the real trivia-manager shape.
internal sealed class IPlayerRepository_h1_Double : IPlayerRepository
{
    internal global::Compono.ReturnConfig<global::System.Threading.Tasks.Task<global::Player?>> __getPlayerByCognitoSub_9f8e;
    private global::Compono.Match<string>? __getPlayerByCognitoSub_m_cognitoSub;
    private global::Compono.Match<string>? __getPlayerByCognitoSub_m_gameName;
    private global::Compono.Match<global::System.Threading.CancellationToken>? __getPlayerByCognitoSub_m_ct;
    private readonly global::System.Collections.Generic.List<(string CognitoSub, string GameName, global::System.Threading.CancellationToken Ct)> __getPlayerByCognitoSub_calls = [];
    private readonly object __getPlayerByCognitoSub_lock = new();

    Task<Player?> IPlayerRepository.GetPlayerByCognitoSubAsync(string cognitoSub, string gameName, CancellationToken ct)
    {
        lock (__getPlayerByCognitoSub_lock) { __getPlayerByCognitoSub_calls.Add((cognitoSub, gameName, ct)); }
        var __matches =
            (__getPlayerByCognitoSub_m_cognitoSub is not { } __m1 || __m1.Matches(cognitoSub)) &&
            (__getPlayerByCognitoSub_m_gameName is not { } __m2 || __m2.Matches(gameName)) &&
            (__getPlayerByCognitoSub_m_ct is not { } __m3 || __m3.Matches(ct));
        return __matches && __getPlayerByCognitoSub_9f8e.HasConfiguredException ? throw __getPlayerByCognitoSub_9f8e.ConfiguredException
            : __matches && __getPlayerByCognitoSub_9f8e.HasConfiguredValue ? __getPlayerByCognitoSub_9f8e.ConfiguredValue
            : /* ADR-0045 required-config / default, unchanged - an unmatched real call is treated
                 identically to an unconfigured member */;
    }
}

internal static class IPlayerRepository_h1_DoubleConfiguration
{
    public static global::Compono.ReturnConfigBuilder<Task<Player?>> GetPlayerByCognitoSubAsync(
        this global::IPlayerRepository_h1_Double self,
        global::Compono.Match<string> cognitoSub, global::Compono.Match<string> gameName, global::Compono.Match<CancellationToken> ct)
    {
        // Stores the Match<T> value itself - Predicate/Matches internals stay encapsulated in
        // Compono.Match<T>, only its public Matches(T) is ever called from generated code (see
        // "Match<T>'s shape" above; this is the same cross-assembly-accessibility fix ADR-0044
        // Amendment 3 already made for ReturnConfig<T>).
        self.__getPlayerByCognitoSub_m_cognitoSub = cognitoSub;
        self.__getPlayerByCognitoSub_m_gameName = gameName;
        self.__getPlayerByCognitoSub_m_ct = ct;
        return new(ref self.__getPlayerByCognitoSub_9f8e);
    }
}

// Usage - literal implicitly converts to Match<string> as an equality matcher, matching real syntax:
repo.Configure().GetPlayerByCognitoSubAsync(player.CognitoSub!, Match.Any<string>(), Match.Any<CancellationToken>()).Returns(player);
```

```csharp
// B. Overloaded member - byte-for-byte ADR-0044's existing shape, completely untouched.
public static global::Compono.ReturnConfigBuilder<IResponseBuilder> Speak(this IResponseBuilder_a1b2c3d4_Double self, string? text) => new(ref self.__speak_9f8e);
public static global::Compono.ReturnConfigBuilder<IResponseBuilder> Speak(this IResponseBuilder_a1b2c3d4_Double self, params ISsml[] ssml) => new(ref self.__speak_2c1d);
// responseBuilder.Configure().Speak(string.Empty) still means "the Speak(string?) overload,
// argument value ignored" - exactly as ADR-0044 shipped it.
```

```csharp
// C. Zero-parameter member - unchanged.
service.Configure().GetPlayer().Returns(player);
service.Verify().GetPlayer().Once();
```

```csharp
// D. Generic method, scoped out - unchanged, CallCount-only.
void ILogger.Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> fmt)
    => __log_CallCount++;
// Verify().Log().Once() still works; no matcher/call-log surface generated for this member.
```

```csharp
// Argument-filtered Verify - same Match<T> shape as Configure(), no .Matching() step.
internal static class IPlayerRepository_h1_DoubleVerification
{
    public static global::Compono.CallVerifier GetPlayerByCognitoSubAsync(
        this global::IPlayerRepository_h1_DoubleVerifier self,
        global::Compono.Match<string> cognitoSub, global::Compono.Match<string> gameName, global::Compono.Match<CancellationToken> ct)
    {
        int count;
        lock (self.Instance.__getPlayerByCognitoSub_lock)
        {
            count = 0;
            foreach (var call in self.Instance.__getPlayerByCognitoSub_calls)
                if (cognitoSub.Matches(call.CognitoSub) && gameName.Matches(call.GameName) && ct.Matches(call.Ct))
                    count++;
        }
        return new(count, "GetPlayerByCognitoSubAsync");
    }
}

// repo.Verify().GetPlayerByCognitoSubAsync(Match.Is<string>(s => s == cognitoSub), Match.Any<string>(), Match.Any<CancellationToken>()).Once();
```

(No `InOrder` sample — out of scope, zero evidence.)

### Allocation and concurrency model

- **Configuration-time:** a literal argument (the common case) allocates no
  closure at all — `Match<T>`'s internal `Kind.Equality` representation
  stores the value directly, compared via `EqualityComparer<T>.Default`
  inside `Matches`. `Match.Any<T>()` allocates nothing beyond the struct
  itself. Only `Match.Is<T>(predicate)` allocates a delegate — the caller's
  own lambda, not something this design adds on top of it.
- **Invocation-time:** one call-log entry appended per call, only for
  members eligible for argument-aware behavior.
- **Call-log lifetime:** grows for the double's lifetime (one test
  method), never trimmed — matches every real site's scale.
- **Thread safety:** append and the filtered-count read both use the
  **same** `lock` — a filtered count is a snapshot-and-count under that
  lock, not a separate unlocked read, avoiding a collection-enumeration
  race for negligible cost at unit-test call volumes. `Configure()` stays
  unsynchronized against concurrent invocation, unchanged from v1/v2.
  Concurrent verification while calls are still in flight is
  unsupported/undefined, matching today's `CallCount` semantics.

### Positive Consequences

- Closes exactly the trivia-manager evidence.
- **Zero interaction with ADR-0044's existing overloaded-member surface**
  — proven safe by a real compiler spike rather than assumed, and the
  resulting scope boundary is evidence-aligned for free (no real
  argument-matching site is on an overloaded member).
- `ReturnConfig<T>`/`CallVerifier` stay exactly as they are; the new
  capability is additive generated state next to them, not a redesign of
  either.

### Negative Consequences

- Argument matching and argument-filtered verification are unavailable on
  an overloaded member — accepted, unevidenced, and would need its own
  design pass if real evidence ever appears (option 2 above is the
  starting point for that future pass, not adopted now).
- Materially larger generated-code volume per eligible member (matcher
  fields, a call-log type, a list) versus v1/v2's single scalar field —
  accepted, same "real generated-code volume is an expected cost"
  precedent ADR-0044 already accepted for overloads.

## Pros and Cons of the Options

### `Match<T>` scoped to non-overloaded members (chosen)

- Good, because it's the only design a real compiler spike showed
  compiles reliably.
- Good, because the resulting scope boundary matches the evidence exactly
  — no real loss relative to what trivia-manager needs.
- Bad, because a future overloaded-member argument-matching need would
  require its own design pass.

### `Match<T>` on every parameter, including overloaded members (original draft)

- Good, because it would have been one uniform mechanism with no scope
  boundary.
- Bad, because it doesn't reliably compile — proven, not assumed.

## Amendment 1 (2026-08-22): eligibility gained three more exclusions during implementation

This ADR's Decision Outcome and "Overload-discriminator interaction"/"Generic
methods" sections describe eligibility as exactly two conditions: not part of
an overload set, and no real parameter referencing the member's own open
method-type-parameter. Two post-acceptance Codex review rounds against the
real implementation (`src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs`)
found three more real, necessary exclusions this ADR never anticipated —
`Equals(string value)` reads as satisfying the documented two-condition
contract but in fact only gets the argument-independent path, which is a real
documentation/implementation mismatch, not merely an omission. The complete,
current rule, as `isEligibleForMatching` actually computes it, is five
conditions, all required:

1. Not part of an overload set (original).
2. No real parameter references the member's own open method-type-parameter
   (original).
3. **No real parameter is a ref-like type** (`Span<T>`, or any other `ref
   struct`). A ref-like type is an ordinary by-value parameter — distinct
   from the `ref`/`out`/`in` *passing-mode* restriction ADR-0042 already
   excludes — so it dispatches fine via the existing argument-independent,
   value-discarded path, but can never be used as a generic type argument
   (`Match<Span<int>>?`, or as an element of the call-log tuple) — `CS0306`.
4. **No derived-auxiliary-name collision.** A member's derived field names
   (the call-log/lock/per-parameter-matcher fields the template splices from
   `FieldName`) must not collide with any name reserved for a *literal*
   top-level field, an overload discriminator suffix, or **another
   candidate's own derived names** — the last of these needed a real
   two-pass fix (reserve every prospective eligible member's derived names
   first, then exclude any member whose names were claimed more than once)
   because checking only against names already reserved *so far* in one
   linear pass missed same-round collisions between two derived names
   neither of which existed in the reservation set yet.
5. **`Equals` with exactly one parameter is excluded.** Its would-be
   `Match<T>`-typed extension shares real call-site arity with the inherited
   `object.Equals(object)` instance method (any `T` implicitly converts to
   `object`, boxing if necessary), and C# always prefers an applicable
   instance method over an extension method regardless of conversion cost —
   the generated extension would never actually be reachable.
   `ToString`/`GetHashCode`/`GetType` need no equivalent exclusion: they only
   collide with their `object` counterpart at arity zero, and eligibility
   already requires at least one real parameter, so an eligible member named
   one of those can never share arity with the inherited zero-arg version.

Every excluded member falls back to its existing v1/v2/ADR-0044 argument-
independent path — none of these three additions changes the "ineligible for
the enhancement, not unsupported" disposition every eligibility exclusion in
this ADR already has. This ADR's original Decision Outcome text stands as
written — conditions 1-2 were the complete, correct rule as understood at
acceptance time; this Amendment records what implementation evidence added,
per this repo's own Amendment mechanic. See
`docs/plans/0048-testdoubles-argument-matching-and-call-verification.md`'s
Notes section and `docs/packages/compono-testdoubles.md` for the
consumer-facing description of the same, now-corrected, five-condition rule.

## Links

- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) — original
  `Compono.TestDoubles` admission decision; Amendment 2 is the
  classification policy this ADR's existence applies.
- [ADR-0044](0044-compono-testdoubles-v2-overloads-generics-verification.md) —
  the `ReturnConfig<T>`/`CallVerifier`/per-overload-discriminator shape
  this ADR extends alongside, without modifying; see its Amendment 18.
- [ADR-0045](0045-testdoubles-configuration-required-members.md) — the
  unmatched-call fallback behavior this ADR's response-matching reuses
  unchanged.
- [ADR-0025](0025-compono-nsubstitute-package-design.md) — `Compono.NSubstitute`,
  the package this ADR's evidence came from wanting to leave behind for
  more of a real consumer's test suite.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the dogfooding-evidence discipline this ADR's Context follows.
- [docs/plans/0048-testdoubles-argument-matching-and-call-verification.md](../plans/0048-testdoubles-argument-matching-and-call-verification.md)
- `ncipollina/trivia-manager`, `docs/adr/0002-staged-migration-to-compono.md`
  Amendment 1 and `docs/plans/0002-staged-compono-migration.md`'s Stage 3
  section — the real evidence this ADR is grounded in.
