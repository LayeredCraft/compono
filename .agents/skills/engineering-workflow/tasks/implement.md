# Task: Implement

**Trigger:** asked to build/implement a feature, write the code for a
slice, or "let's build this now" — following up on `tasks/design.md`
having produced an `Accepted` ADR (and usually a `Plan`). Distinct from
`tasks/design.md`, which decides *what* and *why* and never touches code;
this task is *how*, and assumes the decision is already made. If asked to
implement something with no `Accepted` ADR behind it and the change is
non-trivial (per `design-decisions.md`'s light/deep-dive bar), stop and
run `tasks/design.md` first rather than writing code against an
undocumented decision — retrofitting an ADR after the fact produces a
worse record than writing it first.

## Load these references first

Always:
- `references/coding-standards.md`
- `references/testing.md`
- `references/documentation.md`
- `references/design-decisions.md` (just the ADR/plan status rules — you
  already did the design thinking in `tasks/design.md`)

Conditionally, based on what the ADR/plan actually touches:
- `references/security.md` — the source generator's trust boundary,
  package supply chain, or config/secrets

## Procedure

1. **Confirm there's an `Accepted` ADR before writing any code.** Find it
   (check `docs/adr/README.md`'s index, or the plan referenced by the
   request) and read its Decision Outcome — that's the spec you're
   building against, not your own read of the request. If a `Plan` exists,
   **(re-)read the plan file from disk now**, even if it was already
   discussed earlier in this same conversation — the plan file is the
   source of truth for status/scope, not conversation memory
   (`design-decisions.md`'s "Writing a Plan" section), and treating what
   you already recall as current is exactly how a plan's Tasks checklist
   drifts from what's actually true. If its `Status` is still `Not
   Started` and the ADR is now `Accepted`, flip it to `In Progress` as you
   begin.
2. **Implement per `coding-standards.md`** — naming, nullable annotations
   (comment every `!`), async patterns, traditional constructors for new
   classes, DI-only wiring (no static singletons), 4-param-max methods,
   file layout. Prefer composition over inheritance for the object model —
   a new provider/profile rule, never a new subclass of `Composer`/
   `CompositionContext`. Keep the core `Compono` package free of any
   reference to an integration package (`design-decisions.md` rule 3). XML
   doc comments on every new/substantially-changed public member
   (`documentation.md`) — required from the first public member, not a
   backfill project.
3. **Add tests as you build, not after.** New composition-engine/provider/
   generator behavior gets test coverage per `testing.md` (xUnit v3 on
   Microsoft Testing Platform, AAA with blank-line separation, handwritten/
   explicit test data — **not** the `dotnet-unit-testing-patterns` skill's
   usual AutoFixture guidance; see `testing.md` for why that's deliberately
   out for this repo specifically).
4. **Update the plan as reality meets it, not just at the end.** Check off
   tasks as they're actually done; if the implementation diverges from
   what the plan scoped (a file wasn't needed, an extra one was), edit the
   plan's task list to match — a plan being wrong about *how* doesn't
   require touching the ADR, which stays immutable (`design-decisions.md`).
5. **Build and run the full test suite** before treating anything as done
   — `dotnet build` then `dotnet test`, not just "it compiled."
6. **Update `docs/*.md` in the same change**, not a follow-up
   (`documentation.md`, `contributing.md`) — mark previously "not
   implemented"/stub sections as done, close the Open Items this work
   resolves, add "Verified" detail once you've actually checked the
   behavior, link back to the ADR rather than re-deriving its reasoning.
7. **Do real manual verification for anything source-generator-facing.**
   Unit tests alone don't fully cover generator behavior — a generator can
   pass its own tests while still producing code that fails to compile in
   a real consuming project, or that behaves differently across IDE vs.
   command-line builds. Actually build a small consuming project against
   the change (or run one of the MVP milestones' target scenarios from
   `docs/mvp.md` if applicable), exercise the golden path and the edge case
   the ADR called out, and record what you did in the doc's Verified
   section — not just "tests pass."
8. **Close out the plan.** Once every task is checked off and verification
   is done, set the plan's `Status: Done`. If this implements a slice of a
   milestone tracked in `docs/mvp.md`, check off that slice there too.
9. **Commit, and open a PR if asked**, per `contributing.md`'s "keep PRs
   scoped to one decision or one feature" rule — don't bundle an unrelated
   fix or refactor into the same diff just because you're already touching
   nearby code (`code-of-conduct.md`). If the plan is broken into phases,
   this means **one PR per phase** (`design-decisions.md`'s "Writing a
   Plan" section) — don't hold a finished phase back to bundle it with the
   next one. Route review through `tasks/pr-review.md` once a PR exists,
   rather than self-reviewing informally.

## Output

Working, tested code matching the ADR's Decision Outcome; the plan (if one
exists) at `Status: Done` with every task checked off; `docs/*.md` updated
in the same change, not deferred; and, for anything source-generator-facing,
a real recorded manual verification (a real build against the change) —
not just a green test suite.
