# [ADR-0061] `Compono.Options`: First-Class .NET Configuration/Options Testing Support

**Status:** Accepted

**Date:** 2026-09-07 (revised 2026-09-08 — dogfooding validation, `IOptionsSnapshot<T>`
semantics research, public object model, and registration/identity resolved,
plus source-disposal and concurrency final resolutions; see "Revision
(2026-09-08)" below Context and "Acceptance" at the end. `Accepted`
2026-09-08 — see "Acceptance.")

**Decision Makers:** Nick Cipollina, Claude (design deep dive)

## Context

`Microsoft.Extensions.Options`'s `IOptions<T>`/`IOptionsSnapshot<T>`/
`IOptionsMonitor<T>` is the standard, idiomatic way a modern .NET
application receives strongly-typed configuration. Any real application
composed with Compono that reads configuration this way needs its test
doubles composed the same way every other dependency is.

This ADR is the outcome of an admission investigation and reassessment —
[RESEARCH-0028](../research/0028-compono-options-configuration-admission-research.md) —
run against
[`docs/architecture/capability-admission.md`](../architecture/capability-admission.md)'s
Gate A/Gate B process, triggered by an explicit product-owner request and
then reassessed the same day against a sharpened version of that request
(composition ergonomics as legitimate value, not merely "wrap an
already-simple API"). Both gates cleared; see that research document for
the full evidence trail and per-slice decomposition (Configuration,
`IOptions<T>` alone, and Options validation were each investigated and
separately concluded *not* to need new capability). This ADR records the
problem this capability solves and the recommended architecture — it does
not re-derive RESEARCH-0028's evidence.

**The problem has two independent, evidenced dimensions**, neither of
which alone would have been sufficient (RESEARCH-0028 §1, §13):

1. **Correctness.** `IOptionsMonitor<T>`/`IOptionsSnapshot<T>` have no
   first-party test double. The community's own standard answer — a
   hand-rolled fake, most visibly
   [Ben Foster's widely-cited `TestOptionsMonitor<T>`](https://benfoster.io/blog/20200610-testing-ioptionsmonitor/) —
   is demonstrably incomplete: `Get(name)` ignores `name` entirely (named
   options silently broken), only one `OnChange` subscriber is ever
   honored (a plain field assignment, not a real multicast event), and the
   returned `IDisposable` is a no-op (`Mock.Of<IDisposable>()`) that
   doesn't actually unsubscribe anything.
2. **Composition coherence.** A Compono consumer whose SUT depends on
   `IOptions<T>` *and* `IOptionsMonitor<T>`/`IOptionsSnapshot<T>` for the
   same settings type has to hand-wire each one separately today, with
   nothing preventing them from silently drifting inconsistent. This is
   the same category of value `CompositionBuilder.Share<T>()`
   ([ADR-0056](0056-composition-builder-share-graph-wide-sharing.md)) and
   `Compono.Bogus` ([ADR-0027](0027-compono-bogus-package-design.md))
   were admitted on — composition-native, discoverable, consistent
   behavior a consumer would otherwise reinvent, slightly differently,
   every time.

**What "coherent" does and does not mean here** — the central semantic
question this ADR has to answer, not assume: `IOptions<T>`,
`IOptionsSnapshot<T>`, and `IOptionsMonitor<T>` are **not** interchangeable
views over one mutable value. A design that makes all three reactively
track one mutable value would be **more surprising than the real thing**,
not more correct — `IOptions<T>` genuinely doesn't change in production,
and a Compono fake that makes it change would silently mismatch the real
contract it claims to model. **Precise definition, settled by this
revision (see below):** "coherent" means all three interfaces originate
from **one explicitly test-configured source of truth** per settings type
— but each interface still exposes exactly its own real, distinct
observable contract over that source. A shared origin, not shared
behavior.

## Revision (2026-09-08): dogfooding validation, real `OptionsManager<T>` semantics, and the resulting object model

A follow-up design review (2026-09-08) found that this ADR's original
Snapshot mapping ("one Compono resolution stands in for one DI scope") was
a Compono-invented analogy asserted without checking Microsoft's actual
implementation, and that "single class vs. two" and the exact registration
identity/lifetime contract were left as open questions even though they
determine public semantics, not just implementation shape. This revision
resolves both **before** this ADR is fit for acceptance. Nothing in the
original Context's problem statement, the correctness findings (subscriber
exceptions, thread-safety posture), the Finding B boundary, or the
verification/scope decisions changed — only the Snapshot semantics, the
public object model, and the registration/identity contract, all
superseded by this section and "Decision Outcome" below.

### Dogfooding validation against real consumers

Per RESEARCH-0028's identified consumers, both were inspected directly
(read-only; neither repository was modified) for every real
`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` usage:

- **`alexa-vox-craft`** (`/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft`) —
  32 files reference `IOptions<T>`; **zero** reference
  `IOptionsSnapshot<T>`/`IOptionsMonitor<T>`. Two real test-profile
  registrations found:
  - `test/AlexaVoxCraft.MediatR.Tests/TestKit/MediatRTestProfile.cs`:
    `.Register<SkillServiceConfiguration>(_ => new SkillServiceConfiguration {...})`
    immediately followed by
    `.Register<IOptions<SkillServiceConfiguration>>(context => Options.Create(context.Resolve<SkillServiceConfiguration>()))`
    — **two separately-maintained registrations for the same settings
    type, in the same profile, with nothing enforcing they stay
    consistent** — exactly the coherence risk this ADR's Context
    identifies, concretely present in real code, today.
  - `test/AlexaVoxCraft.Smapi.Tests/TestKit/SmapiHttpTestProfile.cs`:
    `.Register<IOptions<SmapiDeveloperAccessTokenOptions>>(context => Options.Create(new SmapiDeveloperAccessTokenOptions { ClientId = context.Resolve<string>(), ... }))`
    — the file's own comment documents this shape exists **specifically
    because** `context.Resolve<SmapiDeveloperAccessTokenOptions>()`
    throws `CompositionException` (ADR-0052 Finding B, reproduced live in
    this real file) — the record type is never independently discovered
    as a root anywhere in the project.
- **`cosmere-tracker`** (`/Users/ncipollina/source/repos/ncipollina/cosmere-tracker`) —
  3 files reference `IOptions<T>`; **zero** reference
  `IOptionsSnapshot<T>`/`IOptionsMonitor<T>`.
  `test/Cosmere.Tracker.Shared.Tests/TestKit/Profiles/PersistenceTestProfile.cs`:
  `.Register<IOptions<DynamoDbOptions>>(() => Options.Create(new DynamoDbOptions {...}))`
  — a simpler case, an inline literal with no nested resolve at all, no
  Finding B involvement.

**Selected dogfooding validation target: `MediatRTestProfile.cs`'s
`IOptions<SkillServiceConfiguration>` registration.** Chosen over the
`Smapi`/`cosmere-tracker` cases because it's the one real, non-Finding-B-entangled
example of exactly the coherence risk this ADR exists to close — two
registrations for one settings type, wired by hand, with no structural
guarantee they agree. `Compono.Options` is expected to collapse these two
lines into one coherent registration backed by a single source, with the
consistency guarantee built in rather than hand-maintained. It is
**explicitly not** expected to improve the `SmapiHttpTestProfile.cs` case
— that one is Finding-B-shaped, and this ADR's design deliberately doesn't
touch Finding B (unchanged from the original ADR).

**Honest finding, not manufactured:** neither repository exercises
`IOptionsSnapshot<T>` or `IOptionsMonitor<T>` at all. There is **no real
dogfooding evidence for the Monitor/Snapshot correctness dimension** —
that half of this capability's justification rests entirely on external,
well-documented community-fake-defect evidence (Ben Foster et al.,
RESEARCH-0028 §7), not on friction observed in a real Compono consumer.
This doesn't weaken Gate A/Gate B (Gate B was satisfied by explicit
product-owner request, not dogfooding, exactly as `Compono.TUnit`/
`Compono.NUnit` were) but it does mean this design's Monitor/Snapshot
surface has not yet been pressure-tested against a real consumer's actual
usage pattern — recorded as a genuine gap, not glossed over.

### Real `IOptions<T>`/`IOptionsSnapshot<T>` semantics — researched, not assumed

Confirmed directly against
[dotnet/runtime's `OptionsManager.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Options/src/OptionsManager.cs)
(the concrete type behind both interfaces in real Microsoft code) and
[Options pattern - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/options):

- **`OptionsManager<TOptions>` implements *both* `IOptions<TOptions>` and
  `IOptionsSnapshot<TOptions>` — the same concrete class, not two related
  ones.** `IOptions<T>.Value` is literally `Get(Options.DefaultName)`;
  `IOptionsSnapshot<T>.Value`/`Get(name)` is the exact same code path.
  There is **no type-level behavioral difference between `IOptions<T>` and
  `IOptionsSnapshot<T>` at all.**
- Results are cached **per-instance**, keyed by name (`OptionsCache<TOptions>`).
  Calling `Get(name)` twice on the *same instance* returns the cached
  value both times — no recomputation.
- **`OptionsManager<TOptions>` itself has zero scope-awareness.** It
  doesn't know or care whether it's registered Singleton or Scoped.
- **The entire `IOptions<T>` vs. `IOptionsSnapshot<T>` behavioral
  difference is purely a consequence of DI *registration lifetime*, not
  anything in the type**: `IOptions<T>` is registered Singleton (one
  `OptionsManager<T>` instance, and therefore one cache, for the whole
  app's lifetime — hence "never changes"); `IOptionsSnapshot<T>` is
  registered Scoped (a *fresh* `OptionsManager<T>` instance, with a fresh,
  empty cache, per DI scope — hence "recomputed once per scope, fixed
  within it").

This is a materially better-grounded finding than this ADR's original
"one Compono resolution stands in for one DI scope" analogy — that
analogy turns out to be **exactly right**, but for a reason this ADR
didn't originally have evidence for: real Microsoft achieves "fixed
within a lifetime, fresh across lifetimes" purely by controlling *how many
instances exist and when they're constructed*, using one interchangeable
type. Compono already has an exact, existing, `Accepted` primitive for
controlling instance count within a graph: `CompositionBuilder.Share<T>()`
vs. an ordinary (non-shared) `Register<T>` factory. No new lifecycle
concept needs to be invented — this maps onto real Compono mechanics
already in production, not a Compono-specific fiction.

## Decision Drivers

- **Faithfulness over convenience.** A test double for a specific
  interface contract is only useful if its observable behavior matches
  that contract.
- **No reflection, no hidden state** ([ADR-0001](0001-source-generation-first.md)) —
  a hand-written, non-generated runtime package, the same shape
  `Compono.Http` already established.
- **Core `Compono` must never reference an integration package**
  ([design-principles.md](../architecture/design-principles.md)) — this
  package depends on `Microsoft.Extensions.Options` and core `Compono`
  only, never the reverse.
- **ADR-0052 Finding B is a hard boundary, not a design target.** Unchanged
  from the original ADR — see "ADR-0052 Finding B boundary" below.
- **Read naturally both inline and inside a profile.** Unchanged — see
  `MediatRTestProfile.cs`'s real usage above for what "naturally" means in
  practice.
- **Match real `OptionsMonitor<T>`'s actual robustness posture, not an
  imagined stricter one.** Unchanged from the original ADR (subscriber-throw,
  locking posture) — confirmed directly against source, not re-litigated
  in this revision.
- **Prefer existing Compono primitives over inventing a new lifecycle
  concept.** New this revision, directly from the `OptionsManager<T>`
  finding above: if real Microsoft achieves the `IOptions<T>`/
  `IOptionsSnapshot<T>` distinction purely through instance-count control,
  this design should too, using `Share<T>()`/plain `Register<T>` rather
  than inventing a Compono-specific "snapshot lifecycle."
- **Package-boundary discipline** ([ADR-0039](0039-future-extension-package-admission-gate-and-release-sequence.md)) —
  `Compono.DependencyInjection` remains the wrong home (unchanged,
  RESEARCH-0028 §12).

## Considered Options

**Consumer entry point** (unchanged from the original ADR — see "Why
Option 1" below, reasoning unchanged):
1. A single hand-written class per settings type, constructed directly by
   the test, wired into composition via one builder call.
2. Three independent builder extension methods, each registering one
   interface separately.
3. A universal auto-composing stage-4-6 provider.

**Public object model** (new this revision):
1. **One public source type per settings type**, holding the current
   default/named values and the change-notification machinery, directly
   implementing `IOptionsMonitor<T>` itself (the one genuinely live,
   singleton-shaped interface) plus a Compono-native mutation surface
   (illustratively, `.Set(...)`/named overloads). A small **internal**
   frozen-view type — never named by consumer code — implements
   `IOptions<T>`/`IOptionsSnapshot<T>`, constructed fresh from the
   source's current state each time one is produced.
2. **One public type implementing all three Microsoft interfaces at
   once** (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`
   simultaneously on one object).
3. **Three separate public types**, one per Microsoft interface, each
   independently wrapping a shared internal state object.

**`IOptions<T>`/`IOptionsSnapshot<T>` instance/identity model** (new this
revision, replacing the original "frozen at resolution" framing with a
mechanism, not just a behavior):
1. Register `IOptions<T>` **with `Share<T>()`** (one frozen-view instance
   for the whole composition graph — matches Singleton/"one
   `OptionsManager<T>` for the app's life"); register
   `IOptionsSnapshot<T>` as an **ordinary, non-shared** `Register<T>`
   factory (Compono invokes the factory fresh each time it's resolved,
   producing a new frozen-view instance per resolution — matches Scoped/
   "fresh `OptionsManager<T>` per scope").
2. Register both with `Share<T>()` (one frozen instance for the whole
   graph, no distinction between the two interfaces at all).
3. Register both as ordinary, non-shared factories (a fresh frozen view
   every single resolution, for both interfaces).

**Change-notification robustness, unconfigured-named-option behavior,
verification:** unchanged from the original ADR (see "Decision Outcome,"
carried forward below) — this revision found no new evidence requiring
either to change.

## Decision Outcome

**Chosen, per axis:** entry point — **Option 1** (unchanged); public
object model — **Option 1**; `IOptions<T>`/`IOptionsSnapshot<T>` identity
model — **Option 1**; change-notification robustness, unconfigured-named-option
behavior, and verification — all **carried forward unchanged** from the
original ADR (reasoning below, condensed; full reasoning is this ADR's
git history / the original 2026-09-07 text, superseded here per this
repo's "later fact gets its own dated update" convention rather than
silently rewritten).

### Entry point: Option 1 (unchanged reasoning)

Option 3 (universal auto-composing provider) is rejected: it would call
`context.Resolve<T>()` for an arbitrary `T` inside its own `TryProvide`,
hitting ADR-0052 Finding B exactly whenever `T` isn't independently
discovered elsewhere — the identical wall `SmapiHttpTestProfile.cs`
(above) already hits by hand. Option 2 (three independent registrations)
is rejected: it's exactly `MediatRTestProfile.cs`'s existing shape, and
doesn't solve the coherence problem at all. **Option 1**: the test
constructs a single per-settings-type source object directly, then one
builder call wires all three interfaces from it — avoids Finding B
entirely (the test supplies the value; nothing resolves `T` from inside a
factory), needs no new core extension point, reads identically inline and
inside a profile.

### Public object model: Option 1, chosen against your stated preference — with the reasoning that resolves it

You noted a strong preference against "one class implementing multiple
Microsoft interfaces merely because implementation can be shared." That
preference is correct **as a default heuristic** and this decision doesn't
override it casually — it resolves it against the specific evidence
above, which changes the premise: **`IOptions<T>` and `IOptionsSnapshot<T>`
are not two interfaces with different lifetime semantics that happen to
share implementation for convenience — in real Microsoft code they are
the exact same behavior, exposed through two interface names, with the
only real difference being how many instances of that one behavior exist**
(§"Real `IOptions<T>`/`IOptionsSnapshot<T>` semantics," above). Combining
them here is not a shortcut; it's matching the real architecture 1:1,
the same standard this ADR already holds every other fidelity decision to.

**`IOptionsMonitor<T>` is different in kind, not just in cardinality** — it
is a genuinely separate, live-reactive contract in real Microsoft code
too (`OptionsMonitor<T>`, a distinct class from `OptionsManager<T>`). This
design keeps it conceptually distinct: the one **public** type per
settings type is the *source* — the thing the test constructs, mutates,
and subscribes to — and it directly implements `IOptionsMonitor<T>`,
because Monitor's contract ("read the current live value, subscribe to
changes") is exactly what a mutable source naturally *is*, not a
retrofit. The frozen-view type behind `IOptions<T>`/`IOptionsSnapshot<T>`
is a small, **internal** implementation detail a consumer never names —
it's reached only through the standard interface types, the same way a
consumer today never thinks about `OptionsManager<T>`'s own concrete type
either. Option 2 (one type implementing all three at once, including
Monitor) is rejected for the reason you gave: Monitor's live semantics
and the frozen views' fixed semantics are genuinely different in kind, and
collapsing them into one object would obscure that distinction rather
than express it. Option 3 (three fully separate public types) is
rejected: it would either duplicate the frozen-view logic for `IOptions<T>`
and `IOptionsSnapshot<T>` (contradicting the finding that they're the same
behavior) or force an artificial public split between two things that are
genuinely one.

**Resulting public model:** one public type per settings type (name not
finalized) — call it conceptually the **Options source** — is what a test
constructs, configures, mutates, and passes to the one wiring call. It
directly implements `IOptionsMonitor<T>`. `IOptions<T>` and
`IOptionsSnapshot<T>` are satisfied by an internal frozen-view type the
wiring call constructs from the source; a consumer's code only ever sees
these as the ordinary Microsoft interfaces.

### `IOptions<T>`/`IOptionsSnapshot<T>` identity model: Option 1, built entirely on existing Compono primitives

Directly following from the `OptionsManager<T>` finding: the wiring call
registers `IOptions<T>` **via `Share<T>()`** — one frozen-view instance,
constructed once (capturing the source's value at that moment) and reused
for every subsequent request for `IOptions<T>` within the same graph,
matching Singleton/"one instance for the app's life." It registers
`IOptionsSnapshot<T>` as an **ordinary `Register<T>` factory, deliberately
without `Share<T>()`** — per Compono's own existing, unchanged
`StoreSharedValue` semantics (`src/Compono/CompositionContext.cs`), a
plain registration's factory is re-invoked on every resolution unless the
type is shared, so each `IOptionsSnapshot<T>` resolution naturally
produces a **fresh** frozen view capturing the source's *current* state at
that moment — matching Scoped/"fresh instance per scope," with "one
Compono resolution" now precisely and correctly standing in for "one DI
scope" **because Compono's own registration semantics already produce
exactly that shape**, not because this ADR invented a special case for it.

`IOptionsMonitor<T>` is registered the same way `IOptions<T>` is
(`Share<T>()` — one instance per graph) since it *is* the source object
itself, and the source only ever needs one identity per graph regardless
of how many places request it.

**This directly answers your registration/composition questions:**

- Repeated resolution of `IOptions<T>` returns the **same** frozen wrapper
  (shared).
- Repeated resolution of `IOptionsSnapshot<T>` returns a **new** frozen
  view **each time**, reflecting whatever the source's current state is
  at that moment (not shared) — matching real Scoped-per-request
  semantics as closely as a scope-free environment honestly can.
- `IOptionsMonitor<T>` has **stable identity** — the same source instance
  every time (shared), consistent with its real Singleton registration.
- An explicit `Register<IOptions<T>>(...)`/`Register<IOptionsSnapshot<T>>(...)`/
  `Register<IOptionsMonitor<T>>(...)` written by the consumer **after**
  `Compono.Options`'s own wiring call interacts through Compono's ordinary,
  unchanged first-registration-wins rule
  (`src/Compono/CompositionBuilder.cs`) — no special-cased precedence is
  invented for this package. A consumer who explicitly overrides one of
  the three still gets ordinary, predictable Compono behavior, just
  without this package's coherence guarantee for the overridden interface
  specifically.
- `Share<T>()`'s role: internal to how the wiring call registers
  `IOptions<T>`/`IOptionsMonitor<T>` — a consumer never calls `Share<T>()`
  themselves for these types; the package uses the primitive on the
  consumer's behalf.
- Profiles: unaffected — the wiring call is one more ordinary
  `CompositionBuilder` call, exactly like `MediatRTestProfile.cs`'s
  existing `Register<IOptions<T>>(...)` call it's meant to replace.

### Change-notification robustness, unconfigured-named-option behavior, verification (carried forward, condensed)

- **Change notification** matches real `OptionsMonitor<T>` exactly — a
  plain event, synchronous invocation, no per-subscriber exception
  isolation, no locking beyond what a compiler-generated event already
  guarantees. Confirmed directly against `OptionsMonitor.cs`
  (`_onChange` is a plain `event Action<TOptions, string>?`; a throwing
  subscriber blocks remaining ones; no explicit locking anywhere). This
  package's one real correctness addition over the naive community fake:
  a genuine per-subscription `IDisposable` that actually unsubscribes
  (`-=` against the internal event) — the one concrete bug (RESEARCH-0028
  §7) a correctly-used C# event fixes for free.
- **Unconfigured named option: throws, reaffirmed with a sharper
  justification.** Researched further this revision: real
  `IOptionsFactory<T>.Create(name)` for a name with no matching
  `IConfigureOptions<T>`/`IConfigureNamedOptions<T>` silently returns a
  plain `new TOptions()` — no exception, no signal anything was
  unconfigured (confirmed against the documented `IOptionsFactory<T>`
  contract on the Learn page: it applies whichever registered
  configuration delegates match a name and returns the result
  regardless of whether any did). This ADR's throw-instead choice is
  therefore a **real, disclosed divergence from production behavior**,
  not a fidelity-neutral default — but it is not a novel invention
  either: it's the exact same tradeoff Compono's own `Compono.TestDoubles`
  already made and shipped, in the very consumer this ADR validated
  against. `MediatRTestProfile.cs`'s own comments (above) describe
  `IHandlerInput.RequestEnvelope` and `IAttributesManager.Session` as
  deliberately generated *configuration-required* under
  [ADR-0045](0045-testdoubles-configuration-required-members.md) —
  "more honest than implicit auto-population or silent-null defaults, at
  the cost of one explicit line per test that needs it" — the identical
  justification this ADR is making for unconfigured named options,
  already an established, `Accepted`, real-consumer-validated Compono
  precedent, not a general principle asserted in the abstract. Reaffirmed:
  throwing remains the decision.
- **Verification** remains deferred, not rejected — no evidenced demand,
  including from the dogfooding validation above (neither real consumer
  needed to verify change-notification call counts).

### Source disposal: not `IDisposable`/`IAsyncDisposable` (resolved 2026-09-08, final)

The public Options source object does **not** implement `IDisposable` or
`IAsyncDisposable`. It owns no production resource requiring disposal — no
file watcher, no real `IChangeToken`, no DI scope, no configuration
provider. The correct ownership boundary is per-subscription: the
`IDisposable` returned by `OnChange` is what a test disposes to
unregister that specific listener, exactly matching real
`OptionsMonitor<T>`'s own per-registration disposal shape (its `Dispose()`
tears down change-token subscriptions that don't exist in this design at
all — there is nothing analogous for the source itself to dispose).
Giving the source its own `Dispose()` would imply a lifecycle/ownership
contract Compono's broader composition model deliberately doesn't have —
`CompositionRow`/`CompositionScope`/`Composer` own no disposal contract of
their own either (`Compono.DependencyInjection`'s `AsServiceProvider()`
bridge follows the identical rule, per ADR-0047). This is not left as an
implementation convenience question — no `Dispose()` method is added to
the source under any circumstance, including "just to clear all
subscribers at once"; a test that needs that disposes each subscription
individually.

### Concurrent access to one source instance (resolved 2026-09-08, final)

**Reframed from the original ADR's "concurrent test execution" framing,
which was the wrong question.** A Compono.Options source belongs to one
composition/test — sharing one mutable source instance across independent
*parallel tests* is not a supported scenario and does not drive this
design, the same way no other Compono composition primitive is designed
for cross-test sharing. The real, legitimate concern is concurrency
**within** one test/composition: a SUT may read `CurrentValue` on one
thread while another triggers a change, multiple consumers may subscribe/
unsubscribe concurrently, and a named value may be read while a different
name is being changed.

**Behavioral contract (architectural; the primitive that satisfies it is
implementation-level, per Open Questions below):**

- The source remains internally valid under concurrent reads, changes,
  subscriptions, and unsubscriptions — no operation may observe corrupted
  or partially-mutated internal state.
- Named-value storage must not become structurally corrupted by concurrent
  mutation of different names, or of the same name.
- Subscribe/unsubscribe remains safe under concurrent calls, including
  concurrent with an in-flight change notification.
- **A change operation establishes the new current value before invoking
  any change callback** — a callback that reads `CurrentValue`/`Get(name)`
  during its own invocation observes the changed value, never the stale
  one, matching real `OptionsMonitor<T>`'s own cache-then-invoke ordering
  (`InvokeChanged`: `_cache.TryRemove(name)` then recompute, *then*
  `_onChange?.Invoke(...)`).
- Notifications remain synchronous (unchanged from the original ADR).
- Notification ordering follows ordinary multicast-event behavior, already
  decided above — no additional ordering guarantee beyond that.
- **No stronger transactional or cross-operation ordering guarantee is
  promised** — e.g., no atomicity across a multi-name batch change, no
  guaranteed ordering between two threads racing to change different
  names, beyond each individual operation being internally safe. Inventing
  either would be machinery with no evidenced need, exactly the kind of
  speculative robustness this ADR's Decision Drivers already reject
  elsewhere (§"Change-notification robustness").

This contract removes concurrency from the unresolved/open-question list
— the *behavior* is now decided. The concrete synchronization primitive
(a `lock`, a `ConcurrentDictionary`, or another approach) is deliberately
left to implementation planning, since more than one primitive can satisfy
this contract and the choice has no public-observable consequence.

### Positive Consequences

- A correct, reusable `IOptionsMonitor<T>` fake closes a real,
  community-documented correctness gap.
- The object model and identity contract close the exact coherence risk
  found live in `MediatRTestProfile.cs` — one source, one wiring call,
  `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` structurally
  unable to drift apart.
- The `IOptions<T>`/`IOptionsSnapshot<T>` identity model is built entirely
  from existing, `Accepted` Compono primitives (`Share<T>()`, ordinary
  `Register<T>` semantics) — no new lifecycle concept invented.
- No new core extension point, no reflection, no generator dependency.

### Negative Consequences

- No automatic, no-registration composition for arbitrary `T` (unchanged
  from the original ADR) — accepted, blocked on Finding B, out of scope.
- The Monitor/Snapshot correctness case has no real dogfooding validation
  — accepted, since Gate B was satisfied by explicit product-owner
  request, but recorded honestly as a real gap this design has not been
  pressure-tested against.
- Matching real `OptionsMonitor<T>`'s lack of per-subscriber exception
  isolation (unchanged) — accepted, faithfulness over friendliness.
- The unconfigured-named-option-throws choice diverges from real
  `IOptionsFactory<T>` — accepted, on the strength of the `Compono.TestDoubles`/
  ADR-0045 precedent, not merely general principle.

## Pros and Cons of the Options

### Public object model: source implements Monitor; internal frozen-view type implements IOptions/Snapshot (chosen)

- Good, because it matches real Microsoft architecture exactly
  (`OptionsManager<T>` implementing both frozen interfaces; a distinct
  `OptionsMonitor<T>` for the live one) rather than inventing a different
  shape.
- Good, because it keeps the one thing a consumer actually holds (the
  source) conceptually simple — "the live, mutable thing," with the
  frozen views as an implementation detail reached only through the
  standard interfaces.
- Bad, because a consumer inspecting the source's own type will see it
  implements `IOptionsMonitor<T>` specifically (not the frozen
  interfaces) — a minor discoverability cost, mitigated by the wiring
  call and documentation making the full picture obvious.

### Public object model: one type implements all three

- Good, because it's the smallest possible public surface.
- Bad, because it obscures the genuine behavioral difference between
  Monitor (live) and the two frozen interfaces (fixed) on one object —
  exactly the ambiguity you flagged as a concern, and correctly so.

### Public object model: three fully separate public types

- Good, because each interface's public type is maximally simple in
  isolation.
- Bad, because it either duplicates frozen-view logic for two interfaces
  that are genuinely the same behavior in real Microsoft code, or forces
  an artificial split that doesn't reflect reality.

### `IOptions<T>`/`IOptionsSnapshot<T>` identity: `Share<T>()` for `IOptions<T>`, plain `Register<T>` for Snapshot (chosen)

- Good, because it reproduces real Singleton-vs-Scoped behavior using
  Compono's own existing, `Accepted` primitives — no new lifecycle
  concept.
- Good, because "one Compono resolution = one DI scope" is no longer an
  asserted analogy; it's a direct, mechanical consequence of how
  `Register<T>` already behaves.
- Bad, because a consumer has to understand that `IOptionsSnapshot<T>`
  resolved twice in the same graph yields two different frozen instances
  — a real, if minor, surprise risk, mitigated by documentation and by
  the fact that this exactly matches how two different DI scopes would
  behave too.

### `IOptions<T>`/`IOptionsSnapshot<T>` identity: both shared, no distinction

- Good, because it's the simplest possible identity model.
- Bad, because it silently drops the real, documented difference between
  Singleton and Scoped registration — a fidelity gap, not a
  simplification.

### `IOptions<T>`/`IOptionsSnapshot<T>` identity: both fresh every resolution

- Good, because it never risks stale data.
- Bad, because it makes `IOptions<T>` behave like `IOptionsSnapshot<T>`
  — contradicts real `IOptions<T>`'s actual "one instance, once computed"
  contract just as much as making it fully reactive would have.

## Links

- [RESEARCH-0028](../research/0028-compono-options-configuration-admission-research.md) —
  the full admission investigation and reassessment.
- [`docs/architecture/capability-admission.md`](../architecture/capability-admission.md) —
  the governing admission process.
- [ADR-0056](0056-composition-builder-share-graph-wide-sharing.md),
  [ADR-0027](0027-compono-bogus-package-design.md) — the composition-
  ergonomics-as-value precedent this capability's coherence dimension
  follows.
- [ADR-0051](0051-compono-http-handler-based-testing-package.md) — the
  closest architectural precedent.
- [ADR-0047](0047-compono-dependencyinjection-configured-resolution-bridge.md) —
  confirms why this doesn't belong in `Compono.DependencyInjection`.
- [ADR-0052](0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md) —
  Finding B, reproduced live in `SmapiHttpTestProfile.cs` (above).
- [ADR-0018](0018-composition-profiles.md) — profiles need no new
  mechanism to host this capability's registration call.
- [ADR-0045](0045-testdoubles-configuration-required-members.md) — the
  direct, real-consumer-validated precedent for this ADR's
  unconfigured-named-option-throws decision.
- [ADR-0001](0001-source-generation-first.md) — no-reflection-by-default.
- [dotnet/runtime `OptionsMonitor.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Options/src/OptionsMonitor.cs) —
  change-notification fidelity source.
- [dotnet/runtime `OptionsManager.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Options/src/OptionsManager.cs) —
  the 2026-09-08 revision's primary source for the `IOptions<T>`/
  `IOptionsSnapshot<T>` identity model.
- [Testing IOptionsMonitor - Ben Foster](https://benfoster.io/blog/20200610-testing-ioptionsmonitor/) —
  the naive community fake this capability corrects.
- `src/Compono/CompositionContext.cs` (`StoreSharedValue`),
  `src/Compono/CompositionBuilder.cs` (`Register<T>`) — the existing,
  unchanged Compono mechanics the identity model is built on, inspected
  directly this revision.
- `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft`
  (`MediatRTestProfile.cs`, `SmapiHttpTestProfile.cs`) and
  `/Users/ncipollina/source/repos/ncipollina/cosmere-tracker`
  (`PersistenceTestProfile.cs`) — the dogfooding validation sources
  inspected this revision (read-only; neither repository modified).

## Documentation consequences (recorded now, executed later)

### `Compono.Options` package documentation (once implemented)

Must cover: installation; basic setup; `IOptions<T>`; `IOptionsSnapshot<T>`;
`IOptionsMonitor<T>`; the identity model (shared `IOptions<T>`/Monitor vs.
fresh-per-resolution `IOptionsSnapshot<T>`) explained in terms a consumer
can reason about without needing to know `Share<T>()` is involved
internally; named options; deterministic changes; subscription/disposal
semantics; inline usage; profile usage (with `MediatRTestProfile.cs`'s
real before/after as a worked example, once implementation exists);
relationship to ordinary `Microsoft.Extensions.Options` APIs; intentional
non-goals (no real `IConfiguration`/change-token simulation, no
`IOptionsFactory<T>` pipeline, no DI-scope simulation); the
unconfigured-named-option-throws divergence from real `IOptionsFactory<T>`,
stated plainly; the ADR-0052 Finding B limitation.

### Configuration Cookbook — unchanged, still a required deliverable

Preserved exactly as the original ADR recorded it — not weakened or
dropped by this revision. Recorded in
[`docs/roadmap/future-packages.md`](../roadmap/future-packages.md)'s
"Documentation-only ideas" section:

- Basic in-memory `IConfiguration` composition (`ConfigurationBuilder`,
  `AddInMemoryCollection`, `Register<IConfiguration>`).
- Layered configuration / test-specific overrides.
- Reusable configuration through a profile.
- `GetSection`/common consumption patterns.
- Explicit routing guidance: ordinary Configuration for `IConfiguration`
  itself; `Compono.Options` for the Options interfaces it owns; no
  `Compono.Configuration` package exists.

Whichever PR implements `Compono.Options` should treat this Cookbook work
as part of its own definition of done.

## Skill/eval consequences (recorded now, executed at implementation time)

Unchanged from the original ADR: review/update `skills/compono/SKILL.md`'s
detection table, add `references/options.md`, update `evals.json`, run the
mandatory baseline-vs-updated skill-eval comparison — **not performed
now**.

## Open questions

### Resolved this revision (were previously listed as open; now settled)

- ~~Single class vs. two~~ — resolved: one public source type
  implementing `IOptionsMonitor<T>` directly; an internal frozen-view type
  (never public) behind `IOptions<T>`/`IOptionsSnapshot<T>`.
- ~~`IOptionsSnapshot<T>` lifecycle/semantics~~ — resolved: matches real
  `OptionsManager<T>` architecture via `Share<T>()` (for `IOptions<T>`)
  vs. plain `Register<T>` (for `IOptionsSnapshot<T>`); no invented
  Compono-specific snapshot concept.
- ~~Registration identity/lifetime for all three interfaces~~ — resolved,
  precisely (see "Decision Outcome" above): `IOptions<T>`/
  `IOptionsMonitor<T>` shared (one instance per graph);
  `IOptionsSnapshot<T>` fresh per resolution.
- ~~Whether `Share<T>()` has a role~~ — resolved: yes, used internally by
  the wiring call for `IOptions<T>`/`IOptionsMonitor<T>`; never called
  directly by the consumer.
- ~~Unconfigured named-option behavior~~ — reaffirmed (throw), now backed
  by the ADR-0045/`Compono.TestDoubles` precedent found in the dogfooding
  validation, not just general principle.
- ~~Disposal of the source object itself~~ — resolved 2026-09-08: no
  `IDisposable`/`IAsyncDisposable` on the source; per-subscription
  disposal only. See "Source disposal," above.
- ~~Concurrent read/change interaction~~ — resolved 2026-09-08: reframed
  from cross-test sharing (not a supported scenario) to concurrency
  *within* one source instance, with an explicit behavioral contract. See
  "Concurrent access to one source instance," above. The synchronization
  *primitive* that satisfies the contract remains implementation-level
  (see below).

### Still genuinely open — implementation-level, not public-contract

- **Exact public API naming** — the source type's name, its
  `.Set(...)`-shaped mutation method(s), and the wiring call's name are
  all illustrative only. Resolved by PLAN-0064's own naming-finalization
  task before implementation begins, not by this ADR.
- **Thread-safety implementation primitive** for the named-value store —
  the *contract* it must satisfy (§"Concurrent access to one source
  instance") is decided; whether a `lock`, a `ConcurrentDictionary`, or
  another approach satisfies it most simply is an implementation choice
  with no public-observable consequence.

Both remaining items are naming/implementation choices only — neither
changes what a consumer observes, and neither blocks acceptance.

## Acceptance

Every architectural/public-contract question this ADR's design pass
identified is now resolved: the entry point, the public object model, the
`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` identity and
registration contract, named-option semantics (including the unconfigured-name
divergence and its precedent), change-notification robustness, source
disposal, and the concurrent-access contract. The one honest, disclosed
gap — no real Compono consumer dogfoods `IOptionsMonitor<T>`/
`IOptionsSnapshot<T>` specifically (§"Dogfooding validation," above) —
doesn't block Gate A/Gate B (satisfied by explicit product-owner request,
independent of dogfooding by design, exactly as `Compono.TUnit`/
`Compono.NUnit` were) and doesn't leave any architectural question
unresolved; it is carried forward into PLAN-0064 as deterministic
contract-test coverage in lieu of real-consumer Monitor/Snapshot evidence,
rather than glossed over. Accepted 2026-09-08.

## Amendment 1 (2026-09-08): registration-precedence correction

PLAN-0064's implementation surfaced a factual error in the "Decision
Outcome" identity-model section above: the claim that an explicit consumer
`Register<IOptions<T>>(...)`/`Register<IOptionsSnapshot<T>>(...)`/
`Register<IOptionsMonitor<T>>(...)` written after `UseOptions<T>`'s own
wiring call "interacts through Compono's ordinary, unchanged
first-registration-wins rule" and that "a consumer who explicitly
overrides one of the three still gets ordinary, predictable Compono
behavior." **This describes behavior that does not exist.** Real
`CompositionBuilder.Register<T>` (`src/Compono/CompositionBuilder.cs`,
ADR-0019) has no first-registration-wins or last-registration-wins
override semantics at all for an exact-type collision: registering the
same exact type more than once — directly, via a profile, or across two
profiles — is a strict build-time conflict, thrown as
`CompositionConfigurationException` at `Composer.Create`'s validation
step, regardless of call order. Since `UseOptions<T>` itself calls
`Register<IOptions<T>>(...)`/`Register<IOptionsMonitor<T>>(...)`/
`Register<IOptionsSnapshot<T>>(...)` internally, a consumer's own explicit
registration for any of the three collides with it and throws — it does
not silently override it. Confirmed by a real test
(`OrdinaryFirstRegistrationWinsPrecedence_HoldsUnchanged_ForAnExplicitConsumerOverride`,
`test/Compono.Options.Tests/CompositionBuilderExtensionsTests.cs`) that
asserts exactly this: `Composer.Create` throws
`CompositionConfigurationException` when a consumer registration collides
with `UseOptions<T>`'s own.

This does not change the ADR's core decision (the object model, the
identity/lifetime contract, or `Share<T>()`'s internal role) — those hold
exactly as decided. It only corrects the override claim: **a consumer
cannot selectively override one of the three Options interfaces while
keeping `UseOptions<T>`'s coherence guarantee for the other two** — there
is no partial-override path. A consumer who genuinely needs different
behavior for one interface must not call `UseOptions<T>` for that settings
type at all, and wires all three (or whichever it needs) by hand instead,
same as before this package existed. "No special-cased precedence is
invented for this package" remains true and is in fact the reason for this
correction: Compono's real, unchanged, strict duplicate-registration rule
applies here exactly as it does everywhere else, with no override
exception carved out for `Compono.Options` — the ADR's original prose
described that rule incorrectly, not this package behaving inconsistently
with it.

**Second, related correction — the "collapse these two lines into one"
framing in "Selected dogfooding validation target" above, and what
`Compono.Options` actually guarantees coherent.** `Compono.Options`
guarantees coherence **among `IOptions<T>`/`IOptionsSnapshot<T>`/
`IOptionsMonitor<T>`** — all three originate from one `TestOptionsSource<T>`
by construction, and this is real and enforced. It does **not** guarantee
coherence between the bare settings type `T` and those three interfaces
merely because `T` happens to be registered elsewhere — `UseOptions<T>`
never touches a plain `Register<T>()` registration at all, by design (§
"ADR-0052 Finding B boundary": the source must be a value the test
supplies directly, not something Compono resolves and re-wraps). The real
`MediatRTestProfile.cs` dogfooding result (PLAN-0064) still needs a plain
`Register<SkillServiceConfiguration>(...)` registration alongside
`UseOptions<T>`, because a separate consumer in that same test project
(`ServiceRegistrarTests`) depends on the bare type, not an Options
interface — the two-registration shape did not literally collapse into
one call. What changed is that both registrations are now sourced from
the *same* local instance by the test author's own discipline (a single
`var defaultSkillServiceConfiguration = new SkillServiceConfiguration {...}`
passed to both `Register<T>(() => defaultSkillServiceConfiguration)` and
`new TestOptionsSource<T>(defaultSkillServiceConfiguration)`) — coherent
by construction *in that consumer*, not because `UseOptions<T>` enforces
any relationship to a separately-registered bare `T`. This is a narrowing
of the original claim's scope, not a reversal of the admission decision
(RESEARCH-0028/Gate A) — the coherence value `Compono.Options` provides
among the three Options interfaces themselves remains exactly as
evidenced and is what the dogfooding validation actually confirmed.
