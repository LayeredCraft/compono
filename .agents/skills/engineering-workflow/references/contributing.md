# Contributing

There's no `CONTRIBUTING.md` yet. Until one exists, this is the process:

1. Check `README.md` and the relevant `docs/*.md` before starting — don't
   re-derive an architecture decision that's already been made (see
   `design-decisions.md`).
2. Match the coding standards in `coding-standards.md` — don't introduce a
   new pattern (a new DI registration style, a new error-handling approach,
   a new test framework helper) without flagging it first; this repo
   intentionally has one way to do each thing.
3. Tests are expected for new composition-engine/provider/generator
   behavior — see `testing.md`.
4. Update the relevant `docs/*.md` in the same PR (see `documentation.md`).
5. Keep PRs scoped to one decision or one feature — bundling an unrelated
   refactor makes the "what was decided and why" trail (`design-
   decisions.md`) harder to reconstruct later.

If this project opens to outside contributors, split this section out into
a real `CONTRIBUTING.md` at repo root and link it from `README.md`.
