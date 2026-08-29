# Documentation writing

This file teaches *how to think about* documentation, not a fixed tree to
follow — Compono's actual documentation structure is decided elsewhere
(currently [ADR-0030](../../../../docs/adr/0030-compono-documentation-architecture.md)
and `docs/documentation-architecture.md`, kept current as that structure
evolves) and will keep changing as the project grows. The philosophy and
principles below are meant to stay stable across that evolution — apply
them whether today's actual hierarchy is Compono's current one, a future
revision of it, or a different project's entirely.

## Philosophy

Organize documentation around the **developer journey** — what a reader is
trying to do — rather than around implementation structure (namespaces,
packages, internal module boundaries). A newcomer arrives with a question,
not already knowing which package or class owns the answer; documentation
organized around the codebase's own boundaries makes them learn that
structure first, before it's useful to them.

Most documentation falls into four categories, adapted from the
[Diátaxis](https://diataxis.fr) framework:

- **Tutorials** — teach, end to end, for someone who's never done this
  before.
- **Concept guides** — build understanding of what something is and when
  to reach for it.
- **How-to guides** — help a reader accomplish a specific task they
  already have in mind.
- **Reference** — answer a precise question quickly, for a reader who
  already knows what they're looking for.

When writing or reviewing a document, ask which of these four it actually
is before writing a word — a page that tries to teach, explain, guide, and
document reference material all at once usually serves all four readers
poorly. This is a lens for thinking about a new document's shape, not a
mandate that every doc set must literally have four top-level folders named
exactly this — `docs/documentation-architecture.md` shows one concrete
application of this lens to Compono specifically (including areas beyond
the base four, like a Cookbook or a migration guide, that this project's
own evidence showed were worth splitting out).

## Principles

These are principles to weigh, not rules to mechanically enforce:

- Documentation should have a clearly defined audience — know who's reading
  before writing.
- Documentation should answer the questions developers naturally ask, not
  just describe what exists.
- Documentation should be organized around workflows rather than
  namespaces.
- Prefer many focused documents over one enormous document — a reader
  should be able to find and read just the part they need.
- Tutorials should teach.
- Concepts should build understanding.
- Guides should help developers accomplish a task.
- Reference should answer precise questions quickly, and should never be
  the *only* place a capability is documented — see "Quality" below.
- Cookbook-style documentation should solve exactly one practical problem
  per page — short, self-contained, copy/paste friendly.
- Architecture documentation should explain *why*, not merely *how* — the
  tradeoffs and rejected alternatives, matching the bar this repo's own
  ADRs are already held to (`design-decisions.md`).
- Migration guides should explain both mechanics (the API-level
  before/after) and philosophy (*why* the new approach is designed the way
  it is) — not just a mapping table.
- Documentation should evolve alongside implementation in the same PR (see
  "Existing guidance" below — this isn't new, just restated here as a
  principle among the others).

## Quality

- Prefer real examples over synthetic ones whenever practical — an example
  drawn from an actual use case is more trustworthy and more likely to
  reflect a real rough edge than one invented to illustrate a point in the
  abstract.
- Examples discovered while dogfooding a real project (an actual migration,
  an actual integration) are especially valuable — capture them while the
  context is fresh rather than reconstructing a plausible-sounding example
  later. Milestone 7's migration guide is the concrete precedent for this:
  every before/after example in it is drawn from the real
  `cosmere-tracker` migration, not invented, and a PR review caught the one
  place a fabricated "mixed inline/composed" example slipped in — see that
  guide's own history for what avoiding this looks like in practice.
- Avoid duplicating content between concept guides and reference material
  — a concept guide explains *what something is and when to use it*, and
  reference documents its *precise contract*; if the same explanation is
  fully repeated in both, one of them should instead link to the other.
- Link related documentation rather than repeating it. A page that needs
  context another page already covers should link there, not re-explain it
  — this keeps each piece of information owned by exactly one place, so it
  only needs updating once when it changes.
- Treat documentation as a product, not a by-product of writing code. Its
  quality, discoverability, consistency, and maintainability are part of
  what's being built, not an afterthought bolted on once the code is done
  — the same rigor this repo already holds source code to
  (`coding-standards.md`, `testing.md`) applies here too.

## Existing guidance

- Every topic doc in `docs/` follows the same shape: what it does, the
  decisions that shaped it, and *why* alternatives were rejected — not
  just a feature description. Look at `docs/architecture.md` or
  `docs/public-api.md` (now a tombstone deferring to the API-reference
  site) as the template.
- Update docs in the same PR as the behavior change, not as follow-up
  cleanup — once the shipped code diverges from the doc, update the doc
  in that same PR rather than letting "intended" quietly go stale against
  "actual."
- Longer external-reference or comparative-research notes, if they ever
  come up, get their own file rather than being folded into a topic doc —
  keep the writeup separate and have it point back at the ADR it fed into,
  the same way a topic doc points at the ADR that shaped it.
- Code should be self-documenting — clear names and small, well-shaped
  methods carry the "what." Inline comments explain the *why*: a
  workaround, a non-obvious invariant, or a genuinely non-obvious
  algorithm (e.g. why a particular resolution-pipeline ordering matters,
  or why a generator emits a given shape instead of an obvious
  alternative). If you find yourself writing a comment that just restates
  the line below it in English, delete the comment instead (or, more
  often, that's a sign the code below it should be renamed/restructured
  until it doesn't need the restating). Inline comments on genuinely
  non-obvious algorithmic code are encouraged and expected — the bar above
  is about *narration* comments, not about comments in general.
- **XML doc comments are required on every public member** — classes,
  interfaces, methods, properties, and events — across all projects. This
  matters more than usual here: `Compono` and its integration packages are
  published NuGet libraries, and IntelliSense/hover documentation *is* the
  primary discoverability surface for a consumer who's never read the
  source, which is exactly the "easy to discover" goal `docs/public-api.md`
  states. There's no existing code to carry forward as debt yet, so treat
  this as a hard requirement from the first public member added, not a
  backfill project. A good XML doc comment states what the member does and
  any contract a caller needs to know (thrown exceptions, null behavior,
  ordering requirements) — it shouldn't just restate the member's name in
  sentence form, the same "why, not what" bar inline comments are held to
  above.
- Commit messages: explain *why*, not *what* — the diff already shows what
  changed.
