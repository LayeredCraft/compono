# [ADR-0032] API Reference Documentation Toolchain

**Status:** Accepted

**Date:** 2026-08-04

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

[ADR-0030](0030-compono-documentation-architecture.md) designed
`reference/api/` into the documentation hierarchy but explicitly left its
generation toolchain as an Open Item, deferred to "a light-dive ADR of its
own once Milestone 8 reaches that work." That work has arrived.
`documentation.md` already requires XML doc comments on every public
member across all packages, framed explicitly as "the primary
discoverability surface" — this ADR decides how that XML documentation
becomes the published `reference/api/` pages ADR-0030 already scoped a
place for.

The site itself is MkDocs Material (`mkdocs.yml`), already carrying
Getting Started/Concepts/How-to/Cookbook/Samples/etc. as one coherent
nav, search index, and theme, deployed via `.github/workflows/docs.yml`.
Whatever generates the API reference has to fit into that existing single
site, not introduce a second one.

## Decision Drivers

- ADR-0030's Amendment 1 already states the philosophy this ADR must
  honor: "API reference supplements documentation, it never replaces
  it" — Reference is one section among twelve, not a separate product.
- One site, one theme, one nav, one search index, one deploy pipeline —
  splitting the API reference into a second site (its own theme/search)
  would directly contradict ADR-0030's entire premise: a single linear
  developer journey with Reference as a look-up destination reachable
  from anywhere in that same site, not a different site a reader has to
  leave and re-orient in.
- The generated pages must stay mechanically regenerable from the
  compiled assembly + XML doc file — hand-maintained reference content
  drifts from the real public API immediately (this is exactly why
  `documentation.md` makes XML doc comments a hard requirement in the
  first place).
- CI must be able to catch drift (generated output not committed, or
  regeneration producing a diff) and missing documentation on a public
  member, the same rigor `coding-standards.md`/`testing.md` already hold
  source code to.
- Five packages exist today, more may exist later (per ADR-0030's
  package-count-agnostic design) — the chosen tool has to scale to N
  packages without per-package bespoke templating.

## Considered Options

1. **A Markdown generator producing pages inside the existing MkDocs
   site** — a tool reads each package's compiled DLL + XML doc file and
   emits Markdown directly under `docs/reference/api/`, built by MkDocs
   alongside every other page.
2. **DocFX as a separate generated site** — DocFX's own theme, its own
   search, hosted at a sub-path or subdomain, cross-linked from the
   MkDocs site.
3. **Hand-written reference pages** — manually maintained Markdown
   summarizing the public API surface, no generation step.

## Decision Outcome

**Option 1 — a Markdown generator producing pages inside the existing
MkDocs Material site.** DocFX (Option 2) was rejected: its .NET API
rendering is genuinely strong, but running it means maintaining a second
theme, a second navigation tree, and a second search experience
alongside MkDocs — directly undermining ADR-0030's stated goal of one
coherent developer journey rather than "technically covered somewhere
across three documents" (the same anti-pattern ADR-0030's own Decision
Drivers already reject for package-oriented organization, now applied to
tooling). Hand-written reference (Option 3) was rejected: it contradicts
`documentation.md`'s existing stance that XML documentation and the
compiled public API are the source of truth, and would drift from the
real API immediately with nothing to catch it.

### Architecture

1. Every public package builds with `GenerateDocumentationFile=true`
   (already set repo-wide, `Directory.Build.props`) — XML doc comments on
   every public member is a pre-existing hard requirement
   (`documentation.md`), not new work this ADR introduces.
2. A .NET documentation generator runs against each package's compiled
   assembly + XML doc file as a CI step.
3. The generator emits deterministic Markdown under
   `docs/reference/api/<package>/`.
4. MkDocs Material builds those pages through the exact same
   theme/nav/search/deploy pipeline as every hand-written page — no
   separate build step, no separate deploy target.
5. CI fails the build when: regeneration produces uncommitted changes
   (drift between the checked-in pages and what the current source would
   generate), broken internal links, duplicate page paths, or — where the
   selected tool can detect it — a public member missing its required XML
   doc comment (belt-and-suspenders on top of the existing `CS1591`
   build warning `Directory.Build.props` already leaves unsuppressed).

### Tool selection: time-boxed evaluation, not decided by this ADR alone

This ADR commits to the *architecture* above and to evaluating
[`DefaultDocumentation`](https://github.com/Doraku/DefaultDocumentation)
as the leading candidate against
[`xmldocmd`](https://github.com/lunet-io/xmldocmd) (and any other
maintained MkDocs-Markdown-targeting generator surfaced during the
evaluation) — it does not permanently lock in one tool from this
conversation alone. PLAN-0008 Phase 1 runs a small, time-boxed bake-off
against a representative slice of Compono's real public API before
picking a winner, per the following criteria:

- Generic types and methods
- Overloads
- Inheritance and implemented interfaces
- Extension methods
- Attributes
- Nullable reference-type signatures
- `<see>`/`<seealso>` cross-references (including cross-package, e.g. a
  `Compono.XunitV3` type referencing a `Compono` core type)
- `<typeparam>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`
- Stable, predictable filenames and anchors (so hand-written pages can
  link into generated ones without the link breaking on regeneration)
- Output that reads well and navigates cleanly under MkDocs Material's
  theme specifically (not just "produces valid Markdown")
- Deterministic generation in CI (same input → byte-identical output,
  required for the drift-detection CI gate above)
- Current maintenance status and .NET/TFM compatibility with
  `net10.0;net11.0`

Selection criterion: the smallest actively-maintained tool that produces
accurate, navigable Markdown against this representative slice without
requiring substantial custom templating to compensate for gaps. If
neither `DefaultDocumentation` nor `xmldocmd` clears this bar, the
evaluation is recorded in PLAN-0008 Phase 1's own Notes and a wider
search happens then — not pre-decided here.

### What stays hand-written

Generated pages are mechanically produced and never hand-edited directly
— the same "generated file, don't hand-patch it" discipline this repo
already applies to `Compono.Generators`' own output. Explanation,
examples, package-level guidance, and conceptual cross-links belong in
the surrounding Concepts, How-to Guides, Package Guides, and
`reference/index.md` — which link *into* the generated pages rather than
duplicating their content, matching `documentation.md`'s existing
"avoid duplicating content between concept guides and reference material"
principle applied to the generated case specifically.

## Positive Consequences

- One site, one search index, one theme, one deploy pipeline — no
  divergent second documentation product to maintain.
- API reference regenerates automatically as the public API evolves,
  with CI catching drift instead of a human remembering to re-run a tool.
- The tool decision itself is evidence-based (a real bake-off against
  Compono's actual API surface) rather than a cold pick from
  documentation alone.

## Negative Consequences

- Whichever tool wins, MkDocs Material's rendering of generated C# API
  docs will likely be less polished than DocFX's purpose-built renderer
  in some edge cases (e.g. complex generic constraint formatting).
  Accepted: consistent with ADR-0030's own accepted tradeoff of
  organizing around the reader's journey over any one section's
  standalone polish.
- A small ongoing CI cost (a documentation-generation step, plus the
  drift-detection gate) is added to every PR that touches a public
  package's API surface. Accepted: proportionate to the "reference must
  never silently go stale" requirement this ADR exists to satisfy.

## Pros and Cons of the Options

### Markdown generator into MkDocs (chosen)

- Good, because it's one coherent site, matching ADR-0030's core premise.
- Good, because it reuses the existing deploy pipeline (`docs.yml`)
  entirely unmodified.
- Bad, because generated-Markdown-under-MkDocs is a less mature, less
  battle-tested path for C# API docs specifically than DocFX.

### DocFX as a separate site

- Good, because DocFX's C# API rendering (cross-references, generic
  formatting, inheritance diagrams) is more mature and purpose-built.
- Bad, because it means a second theme, nav, and search index —
  contradicting ADR-0030's single-developer-journey premise.

### Hand-written reference

- Good, because it requires no tooling investment at all.
- Bad, because it drifts from the real API immediately and contradicts
  `documentation.md`'s XML-doc-as-source-of-truth requirement.

## Links

- [ADR-0030](0030-compono-documentation-architecture.md) — the hierarchy
  this ADR fills in the `reference/api/` Open Item for; Amendment 1's
  "API reference supplements, never replaces" philosophy this ADR is
  built around
- `documentation.md` (engineering-workflow reference) — the pre-existing
  XML-doc-comment requirement this ADR's toolchain depends on
- `mkdocs.yml`/`.github/workflows/docs.yml` — the existing site/deploy
  pipeline this ADR's generated pages build through unmodified
- [PLAN-0008](../plans/0008-milestone-8-public-preview.md) — Phase 1 runs
  the tool bake-off and wires the winner into CI
