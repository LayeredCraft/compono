# [ADR-0038] net8.0/net9.0 Explicit Multi-Target

**Status:** Accepted

**Date:** 2026-08-10

**Decision Makers:** solo

## Context

[ADR-0037](0037-netstandard2.1-compatibility-floor.md) decided to add
`netstandard2.1` as a compatibility floor across all four Compono packages,
to unblock consumers on .NET 8/.NET 9 (concretely: the `structured-logging`
repo) without enumerating consumer TFMs one at a time. That ADR identified
three known BCL gaps versus `net10.0`/`net11.0` (`Random.Shared`,
`ArgumentNullException.ThrowIfNull`, `required` members) and scoped each a
fix, and its own implementation plan (PLAN-0037) called for an explicit
compatibility audit of the three integration packages before assuming
those three gaps were exhaustive.

That audit, run during implementation, found real gaps beyond the three
known ones:

- `NullabilityInfoContext` (`Compono.XunitV3`) — turned out to be a
  non-issue: the `Meziantou.Polyfill` package (already used elsewhere in
  this repo, for `Compono.Generators`'s own `netstandard2.0` floor) covers
  it fully. Verified by a real build, not just documentation.
- `FrozenDictionary`/`FrozenSet` (`Compono.Bogus`) — net8.0+ types, not
  polyfilled by anything, but used only as private backing-field
  implementation detail, never public surface. A cheap, mechanical
  `#if NET8_0_OR_GREATER`-guarded fallback to a plain wrapped
  `Dictionary`/`HashSet` would have resolved this the same way as the
  original three gaps.
- `DateOnly`/`TimeOnly` (`Compono` core, `PrimitiveValueProvider`) — **not**
  a shimmable gap. These are whole public composable *types* introduced in
  .NET 6, not missing methods on an existing type. No polyfill can add the
  real type to `netstandard2.1`. The only paths were: silently drop these
  two types from the composable surface on `netstandard2.1` only (a real
  per-TFM behavior fork in what `Compono` can compose), or abandon the
  floor.
- `Enum.GetValuesAsUnderlyingType` (`Compono` core, `EnumValueProvider`) —
  **not** a shimmable gap. This method was deliberately chosen (see that
  file's own remarks, and the PR #11 review history it references)
  specifically to avoid `Enum.GetValues(Type)`'s `[RequiresDynamicCode]`
  reflection path, per [ADR-0001](0001-source-generation-first.md)'s
  no-reflection-by-default rule. There is no non-reflection equivalent on
  `netstandard2.1`. The only paths were: silently reintroduce the exact
  reflection fallback PR #11 rejected, scoped to `netstandard2.1` only, or
  abandon the floor.

The two non-shimmable gaps are architecture-level compromises, not
mechanical implementation gaps — exactly the situation ADR-0037's own "The
floor is cheap, not a design target" section anticipated as the trigger to
re-evaluate retaining the floor.

## Decision Drivers

- Same as ADR-0037: unblock consumers on .NET 8/.NET 9 without
  re-litigating TFM policy per consumer.
- Don't ship a public composable-type surface (`DateOnly`/`TimeOnly`) that
  silently differs by TFM — a consumer moving their own project from
  `net10.0` down to a `netstandard2.1`-satisfying TFM shouldn't discover
  Compono quietly composes fewer types there.
- Don't reintroduce a reflection code path ADR-0001/PR #11 specifically
  rejected, even scoped to one TFM — that's a real regression against a
  load-bearing architectural rule, not a cosmetic inconsistency.
- Prefer zero-shim correctness over broader hypothetical-future-TFM
  coverage, now that real evidence shows the shim surface isn't as narrow
  as ADR-0037 assumed.

## Considered Options

Same three options ADR-0037 considered, re-evaluated with the audit
evidence above:

1. Status quo — `net10.0;net11.0` only.
2. Explicit multi-target to `net8.0;net9.0;net10.0;net11.0`.
3. `netstandard2.1` floor (ADR-0037's original choice).

## Decision Outcome

Chosen option: **2 — explicit multi-target to `net8.0;net9.0;net10.0;net11.0`**,
superseding ADR-0037's choice of Option 3, because the audit findings above
make Option 2's original "Bad, because" (only covers two named TFMs, not
the whole netstandard2.1-capable ecosystem) the smaller cost compared to
Option 3's actual, now-evidenced cost (two architecture-level compromises,
not the "narrow and fully accounted for" shim surface ADR-0037 assumed
before implementation surfaced the audit results).

Verified directly: the full solution (`Compono`, `Compono.XunitV3`,
`Compono.NSubstitute`, `Compono.Bogus`, and every test project) builds
clean across all four TFMs — `net8.0`, `net9.0`, `net10.0`, `net11.0` —
with **zero shims, zero polyfill dependencies, and zero `#if` guards**.
Every gap ADR-0037's audit found (`Random.Shared`,
`ArgumentNullException.ThrowIfNull`, `required` members,
`NullabilityInfoContext`, `FrozenDictionary`/`FrozenSet`, `DateOnly`/
`TimeOnly`, `Enum.GetValuesAsUnderlyingType`) is natively available on both
`net8.0` and `net9.0` — every one of those APIs shipped in .NET 6, .NET 7,
or .NET 8, all at or before .NET 8. This is exactly Option 2's original
"Good, because" from ADR-0037: each TFM gets the real, un-shimmed BCL for
its own version.

This ADR **supersedes** ADR-0037 — its core Decision Outcome (which TFM
strategy to add) is being replaced, not corrected or extended, per
`design-decisions.md`'s Amendment-vs-Supersede rule. ADR-0037's original
Context/Decision Outcome/Pros-and-Cons text is left exactly as written (an
accurate record of what was decided and why, at the time, before the audit
evidence existed) — only its `Status` line changes.

This ADR **does not touch** [ADR-0031](0031-public-preview-release-and-versioning-policy.md)'s
Amendment 2, which recorded ADR-0037's floor as compatible with, not a
reversal of, ADR-0031's rolling two-TFM window. That relationship needs a
new note here instead: unlike a `netstandard2.1` floor, an explicit
`net8.0`/`net9.0` multi-target **does** widen ADR-0031's tracked TFM window
backward by two releases, not just add an independent floor underneath it
— see ADR-0031 Amendment 3, added alongside this ADR.

### Positive Consequences

- Zero shims, zero new dependencies, zero `#if` guards anywhere in the
  four packages — the simplest possible resolution, and the one with the
  least ongoing maintenance tax.
- No public-surface divergence across TFMs — every composable type and
  every code path behaves identically on all four TFMs, verified by a
  single solution-wide build.
- No reflection reintroduced anywhere — ADR-0001's no-reflection-by-default
  rule holds unconditionally across every supported TFM, not just the
  primary ones.
- `.NET 8`/`.NET 9` consumers (the actual, evidenced blocker — the
  `structured-logging` repo) get full native behavior, not a
  floor-TFM-scoped subset.

### Negative Consequences

- Only covers `.NET 8`/`.NET 9` by name — a consumer on `.NET 7`, `.NET
  Core 3.1`, or a future release not yet added is still blocked, and
  becomes its own future ADR/amendment rather than already being covered
  by a broader floor. This was Option 2's known downside in ADR-0037 and
  remains true here; it's accepted as the smaller cost given the audit
  evidence.
- Four TFMs per package (sixteen build outputs across all four packages)
  instead of three — larger CI/package surface than ADR-0037's three-TFM
  plan, though smaller than it would have been had ADR-0037's floor grown
  the additional per-TFM special-casing the audit gaps would have required.
- Widens ADR-0031's tracked TFM window backward, which needs that ADR's
  own Amendment 3 to make explicit (see above) — a real policy change,
  not just a new independent addition the way ADR-0037's floor was.

## Pros and Cons of the Options

See ADR-0037's own "Pros and Cons of the Options" section for the original,
pre-audit comparison of all three options — not repeated here verbatim
since it stays accurate as a historical record of what was known at the
time. The one material update: Option 3's "Bad, because" list from
ADR-0037 undersold the real cost, since the audit that ADR's own plan
called for hadn't run yet. With that audit's results in hand, Option 3
also carries "Bad, because it either drops `DateOnly`/`TimeOnly` from the
netstandard2.1-only composable surface, or reintroduces the exact
reflection fallback ADR-0001/PR #11 rejected — both real, not merely
handled the same way as the three originally-identified gaps."

## Links

- [ADR-0037: netstandard2.1 Compatibility Floor](0037-netstandard2.1-compatibility-floor.md) — the decision this ADR supersedes; see its own Context for the original problem statement (unchanged) and its Decision Outcome for the audit findings that motivated this reversal.
- [ADR-0031: Public Preview Release and Versioning Policy](0031-public-preview-release-and-versioning-policy.md) — Amendment 3 (added alongside this ADR) records that this decision widens, not just floors, that ADR's tracked TFM window.
- [ADR-0001: Source-Generation First](0001-source-generation-first.md) — the no-reflection-by-default rule `Enum.GetValuesAsUnderlyingType`'s netstandard2.1 gap would have compromised; this decision keeps it intact unconditionally instead.
