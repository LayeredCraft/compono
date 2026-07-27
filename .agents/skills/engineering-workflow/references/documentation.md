# Documentation writing

- Every topic doc in `docs/` follows the same shape: what it does (or is
  intended to do, for the pre-implementation docs), the decisions that
  shaped it, and *why* alternatives were rejected — not just a feature
  description. Look at `docs/architecture.md` or `docs/public-api.md` as
  the template.
- Update docs in the same PR as the behavior change, not as follow-up
  cleanup. For a doc describing target/intended shape rather than shipped
  behavior (`docs/mvp.md`, parts of `docs/public-api.md`), the same rule
  applies in reverse: once the shipped code diverges from the doc, update
  the doc in that same PR rather than letting "intended" quietly go stale
  against "actual."
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
