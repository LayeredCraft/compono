# Proposed ADRs

A status-filtered view of [`docs/adr/README.md`](../adr/README.md): every
ADR that's `Proposed`, or `Accepted` but not yet implemented.

## Current state: none proposed or pending implementation

[ADR-0044 Amendment 22](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-22-2026-09-06-callverifieratleastintatmostint-added-requirement-3s-minimality-preserved-not-reversed)
(`CallVerifier.AtLeast(int)`/`AtMost(int)`, plus the `Compono.Logging`
`LogVerificationBuilder` forwarding consequence) and
[ADR-0060](../adr/0060-testdoubles-received-calls-and-clear-calls.md)
(`Compono.TestDoubles` `ReceivedCalls()` retrospective inspection and
`ClearCalls()`) were the immediately preceding entries on this page — both
`Accepted`, now fully implemented by
[PLAN-0063](../plans/0063-callverifier-atleast-atmost-and-testdoubles-received-calls-clear-calls.md)
(`Done`): code, tests (unit/generator-snapshot/AOT-smoke/dogfooding), docs
(`docs/packages/compono-testdoubles.md`, `compono-http.md`,
`compono-logging.md`), and the relevant `skills/compono` files (`SKILL.md`,
`references/testdoubles.md`, `references/http.md`, `references/logging.md`,
`evals/evals.json`, plus the mandatory baseline-vs-updated skill-eval
comparison) landed together, so both entries are removed from this page
per its own "entries removed once implemented" rule.

[ADR-0039](../adr/0039-future-extension-package-admission-gate-and-release-sequence.md)
(Future Extension Package Admission Gate and Release Sequence) was the
prior last entry on this page — `Accepted`, with
[PLAN-0039](../plans/0039-future-extension-package-admission-gate-and-release-sequence.md)
`Done`, putting its two-stage admission model into effect across
`docs/roadmap/future-packages.md` and `skills/compono`. See
[Future Packages](future-packages.md) for the resulting per-candidate
disposition it produced.

Every other ADR recorded in [`docs/adr/README.md`](../adr/README.md) is
currently `Accepted` and implemented, `Superseded`, or (for the two
decisions later revisions replaced) implicitly retired by their
successor. See the [Historical Decision Log](../architecture/decision-log.md)
for the full list.
