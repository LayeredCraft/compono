# [ADR-0001] Source Generation First

**Status:** Accepted

**Date:** 2026-07-27

**Decision Makers:** solo

## Context

Compono needs a default construction/composition strategy for how object
graphs get built. Modern .NET has moved decisively toward source
generators, trimming, and Native AOT — but the established prior art in
this space (AutoFixture and similar) is built around runtime reflection.
`docs/manifesto.md` commits Compono to asking "what would a test
composition framework look like if designed for modern .NET today?"
rather than reproducing that reflection-based model, so this decision —
whether generated plans or runtime reflection is the default execution
path — has to be settled before any construction logic gets built, since
it shapes the runtime model, the generator's responsibilities, and the
package boundaries (`docs/architecture.md`'s "Package Boundaries")
underneath almost everything else in the MVP.

## Decision Drivers

- Predictable, measurable performance (`docs/manifesto.md`'s "Performance
  is a feature") — composition runs on every test, so runtime cost compounds.
- Trimming and Native AOT compatibility (`README.md`'s stated goals) —
  reflection-heavy construction is exactly the kind of code that breaks
  trimming.
- Deterministic, explainable behavior (`docs/manifesto.md`'s "Predictability
  over magic") — a simpler runtime model is easier to reason about and
  diagnose.
- Keeping the door open for compatibility scenarios (external/dynamically
  discovered types, migration friction from AutoFixture) without letting
  that need leak into and complicate the default architecture.

## Considered Options

1. Generated plans required — composition fails when no generated plan exists.
2. Automatic reflection fallback — the runtime reflects when no generated plan exists.
3. Opt-in compatibility package or mode — reflection support isolated from the default runtime, available only through explicit opt-in.

## Decision Outcome

Chosen option: a combination of **"Generated plans required" as the
default** and **"Opt-in compatibility package or mode" as the only path
to ever add reflection support**, with "Automatic reflection fallback"
rejected outright. Generated composition plans are the primary execution
mechanism; runtime reflection is intentionally excluded from the default
architecture. If reflection support is ever added, it must require an
explicit opt-in by the consuming project — it must never silently become
the fallback path.

Compono is being designed for modern .NET rather than older
reflection-based architectures. Choosing source generation by default
improves performance, diagnostics, trimming, Native AOT readiness, and
deterministic execution while keeping the runtime simpler.

### Positive Consequences

- Predictable performance and a simple runtime model, since there's no
  hidden reflection path to reason about.
- Strong trimming and Native AOT characteristics by default.
- Forces construction gaps to surface as compile-time generator
  diagnostics instead of being silently papered over at runtime.

### Negative Consequences

- External or dynamically discovered types may require explicit support
  before they can be composed — mitigated by leaving a documented,
  explicit opt-in path open rather than closing the door on compatibility
  entirely.
- Some AutoFixture-migration scenarios may be less convenient out of the
  box; this tradeoff is intentional (`docs/mvp.md`'s "not an AutoFixture
  migration layer").

## Pros and Cons of the Options

### Generated plans required

Composition fails when no generated plan exists.

- Good, because it gives predictable performance.
- Good, because it gives strong trimming and Native AOT characteristics.
- Good, because it keeps the runtime model simple.
- Bad, because external or dynamically discovered types may require
  explicit support.
- Bad, because some test scenarios may be less convenient.

### Automatic reflection fallback

The runtime reflects when no generated plan exists.

- Good, because it gives high compatibility.
- Good, because it lowers migration friction from reflection-based tools.
- Bad, because it makes the runtime more complex.
- Bad, because it weakens AOT guarantees.
- Bad, because it makes performance less predictable.
- Bad, because reflection can silently hide source-generation gaps that
  should have been surfaced as diagnostics.

### Opt-in compatibility package or mode

Reflection support is isolated from the default runtime, available only
through explicit opt-in.

- Good, because it keeps the core architecture clean.
- Good, because it allows compatibility where necessary without making it
  the default.
- Good, because it makes performance tradeoffs explicit to whoever opts in.
- Bad, because the exact mechanism (a separate package, a compiler-visible
  MSBuild property, something else) is still undecided — see
  `docs/architecture.md`'s "Open Architectural Decisions".

## Links

- `docs/manifesto.md` — "Source-generated first" and "Modern .NET first"
  principles this decision implements.
- `docs/architecture.md`'s "Runtime Reflection Policy" section — the
  fuller writeup of these three candidate approaches, and the "Open
  Architectural Decisions" list, which still tracks the unresolved
  question of exactly how a future opt-in reflection mode would be shaped.
- `docs/design-principles.md`'s "Reflection Policy" section.
