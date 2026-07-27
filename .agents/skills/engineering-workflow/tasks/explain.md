# Task: Explain

**Trigger:** asked to explain what was built, walk through the code in
detail, or teach it to someone who doesn't already have the context this
session built up — "explain what we just did," "walk me through the code
line by line," "pretend I don't know anything," "what can I actually do
with this." Distinct from `tasks/pr-review.md` (which hunts for defects)
and from a normal implementation summary (which is terse by design, per
`code-of-conduct.md`'s "be direct and specific" and general terseness
norms elsewhere in this skill) — this task is explicitly asked for when
terse isn't what's wanted, and produces a long, patient, from-first-principles
walkthrough instead.

## Load these references first

There's no fixed reference list for this task — what you load depends
entirely on what's being explained (a generator pipeline change needs
`security.md`'s trust-boundary framing and `design-decisions.md`'s ADRs
for *why*; a plain library change needs neither). Load whatever the
subject matter actually touches, the same way any other task would.

## Procedure

1. **Re-read every file you're about to explain, fresh, right now** —
   never explain from memory or from what was discussed earlier in the
   conversation. This is the same "plan file on disk is the source of
   truth" discipline as `design-decisions.md`'s "Writing a Plan" section,
   applied to code instead of a plan: code changes underneath a
   conversation (another commit, a manual edit, a fix from responding to
   PR feedback), and explaining a stale mental model as if it were current
   produces a confidently wrong walkthrough — worse than no walkthrough,
   because it's not obviously wrong.
2. **Start with the problem, not the code.** Before any file or line, state
   in plain language what problem this thing solves and why it's built
   the way it is — the "big idea" a reader needs before individual lines
   make sense. Assume no prior context: don't lean on jargon, acronyms, or
   prior-message shorthand without defining it the first time it's used.
3. **Walk through the pieces in the order they actually execute or
   interact**, not file-alphabetical or edit-chronological order. For a
   pipeline (a source generator, a request handler chain, anything with a
   defined flow), that means: entry point first, then each stage in the
   order data actually flows through it. Quote the real code (re-read in
   step 1) and explain *why* each piece is shaped the way it is, not just
   *what* it does line-by-line — a reader who wanted only "what" could
   read the code themselves; the value of this task is the "why."
4. **Trace one concrete, complete, worked example end-to-end.** Abstract
   descriptions of a pipeline don't stick — pick one realistic input,
   follow it through every stage explained in step 3, and show the actual
   output it produces (a real generated file, a real response body,
   whatever the subject matter's "output" is). This is the same technique
   `tasks/pr-review.md` uses for findings and `design-decisions.md` uses
   for ADRs: a concrete instance beats an abstract description every time.
5. **Close with an honest, concrete answer to "what can I actually do with
   this."** This is the step most tempting to skip or soften, and the one
   that matters most: distinguish clearly between what's genuinely
   working end-to-end today versus what's scaffolding, a placeholder, or
   stubbed out pending later work (`docs/plans/*.md` phase status is the
   authoritative source for this, not impression or optimism). If there's
   a minimal case that *does* fully work today, show it, and be equally
   explicit about the case that would compile but throw, or behave
   differently than a reader might assume from the rest of the
   walkthrough. Overstating readiness here is worse than underselling it —
   the reader is very likely about to go try it.

## Output

A long-form, patient walkthrough in chat — this is one task in this skill
where terseness is explicitly not the goal, per the trigger's own framing.
Not a written artifact by default (no new file); if the person asks for it
to be written down, that's their call to make, not an assumption to make
for them.
