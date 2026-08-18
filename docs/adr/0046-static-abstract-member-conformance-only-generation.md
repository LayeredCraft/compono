# [ADR-0046] Effective Interface Contract for Inherited Static Abstract Members

**Status:** Accepted

**Date:** 2026-08-18

**Decision Makers:** solo (product-owner-directed: explicit Gate-B
acceptance criterion supplied by the user)

## Context

`Compono.TestDoubles` rejects an entire interface at generation time
(`CMP0021`, informational, whole-interface `Failure`) if that interface's
transitive closure declares **any** static abstract member (a method,
property, or operator with no default body — C# 11+'s static-abstract-in-
interfaces feature) that the analyzer treats as unimplemented. This bucket
also currently catches events, indexers, and C-style variable-argument
methods, but those are unrelated shapes with their own reasons for staying
out of scope (see `docs/packages/compono-testdoubles.md`'s "What it
deliberately doesn't do" and ADR-0042's Non-Goals) — this ADR is scoped to
static abstract members only.

[RESEARCH-0005](../research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md)'s
third `lightsaber-skill` dogfood found this is the **sole** remaining
blocker preventing that real project's test suite from fully removing
`Compono.NSubstitute`: `IAmazonS3` declares one static abstract member,
`CreateDefaultClientConfig()` (inherited from its base interface,
`Amazon.Runtime.IAmazonService`), and because `CMP0021` rejects the whole
interface for it, `IAmazonS3` can't resolve through
`UseGeneratedTestDoubles()` alone — the other 20+ instance members on that
interface, which have no problem of their own, get pulled down with it.

RESEARCH-0005 originally classified this "not a bug, not a roadmap
candidate" under ADR-0029's general "material improvement" bar, then
reclassified it as a roadmap candidate the same day against a stronger,
explicit product-owner requirement:

> I need `Compono.TestDoubles` to be capable of completely replacing
> `Compono.NSubstitute` in `lightsaber-skill`.

That reclassification, and this ADR's first design pass, assumed
`IAmazonS3.CreateDefaultClientConfig()` was genuinely unimplemented —
narrow but real, and the original design response (recorded and later
withdrawn — see "Decision Outcome" below) was to generate a
**conformance-only** stub for it: an explicit static interface
implementation whose body unconditionally throws if invoked, just enough
to satisfy C#'s "the type must implement every interface member" rule
without claiming to support any configurable behavior.

**That assumption was wrong**, and the actual root cause is different and
more general — see "Decision Outcome."

## Decision Drivers

- **Gate-B**: `lightsaber-skill`'s test project must be able to remove
  `Compono.NSubstitute` entirely — the explicit, stated product
  requirement this ADR responds to.
- **Source generation only, no reflection** — `docs/adr/0001-source-generation-first.md`'s
  standing constraint; whatever this ADR decides has to be emittable as
  ordinary C# by the existing Roslyn generator, not resolved at runtime.
- **Native AOT / trimming safety** — the generated code must survive
  `test/Compono.TestDoubles.AotSmokeTest` unchanged in kind (no new
  reflection, no runtime code generation).
- **No general static-mocking subsystem** — explicitly out of scope per
  the product owner's original framing; nothing here grows into
  `Returns()`/`Throws()`/`Verify()` surface for static members.
- **Correctness over convenience** — a fix must not silently change the
  *behavior* of a member the interface itself already resolves; "the type
  compiles" is not sufficient if the generated code changes what a real
  call returns. This driver is what invalidated this ADR's original design
  (see below) and shaped its replacement.
- **Don't ship unreachable machinery** — a capability that can never
  actually be exercised by a real Compono consumer, given Compono's own
  composition mechanism, isn't a capability worth shipping, even if it's
  self-consistent in isolation. This driver is what removed the original
  design's stub-emission path entirely rather than keeping it as
  speculative forward-looking surface.

## Considered Options

1. **Status quo** — continue whole-interface rejection (`CMP0021`) for any
   interface whose closure contains a static abstract member, regardless
   of whether it's actually resolved somewhere in that closure.
2. **Conformance-only generation (original design, withdrawn)** — for a
   static abstract member the analyzer's per-interface closure walk
   encounters unresolved, generate an explicit static interface
   implementation whose body unconditionally throws a new,
   Compono-owned exception if invoked.
3. **Effective-interface-contract analysis (chosen)** — before treating a
   static abstract member as unimplemented, ask whether it's already
   resolved by C#'s own "most specific implementation" rule somewhere in
   the interface's closure (i.e., a more-derived interface re-implements
   it with a concrete body). If so, it was never actually part of this
   double's unimplemented contract — skip it silently, the same
   disposition an ordinary non-abstract static member already gets. Only
   a genuinely unresolved static abstract member (no override anywhere in
   the closure) still whole-interface-rejects.
4. **Full configurable static-member support** — generate a real
   `Configure()`/`Verify()` surface for static members too, so a test
   could `SomeInterface.Configure().StaticMember().Returns(...)`.

## Decision Outcome

Chosen option: **effective-interface-contract analysis (Option 3)** —
correcting the analyzer's closure walk to use C#'s own "most specific
implementation" resolution instead of inspecting each interface in the
closure independently.

### What this ADR originally proposed, and why it's wrong

This ADR's first accepted design (Option 2, "conformance-only
generation") assumed `IAmazonS3.CreateDefaultClientConfig()` was
genuinely unimplemented. During implementation, that assumption was
checked against the real `AWSSDK.S3` package via reflection and a Roslyn
spike, and turned out to be false:

```
Interface chain for IAmazonS3:
  Amazon.S3.IAmazonS3: CreateDefaultClientConfig  IsAbstract=False  HasBody=True
  Amazon.Runtime.IAmazonService: CreateDefaultClientConfig  IsAbstract=True  HasBody=False
```

`IAmazonService` declares `CreateDefaultClientConfig()` as a pure static
abstract member — no body. `IAmazonS3` (the actual interface the double
is generated for) **already re-implements it with a concrete body**. From
`IAmazonS3`'s own perspective, this member has always been fully
resolved — C#'s "most specific implementation" rule for static interface
members means any type implementing `IAmazonS3` inherits `IAmazonS3`'s
own override, not `IAmazonService`'s raw abstract declaration.

`TestDoubleAnalyzer`'s closure walk, though, iterates every interface in
`IAmazonS3.AllInterfaces` independently (`declaringInterface.GetMembers()`
per interface), so it was inspecting `IAmazonService`'s raw abstract
declaration in isolation — never noticing that `IAmazonS3` itself had
already resolved it. **This was an analyzer bug**, not a genuine
capability gap: `IAmazonS3` was never actually unimplemented.

Worse, a compile-and-run spike proved the original conformance-only design
would have been actively harmful for exactly this shape, not merely
unhelpful:

```csharp
public interface IBase { static abstract int M(); }
public interface IDerived : IBase { static int IBase.M() => 42; }
public sealed class Impl : IDerived { static int IBase.M() => throw new Exception("wins"); }
// Generic<Impl>() where T : IBase => T.M() returns... the exception. Impl's own
// explicit implementation of IBase.M is MORE SPECIFIC than IDerived's, and wins.
```

A conformance-only stub emitted directly on the generated double class
(itself the most-derived type in the hierarchy) would have **silently
shadowed and broken `IAmazonS3`'s own real, working implementation** for
any code that calls `CreateDefaultClientConfig()` generically through the
double — turning a member that already worked correctly into one that
throws unconditionally. That's a regression, not conformance.

### The second finding: conformance-only stubs were unreachable anyway

Separately, for the case the original design actually intended to help —
a **genuinely** unresolved static abstract member, with no override
anywhere in its interface's closure — a second compile spike proved the
whole approach was dead on arrival regardless of the shadowing problem
above:

```csharp
public interface IRepo { static abstract int M(); }
// Generic.Resolve<IRepo>() — unconstrained AND constrained (where T : notnull) — both fail:
// error CS8920: The interface 'IRepo' cannot be used as type argument.
// Static member 'IRepo.M' does not have a most specific implementation in the interface.
```

C# unconditionally forbids using an interface with a genuinely unresolved
static abstract member as a type argument to **any** generic method,
constrained or not. Compono's own `ICompositionContext.Resolve<TValue>()`
— the mechanism every constructor-injected dependency resolves through,
both for a generated double and for the ordinary runtime-provider
fallback — is exactly such a call. An interface with a genuinely
unresolved static abstract member was **never actually composable through
Compono at all**, with or without `Compono.TestDoubles`, before or after
this ADR. A generated conformance-only stub for such an interface could
never be reached by any real consumer: the composition call site itself
fails to compile before a double is ever involved.

Both findings point the same direction: Option 2's throwing-stub machinery
is unnecessary for the case it would help (already-resolved members don't
need a stub at all) and unreachable for the case it would apply to
(genuinely-unresolved members can't be composed through Compono
regardless). Nothing in Option 2 survives contact with either finding, so
it's withdrawn in full — not merely postponed.

### The corrected fix

`TestDoubleAnalyzer` now checks, for every static-abstract-shaped member
it encounters in the closure walk,
`interfaceType.FindImplementationForInterfaceMember(member)` — Roslyn's
own API for the same "most specific implementation" resolution C#'s
compiler performs (verified directly against the `IBase`/`IDerived` shape
above: returns the concrete override when one exists, `null` when
genuinely unresolved). If it returns non-null, the member is already
resolved by some interface in the closure — skipped silently, the double
generates completely normally, with zero new diagnostics and zero new
emitted code for that member. If it returns `null`, the member is
genuinely unimplemented, and the whole interface stays rejected under the
original, unchanged `CMP0021` — for the CS8920 reason above, generating
anything for this case would be unreachable machinery.

This is not an `IAmazonS3`-specific workaround. It's a general
correctness fix to how `TestDoubleAnalyzer` reasons about a leaf
interface's *effective* contract: a member declared abstract on some base
interface is not necessarily part of what an implementer of the *leaf*
interface must still provide — C# itself may have already resolved it
somewhere in between. The fix applies uniformly to static abstract
methods, properties, and operators; `IAmazonS3` is this fix's real-world
motivating case and closing acceptance test, not a special case in the
implementation.

### Positive Consequences

- Directly closes Gate-B for `lightsaber-skill`: `IAmazonS3` (and any
  future interface with the same shape) generates and resolves through
  `UseGeneratedTestDoubles()` alone, with every instance member —
  including the previously-blocking one — behaving exactly as the real
  interface actually behaves.
- No new public API surface at all: no new diagnostic code, no new
  exception type, no new emission branch. The fix is entirely internal to
  `TestDoubleAnalyzer`'s existing closure walk.
- Strictly more correct than the withdrawn design: a real, working
  implementation is preserved rather than replaced with an unconditional
  throw.
- General, not narrow: any interface with this inheritance shape benefits,
  not just `IAmazonS3` specifically.

### Negative Consequences

- A genuinely unresolved static abstract member (true green-field, no
  override anywhere) still whole-interface-rejects, unchanged from before
  this ADR — and, per the CS8920 finding above, this isn't a
  `Compono.TestDoubles` limitation to begin with: such an interface was
  never composable through Compono's `Resolve<T>()` mechanism at all.
  If a real, evidenced consumer scenario ever demonstrates otherwise (a
  composition path that doesn't go through a generic type argument), that
  would be new evidence and a new roadmap candidate under
  [ADR-0042 Amendment 2](0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-compononsubstitute-substitutability-is-a-goal-not-an-aspiration) —
  not something this ADR speculates about now.
- This ADR's own history (a design accepted, then found wrong during
  implementation, then replaced within the same PR) is unusual for this
  repo's ADR lifecycle. It's recorded here in full, including the
  withdrawn design and both compile spikes that invalidated it, rather
  than silently rewritten, so a future reader understands why the actual
  fix looks nothing like what "conformance-only generation" would
  suggest.

## Pros and Cons of the Options

### 1. Status quo (continue rejecting)

- Good, because it requires zero new code.
- Bad, because it directly fails Gate-B — `lightsaber-skill` cannot fully
  remove `Compono.NSubstitute` while this stands, which is the entire
  reason this ADR exists.
- Bad, because it's disproportionate: one member that C# itself has
  already resolved disables 20+ members that have real, working generated
  behavior today.

### 2. Conformance-only generation (withdrawn)

- Good, because it would have been the smallest change satisfying a
  literal reading of "static abstract members reject the interface."
- Bad, because it's built on a false premise for the real motivating case:
  `IAmazonS3.CreateDefaultClientConfig()` was never actually unimplemented.
- Bad, because it's actively harmful for that same case: a compile-and-run
  spike proved a stub emitted on the double class would silently shadow
  and break the interface's own real, working implementation for any
  generic call through the double.
- Bad, because for the case it *would* legitimately apply to (a genuinely
  unresolved static abstract member), a second compile spike proved
  Compono's own `Resolve<TValue>()` can't compile with such an interface
  as a type argument at all (`CS8920`) — the stub could never be reached
  by a real consumer.

### 3. Effective-interface-contract analysis (chosen)

- Good, because it directly and correctly closes Gate-B: `IAmazonS3`
  generates with every member behaving exactly as the real interface does.
- Good, because it's a pure correctness fix with zero new public API
  surface — no new diagnostic, no new exception type, no new emission
  branch.
- Good, because it's general (any interface with this inheritance shape),
  not `IAmazonS3`-specific.
- Good, because it can't shadow or break a real implementation — it never
  emits anything for an already-resolved member; the interface's own
  override is what the double inherits, unchanged.
- Bad, because it doesn't help a genuinely unresolved static abstract
  member — but per the CS8920 finding, nothing could, within Compono's
  current generic composition model.

### 4. Full configurable static-member support

- Good, because it would be the most complete answer, closing any future
  gap in this area permanently.
- Bad, because it's explicitly out of scope per the product owner's own
  framing ("no general static mocking subsystem," "no NSubstitute
  feature-parity chase") and per this ADR's Decision Drivers.
- Bad, because static-member "mocking" has real semantic hazards a simple
  `Configure()`/`Returns()` surface doesn't have for instance members —
  static state is process-global, so a `Configure()` call on a static
  member would need to reason about test isolation/parallelization
  (xUnit v3's Microsoft.Testing.Platform runner, which this suite already
  uses, runs tests in parallel by default) in a way instance-member
  doubles never do, since each instance double is already inherently
  test-scoped.
- Bad, because it would still be blocked by the same `CS8920` constraint
  for any interface whose static abstract member is genuinely
  unresolved — the constraint is C#'s, not Compono's, and no amount of
  additional `Compono.TestDoubles` machinery changes it.

## Links

- [RESEARCH-0005](../research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md) —
  the evidence record and Gate-B reclassification this ADR responds to.
- [ADR-0045](0045-testdoubles-configuration-required-members.md) — the
  directly-analogous precedent this ADR's withdrawn design was originally
  modeled on (narrowing a whole-interface-rejection diagnostic into a
  per-member exception for one specific, common shape) — ultimately not
  the right shape for this problem, per the findings above.
- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) — the
  Non-Goals section this ADR narrows (a static abstract member already
  resolved via interface inheritance moves from "unsupported" to
  "supported"; a genuinely unresolved one, and events/indexers/
  variable-argument methods, remain unsupported, unchanged).
- [ADR-0043](0043-compono-generated-test-doubles-design.md) — original
  `CMP0021` whole-interface-rejection design this ADR narrows.
