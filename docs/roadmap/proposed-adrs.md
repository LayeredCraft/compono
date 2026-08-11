# Proposed ADRs

A status-filtered view of [`docs/adr/README.md`](../adr/README.md): every
ADR that's `Proposed`, or `Accepted` but not yet implemented.

## Accepted, not yet implemented

- [ADR-0039](../adr/0039-future-extension-package-admission-gate-and-release-sequence.md) —
  Future Extension Package Admission Gate and Release Sequence.
  `Accepted`. Establishes a two-stage admission model for future
  extension packages — an architectural admission gate this ADR owns,
  feeding into [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
  existing evidence gate — with no committed release sequence. See
  [Future Packages](future-packages.md) for the resulting per-candidate
  disposition. [PLAN-0039](../plans/0039-future-extension-package-admission-gate-and-release-sequence.md)
  (`Not Started`) tracks putting this decision into effect across the
  repo's docs/skill surfaces — no package code is implied or scheduled by
  this ADR itself.

Every other ADR recorded in [`docs/adr/README.md`](../adr/README.md) is
currently `Accepted` and implemented, `Superseded`, or (for the two
decisions later revisions replaced) implicitly retired by their
successor. See the [Historical Decision Log](../architecture/decision-log.md)
for the full list.
