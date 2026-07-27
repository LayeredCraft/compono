# PR review emoji legend

Every comment posted to a Compono PR (inline finding or overall review
verdict) leads with exactly one emoji marking its disposition. This is the
one place that mapping is defined — `tasks/pr-review.md` and any other
review flow reference this file rather than inventing their own scheme, so
the meaning stays consistent across reviews and reviewers (human or agent).

## Per-comment severity

| Emoji | Meaning | When to use |
|---|---|---|
| 🐛 | Blocking bug | Must be fixed before merge — correctness bug, security issue, breaks a documented invariant/ADR decision, missing required test coverage for new behavior (`testing.md`). |
| ⚠️ | Should-fix | Not blocking this merge, but a real concern that should be addressed here or as an immediate, explicitly-tracked fast-follow — a `coding-standards.md` deviation, thin test coverage, a `documentation.md` gap. |
| 📝 | Suggestion | Optional improvement, nitpick, or style preference. Never blocks merge on its own — think "edit," not "problem." |
| ❓ | Question | Needs clarification from the author before the finding can even be judged — not yet a confirmed issue. |
| 👍 | Praise | Optional — calling out something done particularly well. Doesn't block or need "fixing." |

## Overall review verdict

| Emoji | Verdict | When to use |
|---|---|---|
| 🎉 | Approve | No 🐛 findings remain open/unresolved on the PR. ⚠️/📝/❓ findings may still be open. |
| 🛑 | Request changes | At least one 🐛 finding is open and unresolved. |
| 💬 | Comment | Findings exist (⚠️/📝/❓) but none are 🐛, and a formal approve/block verdict isn't appropriate yet (e.g. still waiting on author clarification for a ❓). Used sparingly — prefer 🎉 or 🛑 once the review is actually complete. |

## Mechanics

- Prefix the emoji at the very start of the comment body, before any text:
  `🐛 <finding>`.
- The overall review body (the verdict posted via `gh pr review`) also
  starts with its verdict emoji, e.g. `🎉 <summary>`.
- One emoji per comment — don't combine. A finding that's both a genuine
  open question *and* a real risk is 🐛 (the more conservative read),
  phrased as a question in the body if it's still open.
- If a finding's severity changes after discussion (e.g. a 🐛 turns out not
  to apply once the author explains context), edit or reply on the
  original comment to reflect that rather than leaving a stale severity
  marker sitting in the thread.
- These are the *only* emoji used in review comments — don't decorate
  other parts of a comment (reasoning, code snippets) with emoji; the
  disposition marker is the whole point, extra emoji dilute it.
