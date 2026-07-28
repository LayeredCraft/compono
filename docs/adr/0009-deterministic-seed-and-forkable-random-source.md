# [ADR-0009] Deterministic Seed and Forkable Random Source

**Status:** Superseded by ADR-0012

**Date:** 2026-07-28

**Decision Makers:** solo

## Context

`README.md`'s "Deterministic by design" and `docs/mvp.md`'s Milestone 2
scope both require a root seed and a "forkable random source" — every
composed value must be reproducible from a seed, and unrelated members
added elsewhere in a graph must not shift values for members that already
existed (`docs/architecture.md`'s stated goal: "This reduces accidental
changes when unrelated members are added elsewhere in a graph"). The
architecture doc already sketches the shape (a root seed, forked by stable
path-segment keys, shown as a tree: `root seed → test parameter: command →
Customer → Email`) but doesn't name the actual forking algorithm, and
flags its own caveat: "Compono should not promise that generated values
remain identical across all library versions unless that guarantee can be
maintained."

The `.NET` BCL's `System.Random` is a candidate seeded PRNG, but it isn't
a free choice: forking by key means deriving a *new* seed per path
segment, and the derivation function is exactly the source of stability
Compono needs to control and document — not something to inherit silently
from a BCL implementation detail.

## Decision Drivers

- Determinism is a stated product goal, not an implementation nicety —
  the exact stability guarantee has to be something Compono controls and
  documents, not something that rides along with the BCL's constantly-
  evolving `System.Random` algorithm.
- Forking must be by stable key (a path segment, e.g. `"Customer.Email"`),
  not by call order — the architecture doc's explicit goal is that adding
  an unrelated member elsewhere in the graph doesn't change already-stable
  values.
- No I/O, no allocation-heavy cryptographic hashing on a path that runs
  once per composed value — this sits on the same synchronous hot path
  ADR-0007 already optimized for.
- No reflection, no external dependency — this stays inside `Compono`
  core with no new package reference.

## Considered Options

1. Seed each path segment's `System.Random` with a derived `int` seed
   (parent seed combined with a hash of the key), relying on the BCL's
   seeded `Random` for everything downstream.
2. A small, explicitly-owned deterministic PRNG algorithm (e.g. SplitMix64
   for seed derivation, feeding a xoshiro-family generator for value
   generation), with the derivation function specified and tested by
   Compono itself rather than inherited from the BCL.
3. A single root `System.Random` shared across the whole graph, with no
   per-key forking at all (call order determines the sequence).

## Decision Outcome

**Option 2.** `IRandomSource` (internal to `Compono` core) owns a small,
explicitly-specified deterministic derivation function: forking by key
combines the parent's 64-bit state with a deterministic hash of the key
string (FNV-1a — already the algorithm `coding-standards.md` mandates
elsewhere in this repo for stable hashing, so this reuses an established,
tested primitive rather than introducing a second hash algorithm) to
derive the child's seed. Value generation itself uses a small, explicitly
owned PRNG (not `System.Random`) so the byte-for-byte output sequence is
something Compono's own code controls and can keep stable across .NET
runtime versions — not an implementation detail Microsoft is free to
change (as it already has once, moving off the pre-.NET-6 `Random`
algorithm).

Forking is by **stable string key** derived from the current
`CompositionPath` segment (e.g. `"Customer"`, then `"Customer.Email"`),
matching the tree diagram already in `docs/architecture.md`. Two sibling
requests at the same path depth with different keys fork independently;
re-resolving the same path produces the same child state. The root
`CompositionSeed` is either explicit (a future `WithSeed(...)` builder
call, Milestone 3) or generated once per root composition operation from
a non-deterministic source (e.g. `Random.Shared`) and reported in
diagnostics on failure, matching `docs/architecture.md`'s diagnostic
example ("Seed: 8492173").

Compono's stability guarantee, made explicit here rather than left open:
**the same seed produces the same output for a given `Compono` package
version.** Cross-version stability (a value staying identical after a
`Compono` upgrade) is explicitly *not* promised — the derivation algorithm
and PRNG are implementation details that may change between `Compono`
releases, same as any library's seeded-random contract. This resolves
`docs/mvp.md`'s open "Deterministic output compatibility guarantees"
item for Milestone 2's scope.

### Positive Consequences

- Compono owns its determinism guarantee outright instead of inheriting
  one from `System.Random`'s implementation, which the BCL doesn't
  contractually promise to keep stable.
- Reusing FNV-1a (already mandated for generator hint-name hashing)
  avoids introducing a second hashing algorithm into the codebase for
  what is, at the derivation-function level, the same kind of problem
  (deterministic string → stable numeric value).
- The explicit "same seed, same `Compono` version" guarantee gives a
  concrete, testable claim instead of an open question.

### Negative Consequences

- A hand-rolled PRNG is more code to write and test than
  `new Random(seed)` — accepted, because the alternative (Option 1)
  produces a weaker guarantee that contradicts the product's own stated
  goal.
- Cross-version value stability isn't promised, so a `Compono` upgrade can
  change generated values even with the same seed — this has to be called
  out clearly in the next public-facing docs pass (changelog/versioning
  policy, Milestone 8 scope), not silently discovered by a consumer.

## Pros and Cons of the Options

### `System.Random` per fork, BCL-seeded (Option 1)

- Good, because it's the least code — no PRNG to write or test.
- Bad, because the stability guarantee it produces isn't one Compono
  actually controls; the BCL has changed `Random`'s algorithm before.
- Bad, because deriving a fork seed from a string key still needs a hash
  function chosen and owned by Compono either way — Option 1 doesn't
  actually avoid that design question, it just also inherits a second,
  weaker guarantee on top of it.

### Explicitly-owned deterministic PRNG + FNV-1a key derivation (chosen)

- Good, because the entire determinism chain (key → derived seed → PRNG
  output) is Compono's own, testable, documented code.
- Good, because it reuses an algorithm (FNV-1a) already established and
  tested in this repo for a structurally similar problem.
- Bad, because it's more implementation work than reaching for
  `System.Random` — accepted as the direct cost of the stronger
  guarantee this ADR commits to.

### Single shared root `Random`, no per-key forking

- Good, because it's the simplest possible implementation.
- Bad, because it directly contradicts `docs/architecture.md`'s explicit
  goal that unrelated graph changes shouldn't perturb already-stable
  values — call order becomes the hidden key, which is exactly what the
  architecture doc says to avoid.

## Links

- `docs/architecture.md` — Deterministic Randomness section; updated
  alongside this ADR to record the resolved algorithm choice and
  stability guarantee.
- `references/coding-standards.md` — FNV-1a's existing mandate for
  generator hint-name hashing, reused here.
- `docs/mvp.md`'s "Open Decisions Before Implementation" — resolves
  "Deterministic output compatibility guarantees" for Milestone 2's
  scope.
- [ADR-0007](0007-composition-request-and-provider-pipeline.md) —
  `CompositionRequest.Path`, the source of fork keys.
