# [ADR-0013] Collection Generation Semantics

**Status:** Accepted

**Date:** 2026-07-28

**Decision Makers:** solo

## Context

`docs/mvp.md`'s Milestone 2 built-in-type list includes `Array`,
`List<T>`, `IReadOnlyList<T>`, `HashSet<T>`, and `Dictionary<TKey, TValue>`,
implemented as built-in providers occupying pipeline stage 7
([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)).
A design review flagged that collections aren't ordinary provider
boilerplate — they're the one place path identity
([ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)),
recursion detection
([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)),
determinism, and failure semantics
([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md))
all have to compose correctly at once: each element is its own
`Resolve<T>()` call with its own path segment, a dictionary needs unique
keys generated deterministically, and a recursive element type (`List<Node>`
where `Node` contains another `List<Node>`) has to hit the same
active-construction-frame mechanism as any other recursive type, not a
collection-specific special case.

## Decision Drivers

- Every collection element must be individually path-addressable
  ([ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md))
  so two elements of the same collection don't accidentally receive
  identical values, the same bug class ADR-0012 closed for constructor
  parameters.
- `docs/mvp.md`'s Milestone 3 scope explicitly includes "Collection-size
  configuration" — Milestone 2 needs a sane fixed default, not a
  configuration mechanism.
- `Dictionary<TKey, TValue>` requires unique keys; deterministic
  generation can produce a collision, which needs a bounded, diagnosable
  resolution rather than an infinite retry loop or a silent duplicate-key
  crash.
- The BCL itself makes no enumeration-order guarantee for `HashSet<T>` or
  `Dictionary<TKey, TValue>` — Compono shouldn't promise more than the
  types it's generating already promise.
- "Nullability generation defaults" is an explicitly open `docs/mvp.md`
  item — this ADR must not quietly resolve it while deciding collection
  behavior; it should apply whatever that policy turns out to be
  uniformly, not invent a collection-specific rule.

## Considered Options

This ADR is less a multi-way fork than a set of concrete default
decisions the design review asked to have made explicit before
implementation, each with a considered alternative:

1. **Default collection size** — a fixed internal constant (3, matching
   `docs/public-api.md`'s own `WithCollectionSize(3)` example) versus an
   arbitrary different constant with no grounding in existing docs.
2. **Element path addressing** — a dedicated `CollectionElement`/
   `DictionaryKey`/`DictionaryValue` path segment per element (chosen in
   ADR-0012) versus reusing the parent's path unchanged for every element
   (rejected — the exact ambiguity ADR-0012 closed for named parameters
   would reappear for collection elements).
3. **Duplicate dictionary keys** — bounded per-key retry with a
   diagnosable failure versus unbounded retry (risks an infinite loop for
   a low-cardinality key type) versus silently overwriting a duplicate
   (produces a dictionary smaller than requested with no explanation).
4. **Unconstructable collection type** — treated as an ordinary
   `NotHandled` (falls through to later stages, eventually diagnostic
   failure) versus a distinct "unsupported collection" error class.
5. **`HashSet<T>`/`Dictionary<TKey, TValue>` ordering** — no guarantee
   (matches the BCL's own contract) versus promising insertion-order
   enumeration (would require wrapping the BCL types or promising
   something they don't).

## Decision Outcome

**Default collection size: 3.** A hardcoded internal constant,
matching the count already used in `docs/public-api.md`'s
`WithCollectionSize(3)` example. Milestone 3's collection-size
configuration replaces this constant with a configurable value; Milestone
2 doesn't build any configuration surface for it.

**Element path addressing:** every element of every collection gets its
own path node. A `List<T>`/array/`HashSet<T>` element at position `i`
resolves via a `CollectionElement(i)` segment; a `Dictionary<TKey, TValue>`
entry at position `i` resolves its key via `DictionaryKey(i)` and its
value via `DictionaryValue(i)` — two distinct segments, so a dictionary's
keys and values fork independently even at the same index (a
`Dictionary<Guid, Guid>` doesn't produce identical key/value pairs). These
segments are constructed directly by the collection provider itself
(never via `CompositionRequestDescriptor` — collections aren't part of
the generated-plan call path at all, per
[ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)).
Each element's `Resolve<T>()` call goes through the full 9-stage pipeline
like any other request — an element whose type has a generated plan, a
registration, or a shared value is resolved exactly as it would be
anywhere else in the graph. This is also why recursive element types need
no collection-specific handling: `List<Node>`'s element resolution is an
ordinary `Resolve<Node>()` call that reaches stage 8 and participates in
the same active-construction-frame check
([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
as any other `Node` reference in the graph.

**Duplicate dictionary keys: bounded retry, then diagnosable failure.**
On a generated key colliding with one already present in the dictionary
being built, the provider retries key generation for that same logical
position with a distinct fork (the retry attempt number becomes part of
the fork derivation, so each retry is still fully deterministic — not a
non-deterministic re-roll) up to a fixed internal retry limit. If the
limit is exhausted without producing a unique key, the provider returns
an authoritative `Failure` (not `NotHandled` — the provider has
definitively claimed and cannot complete this request, per
[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
failure semantics) with a diagnostic naming the key type and the
requested count (e.g. "could not generate 3 unique keys of type `bool`
after N attempts — the key type's value space is likely too small for the
requested collection size"). This is expected, correct behavior for a
low-cardinality key type at the default size, not a bug to work around.

**Unconstructable collection type: ordinary `NotHandled`.** The built-in
collection provider only claims the specific enumerated types (`Array`,
`List<T>`, `IReadOnlyList<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`).
Any other collection-shaped type (an unsupported concrete collection, an
open generic, a custom collection type) is simply not matched by this
provider — it returns `NotHandled` like any provider encountering a
request it doesn't handle, falls through remaining stages, and reaches
the same stage-9 diagnostic failure as any other unhandled type. No
distinct "unsupported collection" error path is introduced — collections
don't get special treatment in failure reporting, only in how their
elements are path-addressed.

**Ordering: no guarantee for `HashSet<T>`/`Dictionary<TKey, TValue>`.**
Generation order (the sequence elements/entries are resolved in) is
internally deterministic given a seed, but the *type's own* enumeration
order is whatever `HashSet<T>`/`Dictionary<TKey, TValue>` themselves
provide — which the BCL explicitly does not guarantee. Compono documents
this plainly rather than promising something the underlying type doesn't
back. `List<T>`/`IReadOnlyList<T>`/arrays, by contrast, do preserve
generation order, because indexed collection types make that order
observable and part of their own contract already.

**Nullable collections versus nullable elements: deferred, applied
uniformly.** `List<T>?` (a nullable reference to the collection) and
`List<T?>` (nullable elements) are independent knobs, each carrying its
own `Nullability` value at its own path node — the collection reference's
own request, and each element's individual request, respectively. This
ADR does not invent a collection-specific nullability-generation default;
whatever `docs/mvp.md`'s still-open "Nullability generation defaults"
item eventually resolves to applies identically at both nodes. Resolving
that open item is explicitly out of scope here.

### Positive Consequences

- Collection elements get the same path-identity correctness guarantee
  ADR-0012 established for named parameters — no silent duplicate-value
  bug for collection contents.
- Recursive element types are handled by the same general mechanism as
  any other recursive reference — no bespoke "collection cycle" logic to
  build, test, or keep in sync with ADR-0011 later.
- Duplicate-key handling is bounded and diagnosable instead of an
  infinite loop or a silently-smaller-than-requested dictionary.

### Negative Consequences

- A low-cardinality dictionary key type (e.g. `bool`, a small enum) will
  reliably fail at the default collection size of 3 — expected and
  correct, but worth a clear diagnostic message so it doesn't read as an
  engine bug the first time someone hits it.
- No enumeration-order guarantee for `HashSet<T>`/`Dictionary<TKey, TValue>`
  means a test asserting on generated-collection order for those two
  types is inherently fragile — this needs to be called out in
  `references/testing.md` once collection tests are written, so nobody
  writes an order-dependent assertion against an unordered type.

## Pros and Cons of the Options

### Default size — 3 (chosen)

- Good, because it's already the number used in a published
  `docs/public-api.md` example — no new number to justify from scratch.
- Bad, because it's arbitrary in the sense that any fixed default is —
  mitigated by Milestone 3 making it configurable.

### Duplicate keys — bounded retry, then diagnosable failure (chosen)

- Good, because it stays deterministic (retries are fork-derived, not
  randomly re-rolled) while still terminating.
- Good, because the failure is attributable and explains *why* (key
  space too small), not just "something went wrong."
- Bad, because it's more implementation complexity than either
  alternative — accepted as the direct cost of avoiding both an infinite
  loop and a silently undersized dictionary.

### Duplicate keys — unbounded retry

- Good, because it always eventually succeeds for any key type with
  enough cardinality.
- Bad, because a genuinely small key space (e.g. `bool`) hangs the
  composition operation indefinitely instead of failing clearly.

### Duplicate keys — silently overwrite

- Good, because it's the least code.
- Bad, because it silently produces a dictionary smaller than the
  requested count with no diagnostic — directly against
  `docs/architecture.md`'s "fail clearly" philosophy.

### Ordering — no guarantee (chosen)

- Good, because it promises exactly what the BCL types already promise,
  no more.
- Bad, because a consumer who wants stable snapshot/golden-file testing
  against a generated `HashSet<T>`/`Dictionary<TKey, TValue>` has to sort
  before comparing — an inherent property of those types, not something
  Compono can fix without wrapping them in a different type.

### Ordering — promise insertion order

- Good, because it would make golden-file testing against generated
  collections trivial.
- Bad, because it promises something `HashSet<T>`/`Dictionary<TKey, TValue>`
  themselves don't guarantee — either misleading (the promise doesn't
  actually hold across all BCL versions/implementations) or requires
  wrapping the types in a custom ordered collection, contradicting "use
  the requested BCL type directly."

## Amendment (2026-07-28): `HashSet<T>` uniqueness, a concrete recursion example, and the dispatch bridge

A final pre-implementation review of PLAN-0002 surfaced two gaps and
asked for one clarifying example:

**1. `HashSet<T>` needs the same bounded-retry uniqueness treatment as
`Dictionary<TKey, TValue>` keys — this ADR's original Decision Outcome
only specified it for dictionary keys.** A `HashSet<T>` has exactly the
same uniqueness constraint as a dictionary's key set (a set element can't
duplicate one already present), so the same rule applies verbatim:
generating an element that collides with one already in the set retries
(fork-derived by retry-attempt number, so still fully deterministic) up
to the same bounded internal retry limit as dictionary keys; exhausting
the limit is an authoritative `Failure` naming the element type and the
requested count, exactly as specified for dictionary keys above — not a
silently-undersized `HashSet<T>`. Both `Dictionary<TKey, TValue>` keys
and `HashSet<T>` elements share one internal retry-with-bounded-limit
helper rather than two parallel implementations, since the constraint and
the failure mode are identical.

**2. Concrete recursive-collection example.** The "recursive element
types need no collection-specific handling" claim in this ADR's Decision
Outcome is illustrated concretely with:

```csharp
public sealed record Node(List<Node> Children);
```

Composing a `Node` with no terminating registration/shared value
anywhere in the graph: `Node`'s frame is pushed
([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
when its generated plan is invoked; resolving its `Children` list
resolves element 0 via a `CollectionElement(0)` path segment
([ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)),
which is itself a `Resolve<Node>()` call reaching stage 8 again — finding
`Node`'s frame already active, this fails as an authoritative recursion
`Failure`. The diagnostic's cycle-edge chain includes the collection
index segment explicitly (e.g. `Node → Children[0] → Node`, not just
`Node → Node`), because `CollectionElement(0)` is a real node in
`CompositionPath` like any other segment — this is the concrete assertion
PLAN-0002's Phase 3 recursion test makes, replacing a vaguer
"`List<Node>` containing another `Node`" description.

**3. Collection dispatch bridge.** How a non-generic built-in collection
provider actually invokes `Resolve<TElement>()` generically for a
runtime-discovered element type (e.g. `Address` for a `List<Address>`
request) is specified in
[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
own amendment — a cached, construction-only generic delegate bridge, not
`Activator.CreateInstance`/`MethodInfo.Invoke` per value, and not a
collection-expansion loop emitted into the containing type's generated
plan. Collection requests stay ordinary pipeline requests
(`Resolve<List<Address>>()`), so a registration or higher-precedence
provider can still override built-in collection behavior for a specific
collection type — the bridge is purely an implementation detail of how
the built-in collection provider itself resolves each element/key/value
once it has already claimed the request.

## Links

- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) —
  stage 7 (built-in providers) and `Failure` semantics, which the
  duplicate-key retry-exhaustion case uses.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  the active-construction-frame mechanism collection elements participate
  in without any collection-specific recursion logic.
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) —
  `CollectionElement`/`DictionaryKey`/`DictionaryValue` path segments,
  defined there and consumed here.
- `docs/mvp.md` — Milestone 2's built-in type list; Milestone 3's
  collection-size configuration; the still-open "Nullability generation
  defaults" item this ADR deliberately doesn't resolve.
