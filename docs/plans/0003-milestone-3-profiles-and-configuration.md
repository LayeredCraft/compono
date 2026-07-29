# [PLAN-0003] Milestone 3: Profiles and Configuration

**Status:** In Progress

**Implements:** [ADR-0017](../adr/0017-immutable-composer-configuration-and-builder-model.md)
(immutable `Composer`/`CompositionBuilder`/`CompositionConfiguration` split,
build-time configuration validation, scalar-configuration fail-fast, structured
`CompositionConfigurationException`), [ADR-0018](../adr/0018-composition-profiles.md)
(`ICompositionProfile`, eager profile application, cycle detection, provenance),
[ADR-0019](../adr/0019-registrations-and-service-provider-injection.md) (exact
registrations, duplicate-registration conflicts, native `IServiceProvider`
fallback), [ADR-0020](../adr/0020-composition-configuration-rules.md) (type/member
value rules as internal stage-4 providers, collection-size as queried
configuration policy). `CompositionRequestDescriptor.DeclaringType` (ADR-0020) and
`PathSegment.ManualResolve` (ADR-0019) are additive extensions to the `Accepted`
ADR-0010/ADR-0012 contracts, defined in the new ADRs that need them rather than by
editing those two ADRs' own text — see the review note in this plan's Notes section
for why.

## Goal

`Composer.Create(builder => builder.WithSeed(...).WithCollectionSize(...)
.Register<IClock>(...).UseServiceProvider(...).AddProfile<CustomerProfile>()
.For<Customer>().Member(x => x.Status).Use(...))` builds a real, immutable,
reusable `Composer` — replacing Milestone 2's internal-only
`CreateRootForTesting`/`CreateManyForTesting` test seam with the actual public
configuration surface `docs/public-api.md` sketches — with every configuration
mistake (duplicate registration, duplicate rule, duplicate scalar setting, profile
cycle) failing loudly and specifically at `Composer.Create(...)` time, per
`docs/mvp.md`'s Milestone 3 exit criterion: one reusable profile, usable both
programmatically and (in Milestone 4) from a test framework.

## Scope

Per `docs/mvp.md`'s Milestone 3 section, mapped onto ADR-0017/0018/0019/0020's
concrete shape:

- `CompositionBuilder` (public, mutable, short-lived) and `CompositionConfiguration`
  (internal, immutable, frozen at `Build()`) — ADR-0017.
- `CompositionConfigurationException`, carrying a structured, inspectable list of
  `CompositionConfigurationError`s — a discriminated union (`DuplicateRegistration`/
  `DuplicateRule`/`DuplicateConfigurationOption`/`ProfileCycle`), matching this
  codebase's existing `PathSegment`/`CompositionResult` shape, not just a formatted
  message — ADR-0017's Amendment.
- Fail-fast, consistent duplicate-configuration behavior across every scalar verb
  (`WithSeed`, `WithCollectionSize`'s global default, `UseServiceProvider`) and
  every keyed verb (`Register<T>`, type/member rules) — ADR-0017's Amendment.
- `ICompositionProfile`, `AddProfile<TProfile>()`/`AddProfile(ICompositionProfile)`,
  eager in-order application, type-keyed cycle detection (failing **immediately**,
  distinct from `Build()`'s aggregated conflict pass), source-chain provenance on
  every accumulated entry — ADR-0018.
- `Register<T>(Func<ICompositionContext, T>)` / `Register<T>(Func<T>)`, an immutable
  exact-registration lookup, duplicate-registration build-time conflict detection —
  ADR-0019. The lookup's internal representation is an implementation choice made
  during Phase 1, not prescribed by this plan — see that phase's notes.
- `ICompositionContext.Resolve<T>()` (descriptor-less overload) and
  `PathSegment.ManualResolve`, scoped to a per-invocation frame (not a per-node
  counter) — ADR-0019, verified against ADR-0012's existing reproducibility
  contract without editing ADR-0012 itself.
- `CompositionRequestDescriptor.DeclaringType` (and the matching field on the
  internal `CompositionRequest`) — ADR-0020, an additive extension to ADR-0010's
  `Accepted` descriptor shape without editing ADR-0010 itself — plus the
  `Compono.Generators` template/emitter change and snapshot regeneration needed to
  populate it.
- `UseServiceProvider(IServiceProvider)` and stage 3's registration-then-container
  fallback, with the full exception/assignability/scope-ownership semantics ADR-0019
  specifies.
- Type and member value rules (`.For<T>().Use(...)`,
  `.For<T>().Member(x => x.Y).Use(...)`), compiled into internal stage-4 providers,
  matched by `CompositionRequest.DeclaringType` + member name (never inferred from
  path state), type-vs-member precedence, exact-type matching — ADR-0020. Whether
  this compiles to one internal provider type or several is an implementation
  choice — see Phase 3.
- `WithCollectionSize(n)` (global) and `.For<T>().Member(x => x.Y).WithCollectionSize(n)`
  (member-scoped), `CollectionSizePolicy` on `CompositionConfiguration`, and the new
  `ICompositionContext.ResolveCollectionSize()` — **parameterless**, not a
  descriptor-taking overload (a generated collection plan's `Compose(ICompositionContext)`
  has no descriptor to pass; the context reads the current member's declaring
  type/name from the already-expanded `CompositionRequest` it's still resolving —
  the **same `DeclaringType` field member value-rule matching uses**, not a
  separately-derived parent-path-node type, which would be wrong for an inherited
  required member — the same "no parameter needed, the context already knows"
  shape `ResolveRoot<T>()` uses) — one method, used identically by root-level and
  member-scoped collection plans alike — ADR-0020.
  Requires updating `Compono.Generators`' collection-plan template
  (`CollectionPlan.scriban`) to call the new query instead of emitting the literal
  `3` ADR-0013 hardcoded.
- `WithSeed(int)` — the public seed-configuration entry point Milestone 2 deferred.

Explicitly deferred (later milestones, or explicitly out of scope per the M3 design
review):

- **Public provider extensibility** (an interface a third-party package like
  `Compono.NSubstitute` implements to contribute open-ended, pattern-matching logic
  to stages 5/6) — deferred to Milestone 5. Nothing in this plan requires it: value
  rules compile into *internal* Compono-authored providers, and service injection is
  a stage-3 fallback, not a provider.
- **`UseNSubstitute()`/`UseBogus()`** themselves — Milestones 5/6. Only the generic
  extension-method mechanism (ordinary C# extension methods on `CompositionBuilder`)
  is relevant here, and it needs no new code — it falls out of `CompositionBuilder`
  being `public`.
- **Richer `Microsoft.Extensions.DependencyInjection` integration** — explicitly out
  of core's scope per ADR-0019; a future optional package, not designed here.
- **`RegisterOpenGeneric`** — not required for the MVP, not in this plan.
- **An explicit override/replace verb** (`TryRegister`/`Replace`) — ADR-0019
  explicitly deferred this; M3 ships strict throw-on-duplicate only.
- **Assignability-based type-rule matching** — ADR-0020 ships exact-type matching
  only.
- **The `[Shared]` attribute, xUnit-specific composition context, inline values** —
  Milestone 4, unchanged from Milestone 2's scope boundary.

## Execution Flow: `Composer.Create(builder => ...)` through `Create<Customer>()`

Written out once, in full, so "when does a profile's `Configure` actually run,"
"when is a conflict detected versus thrown immediately," and "how does a
registration factory's nested `Resolve<T>()` call get a deterministic path" have one
unambiguous answer. Assume:

```csharp
public sealed class ClockProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<IClock>(() => new FakeClock());
}

var composer = Composer.Create(builder => builder
    .WithSeed(4219)
    .AddProfile<ClockProfile>()
    .For<Customer>().Member(x => x.Status).Use(CustomerStatus.Active));

var customer = composer.Create<Customer>();
```

1. **Builder construction.** `Composer.Create(configure)` constructs one
   `CompositionBuilder` and invokes `configure(builder)` synchronously.
2. **`WithSeed(4219)`** records the explicit seed on the builder's accumulating
   state. A *second* `WithSeed(...)` call anywhere in this same configuration
   (direct or from a profile) would be a `Build()`-time conflict, per ADR-0017's
   Amendment — not relevant to this walkthrough, which calls it once.
3. **`AddProfile<ClockProfile>()`.** The builder pushes `typeof(ClockProfile)` onto
   its cycle-detection stack, constructs `new ClockProfile()` (reflection-free —
   ordinary `new()`-constrained generic instantiation), and calls
   `Configure(builder)` **immediately** — still inside step 3, before
   `AddProfile` returns. Inside `Configure`, `builder.Register<IClock>(...)`
   accumulates one exact-registration entry, tagged with source chain
   `["ClockProfile"]` (not `"Direct"`, since it's running inside `ClockProfile`'s
   `Configure`). `AddProfile` pops `typeof(ClockProfile)` off the cycle stack in a
   `finally` before returning. Had this profile (transitively) added itself, the
   cycle would be detected and thrown **right here, immediately** — a single-error
   `CompositionConfigurationException`, before `configure(builder)` even finishes
   running, not batched with any other conflict the rest of the callback might have
   gone on to introduce (ADR-0017's Amendment, point 3).
4. **`.For<Customer>().Member(x => x.Status).Use(...)`.** `For<Customer>()` returns
   a type-rule builder scoped to `Customer`; `.Member(x => x.Status)` immediately
   parses the expression into `(typeof(Customer), "Status")` (not deferred);
   `.Use(CustomerStatus.Active)` accumulates one member-rule entry on the same
   underlying builder state, tagged source chain `"Direct"` (this call is outside
   any profile's `Configure`).
5. **`configure(builder)` returns; `Build()` runs.** One validation pass scans every
   accumulated registration/rule/scalar entry for a duplicate `(kind, key)` pair —
   none exists here (one `IClock` registration, one `Customer.Status` member rule,
   one `WithSeed` call, no `WithCollectionSize`/`UseServiceProvider` calls at all) —
   so no `CompositionConfigurationException` is thrown. The accumulated state
   compiles into an immutable `CompositionConfiguration`: an exact-registration
   lookup (`IClock` → factory), the compiled stage-4 rule set (one entry for
   `Customer.Status`), the seed policy (explicit, `4219`), an empty
   `CollectionSizePolicy` (default `3` throughout), and no configured
   `IServiceProvider`. `Composer.Create(...)` returns a `Composer` wrapping this
   configuration.
6. **`composer.Create<Customer>()`.** Builds a fresh `CompositionContext` from the
   frozen configuration exactly as Milestone 2 already does, seeded with `4219`.
   Stages 1–2 decline for the root `Customer` request (nothing explicit/shared).
   Stage 3 has no exact registration for `Customer` itself and no configured
   `IServiceProvider` — declines. Stage 4's compiled rule set is tried but the root
   `Customer` request carries no `DeclaringType`/member name of its own (only a
   `RequiredMember`/`ConstructorParameter` request does) — declines for the root.
   Stages 5–7 decline (no semantic/test-double/built-in provider claims a composite
   record). Stage 8 finds `PlanCache<Customer>.Instance` and dispatches into the
   generated plan, exactly as Milestone 2 already does.
7. **`Customer`'s `Status` member resolves via the compiled rule.** The generated
   plan's `context.Resolve<CustomerStatus>(descriptor)` call for the `Status`
   parameter passes a descriptor whose `DeclaringType` is `typeof(Customer)` and
   `Name` is `"Status"` (ADR-0020 — generator-emitted, not inferred).
   The context expands it into a `CompositionRequest` carrying the same
   `DeclaringType`. Stage 4's compiled rule for `(typeof(Customer), "Status")`
   matches directly against that request field — no path-parent lookup involved —
   and returns `CustomerStatus.Active`, `Success`. No further stages run for this
   request.
8. **A hypothetical `IClock` dependency elsewhere in the graph** resolves via
   stage 3's exact-registration lookup: `context.Resolve<IClock>(descriptor)` finds
   the `ClockProfile`-sourced registration, invokes its factory
   (`() => new FakeClock()`) — a zero-argument factory here, so no nested
   `Resolve<T>()`/`ManualResolve` call happens; if it *had* called
   `context.Resolve<T>()` internally, `CompositionContext` would push a
   manual-resolve invocation frame around that factory call before invoking it,
   giving each `Resolve<T>()` made inside it a `ManualResolve(0)`, `ManualResolve(1)`,
   ... segment from that frame's counter, popped in `finally` when the factory
   returns or throws — per
   [ADR-0019](../adr/0019-registrations-and-service-provider-injection.md).
9. **Reuse.** A second `composer.Create<Customer>()` call (same `Composer`
   instance) repeats steps 6–8 against the exact same frozen
   `CompositionConfiguration` — nothing about step 6–8 mutates any state steps 1–5
   produced; only a fresh `CompositionContext` (new scope, path, frame stack, trace
   buffer, and — absent an explicit seed override — a newly generated seed) is
   created per call, exactly as Milestone 2 already does for `Create<T>()`'s
   per-call lifetime.

## Phases

Ordered so each phase either unblocks the next or closes a real gap against the
exit criteria — provenance/cycle bookkeeping (needed by conflict diagnostics) is
built once in Phase 0 rather than retrofitted into registrations in a later phase.
Each phase below is an **independently reviewable implementation unit** — it should
be possible to review and merge it on its own — but that doesn't mean exactly one PR
per phase: Phase 0 and Phase 1 in particular are small and tightly coupled enough
that combining them into one PR may turn out to be the more reviewable shape once
implementation is underway, and a phase later in the list turning out larger than
expected may be worth splitting. Use judgment against what's actually reviewable,
not the phase numbering, as the PR boundary.

### Phase 0 — Builder/configuration skeleton and build-time validation

Establishes `CompositionBuilder`/`CompositionConfiguration`/`Composer` wiring and
the validation framework every later phase's conflict detection plugs into. This
phase also ships the structured `CompositionConfigurationException` model in full
(not deferred to a later phase, since every subsequent phase's conflict tests depend
on it existing already) and the general scalar-fail-fast rule (ADR-0017's
Amendment), demonstrated here only via `WithSeed` since it's the only scalar verb
that exists yet.

- [x] `CompositionBuilder` (`public sealed class`) — internal mutable
      accumulator state only; no public configuration verbs yet beyond `WithSeed(int)`
- [x] `ConfigurationSource` (a small value: `Direct`, or an ordered list
      of profile `Type`s) — the provenance concept every later phase's accumulated
      entries carry, built now so Phase 2 (Profiles) doesn't need to retrofit it
      into Phase 1 (Registrations)'s entries. Shipped `public`, not `internal` as
      originally scoped — see this phase's Notes
- [x] `CompositionConfiguration` (`internal sealed class`) — holds the frozen seed
      policy for now; every other field (registrations, compiled stage-4 rules,
      `IServiceProvider`, `CollectionSizePolicy`) added by its owning phase below
- [x] `CompositionConfigurationException` (`public`, distinct from
      `CompositionException`) and `CompositionConfigurationError` — a sealed
      abstract base with one sealed record case per conflict kind. Only
      `DuplicateConfigurationOption` exists so far, per ADR-0017's Amendment;
      `DuplicateRegistration`/`DuplicateRule`/`ProfileCycle` are added by the phases
      that introduce them (1/3/2 respectively), matching the discriminated-union
      shape this codebase already uses for `PathSegment`/`CompositionResult` — the
      exception's `Message` is rendered from its `Errors` list via a `switch` over
      each case, not the other way around
- [x] Scalar-configuration fail-fast, generalized: `Build()`'s validation pass
      treats a scalar verb called more than once (this phase: `WithSeed` only) as
      one `DuplicateConfigurationOption` error — the same code path Phase 1/3
      extend for `UseServiceProvider`/`WithCollectionSize`, not a
      `WithSeed`-specific special case. This is a deliberate departure from a
      typical "options builder" last-wins convention — see ADR-0017's Amendment for
      the full rationale (a contradictory scalar configuration, like two different
      seeds, has no coherent "effective value" to fall back to). Implemented via a
      small internal `ConfigurationOptionSlot<TValue>` helper (tracks a value plus
      every source that set it) that every scalar verb shares, not prescribed by
      name in this plan but exactly the kind of shared mechanism this task called for
- [x] `Composer.Create(Action<CompositionBuilder> configure)` — constructs the
      builder, invokes `configure`, calls `Build()`, wraps the result. The existing
      parameterless `Composer.Create()` now delegates to it with an empty callback
      rather than being a second, independent code path
- [x] Rewire `Composer.Create<T>()`/`CreateMany<T>(count)` onto the new
      `CompositionConfiguration`'s seed policy
- [x] Unit tests: `Composer.Create(builder => builder.WithSeed(n))` produces the same
      output as `CreateRootForTesting` with the same seed (parity test, proving the
      new public path and the old internal seam agree); `Composer` is safe to call
      `Create<T>()` on multiple times/concurrently with no observable state bleed
      between calls; an empty `configure` callback behaves identically to
      Milestone 2's default-seed behavior; `WithSeed` called twice throws
      `CompositionConfigurationException` whose `Errors` contains exactly one
      `DuplicateConfigurationOption` entry naming `"WithSeed"` — asserted against
      the structured `Errors` list (a type/pattern check), not message text

### Phase 1 — Registrations and service injection

Direct registrations only — no profile involvement in this phase's own tests (the
`ConfigurationSource`/provenance plumbing from Phase 0 is exercised here only as
`Direct`; profile-sourced conflicts are Phase 2's tests, once profiles actually
exist to source anything).

- [ ] `Register<T>(Func<ICompositionContext, T> factory)` and
      `Register<T>(Func<T> factory)` on `CompositionBuilder`, accumulating into an
      internal exact-registration lookup. Outcome requirements only — the concrete
      internal representation (a dictionary, a small array scanned linearly, or
      anything else) is an implementation choice made during this phase, not
      prescribed here: immutable once `Build()` completes, no reflection anywhere
      in registration or lookup, no per-resolution compilation/codegen step, and a
      factory is invoked through the existing `ICompositionContext` the same way any
      other resolution consumer already is
- [ ] `ICompositionContext.Resolve<T>()` (descriptor-less overload) and
      `PathSegment.ManualResolve(int Ordinal)` (ADR-0019) — implemented
      as a manual-resolve invocation frame pushed immediately before a registration/
      rule factory is invoked and popped in `finally` immediately after it returns
      or throws; the frame holds one counter, shared and incremented by every
      descriptor-less `Resolve<T>()` call made during that one factory invocation; a
      factory that itself triggers a nested factory invocation gets a **new**,
      independent frame for that nested call, never the outer counter continued.
      `CompositionRequestKind.ManualResolve` extends the existing enum
- [ ] **Construction-cycle guard around registration/rule factory invocation**
      (ADR-0019 correction) — a genuine gap found during PR review, not just a
      docs fix: stage 3/4 registrations and rules are checked *before* stage 8, so
      a self-referencing registration/rule (`Register<IClock>(context =>
      context.Resolve<IClock>())`) never reaches the existing active-construction-
      frame check at all and recurses to `StackOverflowException`. Fix: push the
      requested type onto the same active-construction-frame stack
      ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md))
      immediately before invoking a registration or rule factory, pop in `finally`;
      a type already active on the stack (from an enclosing generated-plan
      dispatch or an enclosing factory invocation) fails with a diagnosable
      `CompositionException` naming the chain, the same shape stage 8's existing
      cycle failure already produces — never a raw stack overflow
- [ ] `Build()` validation: scan the accumulated registration list for duplicate
      exact-registration types; on any duplicate, add a `DuplicateRegistration`
      error (affected type, contributing `Sources`) to the aggregated list; on no
      duplicates, compile the final immutable lookup onto `CompositionConfiguration`
- [ ] Stage 3 dispatch: `CompositionContext.ResolveCore<T>` checks the compiled
      exact-registration lookup (unchanged from Milestone 2's internal-only
      behavior, now populated for real)
- [ ] `UseServiceProvider(IServiceProvider provider)` on `CompositionBuilder` — a
      scalar verb, following Phase 0's general scalar-fail-fast validation path (a
      second call anywhere in the same configuration is a `DuplicateConfigurationOption` error),
      not a bespoke `IServiceProvider`-specific conflict check
- [ ] Stage 3's `IServiceProvider` fallback sub-step: on an exact-registration miss,
      if configured, call `provider.GetService(typeof(T))`; `null` falls through to
      stage 4; a thrown exception surfaces as `CompositionException` with the
      original as `InnerException` (never `throw ex;`); a non-null,
      non-`T`-assignable result throws a structured `CompositionException` naming
      both types; `Compono` never creates, resolves, or disposes a scope
- [ ] Unit tests (direct registrations/configuration only — no profiles): a
      registration factory resolves and is invoked exactly once per request;
      `Func<T>`/`Func<ICompositionContext, T>` overloads both work; two *direct*
      registrations for the same type throw `CompositionConfigurationException`
      with one `DuplicateRegistration` error naming both as `Direct` sources;
      manual-resolve invocation-frame tests proving (a) two sibling
      `context.Resolve<T>()` calls inside one factory fork independently
      (mirroring ADR-0012's existing tag-collision test shape), (b) a nested
      factory invocation gets its own independent counter rather than continuing
      the outer one, (c) an exception thrown mid-factory still pops the frame (a
      subsequent, unrelated request's manual-resolve ordinal starts at `0`, not
      wherever the failed factory's counter left off); `UseServiceProvider`
      fallback order (registration wins, `null` falls through, exception
      propagates with `InnerException` preserved, wrong-type result throws a
      structured exception, no scope/dispose call ever made — verify via a
      test-double `IServiceProvider` that fails the test if `Dispose`/scope-related
      members are ever touched); `UseServiceProvider` called twice throws with one
      `DuplicateConfigurationOption` error; a self-referencing registration
      (`Register<IClock>(context => context.Resolve<IClock>())`) and a
      self-referencing configuration rule each fail with a diagnosable
      `CompositionException` naming the cycle, not a `StackOverflowException` —
      the construction-cycle guard's own regression test, with a timeout/bounded
      assertion so a regression here fails the test suite rather than hanging it

### Phase 2 — Profiles

- [ ] `ICompositionProfile` (`public interface`) — `void Configure(CompositionBuilder
      builder)`
- [ ] `AddProfile<TProfile>()` (`where TProfile : ICompositionProfile, new()`) and
      `AddProfile(ICompositionProfile profile)` on `CompositionBuilder`
- [ ] Cycle-detection stack (`internal`, `Stack<Type>` keyed by
      `profile.GetType()`, pushed before `Configure` runs, popped in `finally`);
      `AddProfile` for a type already on the stack throws
      `CompositionConfigurationException` **immediately, from within `AddProfile`
      itself** — not from `Build()` — containing exactly one `ProfileCycle` error
      naming the full chain in application order. This is a distinct failure path
      from `Build()`'s aggregated
      validation (ADR-0017's Amendment, point 3) — implementation should not route
      cycle detection through the same aggregation buffer `Build()`'s conflict scan
      uses
- [ ] `ConfigurationSource` population: every registration/rule accumulated while
      the cycle-detection stack is non-empty is tagged with the current stack's
      contents (outermost to innermost profile) instead of `Direct`
- [ ] Unit tests: `AddProfile<T>()`/`AddProfile(instance)` both apply `Configure`
      synchronously and in call order; **direct + profile** and **profile +
      profile** duplicate-registration conflicts (moved here from Phase 1, now that
      profiles exist to source them) produce a `CompositionConfigurationException`
      whose `Errors` name both real sources by profile name; `ProfileA → ProfileB →
      ProfileA` throws immediately, before `Build()` runs, with a single
      `ProfileCycle` error naming the full chain — not a `StackOverflowException`,
      and not aggregated with any other conflict a fuller builder chain might also
      contain; a profile that adds another (non-cyclic) profile from inside its own
      `Configure` works and both profiles' registrations end up in the final
      configuration; a three-level nested-profile conflict's exception names the
      full chain, not just the innermost

### Phase 3 — Configuration rules: type/member value rules and collection size

- [ ] `CompositionRequestDescriptor.DeclaringType` (ADR-0020) and the
      matching field on the internal `CompositionRequest` — the generator-emitted
      value for `ConstructorParameter`/`RequiredMember` requests; unused for other
      request kinds
- [ ] `Compono.Generators`: descriptor-emission template/emitter updated to include
      `DeclaringType` (a mechanical, `global::`-qualified `typeof(...)` argument
      alongside the existing `Kind`/`Ordinal`/`Name`/`Nullability` arguments);
      existing `.verified.cs` snapshots regenerated (content reviewed, same class of
      change as PLAN-0002 Phase 0's Scriban-template regression fix)
- [ ] `builder.For<T>()` — the type/member rule entry point on `CompositionBuilder`.
      Calling `.Use(...)` directly registers a type rule; calling
      `.Member(x => x.Y)` first, then `.Use(...)`, registers a member rule instead.
      Whether this is one class with two builder-returning code paths, or two
      distinct classes (a type-scoped builder and a member-scoped builder returned
      from `.Member(...)`), is an implementation choice — the plan requires the
      public call shape and the `.Member(...)` expression being parsed immediately
      (not deferred to `Build()`), not a specific class split
- [ ] Internal compiled stage-4 rule matching: for a member rule, matches when an
      incoming request's `DeclaringType` and member name equal the rule's captured
      values (never inferred from path state); for a type rule, matches by
      `RequestedType` alone. Whether this is one internal provider type or several
      is an implementation choice — the plan requires member rules to take
      precedence over type rules for the same effective request (specificity-based,
      enforced by compiled dispatch order, not call order), not a specific class
      split
- [ ] `Build()` validation: a duplicate exact-key conflict (same `(declaring type,
      member name)` pair twice, or the same type-rule type twice) adds a
      `DuplicateRule` error to the aggregated list, same shape as Phase 1's
      registration conflicts; a member rule and a type rule that could both match
      the same request are explicitly **not** a conflict
- [ ] `WithCollectionSize(int)` — a scalar verb (global default), following the same
      `DuplicateConfigurationOption` validation path as `WithSeed`/`UseServiceProvider`; and
      `.For<T>().Member(x => x.Y).WithCollectionSize(int)` (member-scoped,
      reusing the same expression-parsing path as a member value rule, and the same
      keyed-conflict validation as a member value rule) — both accumulated into
      `CollectionSizePolicy` on `CompositionConfiguration`, not compiled into any
      stage-4 rule
- [ ] `ICompositionContext.ResolveCollectionSize()` — **parameterless**, the
      **only** new public context method for collection size. Corrected twice
      during review: (1) a generated collection plan's `Compose(ICompositionContext)`
      has no descriptor to pass (that's fully consumed before `CollectionPlanCache<T>`
      dispatch ever runs), so the earlier descriptor-taking design was
      unimplementable as scoped; (2) the member-override key must come from the
      current request's own `DeclaringType` field (the same field member value-rule
      matching already uses, base-aware for inherited required members) — **not**
      from the parent path node's `RequestedType` (a first-draft fix for (1) that
      introduced a new bug: for an inherited member, the parent path node's type is
      the composed/runtime type, not the declaring type a `.Member(...)` rule
      captures via reflection, so the override would silently never match for
      inheritance). Root-level and member-scoped collection plans call the exact
      same method; a root request has no `DeclaringType` at all and falls through
      to the global default/built-in `3`. The three-level precedence lookup
      (member-scoped override → global default → ADR-0013's built-in `3`) is a
      plain configuration read — no randomness, no
      *new* path segment pushed (it reads the one already there)
- [ ] `Compono.Generators`: `CollectionPlan.scriban` (and its emitter model) updated
      to call `context.ResolveCollectionSize(...)` instead of emitting the literal
      `3`; existing `.verified.cs` collection-plan snapshots regenerated
- [ ] Unit tests: a member value rule wins over a type rule for the same effective
      request; two member rules for different declaring types with the same member
      name and value type don't collide (the exact bug flagged during design
      review) — asserted via `DeclaringType`-based matching specifically, not path
      inference; exact-type matching confirmed (a rule for `IClock` does not
      satisfy a request for a concrete `SystemClock` type); malformed
      `.Member(...)` expression throws immediately at the call site, not at
      `Build()`; duplicate type rule and duplicate member rule each produce one
      `DuplicateRule` error; `WithCollectionSize` called twice (global) throws one
      `DuplicateConfigurationOption` error; global `WithCollectionSize` changes default
      collection length end-to-end through a real generated collection plan;
      member-scoped `WithCollectionSize` overrides the global default for that
      member only, confirmed against a sibling member that keeps the global default;
      **a member rule matches a positional-record constructor parameter correctly**
      (`Customer(string FirstName, ...)` — the primary documented usage shape,
      exercised through a real generated plan, not a hand-written test double) —
      this is the coverage that actually proves the documented "records always
      match, hand-written classes with divergent parameter/property naming are a
      known limitation" claim (ADR-0020) rather than leaving it asserted but
      unverified
- [ ] `Compono.Generators.Tests`: at least one regenerated snapshot confirming
      `DeclaringType` in an emitted descriptor construction, and at least one
      regenerated collection-plan snapshot confirming the `ResolveCollectionSize`
      call site replacing the literal `3`

### Phase 4 — End-to-end wiring, external verification, and cleanup

By this phase, every conflict-diagnostic path, every message's structured-error
shape, and every feature's own unit/generator-level tests already exist and pass
(Phases 0–3 each test their own diagnostics and integration behavior directly —
none of that is deferred here). This phase is deliberately narrow:

- [ ] One combined end-to-end test exercising every prior phase's feature together
      in one graph (registrations + `IServiceProvider` + profiles + type/member
      rules + collection size), matching this plan's Execution Flow section —
      an integration-level confirmation that the phases compose correctly, not a
      re-test of any individual phase's own conflict/diagnostic logic
- [ ] Real manual verification (per `tasks/implement.md` step 7 and PLAN-0002 Phase
      2's lesson about skipping this): `dotnet pack` `Compono` into a local feed,
      reference it from a genuinely separate throwaway console project, exercise a
      representative `Composer.Create(builder => ...)` combining registrations, a
      profile, a member rule, and collection-size configuration, confirm real
      generated-plan dispatch and the compiled stage-4 rules actually apply
- [ ] **Review Milestone 2's internal test seams** (`Composer.CreateRootForTesting<T>`/
      `CreateManyForTesting<T>`) now that the real public configuration path exists:
      for each, determine whether it's still pulling weight — prefer testing
      through `Composer.Create(builder => ...)` wherever the public API can express
      the same scenario. Retain a seam only where it provides coverage the public
      API genuinely cannot reach (e.g. a raw-seed test that predates and is
      orthogonal to configuration entirely); remove or narrow it otherwise, with the
      reasoning recorded in this plan's Notes section once decided
- [ ] `docs/mvp.md`/`docs/architecture.md`/`docs/public-api.md` updated to describe
      Milestone 3's shipped behavior as current state (not "not yet implemented"),
      per `tasks/implement.md`'s doc-update step

## Critical Files

New (paths and exact class/file splits below are starting candidates, not
requirements — Phase 3 in particular explicitly leaves the rule-builder and
compiled-provider class shape to implementation). **Correction after Phase 0**:
this repo's actual `src/Compono` layout is flat (namespace `Compono`, no nested
folder) for everything except `Providers/` — despite `coding-standards.md`'s
"organize by feature/concern folder" guidance, no Milestone 1/2 file ever actually
nested under a `Composition/`/`Configuration/` folder. Phase 0 followed the real,
established convention instead of this plan's originally-sketched `Configuration/`
subfolder; the file list below is corrected to match:

- `src/Compono/CompositionBuilder.cs` — done, Phase 0
- `src/Compono/CompositionConfiguration.cs` — done, Phase 0
- `src/Compono/ConfigurationSource.cs` — done, Phase 0 (public, not internal — see
  this phase's Notes)
- `src/Compono/CompositionConfigurationException.cs` — done, Phase 0
- `src/Compono/CompositionConfigurationError.cs` — done, Phase 0 (only
  `DuplicateConfigurationOption` so far)
- `src/Compono/ConfigurationOptionSlot.cs` — done, Phase 0 (not originally listed;
  the shared scalar-fail-fast mechanism the Amendment called for)
- `src/Compono/ICompositionProfile.cs` — Phase 2
- `src/Compono/CollectionSizePolicy.cs` — Phase 3
- One or more files under `src/Compono/` for the `.For<T>()` rule builder(s) —
  final split decided during Phase 3
- One or more files under `src/Compono/Providers/` for the compiled stage-4 rule
  matching — final split decided during Phase 3

Modified:

- `src/Compono/Composer.cs` — done, Phase 0: `Create(Action<CompositionBuilder>)`,
  seed-policy wiring for `Create<T>()`/`CreateMany<T>()`; `CreateRootForTesting`/
  `CreateManyForTesting` untouched (still bypass configuration entirely)
- `src/Compono/CompositionSeed.cs` — done, Phase 0: doc comment only, now
  referencing `CompositionBuilder.WithSeed`
- `src/Compono/CompositionContext.cs` — Phase 1 (descriptor-less `Resolve<T>()`,
  stage 3's `IServiceProvider` fallback sub-step) and Phase 3
  (`ResolveCollectionSize`)
- `src/Compono/ICompositionContext.cs` — Phase 1/3 (new members)
- `src/Compono/CompositionRequestDescriptor.cs` — Phase 3 (`DeclaringType` field)
- `src/Compono/CompositionRequest.cs` — Phase 3 (`DeclaringType` field)
- `src/Compono/CompositionRequestKind.cs` — Phase 1 (`ManualResolve` case)
- `src/Compono/PathSegment.cs` — Phase 1 (`ManualResolve` case)
- `src/Compono.Generators/Templates/CompositionPlan.scriban` (and its emitter
  model) — Phase 3, `DeclaringType` argument in emitted descriptor construction
- `src/Compono.Generators/Templates/CollectionPlan.scriban` (and its emitter
  model) — Phase 3, `ResolveCollectionSize` call site replacing the literal `3`
- `docs/architecture.md` — done, Phase 0: Immutable Configuration Model section
  updated to describe the builder/configuration split and `WithSeed` as shipped,
  the rest of Milestone 3's configuration verbs still marked not yet implemented;
  `docs/mvp.md`/`docs/public-api.md` unchanged (already accurate as intended-shape
  docs). Phase 4 does the final reconciliation pass once every phase ships

## Test Plan

Per `references/testing.md`'s established pattern (xUnit v3, AutoFixture,
NSubstitute, AwesomeAssertions in `test/Compono.Tests`; Verify-based snapshot tests
in `test/Compono.Generators.Tests` for the Phase 3 generator template changes).
Coverage is itemized per phase above; cross-cutting concerns:

- Every `CompositionConfigurationException` path (duplicate registration, duplicate
  rule, duplicate scalar, profile cycle) gets a direct test asserting the
  **structured** `Errors` list (`Kind`, affected type/member, `Sources`) — not
  string-matching the rendered message. Message rendering itself gets exactly one
  test confirming it reflects the structured data, not a test per conflict kind.
- Profile-cycle tests assert the exception is thrown synchronously from
  `AddProfile`, before `Build()` runs, and contains exactly one error — distinct
  from every `Build()`-aggregated conflict test, which asserts on a
  multi-`Composer.Create(...)`-call chain that intentionally introduces more than
  one conflict at once and checks all of them appear.
- Manual-resolve invocation-frame behavior (sibling distinctness, independent
  nested-invocation counters, frame cleanup on exception) is tested at the
  `CompositionContext`/`RandomSource` level, mirroring the shape of
  `RandomSourceTests`/`CompositionRandomIntegrationTests` from PLAN-0002 Phase 1.
- Real generated-plan/generated-collection-plan execution (via
  `GeneratorTestHelpers.CompileAndExecute`, per PLAN-0002 Phase 2's precedent) for
  at least: a type rule, a member rule (confirming `DeclaringType`-based matching
  through real generated code, not a hand-written test double), a global
  collection-size override, and a member-scoped collection-size override.
- Manual `dotnet pack`-based verification, once, in Phase 4 — not deferred or
  skipped, per the explicit lesson recorded in PLAN-0002 Phase 2's fifth review
  round.

## Notes

### Phase 0 (Done)

- **Flat file layout, not a `Configuration/` subfolder.** This plan's Critical
  Files section originally sketched `src/Compono/Configuration/*.cs`. The real
  `src/Compono` layout is flat (namespace `Compono`) for everything except
  `Providers/` — no Milestone 1/2 file actually nested under a `Composition/`-style
  folder despite `coding-standards.md`'s aspirational "organize by feature/concern
  folder" guidance. Followed the established real convention instead; see the
  corrected Critical Files list above.
- **`ConfigurationSource` and `CompositionConfigurationError` are `public`, not
  `internal`** as this plan's Scope section originally implied for
  `ConfigurationSource`. `CompositionConfigurationError.DuplicateConfigurationOption`
  exposes `IReadOnlyList<ConfigurationSource> Sources` as a public property — an
  `internal` type there would be a compile error (CS0053, inconsistent
  accessibility) on a `public` record. Consumers inspecting a caught
  `CompositionConfigurationException.Errors` can meaningfully render *where* a
  conflicting configuration came from this way, which is a reasonable diagnostic
  capability to expose regardless of the accessibility question.
- **`ConfigurationOptionSlot<TValue>`** (not named in this plan's original task
  list) is the internal helper implementing the "shared code path, not a
  `WithSeed`-specific special case" requirement: tracks one scalar option's value
  and every source that set it, reused unchanged by `UseServiceProvider`
  (Phase 1) and `WithCollectionSize` (Phase 3).
- The parameterless `Composer.Create()` now delegates to
  `Create(static _ => { })` rather than being a second, independent construction
  path — one implementation, not two, for "no explicit configuration."
- All 83 `Compono.Tests` pass (both `net10.0`/`net11.0`), including 8 new tests in
  `ComposerConfigurationTests`. `Compono.Generators`/`Compono.Generators.Tests`
  untouched and unaffected (confirmed via a clean build) — Phase 0 doesn't touch
  generator-facing code, so no manual `dotnet pack` verification was needed for
  this phase specifically (Phase 4 still does one for the whole milestone).
- `docs/architecture.md`'s Immutable Configuration Model section updated in the
  same change to describe `WithSeed`/the builder split as shipped, everything else
  still pending.

### PR review correction (2026-07-29): real bug found while adding the promised concurrency/default-seed tests

PR #16 review (Codex) correctly flagged that this phase's original tests didn't
cover what its own checklist item promised (concurrent `Create<T>()` calls, and an
empty-configure composer's default-seed-per-call behavior). Adding real coverage
for both surfaced an actual, independent defect: **every `Composer.Create()` call
with no explicit `WithSeed(...)` was silently using seed `0` for every root
operation, instead of generating a fresh seed per call.**

Root cause: `ConfigurationOptionSlot<TValue>.Value` was declared `TValue?` on an
*unconstrained* generic type parameter. `T?` on an unconstrained `T` is a
compile-time-only nullable annotation, not `Nullable<T>` — for a value-type
instantiation (`TValue = CompositionSeed`), it erases to plain `TValue`, so an
unset slot's `Value` was `default(CompositionSeed)` (`Value = 0`), not `null`.
`CompositionBuilder.Build()`'s `Seed = _seed.Value` therefore always produced a
non-null `CompositionSeed(0)`, and `Composer.Create<T>()`'s
`_configuration.Seed ?? CompositionSeed.Generate()` never reached the `Generate()`
fallback at all.

Fixed by giving `ConfigurationOptionSlot<TValue>` an explicit `HasValue` (backed by
`_sources.Count > 0`, already tracked for conflict detection) instead of relying on
`Value`'s nullability to distinguish "never set" from "set to `default`" — the only
correct way to do this for an unconstrained generic. `CompositionBuilder.Build()`
now reads `_seed.HasValue ? _seed.Value : null`. This is exactly the kind of defect
`testing.md`'s "add tests as you build" principle exists to catch before merge —
found here specifically because the review pushed the promised coverage from
"claimed" to "real."

### PR review correction (2026-07-29): ADR-0010/ADR-0012 amendments reverted

PR #16 review (Codex) correctly flagged that this PR's original ADR-0010/ADR-0012
edits — appending `## Amendment 3` sections adding `DeclaringType`/`ManualResolve`
to those two `Accepted` ADRs — violated `AGENTS.md`'s non-negotiable ADR
immutability rule. ADR-0012's own prior amendments (which this PR's first draft
took as precedent) were made *during Milestone 2's own implementation*, before
PLAN-0002 reached `Done` — the same design cycle as the ADR itself. This PR's edits
reached back into `Done`-milestone ADRs from a *later*, separate design cycle
(Milestone 3), which is a materially different, genuinely rule-violating situation,
not the same precedent.

Fixed by reverting both ADRs to their pre-PR content entirely and moving the
`DeclaringType` field definition into ADR-0020 (which needed it and already
discussed it at length, previously via a cross-reference instead of an inline
definition) and the full `ManualResolve` definition/verification into ADR-0019
(previously a cross-reference to ADR-0012's now-removed amendment) — both as those
new ADRs' own Decision Outcome content, additive to ADR-0010/ADR-0012's `Accepted`
text without editing it. Every cross-reference to the removed amendments across
`docs/architecture.md` and this plan was corrected to point at ADR-0019/ADR-0020
directly. No source code was affected — `DeclaringType`/`ManualResolve` are Phase
1/3 scope, not yet implemented, so this was a pure documentation/ADR-structure fix.
