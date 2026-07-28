# [ADR-0012] Composition Path Identity, Deterministic Random Forking, and CreateMany Seed Derivation

**Status:** Accepted

**Date:** 2026-07-28

**Decision Makers:** solo

## Context

[ADR-0009](0009-deterministic-seed-and-forkable-random-source.md) decided
*that* random forking is key-based and PRNG-owned by Compono, but assumed
`CompositionPath` was already a sufficient source of fork keys — modeled
as an ancestor chain of *requested types*. A design review showed that's
not sufficient identity:

```csharp
Customer(string FirstName, string LastName)
```

Both parameters share a requested type (`string`) and path depth — a
type-only path can't distinguish `FirstName` from `LastName`, so they'd
fork identically and receive the same generated value. The same ambiguity
applies to two required properties of the same type, collection elements,
dictionary keys versus values, and nested collection indices. Separately,
`CreateMany<T>(count)` needs a precise contract for how item `i`'s root
seed relates to the batch's root seed — "fresh scope per item"
([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
doesn't by itself say whether item 0 of a 3-item batch and item 0 of a
10-item batch produce the same value.

This ADR supersedes ADR-0009's path/forking content. ADR-0009's core
choice — a Compono-owned PRNG rather than `System.Random`, FNV-1a for key
derivation, "same seed/same output within a `Compono` version" as the
stability guarantee — remains correct and is carried forward unchanged.

## Decision Drivers

- The determinism guarantee (`README.md`'s "Deterministic by design") is
  only as strong as the path identity feeding it — an ambiguous path
  silently produces wrong-but-plausible determinism (two different
  members getting the same value), which is worse than an obviously
  broken one.
- No source locations, no runtime object hashes (`GetHashCode()`,
  reference identity) — determinism must survive process boundaries and
  be unaffected by unrelated source edits, per the explicit constraint in
  the design review.
- `docs/architecture.md`'s explicit goal: adding an unrelated member
  elsewhere in the graph must not change an already-resolved member's
  value — path identity has to be structural (derived from *where* a
  value sits in the graph), not derived from anything that shifts when
  unrelated code changes (call order, iteration order of reflection APIs,
  etc.).
- `CreateMany`'s output for items 0..2 should stay stable whether the
  batch requests `count: 3` or `count: 10` — appending more items
  shouldn't perturb earlier ones, the same principle
  `docs/architecture.md` already applies to unrelated graph members.

## Considered Options

**Path segment representation:**
1. Type-only ancestor chain (ADR-0009's original design) — insufficient,
   kept only as the rejected baseline.
2. A single incrementally-built string key (e.g. `"Customer/FirstName"`,
   `"Customer/Items[2]"`), consumed directly by both diagnostics and
   FNV-1a hashing.
3. A structured, typed `PathSegment` list as the source of truth — used
   directly (not via string formatting) for hashing, with a display
   string derived from it only for human-readable diagnostics.

**`CreateMany` seed derivation:**
1. Each item gets an independently generated (non-deterministic) root
   seed, even under an explicit batch seed.
2. Each item forks from the batch's root seed using a stable per-item key
   (`"CreateMany"` then the item's index), so item `i`'s output is stable
   regardless of `count`.

## Decision Outcome

**Path segment representation: Option 3, structured segments.**
`CompositionPath` is a chain of nodes, each carrying the requested `Type`
and an optional structural segment identifying *how* that node was
reached from its parent (`null` only at the root):

```csharp
internal abstract record PathSegment
{
    private PathSegment() { }

    internal sealed record ConstructorParameter(string Name) : PathSegment;
    internal sealed record RequiredMember(string Name) : PathSegment;
    internal sealed record CollectionElement(int Index) : PathSegment;
    internal sealed record DictionaryKey(int Index) : PathSegment;
    internal sealed record DictionaryValue(int Index) : PathSegment;
}

internal readonly record struct CompositionPathNode(Type RequestedType, PathSegment? Segment);
```

Identity uses **name**, not constructor-parameter ordinal — a
constructor parameter's or required member's *name* is what generated
code and diagnostics already refer to (`docs/architecture.md`'s existing
tree diagram uses names: `Customer → Email`), and it's stable under
constructor-parameter reordering, which ordinal is not. (Renaming a
parameter is a source edit that changes what the parameter *means* and is
expected to perturb its derived value; reordering unrelated parameters
around it is not, and name-based identity is immune to the latter while
ordinal-based identity wouldn't be.)

`ConstructorParameter`/`RequiredMember` segments come from
`CompositionRequestDescriptor.Kind`/`Name`
([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)) —
generated code supplies the name, `CompositionContext` builds the
segment. `CollectionElement`/`DictionaryKey`/`DictionaryValue` segments
are never generator-supplied — they're constructed internally by the
built-in collection providers themselves as they resolve each element
([ADR-0013](0013-collection-generation-semantics.md)), since collections
aren't part of the generated-plan/descriptor call path at all.

Random forking hashes the **structured segment data directly** — never a
formatted display string. Each segment kind has a fixed tag byte (to
guarantee no cross-kind collision, belt-and-suspenders on top of the fact
that C# identifiers can't collide with synthetic index formatting) plus
its identifying payload (the name, or the index as bytes); FNV-1a
(ADR-0009's existing choice) combines the tag+payload with the parent
node's derived state to produce the child's seed. A display string (e.g.
`"Customer.Email"` or `"Customer.Items[2]"`) is derived from the same
structured path **only** for human-readable diagnostic output — it is
never the input to hashing, which is what avoids any string-escaping or
formatting-collision risk entirely, not just mitigates it.

**`CreateMany` seed derivation: Option 2.** `CreateMany<T>(count)`
derives item `i`'s independent root seed by forking the batch's root seed
(explicit via a future `WithSeed(...)`, or generated once per call) with
a stable two-level key: fork by the literal key `"CreateMany"`, then fork
that result by `i`'s value (as a canonical decimal string, matching the
same tag+payload FNV-1a mechanism as path segments above — index-based,
not existence-of-neighboring-items-based). Concretely:

```text
batchRootSeed
  └─ fork("CreateMany")
       ├─ fork("0") → item 0's root seed
       ├─ fork("1") → item 1's root seed
       └─ fork("2") → item 2's root seed
```

Because each item's derivation depends only on its own index and the
batch root — never on `count` — items 0–2 of a `CreateMany<T>(3)` call
produce byte-for-byte identical output to items 0–2 of a
`CreateMany<T>(10)` call with the same root seed. This mirrors
`docs/architecture.md`'s "unrelated member elsewhere in the graph doesn't
change an existing value" guarantee at the batch level: an unrelated
*item* (index 3 through 9) doesn't change an earlier one.

Standalone `Create<T>()` calls are unaffected by any of this: repeated
calls with the same explicit seed are simply that seed used as the root,
directly — no hidden per-call counter or forking, so two independent
`Create<T>()` calls with the same explicit seed are fully identical, by
construction (there's no batch-key fork applied outside of
`CreateMany`).

### Positive Consequences

- Two constructor parameters of the same type reliably produce different
  values — the core bug the design review surfaced is closed structurally,
  not by convention.
- Hashing structured data instead of a formatted string eliminates an
  entire class of potential key-collision bugs (ambiguous string
  boundaries) before they can occur, rather than requiring careful
  escaping logic to avoid them.
- `CreateMany`'s stability-under-count-change guarantee is precise and
  testable (items 0–2 identical whether `count` is 3 or 10), not just
  "probably stable in practice."

### Negative Consequences

- A structured `PathSegment` chain is more allocation than a plain
  `Type[]` — each node is a small record, though as an `internal`,
  short-lived, per-composition-operation structure this is expected to be
  cheap relative to the values actually being generated; worth confirming
  against the same benchmark gate ADR-0010's diagnostics tracing is
  subject to.
- Collection-element segments (`CollectionElement`/`DictionaryKey`/
  `DictionaryValue`) are constructed by the collection providers
  themselves rather than flowing through the generic descriptor path —
  this means `PathSegment` has two different producers (generated code
  via descriptors, and internal collection providers directly), which
  has to stay consistent as new segment kinds are ever added.

## Pros and Cons of the Options

### Path — type-only ancestor chain (rejected baseline)

- Good, because it's the simplest possible structure.
- Bad, because it can't distinguish two same-typed constructor parameters
  or members — the exact bug this ADR exists to fix.

### Path — single incrementally-built string key

- Good, because it matches `docs/architecture.md`'s existing dot-path
  diagram most literally, and is simple to log/inspect directly.
- Bad, because using the string as the hash input (rather than only for
  display) means any future segment kind or separator choice has to be
  proven collision-free by construction — solvable, but a self-imposed
  constraint the structured option doesn't need at all.

### Path — structured segments, hashed directly (chosen)

- Good, because hashing structured tag+payload data sidesteps
  string-collision concerns entirely rather than requiring careful
  escaping to avoid them.
- Good, because the same structure serves diagnostics (via a derived
  display string) and forking (via direct structural hashing) without
  either concern constraining the other's design.
- Bad, because it's two code paths conceptually (segment construction,
  display-string derivation) instead of one string built once — mitigated
  by the display-string derivation being a small, purely presentational
  function with no correctness burden.

### `CreateMany` seed — independent seed per item

- Good, because it's the simplest possible rule — no forking logic
  needed at all.
- Bad, because it isn't deterministic from the batch's root seed at all
  (defeats the entire point of an explicit seed for a `CreateMany` call),
  and directly contradicts the product's determinism goal.

### `CreateMany` seed — stable per-item fork from batch root (chosen)

- Good, because it gives `CreateMany` the same determinism guarantee
  `Create<T>()` already has, plus stability under `count` changes.
- Good, because it reuses the exact same tag+payload FNV-1a mechanism as
  path-segment forking — one derivation primitive, two call sites.
- Bad, because it's one more concrete contract to test (stability across
  differing `count` values specifically) beyond ordinary same-seed-same-
  output determinism.

## Amendment (2026-07-28): ordinal-based identity, and an explicit ban on hashing type identity

A final pre-implementation review of PLAN-0002 asked for stable
constructor/member identity beyond names, and an explicit statement of
what may and may not feed seed/path hashing. Two changes to this ADR's
Decision Outcome:

**1. `PathSegment` identity is ordinal-based; names are diagnostic-only.**
This ADR's original text argued for name-based identity ("a constructor
parameter's or required member's *name*... is stable under
constructor-parameter reordering, which ordinal is not"). On review, this
is reversed: **ordinal is the identity `PathSegment` hashes on; `Name` is
carried for diagnostic display only, never for hashing.**

```csharp
internal abstract record PathSegment
{
    private PathSegment() { }

    internal sealed record ConstructorParameter(int Ordinal, string Name) : PathSegment;
    internal sealed record RequiredMember(int Ordinal, string Name) : PathSegment;
    internal sealed record CollectionElement(int Index) : PathSegment;
    internal sealed record DictionaryKey(int Index) : PathSegment;
    internal sealed record DictionaryValue(int Index) : PathSegment;
}
```

- `ConstructorParameter.Ordinal` is the parameter's position in the
  constructor [ADR-0002](0002-constructor-selection-algorithm.md)
  selected for the declaring type — a stable, generator-known value
  requiring no new convention.
- `RequiredMember.Ordinal` is a **generator-controlled** stable index:
  required members are numbered in their syntactic declaration order
  within the type's primary declaration (the same order the Milestone 1
  generator already walks required members in, per
  [ADR-0006](0006-required-members-and-nullability-metadata.md)),
  assigned once per type at generation time. This ordering is
  deterministic for a given source layout and is unaffected by an
  unrelated member being renamed elsewhere in the type.
- The fork-key hash (FNV-1a over tag + payload, per this ADR's original
  Decision Outcome) uses **`Ordinal`**, not `Name`, as the payload for
  both segment kinds. `Name` is carried on the segment purely so the
  display-string derivation (used only for human-readable diagnostics,
  never for hashing, per the original Decision Outcome) can render
  `"Customer.firstName"` instead of `"Customer.[param 0]"`.
- Practical consequence: renaming a constructor parameter or required
  member (with no reordering) no longer perturbs that member's derived
  value — only reordering does. This is a deliberate reversal of this
  ADR's original reasoning, made because generator-controlled ordinal
  stability is a stronger, more directly guaranteed property than
  name-based stability, and because names are exactly the kind of
  surface-level identifier a source edit changes without intending to
  change semantics.

`CompositionRequestDescriptor` ([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
amendment) carries the matching `Ordinal` field the generator supplies
alongside `Name`.

**2. Type identity is never an input to hashing — explicitly.** Nothing
in this ADR's fork-key derivation has ever used `RequestedType` (the
`Type` each `CompositionPathNode` carries) as hash input — only
`PathSegment` tag+payload data does, chained through parent state. This
amendment makes that explicit rather than leaving it implied:
**`Type.GetHashCode()` and any other runtime-reflection-derived
identifier (e.g. a handle, a token, `RuntimeTypeHandle`) must never feed
seed or path hashing** — .NET does not guarantee `Type.GetHashCode()` is
stable across separate process runs, which would silently break the
"same seed, same output" guarantee for exactly the reason this ADR's
Context section already excludes runtime object hashes. `RequestedType`
on a `CompositionPathNode` exists solely for provider dispatch
(`PlanCache<T>` generic lookup, [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)'s
active-construction-frame keys — both ordinary reference/value equality
checks, which only need to be correct *within one process run*, not
stable *across* runs) and for diagnostic display. If a future need ever
required type identity to feed a hash (none exists in Milestone 2), the
only acceptable encoding would be a compile-time-stable string — e.g. the
generator-emitted fully-qualified name
(`SymbolDisplayFormat.FullyQualifiedFormat`, already
`coding-standards.md`'s mandated convention for generated code) — never
a runtime-computed hash code or reflection token.

## Amendment 2 (2026-07-28): required-member ordinal algorithm, tag-collision coverage, and the reproducibility contract

A final pre-Phase-0 review of PLAN-0002 asked for a fully specified
required-member ordinal algorithm (Amendment 1 only said "declaration
order," which under-specifies partial declarations, base members, and
generator-produced members), explicit test coverage proving segment kinds
don't collide at the same ordinal/index, and for the "no type identity in
hashing" rule to be stated as a guarantee, not just an implementation
note.

**1. Canonical required-member ordinal algorithm.** For a composed type
`T`, Compono's generator assigns `RequiredMember.Ordinal` by:

1. Build `T`'s inheritance chain from its ultimate base (excluding
   `object`) down to `T` itself — base-to-derived order.
2. For each type in that chain, take that type's `INamedTypeSymbol.GetMembers()` —
   Roslyn's own order, which already merges all of a (possibly partial)
   type's declaring syntax references deterministically for a given
   compilation, regardless of how many files the type is split across —
   filtered to required members **declared directly on that type** (not
   inherited; inherited ones are already covered by an earlier link in
   the chain).
3. Concatenate each type's filtered sequence in chain order (base
   members first, then the type's own).
4. `Ordinal` is the resulting sequence's index (`0, 1, 2, ...`).

This delegates "how do partial declarations merge" entirely to Roslyn's
own `GetMembers()`, which is deterministic per compilation, rather than
Compono re-deriving syntax-tree ordering itself — and it handles a
required member added by *another* source generator identically to a
hand-written one, since by the time Compono's generator queries the
semantic model, both are equally just declaring syntax references on the
symbol. The one honest limitation: a required member added by a
sibling incremental generator whose output Compono's generator's semantic
model can't observe in the same generation pass (a Roslyn platform
constraint on inter-generator visibility, not a Compono gap) won't appear
in this algorithm's input at all. Rather than silently mis-numbering,
Compono's generator reports a diagnostic (matching
[ADR-0002](0002-constructor-selection-algorithm.md)'s existing
unsupported/ambiguous-construction pattern) when it detects — via the
same required-member-coverage check it already needs for construction
correctness — that its own view of required members doesn't fully
account for what construction needs.

**2. Explicit tag-collision test requirement.** Ordinal/index `0` is
reused across every `PathSegment` kind (`ConstructorParameter(0, ...)`,
`RequiredMember(0, ...)`, `CollectionElement(0)`, `DictionaryKey(0)`,
`DictionaryValue(0)`) — the per-kind tag byte (this ADR's original
Decision Outcome) is what's supposed to keep them from colliding. This
needs a direct test, not an inference from "we included a tag byte":
Phase 1 (PLAN-0002) adds a determinism test that forks all five kinds at
ordinal/index `0` from the *same* parent state and asserts all five
produce pairwise-distinct output. This is the concrete proof the tag
mechanism does what Amendment 1 claimed.

**3. Structural-position-based random identity is a reproducibility
*contract*, not an implementation detail.** Stated explicitly, as a
guarantee `Compono` documents and tests hold it to, not merely a property
that happens to fall out of the current design: **a resolved value's
random identity is derived exclusively from its structural position in
the composition graph — the chain of `PathSegment` tags and
ordinals/indices from the root — and never from `CompositionRequest.RequestedType`
or any other type identity.** Two members at the same structural position
under the same seed always produce the same output regardless of what
concrete type occupies that position; conversely, the *same* concrete
type at two different structural positions forks independently. This is
the guarantee `docs/architecture.md`'s "changing an unrelated member
doesn't change an existing value" goal, this ADR's `CreateMany`
stability contract, and Amendment 1's "type identity never feeds hashing"
rule are all specific consequences of — worth stating once, as the
contract those are downstream of, rather than leaving a reader to infer
it from three separate places.

## Links

- Supersedes [ADR-0009](0009-deterministic-seed-and-forkable-random-source.md)
  (kept as a historical record; its PRNG-ownership and stability-guarantee
  decisions are restated here unchanged).
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) —
  `CompositionRequestDescriptor`, the source of `ConstructorParameter`/
  `RequiredMember` segments.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  keeps `CompositionPath` (this ADR) distinct from the active-construction-
  frame stack used for recursion detection.
- [ADR-0013](0013-collection-generation-semantics.md) — the producer of
  `CollectionElement`/`DictionaryKey`/`DictionaryValue` segments.
