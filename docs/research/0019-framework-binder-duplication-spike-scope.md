# [RESEARCH-0019] Framework Binder Duplication — Spike Scope

**Status:** Scoped, not yet executed — spawned by the pre-1.0 cleanup gate
(PLAN-0061), explicitly non-blocking for 1.0.

## Why this exists

The pre-1.0 cleanup audit found `BindingPlan`, `ParameterBindingPlan`,
`PositionalArgumentBinder`, and `RowInvokers`-adjacent binding-dispatch code
structurally duplicated across `Compono.XunitV3`, `Compono.TUnit`,
`Compono.MSTest`, and `Compono.NUnit`. Structural similarity across four
independent packages is not, by itself, evidence that consolidation is worth
doing — `references/design-decisions.md`'s duplication classification
(harmful / intentional package-local / coincidental / validation) requires
actually comparing the implementations, not just their shape. This document
scopes that comparison; it does not perform it. A future execution of this
spike fills in the sections below with real findings, then either updates this
document to a completed research record or produces a design/ADR based on it.

**Default disposition, per explicit product direction:** keep package-local
duplication unless the spike demonstrates one stable, shared concept whose
extraction clearly improves maintainability without increasing coupling. A
result of "keep the duplication as-is" is a fully valid, complete outcome of
this spike, and this document existing does not create an implied obligation
to extract anything — a spike that concludes "keep it as-is" is not a lesser
outcome than one that recommends consolidation.

## What the spike must compare

The binding-dispatch code in each of the four framework-integration packages'
`Binding/` directories — `BindingPlan.Build`, `ParameterBindingPlan`,
`PositionalArgumentBinder`, and each package's own `RowInvokers`/dispatch
glue (post-ADR-0041, these read from core `Compono`'s `RowInvokerRegistry`
rather than building delegates themselves, so "duplication" here means the
surrounding binding-plan/validation code, not the dispatch mechanism ADR-0041
already centralized) — **and**, added 2026-09-03 during the audit-to-plan
traceability reconciliation, each package's own `ConfigProfileBinder` and its
`[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`
annotation coverage (ADR-0041 Amendments 4-5, ADR-0057). This was flagged by
the original audit as "NEEDS-SPIKE alongside binding-algorithm consolidation"
— currently in sync across all four packages, no drift observed today, but
in the same code family this spike already covers, so it's folded in here
rather than treated as a separate research item.

The spike must verify, across `Compono.XunitV3`/`Compono.TUnit`/
`Compono.MSTest`/`Compono.NUnit`:

- equivalent `[DynamicallyAccessedMembers(PublicConstructors)]` placement
  wherever the same reflection behavior (`ConstructorInfo.Invoke`-based
  `TConfig`/`TProfile` construction) exists;
- any framework-specific difference in constructor/profile-binding behavior
  that would justify different annotations rather than identical ones;
- whether annotation drift is a real, distinct maintenance risk of
  package-local binder duplication, separate from the binding-plan/dispatch
  risk the rest of this spike covers;
- whether the annotations themselves would become easier or harder to
  maintain correctly under any shared abstraction this spike might otherwise
  propose.

A valid result for this part of the spike, same as the rest of it: *all four
binders currently carry equivalent AOT contracts; keep the package-local
implementations.*

## Questions the spike must answer

1. Which logic is byte-for-byte or semantically identical across all four
   packages?
2. Which behavior is genuinely framework-specific (e.g. how each framework's
   own `MethodInfo`/`DataGeneratorMetadata`/theory-row shape gets turned into
   an ordered parameter list)?
3. What invariants are genuinely shared across all four — not just "looks the
   same today," but "has the same reason to change"?
4. What is each binder's own reason to change, independent of the others (a
   framework API change in one framework's own SDK vs. a Compono-side
   composition-model change)?
5. Would sharing require a new runtime or package dependency between
   currently-independent integration packages, or a new dependency on core
   `Compono` beyond what already exists?
6. Would the resulting shared abstraction become public, or generator-facing
   (i.e. would `Compono.Generators` need to emit code against it)?
7. Would extraction reduce each package's current independence — could one
   package upgrade/change its own binder without a coordinated release of the
   others afterward?
8. Does it affect any existing trimming/AOT annotation
   (`[DynamicallyAccessedMembers(...)]`, per ADR-0041's own amendments) on any
   of the four binders?
9. Would a change to the shared code now require a coordinated release across
   otherwise-independent integration packages that ship on independent
   schedules today?
10. Does extraction materially reduce real drift risk (a bug fixed in one
    binder and never ported to the others — the exact failure mode the
    negative-seed-guard finding in PLAN-0061 Phase 1 already demonstrated is
    real), or does it merely reduce line count with no corresponding risk
    reduction?

## Escalation to a correctness/public-API concern

This spike is explicitly non-blocking for 1.0 unless it uncovers an actual
correctness or public-contract risk that becomes materially harder to fix
after the public API freezes (e.g. a second drift bug of the same shape as
the negative-seed-guard finding, discovered by direct comparison rather than
audit sampling). If that happens, the spike's findings should be escalated
immediately as their own light/deep design dive per `tasks/design.md`, rather
than waiting for this document to be "finished" in the ordinary sense.
Discovering shared duplication is never, by itself, evidence that
consolidation is the right answer — this spike may just as validly conclude
that the duplication should stay exactly as it is.

## Links

- PLAN-0061 — the pre-1.0 cleanup plan that spawned this spike; this research
  runs independently of both of its implementation phases.
- [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md) — the ADR that
  already centralized the dispatch-delegate mechanism these four packages'
  own binding-plan code sits on top of.
