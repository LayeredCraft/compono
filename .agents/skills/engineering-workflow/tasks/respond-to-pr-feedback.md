# Task: Respond to PR feedback

**Trigger:** asked to address, respond to, or handle feedback/comments left
on an existing PR in this repo — "address that feedback", "handle the
review comments", "what did Codex/the reviewer say, take care of it".
Distinct from `tasks/pr-review.md` (which *produces* findings on a PR);
this task *consumes* findings someone else already posted. If you're doing
both in one sitting (reviewing, then later handling replies to your own
review), still treat them as two separate passes of this repo's process —
don't blend "propose new findings" and "resolve existing ones" into one
undifferentiated pass.

## Load these references first

Always:
- `references/coding-standards.md`
- `references/documentation.md`
- `references/code-of-conduct.md`
- `references/review-emoji-legend.md`

Conditionally, based on what the feedback actually touches once you've
read it:
- `references/design-decisions.md` — feedback questions an architecture
  choice, or a fix needs its own ADR/plan update
- `references/testing.md` — any fix needs a regression test (it almost
  always does)
- `references/security.md` — feedback touches that territory

## Procedure

This is three distinct phases, in order — don't collapse them. Phase 1 is
read-only and sequential (one comment at a time, waiting on the user each
time). Phase 2 is a single batch of implementation. Phase 3 is a cleanup
pass over every comment from phase 1.

### Phase 1 — triage every comment, one at a time

1. **Fetch every comment on the PR**, not just the newest ones, and not
   just the first page — a busy PR can exceed one page on any of these,
   and "fetch every comment" means every comment, not "every comment that
   fit on page one":
   - **Inline review comments — GraphQL only, not the REST list.**
     `gh api --paginate repos/{owner}/{repo}/pulls/{pr}/comments` returns
     a flat list mixing root comments *and* their replies (a reply has
     `in_reply_to_id` set), but GitHub's "create a reply" endpoint requires
     the *root* comment's ID — replying using a reply's own ID fails and
     leaves that thread unhandled. The GraphQL query below already groups
     everything by thread, so use it as the single source for inline
     comments instead of the REST list: for each thread,
     `comments.nodes[0]` is always the root comment (the ID step 8's reply
     call needs), and any later entries in that same array are its
     replies — you don't need to separately figure out which fetched
     comment is a root vs. a reply.
   - Top-level issue comments: `gh api --paginate
     repos/{owner}/{repo}/issues/{pr}/comments` (pull requests are issues
     for this endpoint) — **not** `gh pr view --json comments`, which goes
     through GitHub's own single-page GraphQL binding and isn't a reliable
     "fetch everything" source on a PR with a lot of discussion.
   - Review bodies: `gh api --paginate
     repos/{owner}/{repo}/pulls/{pr}/reviews`
   - Inline threads + resolution state, via GraphQL — note `databaseId` on
     each comment: that's the numeric REST-style ID step 8's reply call
     needs (`id` here is a GraphQL node ID, a different value, and won't
     work against the REST reply endpoint). This query has **two**
     paginated connections, and both need checking, not just the outer
     one — GraphQL connections cap at 100 per page regardless of which
     connection it is:
   ```
   gh api graphql -f query='
     query($owner:String!, $repo:String!, $pr:Int!, $after:String) {
       repository(owner:$owner, name:$repo) {
         pullRequest(number:$pr) {
           reviewThreads(first:100, after:$after) {
             pageInfo { hasNextPage endCursor }
             nodes {
               id isResolved
               comments(first:100) {
                 pageInfo { hasNextPage endCursor }
                 nodes { id databaseId body path line author { login } }
               }
             }
           }
         }
       }
     }' -f owner=<owner> -f repo=<repo> -F pr=<number>
   ```
   - **Outer `reviewThreads`**: if `pageInfo.hasNextPage` is `true`,
     repeat the whole query passing `pageInfo.endCursor` as `-f
     after=<cursor>` until it's `false`.
   - **Nested `comments` on any individual thread**: if that thread's
     `comments.pageInfo.hasNextPage` is `true`, its replies were
     truncated — before triaging or resolving that specific thread,
     re-query just its `comments` connection with its own cursor:
     ```
     gh api graphql -f query='
       query($id:ID!, $after:String) {
         node(id:$id) {
           ... on PullRequestReviewThread {
             comments(first:100, after:$after) {
               pageInfo { hasNextPage endCursor }
               nodes { id databaseId body path line author { login } }
             }
           }
         }
       }' -f id=<thread id> -f after=<cursor>
     ```
     Repeat until that thread's `hasNextPage` is `false` too. Don't skip
     this even though it's rare — a thread actually hitting 100+ replies
     is unlikely on this repo's PRs, but the check costs nothing and
     "unlikely" isn't "impossible."
2. **Skip everything already handled, before classifying anything** — a
   rerun of this task on the same PR must converge, not re-triage forever.
   Three separate skip rules, all applied before step 3:
   - **Inline review threads**: skip any thread where `isResolved` is
     `true`. This is a literal filter step, not just a fact about
     inline comments — apply it to the GraphQL `reviewThreads` results
     from step 1 before doing anything else with them.
   - **Top-level comments/review bodies already replied to**: these have
     no resolved state on GitHub at all, so this task's own replies (see
     step 8's permalink-marker convention) are the only signal. Before
     classifying a top-level comment/review body, check whether its own
     permalink (`.../pull/<number>#issuecomment-<id>` or
     `#pullrequestreview-<id>`) already appears in the body of any *later*
     top-level comment on the PR — if so, it was already handled, skip it
     entirely (don't re-triage, don't re-reply).
   - **This task's own marker replies**: a reply this task posted in a
     prior run is itself just another top-level comment, and the rule
     above only skips the *original* comment it replied to — it doesn't
     stop the marker reply itself from being fetched and treated as new
     feedback on the next run, which would produce a reply-to-a-reply loop
     that never converges. Recognize and skip these directly: any
     top-level comment whose body starts with `Re: <permalink>` (step 8's
     required format) is a marker reply generated by this task, not
     feedback from someone else — skip it unconditionally, it is never
     itself a candidate for classification or reply.
3. **For each remaining comment/thread**, using the loaded references,
   classify it as one of:
   - **Real issue** — a genuine defect, standards deviation, or gap per
     the loaded references; needs a code/doc change.
   - **Deferred** — a valid point, but legitimately out of scope for this
     PR (e.g. a larger refactor, a pre-existing issue merely surfaced
     here). Per `design-decisions.md`/`contributing.md`, deferring doesn't
     mean dropping it — it becomes a tracked follow-up (a note in the
     relevant `docs/*.md` Open Items, or an explicit statement that a new
     issue/PR will track it).
   - **No action** — acknowledgment, praise, a bot's boilerplate, or
     something already addressed by a prior commit.
4. **Display each classified comment to the user one at a time** — the
   comment text, your classification, and your recommendation (what fix
   you'd make, or why it should be deferred, or why no action is needed).
   Wait for the user's decision on *that* comment before moving to the
   next one. Record the user's decision (fix / defer / no action, and any
   specifics they added) — this record drives phases 2 and 3. Do not
   batch-display the whole list up front the way `pr-review.md` does with
   fresh findings — these are already-posted comments from someone else,
   and each may need independent judgment before you've even reasoned
   about the next one.

### Phase 2 — implement, once, after triage is complete

5. Once every comment has a recorded decision, implement all "fix"
   decisions together (not one commit per comment unless the changes are
   genuinely unrelated) — following `coding-standards.md`, adding
   regression tests per `testing.md`, updating `docs/*.md` per
   `documentation.md` in the same change. For "deferred" decisions, make
   the tracking update now too (the Open Items note, the follow-up
   reference) — deferring shouldn't mean doing nothing.
6. Build and run the full test suite before committing — don't push
   something that doesn't compile or regresses existing coverage.
7. Commit and push per `git-workflow`'s conventions (or this repo's
   established commit-message style if `git-workflow` isn't in play) —
   one coherent commit (or a small number of logically-grouped commits)
   covering everything from phase 1's "fix" decisions.

### Phase 3 — reply and resolve, once the fix is pushed

8. **Reply to every comment/thread from phase 1**, not just the ones that
   got code changes. GitHub has two different comment types with two
   different reply endpoints — use the one matching what you fetched in
   step 1, not just the review-comment one for everything:
   - **Inline review comments** (from the GraphQL `reviewThreads` fetch) —
     reply using the thread's root comment's `databaseId` (i.e.
     `comments.nodes[0].databaseId` — **not** `id`, which is a GraphQL node
     ID the REST endpoint doesn't accept, and **not** any later entry in
     `comments.nodes`, which are replies, not valid reply targets
     themselves):
     ```
     gh api repos/{owner}/{repo}/pulls/{pr}/comments/{root_database_id}/replies \
       -f body="<reply text>"
     ```
   - **Top-level issue comments and review bodies** (from the
     `issues/{pr}/comments` / `pulls/{pr}/reviews` fetch) — these aren't
     inline review comments, so the endpoint above doesn't apply to them
     (it will either 404 or silently reply to the wrong thing). Reply as a
     new top-level PR comment instead, and **always include the original
     comment's permalink in the reply body** — this is the convergence
     marker step 2 relies on to skip already-handled comments on a future
     run, so don't drop it even when the reply feels self-explanatory
     without it:
     ```
     gh pr comment <number> --body "Re: <original permalink> — <reply text>"
     ```
     or equivalently `gh api repos/{owner}/{repo}/issues/{pr}/comments -f
     body="Re: <original permalink> — <reply text>"` (pull requests are
     issues for this endpoint).
   - Real issue, fixed → reply pointing at the fixing commit SHA and what
     changed. No severity emoji needed (it's a resolution, not a new
     finding) — plain text, or a 👍 if also acknowledging the original
     point was correct.
   - Deferred → reply explaining why, and where it's now tracked (the doc
     section, the follow-up).
   - No action → a short acknowledgment reply — still with the permalink
     marker for top-level comments, so it's still skippable on a future
     run — or skip the reply entirely for pure praise/thank-you comments
     if there'd be nothing to skip anyway (nothing else references them).
9. **Resolve every inline review thread** handled in this pass:
   ```
   gh api graphql -f query='
     mutation($id:ID!) {
       resolveReviewThread(input:{threadId:$id}) { thread { isResolved } }
     }' -f id=<thread id from step 1's query>
   ```
   `resolveReviewThread` only applies to inline review-comment threads —
   top-level issue comments and review bodies have no "resolved" state on
   GitHub at all; the permalink-marked reply in step 8 is the entire
   deliverable for those (see step 2).

## Output

The deliverable is the pushed fix commit(s) plus every triaged thread
replied-to and resolved on GitHub — not a private summary. If the user
declines to act on every single comment (e.g. explicitly says "skip that
one"), still reply on it explaining the decision and resolve it, rather
than leaving it dangling unresolved with no trail.
