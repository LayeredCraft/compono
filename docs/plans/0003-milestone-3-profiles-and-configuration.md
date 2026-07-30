# [PLAN-0003] Milestone 3: Profiles and Configuration

**Status:** Done

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

- [x] `Register<T>(Func<ICompositionContext, T> factory)` and
      `Register<T>(Func<T> factory)` on `CompositionBuilder`, accumulating into an
      internal exact-registration lookup. Outcome requirements only — the concrete
      internal representation (a dictionary, a small array scanned linearly, or
      anything else) is an implementation choice made during this phase, not
      prescribed here: immutable once `Build()` completes, no reflection anywhere
      in registration or lookup, no per-resolution compilation/codegen step, and a
      factory is invoked through the existing `ICompositionContext` the same way any
      other resolution consumer already is
- [x] `ICompositionContext.Resolve<T>()` (descriptor-less overload) and
      `PathSegment.ManualResolve(int Ordinal)` (ADR-0019) — implemented
      as a manual-resolve invocation frame pushed immediately before a registration/
      rule factory is invoked and popped in `finally` immediately after it returns
      or throws; the frame holds one counter, shared and incremented by every
      descriptor-less `Resolve<T>()` call made during that one factory invocation; a
      factory that itself triggers a nested factory invocation gets a **new**,
      independent frame for that nested call, never the outer counter continued.
      `CompositionRequestKind.ManualResolve` extends the existing enum
- [x] **Factory-reentrance guard around registration/rule factory invocation**
      (ADR-0019 correction, revised twice during review) — a genuine gap, not a
      docs fix: stage 3/4 registrations and rules are checked *before* stage 8, so
      a self-referencing registration/rule (`Register<IClock>(context =>
      context.Resolve<IClock>())`) never reaches the existing active-construction-
      frame check at all and recurses to `StackOverflowException`. **Do not** push
      the requested *type* onto ADR-0011's existing type-keyed active-construction-
      frame stack (a first-draft fix that would reject a legitimate cycle-
      terminating rule as a false positive — e.g. a self-referencing `Node`'s
      generated plan active on that stack, with a `.Member(x => x.Child).Use(_ =>
      new Node(null))` rule correctly terminating the graph without recursing at
      all). Instead: a **separate stack, keyed by factory delegate identity**, not
      type — push the specific `Func<...>` instance about to be invoked (the exact
      registration's or compiled rule's factory) immediately before invoking it,
      pop in `finally`; if that *same delegate instance* is already active, it's a
      genuine self-recursive cycle (the factory calling back into resolving a
      request that routes to itself) and fails with a diagnosable
      `CompositionException` naming the chain — never a raw stack overflow. Two
      different registrations/rules targeting the same type, or a rule and an
      unrelated enclosing generated plan for the same type, never collide, since
      they're different delegate instances — this is what keeps the
      `Node.Child`-style terminator working correctly. The guard *mechanism* is
      built once, here, since registrations already need it; **its rule-specific
      regression coverage (a self-referencing rule, and the `Node.Child` legitimate-
      termination case) is Phase 3's task, not this one** — rules don't exist as an
      API until Phase 3, so a test exercising `.Member(...).Use(...)` can't compile
      if this phase ships as its own PR before Phase 3 lands, per this plan's own
      phase-independence requirement
- [x] `Build()` validation: scan the accumulated registration list for duplicate
      exact-registration types; on any duplicate, add a `DuplicateRegistration`
      error (affected type, contributing `Sources`) to the aggregated list; on no
      duplicates, compile the final immutable lookup onto `CompositionConfiguration`
- [x] Stage 3 dispatch: `CompositionContext.ResolveCore<T>` checks the compiled
      exact-registration lookup (unchanged from Milestone 2's internal-only
      behavior, now populated for real)
- [x] `UseServiceProvider(IServiceProvider provider)` on `CompositionBuilder` — a
      scalar verb, following Phase 0's general scalar-fail-fast validation path (a
      second call anywhere in the same configuration is a `DuplicateConfigurationOption` error),
      not a bespoke `IServiceProvider`-specific conflict check
- [x] Stage 3's `IServiceProvider` fallback sub-step: on an exact-registration miss,
      if configured, call `provider.GetService(typeof(T))`; `null` falls through to
      stage 4; a thrown exception surfaces as `CompositionException` with the
      original as `InnerException` (never `throw ex;`); a non-null,
      non-`T`-assignable result throws a structured `CompositionException` naming
      both types; `Compono` never creates, resolves, or disposes a scope
- [x] Unit tests (direct registrations/configuration only — no profiles): a
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
      (`Register<IClock>(context => context.Resolve<IClock>())`) fails with a
      diagnosable `CompositionException` naming the cycle, not a
      `StackOverflowException` — the factory-reentrance guard's own regression
      test for the registration case, with a timeout/bounded assertion so a
      regression here fails the test suite rather than hanging it. (The
      equivalent rule-based tests — a self-referencing configuration rule, and the
      `Node.Child` legitimate-termination case proving the guard doesn't share
      ADR-0011's type-keyed stack — are Phase 3's task, once `.Member(...).Use(...)`
      exists; see that phase's checklist)

### Phase 2 — Profiles

- [x] `ICompositionProfile` (`public interface`) — `void Configure(CompositionBuilder
      builder)`
- [x] `AddProfile<TProfile>()` (`where TProfile : ICompositionProfile, new()`) and
      `AddProfile(ICompositionProfile profile)` on `CompositionBuilder`
- [x] Cycle-detection stack (`internal`, `Stack<Type>` keyed by
      `profile.GetType()`, pushed before `Configure` runs, popped in `finally`);
      `AddProfile` for a type already on the stack throws
      `CompositionConfigurationException` **immediately, from within `AddProfile`
      itself** — not from `Build()` — containing exactly one `ProfileCycle` error
      naming the full chain in application order. This is a distinct failure path
      from `Build()`'s aggregated
      validation (ADR-0017's Amendment, point 3) — implementation should not route
      cycle detection through the same aggregation buffer `Build()`'s conflict scan
      uses
- [x] `ConfigurationSource` population: every registration/rule accumulated while
      the cycle-detection stack is non-empty is tagged with the current stack's
      contents (outermost to innermost profile) instead of `Direct`
- [x] Unit tests: `AddProfile<T>()`/`AddProfile(instance)` both apply `Configure`
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

- [x] `CompositionRequestDescriptor.DeclaringType` — **`Type?`, nullable, not
      `Type`** (ADR-0020, corrected during review: a non-nullable field leaves no
      contract-valid value for `CollectionElement`/`DictionaryKey`/`DictionaryValue`
      requests, which have no declaring type at all — forcing an undocumented
      sentinel otherwise) — and the matching field on the internal
      `CompositionRequest`. The generator-emitted, non-null value for
      `ConstructorParameter`/`RequiredMember` requests; `null` (never a sentinel)
      for every other request kind
- [x] `Compono.Generators`: descriptor-emission template/emitter updated to include
      `DeclaringType` (a mechanical, `global::`-qualified `typeof(...)` argument
      alongside the existing `Kind`/`Ordinal`/`Name`/`Nullability` arguments);
      existing `.verified.cs` snapshots regenerated (content reviewed, same class of
      change as PLAN-0002 Phase 0's Scriban-template regression fix)
- [x] `builder.For<T>()` — the type/member rule entry point on `CompositionBuilder`.
      Calling `.Use(...)` directly registers a type rule; calling
      `.Member(x => x.Y)` first, then `.Use(...)`, registers a member rule instead.
      Whether this is one class with two builder-returning code paths, or two
      distinct classes (a type-scoped builder and a member-scoped builder returned
      from `.Member(...)`), is an implementation choice — the plan requires the
      public call shape and the `.Member(...)` expression being parsed immediately
      (not deferred to `Build()`), not a specific class split
- [x] Internal compiled stage-4 rule matching: for a member rule, matches when an
      incoming request's `DeclaringType` and member name equal the rule's captured
      values (never inferred from path state); for a type rule, matches by
      `RequestedType` alone. Whether this is one internal provider type or several
      is an implementation choice — the plan requires member rules to take
      precedence over type rules for the same effective request (specificity-based,
      enforced by compiled dispatch order, not call order), not a specific class
      split
- [x] `Build()` validation: a duplicate exact-key conflict (same `(declaring type,
      member name)` pair twice, or the same type-rule type twice) adds a
      `DuplicateRule` error to the aggregated list, same shape as Phase 1's
      registration conflicts; a member rule and a type rule that could both match
      the same request are explicitly **not** a conflict
- [x] `WithCollectionSize(int)` — a scalar verb (global default), following the same
      `DuplicateConfigurationOption` validation path as `WithSeed`/`UseServiceProvider`; and
      `.For<T>().Member(x => x.Y).WithCollectionSize(int)` (member-scoped,
      reusing the same expression-parsing path as a member value rule, and the same
      keyed-conflict validation as a member value rule) — both accumulated into
      `CollectionSizePolicy` on `CompositionConfiguration`, not compiled into any
      stage-4 rule. **Both reject a negative size immediately** via
      `ArgumentOutOfRangeException.ThrowIfNegative(size)`, added during review —
      not deferred into the aggregated `CompositionConfigurationError` list (this
      is ordinary argument validation, not a cross-entry conflict), matching
      `Composer.CreateMany<T>(int count)`'s existing immediate-validation pattern
      for the identical shape of mistake — not left to fail later, confusingly,
      inside a generated collection plan's array/list allocation
- [x] `ICompositionContext.ResolveCollectionSize()` — **parameterless**, the
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
- [x] `Compono.Generators`: `CollectionPlan.scriban` (and its emitter model) updated
      to call `context.ResolveCollectionSize(...)` instead of emitting the literal
      `3`; existing `.verified.cs` collection-plan snapshots regenerated
- [x] Unit tests: a member value rule wins over a type rule for the same effective
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
      a negative `WithCollectionSize(int)` (both global and member-scoped) throws
      `ArgumentOutOfRangeException` immediately at the call site, not at `Build()`
      and not inside a generated collection plan; a `CollectionElement`/
      `DictionaryKey`/`DictionaryValue` request's `DeclaringType` is `null`,
      confirmed not to match any configured member rule (proving the nullable
      field, not a sentinel, correctly represents absence);
      **a member rule matches a positional-record constructor parameter correctly**
      (`Customer(string FirstName, ...)` — the primary documented usage shape,
      exercised through a real generated plan, not a hand-written test double) —
      this is the coverage that actually proves the documented "records always
      match, hand-written classes with divergent parameter/property naming are a
      known limitation" claim (ADR-0020) rather than leaving it asserted but
      unverified; **the factory-reentrance guard's rule-specific coverage,
      deferred here from Phase 1** since `.Member(...).Use(...)` didn't exist yet
      when the guard itself was built: a self-referencing configuration rule
      (`.For<IClock>().Use(context => context.Resolve<IClock>())`) fails with a
      diagnosable `CompositionException`, not a `StackOverflowException`; and,
      equally important, a rule that legitimately *terminates* a self-referencing
      generated-plan graph succeeds rather than being incorrectly rejected — a
      self-referencing `Node` composed through its real generated plan, with a
      `.Member(x => x.Child).Use(_ => new Node(null))` rule terminating it — this
      second case is the regression test proving the guard doesn't share
      ADR-0011's type-keyed active-construction-frame stack and doesn't reject the
      false-positive case review caught against Phase 1's first-draft design
- [x] **Rename `PipelineStage.ProfileRule` to `PipelineStage.ConfigurationRule`**
      (found during review, never previously scheduled) — this design review
      renamed the concept everywhere in the docs/ADRs ("profile rules" →
      "configuration rules," since profiles are a reusable application mechanism
      over stage 4, not its owner), but the shipped Milestone 2 public enum member
      itself was never scheduled for the matching rename. A public API rename
      (`src/Compono/PipelineStage.cs`), plus its one call site
      (`src/Compono/CompositionContext.cs`, where stage 4 attempts are recorded)
      and the existing diagnostic test that references it
      (`test/Compono.Tests/CompositionDiagnosticsTests.cs`) — acceptable as a
      breaking rename pre-1.0/pre-NuGet-publish, per this repo's own stated
      compatibility posture, but real work this phase needs to actually do, not
      assume already covered by the docs-only rename
- [x] `Compono.Generators.Tests`: at least one regenerated snapshot confirming
      `DeclaringType` in an emitted descriptor construction, and at least one
      regenerated collection-plan snapshot confirming the `ResolveCollectionSize`
      call site replacing the literal `3`

### Phase 4 — End-to-end wiring, external verification, and cleanup

By this phase, every conflict-diagnostic path, every message's structured-error
shape, and every feature's own unit/generator-level tests already exist and pass
(Phases 0–3 each test their own diagnostics and integration behavior directly —
none of that is deferred here). This phase is deliberately narrow:

- [x] One combined end-to-end test exercising every prior phase's feature together
      in one graph (registrations + `IServiceProvider` + profiles + type/member
      rules + collection size), matching this plan's Execution Flow section —
      an integration-level confirmation that the phases compose correctly, not a
      re-test of any individual phase's own conflict/diagnostic logic
- [x] Real manual verification (per `tasks/implement.md` step 7 and PLAN-0002 Phase
      2's lesson about skipping this): exercised against the real, already-published
      `Compono 0.1.0-alpha.19` NuGet package (not a fresh local `dotnet pack`, since
      Phase 4 added no new `src/Compono` production code — every Milestone 3 feature
      shipped in alpha.19 already) from the existing `LayeredCraft`-external
      dogfooding project (`ncipollina/lightsaber-skill`, `chore/dogfood-compono`
      branch, bumped from alpha.8), via a new
      `ComponoMilestone3VerificationTests.cs` combining a profile-sourced
      registration, an `IServiceProvider` fallback, a member configuration rule, and
      global/member-scoped collection-size overrides through a real generated plan —
      confirmed passing. See this phase's Notes for the full detail and for a stale
      Milestone-1-era assertion in that same project's pre-existing verification
      test, fixed in the same commit
- [x] **Review Milestone 2's internal test seams** (`Composer.CreateRootForTesting<T>`/
      `CreateManyForTesting<T>`) now that the real public configuration path exists:
      for each, determine whether it's still pulling weight — prefer testing
      through `Composer.Create(builder => ...)` wherever the public API can express
      the same scenario. Retain a seam only where it provides coverage the public
      API genuinely cannot reach (e.g. a raw-seed test that predates and is
      orthogonal to configuration entirely); remove or narrow it otherwise, with the
      reasoning recorded in this plan's Notes section once decided
- [x] `docs/mvp.md`/`docs/architecture.md`/`docs/public-api.md` updated to describe
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
- `src/Compono/CompositionConfigurationError.cs` — done, Phase 0
  (`DuplicateConfigurationOption`) + Phase 1 (`DuplicateRegistration`)
- `src/Compono/ConfigurationOptionSlot.cs` — done, Phase 0 (not originally listed;
  the shared scalar-fail-fast mechanism the Amendment called for)
- `src/Compono/ICompositionProfile.cs` — Phase 2

New, not originally listed (existing Milestone 2 internal-only type, given a real
public entry point in Phase 1):

- `src/Compono/CompositionRegistrations.cs` — pre-existing (Milestone 2, internal
  test seam), reshaped in Phase 1 from a plain `Type → object?` value map to a
  `Type → Func<ICompositionContext, object?>` factory map, so every registration
  (whether from `Register<T>(Func<ICompositionContext, T>)` or the `Register<T>(Func<T>)`
  convenience overload) is invoked identically through `ICompositionContext` — see
  this phase's Notes
- `src/Compono/CollectionSizePolicy.cs` — Phase 3
- One or more files under `src/Compono/` for the `.For<T>()` rule builder(s) —
  final split decided during Phase 3
- One or more files under `src/Compono/Providers/` for the compiled stage-4 rule
  matching — final split decided during Phase 3

Modified:

- `src/Compono/Composer.cs` — done, Phase 0: `Create(Action<CompositionBuilder>)`,
  seed-policy wiring for `Create<T>()`/`CreateMany<T>()`; `CreateRootForTesting`/
  `CreateManyForTesting` untouched (still bypass configuration entirely). Done,
  Phase 1: `Create<T>()`/`CreateMany<T>()` also wired to
  `_configuration.Registrations`/`_configuration.ServiceProvider` — a real gap
  found while implementing this phase, not originally itemized: `CreateMany<T>()`'s
  private `ComposeMany` helper previously bypassed configuration entirely (routed
  every item through `CreateRootForTesting`, ignoring registrations/service
  provider), which `testing.md`'s own recorded `CreateMany<T>()` discovery lesson
  exists to catch. Fixed by threading `CompositionRegistrations`/`IServiceProvider?`
  through `ComposeMany` and constructing each item's `CompositionContext` with them,
  same as `Create<T>()` already does
- `src/Compono/CompositionSeed.cs` — done, Phase 0: doc comment only, now
  referencing `CompositionBuilder.WithSeed`
- `src/Compono/CompositionContext.cs` — done, Phase 1 (descriptor-less
  `Resolve<T>()`, the manual-resolve invocation frame stack, the factory-reentrance
  guard, stage 3's registration-factory invocation and `IServiceProvider` fallback
  sub-step; a new `CompositionContext(CompositionSeed, CompositionRegistrations,
  IServiceProvider?)` constructor), Phase 3 (`ResolveCollectionSize`, and the
  `PipelineStage.ConfigurationRule` rename's one call site)
- `src/Compono/ICompositionContext.cs` — done, Phase 1 (descriptor-less
  `Resolve<T>()`), Phase 3 (new members)
- `src/Compono/CompositionException.cs` — done, Phase 1: a second constructor
  overload preserving an `InnerException`, for the `IServiceProvider` fallback's
  throwing-container case (ADR-0019's "never `throw ex;`" requirement)
- `src/Compono/CompositionRequestDescriptor.cs` — Phase 3 (`DeclaringType` field)
- `src/Compono/CompositionRequest.cs` — Phase 3 (`DeclaringType` field)
- `src/Compono/CompositionRequestKind.cs` — done, Phase 1 (`ManualResolve` case)
- `src/Compono/PathSegment.cs` — done, Phase 1 (`ManualResolve` case); also
  `CompositionPath.cs` (display-string cases) and `RandomSource.cs` (fork-hash
  tag/case) needed the matching additions to stay exhaustive — not itemized by
  name in this plan originally, but the same kind of mechanical addition every
  prior `PathSegment` case already required
- `src/Compono/PipelineStage.cs` — Phase 3, `ProfileRule` → `ConfigurationRule`
  rename (found during review, not originally scoped)
- `test/Compono.Tests/CompositionDiagnosticsTests.cs` — Phase 3, updated for the
  `PipelineStage.ConfigurationRule` rename
- `src/Compono.Generators/Templates/CompositionPlan.scriban` (and its emitter
  model) — Phase 3, `DeclaringType` argument in emitted descriptor construction
- `src/Compono.Generators/Templates/CollectionPlan.scriban` (and its emitter
  model) — Phase 3, `ResolveCollectionSize` call site replacing the literal `3`
- `docs/architecture.md` — done, Phase 0: Immutable Configuration Model section
  updated to describe the builder/configuration split and `WithSeed` as shipped.
  Done, Phase 1: Registrations and Service Injection section, the pipeline-stage-3
  table row, and the Immutable Configuration Model section's scalar-verb list
  updated to describe `Register<T>`/`UseServiceProvider` as shipped; `AddProfile`,
  `.For<T>()`, and `WithCollectionSize` remain marked not yet implemented;
  `docs/mvp.md`/`docs/public-api.md` unchanged (already accurate as intended-shape
  docs). Phase 4 does the final reconciliation pass once every phase ships

## Test Plan

Per `references/testing.md`'s established pattern — xUnit v3, handwritten/explicit
test data (**not** AutoFixture, which `testing.md` explicitly excludes from this
repo's own test suite: Compono is itself an AutoFixture replacement, and dogfooding
AutoFixture here would contradict the product), NSubstitute, and AwesomeAssertions
in `test/Compono.Tests`; Verify-based snapshot tests in `test/Compono.Generators.Tests`
for the Phase 3 generator template changes.
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

### Phase 1 (Done)

- **`CompositionRegistrations` reshaped from a value map to a factory map.** The
  Milestone 2 internal test seam stored `Type → object?` (a raw already-built
  value); real registrations need a `Type → Func<ICompositionContext, object?>`
  factory so a factory can call `context.Resolve<T>()` to compose nested
  dependencies. Reshaped rather than adding a second, parallel lookup — one
  representation, one invocation path, `Register<T>(Func<T>)`'s no-dependency
  overload stored as a trivial `_ => value` wrapper. The one existing test
  exercising the old shape (`CompositionRegistrationTests`) was updated to match,
  not left on a stale API.
- **`Composer.CreateMany<T>()` was silently bypassing configuration entirely** —
  a real gap found while wiring `Create<T>()`/`CreateMany<T>()` to the new
  `Registrations`/`ServiceProvider` fields, not itemized by this plan originally.
  `ComposeMany`'s per-item loop called `CreateRootForTesting<T>`, the internal
  test seam that always uses `CompositionRegistrations.Empty`/no service provider
  — so a composer configured with `Register<T>(...)`/`UseServiceProvider(...)`
  would have honored them from `Create<T>()` but silently ignored them from
  `CreateMany<T>()`. This is exactly the shape of gap `testing.md`'s own recorded
  `CreateMany<T>()` discovery lesson (PLAN-0002) warns about: a new/changed entry
  point riding along on a sibling entry point's already-working path instead of
  being independently wired. Fixed by threading `CompositionRegistrations`/
  `IServiceProvider?` through `ComposeMany`, so each item's `CompositionContext`
  is constructed identically to `Create<T>()`'s.
- **Ambiguous `cref` cleanup, not itemized by this plan originally.** Adding the
  descriptor-less `Resolve<TValue>()` overload, per
  `docs/adr/0019-registrations-and-service-provider-injection.md`, extends
  `ICompositionContext.Resolve<TValue>(in CompositionRequestDescriptor)` with a
  second, descriptor-less `Resolve<TValue>()` overload — a genuine risk for a
  cref pointing at just `Resolve{TValue}` (ambiguous between the two overloads,
  `CS0419`). Every pre-existing `<see cref="...Resolve{TValue}"/>` reference across
  the codebase (`CompositionContext.cs`, `CompositionException.cs`,
  `CompositionPath.cs`, `CompositionRequestDescriptor.cs`, `CompositionResult.cs`,
  `ICompositionContext.cs`, `Nullability.cs`) was disambiguated to name the
  specific overload it meant, at the same time the new overload was added — not
  left to accumulate as build warnings.
- **Reentrance-guard identity uses `ReferenceEquals` over a plain `List<Func<...>>`,
  not a `HashSet` with the default comparer.** A delegate's default `Equals`
  compares target + method, not reference identity — a `HashSet<Func<...>>` with
  no explicit comparer would treat two different registrations/rules that happen
  to share the same captured-state shape as "the same" factory, exactly the false-
  positive collision ADR-0019's corrected design rules out. Mirrors the existing
  `_activeFrames` (`List<Type>`) push/pop-by-index pattern already used for
  ADR-0011's stage-8 recursion stack, just keyed by delegate reference instead of
  `Type`.
- All 118 `Compono.Tests` (up from 83 after Phase 0) pass on both `net10.0`/
  `net11.0`, including 2 new test files (`ComposerRegistrationTests`,
  `CompositionManualResolveTests`) plus small additions to
  `CompositionConfigurationErrorTests`/`CompositionConfigurationExceptionTests`
  for `DuplicateRegistration`. `Compono.Generators`/`Compono.Generators.Tests`
  (146 tests) untouched and unaffected (confirmed via a clean build) — Phase 1
  doesn't touch generator-facing code, so no manual `dotnet pack` verification was
  needed for this phase specifically (Phase 4 still does one for the whole
  milestone).
- `docs/architecture.md` updated in the same change: the pipeline-stage-3 table
  row, the Immutable Configuration Model section's scalar-verb list, and the
  Registrations and Service Injection section's header note all now describe
  `Register<T>`/`UseServiceProvider` as shipped (Phase 1); `AddProfile`, the
  `.For<T>()` rule DSL, and `WithCollectionSize` remain marked not yet implemented.

### Phase 2 (Done)

- **Cycle/provenance stack keyed by `Stack<Type>`, mirroring Phase 0/1's existing
  patterns.** `CompositionBuilder._applyingProfiles` is pushed with
  `profile.GetType()` immediately before `Configure` runs and popped in `finally`
  — the same push-on-entry/pop-in-`finally` shape ADR-0011's active-construction-
  frame stack and Phase 1's factory-reentrance guard already use. `AddProfile<T>()`
  and `AddProfile(instance)` both funnel through one private `ApplyProfile` helper,
  so cycle detection and `Configure` invocation have exactly one code path
  regardless of which overload a caller used.
- **`CurrentSource()` replaces the hardcoded `ConfigurationSource.Direct`** that
  every scalar verb (`WithSeed`, `UseServiceProvider`) and `AddRegistration` used
  in Phases 0/1 — now reads `_applyingProfiles` and returns `Direct` when empty or
  a `ProfileChain` (outermost-first, via `Stack<Type>.Reverse()`) otherwise. This
  is why Phase 0/1's own entries didn't need touching again: every accumulation
  call site already routed through a single `Set`/`AddRegistration` call, so
  swapping the hardcoded source for `CurrentSource()` at each of those three call
  sites was sufficient — no separate "is a profile applying" branch needed
  elsewhere.
- **`CompositionConfigurationError.ProfileCycle`** carries the full cycle
  (`IReadOnlyList<Type> Chain`, at least two entries, the repeated type at both
  ends — e.g. `[ProfileA, ProfileB, ProfileA]`) and is thrown immediately from
  `ApplyProfile` as its own single-error `CompositionConfigurationException`, never
  routed through `Build()`'s aggregated validation pass — a distinct failure path,
  per ADR-0018.
- All 141 `Compono.Tests` (up from 118 after Phase 1) pass on both `net10.0`/
  `net11.0`, including one new test file (`ComposerProfileTests`) plus small
  additions to `CompositionConfigurationErrorTests`/`CompositionConfigurationExceptionTests`
  for `ProfileCycle`. `Compono.Generators`/`Compono.Generators.Tests` (146 tests)
  untouched and unaffected (confirmed via a clean full-solution build/test) — Phase
  2 doesn't touch generator-facing code, so no manual `dotnet pack` verification
  was needed for this phase specifically (Phase 4 still does one for the whole
  milestone).
- `docs/architecture.md`/`docs/public-api.md` updated in the same change: the
  Immutable Configuration Model and Profiles sections in `architecture.md`, and the
  Programmatic Composition section's shipped-verb list in `public-api.md`, now
  describe `AddProfile`/`ICompositionProfile` as shipped (Phase 2); the `.For<T>()`
  rule DSL and `WithCollectionSize` remain marked not yet implemented.

### Phase 3 (Done)

- **`InvokeFactory` generalized to take a `PipelineStage` parameter** — a real gap
  found while wiring rule dispatch through it: it previously hardcoded
  `PipelineStage.ExactRegistration` in its two trace-recording sites, which was
  correct while stage 3 (registrations) was its only caller, but would have
  mis-attributed a configuration-rule factory's reentrance/thrown-exception trace
  entries to `ExactRegistration` instead of `ConfigurationRule` once rules became a
  second caller. Fixed by threading the calling stage through explicitly (stage 3's
  own call site passes `PipelineStage.ExactRegistration`, `TypeRuleProvider`/
  `MemberRuleProvider` pass `PipelineStage.ConfigurationRule`) rather than
  special-casing rules inside `InvokeFactory` itself. `InvokeFactory` was widened
  from `private` to `internal` so `Compono.Providers.TypeRuleProvider`/
  `MemberRuleProvider` (same assembly) can call it directly - the same reentrance
  guard and manual-resolve invocation frame stage 3's registrations already share,
  per ADR-0019/ADR-0020.
- **Value rules compile into `TypeRuleProvider`/`MemberRuleProvider`
  (`src/Compono/Providers/`)**, populated into `CompositionConfiguration.Rules` -
  the same field `CompositionContext` threads into what was `_profileProviders`
  (now `_configurationRuleProviders`, matching the `PipelineStage.ConfigurationRule`
  rename). `Build()` places every member-rule provider ahead of every type-rule
  provider unconditionally (`ICompositionProvider` list order), matching ADR-0020's
  specificity-based (not call-order-based) precedence - achieved by concatenating
  the two compiled sequences in that fixed order, not by a runtime specificity
  comparison.
- **`CompositionConfigurationError.DuplicateRule`** covers three distinct
  conflicts with one shape (`RuleType`, nullable `MemberName`, `Sources`): a
  duplicate type rule (`MemberName: null`), a duplicate member rule, and a
  duplicate member-scoped `WithCollectionSize` override - the last of these reuses
  the identical `(declaring type, member name)`-keyed conflict-detection
  mechanism as member value rules (a `Dictionary<(Type, string), List<ConfigurationSource>>`
  scanned by `Build()`), per this plan's own "same keyed-conflict validation as a
  member value rule" phrasing, even though a collection-size override never
  compiles into a stage-4 provider. `CompositionConfigurationException.DescribeError`
  needed a new `switch` arm for this case (found while writing the duplicate-rule
  regression tests - the exhaustive switch throws `ArgumentOutOfRangeException`
  for any unhandled case, which surfaced immediately as a test failure rather than
  silently producing a wrong message).
- **`ResolveCollectionSizeCore` reads a `_currentDeclaringType` field**, pushed/
  popped in `ResolveCore` alongside `_path`/`_random` (same push-before/pop-in-
  `finally` shape), rather than threading a `CompositionRequest` parameter through
  private state some other way - an implementation choice this ADR's own text
  explicitly left open. The member name half of the lookup key is read from
  `_path!.Segment` (`PathSegment.ConstructorParameter.Name`/`RequiredMember.Name`),
  the same field `MemberRuleProvider` reads for its own matching - one shared
  helper (`MemberNameOfCurrentRequest`/`MemberNameOf`) exists in each type
  independently since the two live in different files with no natural shared base,
  not because the logic is meant to diverge.
- **`CompositionTypeRuleBuilder<T>.Member(...)`'s expression parser rejects a
  nested member access** (`x => x.Email.Length`), not just a non-member expression
  - found while writing the malformed-expression regression test: an initial
  `Body is MemberExpression { Member: PropertyInfo or FieldInfo }` pattern alone
  still matches `x.Email.Length` (its outer `MemberExpression`'s `Member` is
  `string.Length`, a real `PropertyInfo`), silently capturing `DeclaringType =
  typeof(string)`/`MemberName = "Length"` instead of throwing. Fixed by also
  requiring `Expression: ParameterExpression` on the outer `MemberExpression` -
  only a direct `x.Y` access has the lambda's own parameter as its immediate
  target.
- **`Compono.Generators`**: `RequiredMemberInfo` gained a `DeclaringTypeFullyQualifiedName`
  field, populated from `member.ContainingType` in `RequiredMemberCollector` - for
  an inherited required member this is the base type (the same symbol
  `RequiredMemberCollector`'s existing base-to-derived ordinal walk already
  resolves to), never the composed leaf type. `ConstructorParameterInfo` needed no
  matching field - every constructor parameter's declaring type is simply the
  composed type itself, so `CompositionPlanEmitter` reads `type.FullyQualifiedName`
  directly for that case. All 62 existing `Compono.Generators.Tests` `.verified.cs`
  snapshots (`CompositionPlanVerifyTests`/`CollectionPlanVerifyTests`) were
  regenerated in the same change - reviewed for the expected mechanical diff only
  (`typeof(...)` added as the descriptor's fourth argument; collection-plan
  snapshots additionally replace the literal `3` with a `size` local read from
  `context.ResolveCollectionSize()`), confirmed correct for the inherited-member
  case specifically (`RequiredMembersOnBaseAndDerivedType_...`'s snapshot shows
  `typeof(global::TestNamespace.Animal)` for the base-declared members and
  `typeof(global::TestNamespace.Dog)` only for `Dog`'s own).
- **One test - proving a member rule matches a positional record's constructor
  parameter - runs through a real generator-emitted plan**
  (`Compono.Generators.Tests/ConfigurationRuleExecutionTests.cs`, via
  `GeneratorTestHelpers.CompileAndExecute`), not a hand-written test double, per
  this plan's own explicit call-out: every other Phase 3 test (`Compono.Tests`)
  fakes its `ICompositionPlan<T>`/`PlanCache<T>` entry with a hand-constructed
  `CompositionRequestDescriptor`, which proves the matching logic but not that the
  generator actually emits `DeclaringType` correctly for real source - only this
  one test proves both together.
- All 166 `Compono.Tests` (up from 144 after Phase 2) and 74
  `Compono.Generators.Tests` (up from 73) pass on both `net10.0`/`net11.0`,
  including two new test files in `Compono.Tests`
  (`ComposerConfigurationRuleTests`, `ComposerCollectionSizeTests`) plus small
  additions to `CompositionConfigurationErrorTests`/`CompositionConfigurationExceptionTests`
  for `DuplicateRule`, and one new `Compono.Generators.Tests` file
  (`ConfigurationRuleExecutionTests`). No `dotnet pack` verification performed for
  this phase specifically - Phase 4 still does one for the whole milestone, per
  this plan's own Test Plan section.
- `docs/architecture.md`/`docs/public-api.md` updated in the same change: both
  now describe `.For<T>()`, `WithCollectionSize`, and `DeclaringType` as shipped
  (Phase 3) rather than pending: the Immutable Configuration Model, Configuration
  Rules, and Composition Requests sections in `architecture.md`, the pipeline
  stage-4 table row (renamed `ConfigurationRule`), and the Programmatic
  Composition section's shipped-verb list in `public-api.md`.

### PR review correction (2026-07-30): a cycle below a non-cyclic outer profile leaked the outer profile into `Chain`

PR #18 review (Codex) correctly flagged that `ApplyProfile`'s cycle-chain
construction sliced from the bottom of the *entire* `_applyingProfiles` stack,
not from the cyclic profile's own first occurrence. For a cycle nested under a
non-cyclic outer profile (`RootProfile -> ProfileA -> ProfileB -> ProfileA`),
this built `[RootProfile, ProfileA, ProfileB, ProfileA]` — `RootProfile` at the
front, `ProfileA` (the actual repeated type) at the back, violating
`ProfileCycle.Chain`'s own documented "repeated type at both ends" contract and
handing a consumer a prefix that was never part of the cycle.

Fixed by finding `profileType`'s first index in the outermost-first stack and
slicing from there before appending the closing type, so `Chain` now correctly
starts and ends at the same, actually-repeated profile regardless of how many
non-cyclic outer profiles wrap it. One regression test added
(`AddProfile_CycleBelowANonCyclicOuterProfile_ChainExcludesTheOuterProfile`) —
the existing cycle tests all happened to start the cycle at the bottom of the
stack, which is exactly why this shape of bug survived the original PR's own
test suite.

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

### PR review correction (2026-07-29): `CompositionRegistrations` wasn't defensively copying its factory map

PR #17 review (Codex) correctly flagged that `CompositionBuilder.Build()` handed
its own live, mutable `_registrationFactories` dictionary straight into
`CompositionRegistrations`'s constructor, which stored it only as an
`IReadOnlyDictionary<...>`-typed view — not a copy. A consumer capturing the
`CompositionBuilder` out of the configuration callback (e.g. assigning it to an
outer-scope variable) could keep calling `Register<T>` after
`Composer.Create(...)` had already returned, silently mutating the "frozen"
composer's registrations after the fact and racing with concurrent
`Create<T>()`/`CreateMany<T>()` calls — a real violation of
[ADR-0017](../adr/0017-immutable-composer-configuration-and-builder-model.md)'s
frozen-configuration guarantee, and the one place this phase's new collection
handoff missed the `ImmutableSnapshot`-style defensive-copy pattern already used
elsewhere in this codebase (`CompositionConfigurationException.Errors`,
`ConfigurationSource.ProfileChain.Profiles`,
`CompositionConfigurationError.DuplicateRegistration.Sources`).

Fixed by having `CompositionRegistrations`'s constructor defensively copy its
input into a `ReadOnlyDictionary<Type, Func<ICompositionContext, object?>>`
wrapping a freshly-copied `Dictionary` it never exposes — mirroring
`ReadOnlyCollection<T>`'s guarantee for `ImmutableSnapshot`, just for a
dictionary instead of a list. Fixed at the constructor rather than at
`CompositionBuilder.Build()`'s call site specifically, so every current and
future caller of `CompositionRegistrations` gets the guarantee automatically,
not just this one. Two regression tests added:
`CompositionRegistrationTests.Constructor_DefensivelyCopiesTheFactoryMap_...`
(mutating the original dictionary after construction has no effect on
`TryGet`) and
`ComposerRegistrationTests.Register_CalledOnACapturedBuilder_AfterCreateReturns_...`
(the real-world scenario: a captured builder's post-`Create` `Register<T>` call
never becomes observable on the already-created `Composer`).

### PR review correction (2026-07-30): a registration factory's own exceptions weren't authoritative failures, and stage 3 was missing its Pending trace marker

PR #17's second Codex review round (against commit `7a0c205`) correctly flagged
two related gaps in `InvokeFactory`/stage 3's dispatch, both now fixed together
since the fixes touch the same code:

1. **A registration factory that threw directly escaped raw**, with no
   `CompositionException`, no `Diagnostic` (path/seed/trace), and the original
   exception not preserved as `InnerException`. This directly contradicts
   [ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
   already-`Accepted` Failure semantics, which name this exact case explicitly:
   *"`Failure` is reserved for context-owned authoritative stages: an exact
   registration (stage 3) whose factory throws, or generated-plan dispatch..."*
   Fixed by wrapping the `factory(this)` call in `InvokeFactory` with a
   `catch (Exception ex)` that records `Failure` and throws a structured
   `CompositionException` with `ex` preserved as `InnerException` — mirroring
   the `IServiceProvider` fallback sub-step's existing catch shape exactly.
   A `catch (CompositionException) { throw; }` guards against double-wrapping:
   a nested `context.Resolve<T>()` call made *inside* the factory already
   produces a fully-diagnosed `CompositionException` (via the nested
   `ResolveCore` call's own `BuildException`), whose own, more specific
   diagnostic is strictly better than anything the outer catch could construct
   — re-wrapping it would discard that detail behind a generic "the factory
   threw" message.
2. **Stage 3's registration dispatch never recorded a `Pending` trace entry
   before invoking the factory** — unlike collection-plan dispatch and
   generated-plan dispatch (`ResolveViaGeneratedPlan`), both of which record
   `Pending` immediately before calling into code that might recurse and fail,
   specifically so a failing descendant's diagnostic still shows the ancestor
   was genuinely in flight. Without it, a registration factory's nested
   `context.Resolve<T>()` failure produced a diagnostic that omitted the
   `ExactRegistration` ancestor entirely (the nested `BuildException` snapshots
   the trace before control ever returns to stage 3's own recording line).
   Fixed by recording `Pending` for `PipelineStage.ExactRegistration`
   immediately before calling `InvokeFactory`, matching the existing
   two-entries-per-node shape those other two dispatch sites already use.

Three regression tests added: `CompositionRegistrationTests` gained one proving
a directly-thrown factory exception surfaces as `CompositionException` with the
original preserved as `InnerException`, and one proving the resulting
diagnostic's `Trace` contains a `Pending`/`ExactRegistration` entry when a
nested `Resolve<T>()` call inside the factory fails.
`CompositionManualResolveTests` gained one proving a nested `Resolve<T>()`
failure's own `CompositionException` propagates unmodified (no
double-wrapping, no "threw" text in the message) rather than being re-caught
and rewrapped by the outer `InvokeFactory` call. One existing test
(`InvokeFactory_PopsTheFactoryReentranceStack_WhenTheFactoryThrows_...`) was
updated to assert the new, correct behavior — a factory-thrown exception now
surfaces as `CompositionException` with `InnerException` set, not the raw
exception type it asserted before this fix.

### Known limitation (2026-07-30): the generator doesn't recognize a registered/service-provider-satisfied root

PR #17 review (Codex) correctly flagged that `Register<T>`/`UseServiceProvider`
only unblock `Composer.Create<T>()` for a real, analyzer-enabled consumer if
`Compono.Generators` also agrees `T` is a valid root. Today it doesn't:
`TransitiveClosureWalker.EnqueueRoot` sends every non-built-in root through
`ConstructorSelector` unconditionally, which still emits CMP0001/2/3 for an
interface, abstract class, delegate, or constructor-rejected concrete type —
regardless of whether that same `Composer.Create(builder => ...)` call also
registers it or supplies a fallback `IServiceProvider`. A real consumer with
the analyzer enabled cannot currently register their way past a
generator-time diagnostic for such a root.

**Deferred, not fixed in this PR** — closing it correctly means teaching
`Compono.Generators` to recognize `Register<T>()`/`UseServiceProvider(...)`
syntax within the same `Composer.Create(...)` call and skip diagnostic
emission for those roots specifically. That's a real design question (exactly
which syntactic shapes count, whether a profile-added registration is
compile-time-visible at all, whether this needs its own ADR amendment) and
touches `Compono.Generators` — a different project than this phase's stated
scope (core-side registrations/service-provider injection). Tracked here as a
known gap for a future phase/issue to close, not attempted as a same-PR
scope-creep fix. `Compono.Tests` (core-only) doesn't exercise the packaged
generator, so this gap isn't caught by this phase's own test suite — a real
generator/consumer integration test is part of whatever future work closes it.

### PR review correction (2026-07-30): stale shipped-configuration documentation

PR #17 review (Codex) correctly flagged that `docs/public-api.md`'s
Programmatic Composition section still said Milestone 3 was at Phase 0 with
`WithSeed` as the only shipped configuration verb, and `docs/architecture.md`'s
`ICompositionContext` snippet omitted the descriptor-less `Resolve<T>()`
overload added in Phase 1 — both left stale by the Phase 1 doc update above,
which updated `docs/architecture.md`'s prose sections but missed these two
spots. Fixed by updating `public-api.md`'s Programmatic Composition section to
list `Register<T>`/`UseServiceProvider` as shipped alongside `WithSeed`, and
adding the descriptor-less `Resolve<T>()` overload to `architecture.md`'s
`ICompositionContext` snippet.

### PR review correction (2026-07-30): a pipeline-diagnosed exception was retaining its whole CompositionContext

PR #17's fourth Codex review round (against commit `199cbf6`) correctly
flagged that the previous round's fix — comparing `CompositionException` to
its originating `CompositionContext` by reference — stored the actual context
instance on the exception to make that comparison. A `CompositionContext`
reaches its configured `IServiceProvider`, every registration factory's
captured closures, `CompositionScope`'s already-composed shared values, and
the trace buffer — none of which the origin check needs, but all of which a
consumer or test runner retaining a thrown `CompositionException` (common:
assertion history, retry logging) would keep alive for as long as the
exception itself is kept.

Fixed by replacing the stored `CompositionContext` reference with a cheap,
otherwise-empty `object` identity token (`CompositionContext._identity`,
allocated once per instance) — `CompositionException.DiagnosingContextIdentity`
now holds that token instead of the context, and `InvokeFactory`'s catch guard
compares tokens (`ReferenceEquals(ex.DiagnosingContextIdentity, _identity)`)
instead of contexts. Observable behavior is unchanged (same-context
pass-through vs. foreign-context wrap-and-throw), already covered by the
existing `CompositionManualResolveTests` cases from the prior two rounds.
Added a new regression test instead of a GC/`WeakReference`-based one — CLR
retention timing is inherently unreliable to assert deterministically in a
Debug-configuration test run — asserting structurally that no field on
`CompositionException`, including compiler-generated auto-property backing
fields, is typed as or assignable from `CompositionContext`.

### Known limitation (2026-07-30): a stale, manually-recaptured `CompositionException` can pass through a sibling factory unwrapped

PR #17's fifth Codex review round (against commit `f46fac9`) correctly
identified a narrower version of the same-context-vs-different-context
question `96f9e21`/`f46fac9` already closed: `InvokeFactory`'s pass-through
guard keys on `DiagnosingContextIdentity`, which is one token per
`CompositionContext` — i.e. one token for the *entire* root composition
operation, not one per invocation. If a registration factory catches a
failed `ctx.Resolve<T>()`'s `CompositionException`, retains it (e.g. in a
field or captured variable) instead of letting it propagate, and a
*different*, unrelated sibling registration factory later throws that same
retained instance, the guard still matches (same context, same token) and
lets it escape unwrapped — with the earlier failure's stale path/trace, not
the sibling factory's own.

**Deferred, not fixed in this PR.** Two reasons: first, triggering this
requires the factory's own code to catch a `CompositionException` and
manually rethrow a captured, no-longer-current instance later — itself a
violation of `coding-standards.md`'s own "always bare `throw;`, never
`throw ex;`" rule applied to a stored exception, i.e. the triggering pattern
is already caller-side misuse, not an ordinary usage this library needs to
make safe. Second, a correct fix needs per-invocation-frame identity (was
this exception diagnosed underneath *this specific* `factory(this)` call
that's currently unwinding, not merely somewhere in the same operation) —
real added complexity in a piece of dispatch logic that's already been
narrowed five review rounds running (`09e4eb3`, `78cb824`, `0d1792b`,
`96f9e21`, `f46fac9`). Tracked here as a known gap for a future
pass to close if a real (non-misuse-triggered) case ever surfaces, rather
than adding another layer of identity-tracking machinery speculatively.

### Performance correction (2026-07-30): a closure allocated per registration-factory invocation

PR #17's fifth Codex review round also flagged that `InvokeFactory`'s
reentrance check, `_activeFactories.Exists(active => ReferenceEquals(active,
factory))`, allocates a new closure/predicate delegate capturing `factory` on
every single registration/configuration-rule factory invocation — real
allocation on the composition hot path every composed test runs through,
contradicting ADR-0007's allocation-conscious hot-path goal. Fixed by
replacing it with `IsFactoryActive`, a plain indexed loop over
`_activeFactories` comparing by `ReferenceEquals` directly — identical
semantics (reference identity, never the delegate's own `Equals`), zero
allocation. No behavior change, so no new test beyond the existing
reentrance-guard coverage.

### PR review correction (2026-07-30): a rule factory's own Failure trace entry lost its provider identity

PR #19 review (Codex) correctly flagged that `InvokeFactory`'s two Failure-recording
sites hardcoded `provider: null`, which was correct while stage 3 (exact
registrations, which have no `ICompositionProvider` identity of their own) was its
only caller — but became a real gap once `TypeRuleProvider`/`MemberRuleProvider`
started calling it too: a rule factory that throws, or that trips the
factory-reentrance guard, produced a `ConfigurationRule` `Failure` trace entry with
no provider identity, even though `TryProviders`' own `Pending`/`NotHandled` entries
for that exact same candidate already carry its concrete type
(`ProviderAttempt.Provider`). The result looked like a context-owned failure rather
than a specific rule's, and was strictly less informative than the `Pending` entry
sitting right next to it in the same trace.

Fixed by threading a `Type? provider` parameter through `InvokeFactory` (now four
parameters — still within `coding-standards.md`'s limit): stage 3's own call site in
`CompositionContext.ResolveCore` passes `provider: null` (unchanged), while
`TypeRuleProvider`/`MemberRuleProvider` now pass their own `GetType()`. One
regression test added
(`ComposerConfigurationRuleTests.SelfReferencingTypeRule_DiagnosticTrace_AttributesTheFailureToTypeRuleProvider`),
asserting the diagnostic's `Trace` contains a `ConfigurationRule`/`Failure` entry
tagged `typeof(TypeRuleProvider)` for a self-referencing type-rule factory tripping
the reentrance guard.

**A pre-existing, unrelated flaky test surfaced while re-running the full suite for
this fix**: `ComposerCollectionSizeTests` (added earlier in this same phase) and the
already-existing `CollectionPlanCacheDispatchTests` both drove
`CollectionPlanCache<List<int>>.Instance` — a static, type-keyed slot — and xUnit
runs different test classes in parallel by default, so the two classes raced on the
same slot whenever both happened to be mid-test at once (reproduced non-deterministically,
roughly one run in three). Fixed by changing `ComposerCollectionSizeTests`'
probe element type from `List<int>` to `List<long>`, giving it its own,
non-colliding `CollectionPlanCache<T>` slot — the same "give each entry point/test
its own type" isolation rule `testing.md` already states for a new public entry
point, applied here to a shared static test seam instead. Confirmed stable across
five consecutive `net10.0` runs after the fix (previously reproduced on the first
or second run almost every time).

### PR review correction (2026-07-30): a duplicate collection-size override was misreported as a duplicate rule

PR #19's second Codex review round (against commit `85108dd`) correctly flagged
that `CompositionBuilder.Build()`'s duplicate-member-collection-size-override check
reused `CompositionConfigurationError.DuplicateRule` — the same case a duplicate
member *value* rule (`.Member(...).Use(...)`) produces — so a duplicate
`.Member(...).WithCollectionSize(...)` call rendered as "A member rule for
'Customer.PastOrders' was configured more than once," even though no value rule was
ever configured. `DuplicateRule` carries no discriminator between the two, so a
consumer catching `CompositionConfigurationException` had no reliable way to tell
which kind of duplicate they actually had, beyond re-deriving it from the message
string this type's own contract says never to parse.

Fixed by adding a new case, `CompositionConfigurationError.DuplicateCollectionSizeOverride`
(`DeclaringType`, non-nullable `MemberName`, `Sources`) — matching the discriminated-
union discipline this codebase already uses (`PathSegment`/`CompositionResult`: one
case per real kind, not a flag bolted onto an existing one), and consistent with
ADR-0020's own framing that a collection-size override is never compiled into a
stage-4 rule, so reporting it as "a rule" was a category error, not just an
imprecise word choice. `DuplicateRule` itself is now scoped exclusively to type/
member *value* rules — its doc comment and constructor parameter docs were both
corrected to drop the collection-size mention they previously (incorrectly) carried.
`CompositionBuilder.Build()`'s one call site for the member-collection-size conflict
now constructs the new case instead. Four regression tests added: two on
`CompositionConfigurationErrorTests` mirroring `DuplicateRule`'s own coverage
(mutation immunity, fewer-than-two-sources throws), one on
`CompositionConfigurationExceptionTests` asserting the rendered message says
"collection size," not "rule," and one true end-to-end test on
`ComposerCollectionSizeTests` calling `.WithCollectionSize(...)` twice for the same
member through the real `Composer.Create(...)` path and asserting the thrown
exception's structured error is the new case, not `DuplicateRule`. All 494
`Compono.Tests`/`Compono.Generators.Tests` (up from 482) pass on both `net10.0`/
`net11.0`, confirmed stable across three consecutive full-solution runs.

### PR review correction (2026-07-30): a member rule could match a differently-typed member sharing the same name

PR #19's third Codex review round (against commit `c980307`) correctly flagged that
`MemberRuleProvider` matched purely on `(DeclaringType, member name)`, which isn't
actually a unique key: a hand-written class can legally declare a property and a
constructor parameter that share the exact same case-sensitive name but not a type
(e.g. `object Value { get; }` alongside `C(string Value)` - two entirely separate
CLR symbols, not an override or a naming convention). `.Member(x => x.Value)`
captures the *property* (`TMember = object`), but the generator's descriptor for the
constructor parameter carries `RequestedType = string` with the identical
`DeclaringType`/name - `MemberRuleProvider` couldn't tell the two apart, wrongly
claimed the parameter's request too, and handed back an `object`-typed value that
`CastResult<TValue>` then failed to cast to `string` with a raw, undiagnosed
`InvalidCastException` - not the structured `CompositionException` every other
failure in this pipeline produces, and a direct violation of `AGENTS.md`'s "a useful
compile-time diagnostic is better than a clever runtime fallback" principle.

Fixed by capturing the member's actual CLR type (`typeof(TMember)`, available where
`.Member<TMember>(...)` is parsed) and requiring **exact** equality with
`request.RequestedType` in `MemberRuleProvider.TryCompose`, alongside the existing
`DeclaringType`/name check - exact matching, not assignability, mirroring
`TypeRuleProvider`'s own already-established "exact type only" rule from ADR-0020,
so the fix introduces no new matching philosophy. A mismatched request now cleanly
declines (`NotHandled`) instead of wrongly claiming the request - exactly the
graceful-decline behavior `ICompositionProvider`'s own contract already calls for,
letting a later stage (here, a built-in provider) satisfy it instead.

Threaded through as data, not a new mechanism: `CompositionBuilder._memberRuleFactories`
changed from `Dictionary<(Type, string), Func<...>>` to
`Dictionary<(Type, string), (Type MemberType, Func<...> Factory)>`, and
`AddMemberRule` gained a `Type memberType` parameter that
`CompositionMemberRuleBuilder<TParent, TMember>`'s two `Use(...)` overloads now
supply as `typeof(TMember)`. Duplicate-rule conflict detection needed no change -
still correctly keyed by `(DeclaringType, MemberName)` alone, since two `.Member(x
=> x.Y)` calls for the same real property always agree on `TMember`. Member-scoped
`WithCollectionSize` needed no equivalent fix - it returns a plain `int`, never a
cast value, so this specific risk doesn't apply there.

One regression test added
(`ComposerConfigurationRuleTests.MemberRule_DoesNotMatch_WhenARequestForADifferentlyTypedMemberSharesTheSameName`),
reproducing the exact reported shape (a `Conflicted` class with a `string`
constructor parameter and an `object` property both named `Value`) through the real
`Composer.Create(...)` path and asserting composition succeeds (falls through to a
built-in provider for the mismatched parameter) rather than throwing. All 496
`Compono.Tests`/`Compono.Generators.Tests` (up from 494) pass on both `net10.0`/
`net11.0`, confirmed stable across three consecutive full-solution runs.

### PR review correction (2026-07-30): a member-scoped collection-size override had the same name-collision gap as the value-rule fix

PR #19's fourth Codex review round (against commit `f1ea4ad`) correctly flagged
that the requested-type fix just applied to `MemberRuleProvider` had a sibling gap:
`CollectionSizePolicy.TryGetMemberOverride` still matched purely on `(DeclaringType,
MemberName)`. A class with, say, an `object Value` property and an unrelated
`List<long> Value` constructor parameter - the same colliding shape the value-rule
fix addressed - would have `.Member(x => x.Value).WithCollectionSize(9)` (binding to
the property) silently apply its override to the unrelated `List<long>` parameter's
collection size too. Unlike the value-rule bug this doesn't crash (both sides are
plain `int`-shaped data), but it silently produces a wrongly-sized collection
instead of the size the caller actually asked for on the member they actually meant.

Fixed the same way, on principle (AGENTS.md: follow the existing convention for a
given concern rather than inventing a second one) as well as mechanism: capture
`typeof(TMember)` where `.Member<TMember>(...).WithCollectionSize(...)` is called,
store it alongside the size in `CollectionSizePolicy`'s per-member table (now
`(Type MemberType, int Size)` instead of a bare `int`), and require it to equal the
currently-resolving request's own requested type before applying the override.
That requested type needed no new field - `CompositionContext` already tracks the
current node's `_path`, and `CompositionPath.RequestedType` already holds exactly
this value; `ResolveCollectionSizeCore` now reads `_path!.RequestedType` and passes
it into `TryGetMemberOverride`. `CompositionBuilder.AddMemberCollectionSize` gained
a `Type memberType` parameter, threaded from `CompositionMemberRuleBuilder<TParent,
TMember>.WithCollectionSize`'s already-known `TMember`. One regression test added
(`ComposerCollectionSizeTests.MemberScopedOverride_DoesNotApply_WhenARequestForADifferentlyTypedMemberSharesTheSameName`),
asserting the mismatched `List<long>` parameter falls through to the global default
size rather than silently inheriting the unrelated property's override - a
behavioral assertion (element count), not just "doesn't throw," since this gap's
failure mode is silent wrong data, not a crash. All 498
`Compono.Tests`/`Compono.Generators.Tests` (up from 496) pass on both `net10.0`/
`net11.0`, confirmed stable across three consecutive full-solution runs.

### PR review correction (2026-07-30): two more findings from Codex's fifth review round, a stale diagnostic string and a checklist claim that outran its own test coverage

PR #19's fifth Codex review round (against commit `4351ebf`) flagged two unrelated,
independent gaps:

1. **The stage-9 terminal failure message still said "profile rule."**
   `CompositionContext.ResolveCore`'s final `BuildException` call - reached only
   when every stage declines - lists every stage by name in its message; that list
   was never updated when `PipelineStage.ProfileRule` was renamed to
   `ConfigurationRule` earlier in this same plan (Phase 3's own checklist item).
   Anyone whose direct `.For<T>()` rule (not reached via any profile at all) failed
   to satisfy a request would have seen an obsolete, misleading term in the one
   message meant to tell them what was tried. Fixed by updating the string to say
   "configuration rule." One regression test added
   (`CompositionContextTests.ResolveRoot_TerminalFailureMessage_NamesConfigurationRule_NotTheStaleProfileRuleTerm`),
   asserting both that the new term appears and that the stale one doesn't - the
   kind of two-sided assertion that would have caught this rename gap immediately
   had it existed before the rename.
2. **This plan's own Phase 3 checklist claimed something its test suite didn't
   actually do.** The checklist item for `WithCollectionSize` says global and
   member-scoped overrides were verified "end-to-end through a real generated
   collection plan" - but `Compono.Tests/ComposerCollectionSizeTests` only ever
   exercised a hand-written `SizeProbeListPlan` registered directly into
   `CollectionPlanCache<T>.Instance`, the same fast-unit-test seam every other
   `Compono.Tests` collection test uses, never the real source generator. Only the
   member-rule positional-record test (`ConfigurationRuleExecutionTests`, added
   for a prior review round) actually exercised `Compono.Generators.Tests`' real
   `GeneratorTestHelpers.CompileAndExecute` path. This checklist claim was simply
   inaccurate at the point it was checked off, not a design gap - the fake-plan
   tests correctly proved `ResolveCollectionSizeCore`'s own logic, but never
   confirmed the generator actually emits `context.ResolveCollectionSize()` in real
   generated code (as opposed to, say, silently still emitting the pre-Phase-3
   literal `3`). Fixed by adding two real tests to
   `Compono.Generators.Tests/ConfigurationRuleExecutionTests.cs`:
   `GlobalWithCollectionSize_ChangesTheCollectionLength_ThroughARealGeneratedCollectionPlan`
   (a root-level `List<int>` composed with a global `WithCollectionSize(7)`,
   asserting a real 7-element list) and
   `MemberScopedWithCollectionSize_OverridesTheGlobalDefault_ForThatMemberOnly_ThroughARealGeneratedPlan`
   (a two-member record, member-scoped override on one member only, asserting the
   overridden member and its sibling end up with different, correct counts) - both
   compiled and executed as real, separate assemblies via `CompileAndExecute`, not
   hand-faked plans.

All 504 `Compono.Tests`/`Compono.Generators.Tests` (up from 498) pass on both
`net10.0`/`net11.0`, confirmed stable across three consecutive full-solution runs.

### PR review correction (2026-07-30): a sixth-round checklist audit found the flagged gap plus a second, unflagged instance of the same pattern

PR #19's sixth Codex review round (a review body, not an inline thread, against
commit `a12732f`) flagged that this plan's own checklist claims a
`CollectionElement`/`DictionaryKey`/`DictionaryValue` request's null `DeclaringType`
was "confirmed not to match any configured member rule," but no test anywhere in
the new configuration-rule/collection-size test files actually resolved one of
those three request kinds in the presence of a configured member rule - only the
generator snapshots proved `DeclaringType` is emitted as `null` for those kinds,
never the runtime consequence.

Given two consecutive rounds had now caught this exact class of gap (a checklist
line claiming test coverage the actual suite didn't have), a full audit of every
distinct claim in Phase 3's "Unit tests" checklist line was done against the real
test files rather than fixing only the flagged item. That audit found one more,
previously unflagged instance of the identical pattern: the checklist's own text
says the `Node.Child` self-referencing-termination case was "composed through its
real generated plan," but the actual regression test
(`Compono.Tests/ComposerConfigurationRuleTests.RuleThatLegitimatelyTerminatesASelfReferencingGraph_Succeeds`)
registers a hand-written `RecursingNodePlan` directly into `PlanCache<Node>.Instance`
- never the real source generator. Every other distinct claim in that checklist
line was cross-checked against the real test files and found accurate.

Both gaps fixed:

1. **`CollectionElement`/`DictionaryKey`/`DictionaryValue` non-match**: two tests
   added to `Compono.Tests/ComposerConfigurationRuleTests.cs`
   (`MemberRule_DoesNotMatch_ACollectionElementRequest_WhoseDeclaringTypeIsAlwaysNull`,
   `MemberRule_DoesNotMatch_ADictionaryKeyOrValueRequest_WhoseDeclaringTypeIsAlwaysNull`),
   each configuring a real member rule (`.For<Customer>().Member(x => x.Email).Use("from-rule")`)
   then resolving the relevant request kind through a hand-faked
   `CollectionPlanCache<T>` plan (matching this file's own existing fast-unit-test
   convention - this claim never claimed "real generated plan," only "confirmed not
   to match," so a fake plan is the right-weight test here, unlike gap 2) and
   asserting the rule's value never appears in the result.
2. **`Node.Child` real-generator coverage**: a new test added to
   `Compono.Generators.Tests/ConfigurationRuleExecutionTests.cs`
   (`RuleThatLegitimatelyTerminatesASelfReferencingGraph_Succeeds_ThroughARealGeneratedPlan`),
   compiling a real, self-referencing `Node` class and a real
   `.For<Node>().Member(x => x.Child).Use(...)` rule through the actual generator via
   `GeneratorTestHelpers.CompileAndExecute`. Writing this test surfaced a real,
   useful discovery in the test fixture itself (not a product bug): the first draft
   used a lowercase constructor parameter (`child`) against a `PascalCase` property
   (`Child`) - exactly the casing-mismatch limitation ADR-0020 already documents for
   hand-written classes (the generator emits the parameter's own name, never
   correlated to the property), which made the rule silently never match in real
   generated code and let the pre-existing recursion guard fire instead of the
   rule's terminating value. Fixed by renaming the constructor parameter to match
   the property's exact casing (`public Node(Node? Child) => _child = Child;`),
   not by changing any product code - the guard and the rule matching were both
   already correct; the test fixture was the one violating ADR-0020's own
   documented naming requirement.

All 510 `Compono.Tests`/`Compono.Generators.Tests` (up from 504) pass on both
`net10.0`/`net11.0`, confirmed stable across three consecutive full-solution runs.

### Phase 4 (Done)

- **Combined end-to-end test.** `Compono.Tests/ComposerEndToEndConfigurationTests.cs`
  (new file) composes a hand-plan-backed `Order` graph through
  `Composer.Create(builder => ...)` combining `WithSeed`, `WithCollectionSize` (global
  and member-scoped), `UseServiceProvider`, `AddProfile<ClockProfile>()` (a
  profile-sourced `Register<IClock>`), and `.For<Order>().Member(x =>
  x.Status).Use(...)` in one graph, asserting every feature's effect together and
  that a second `Create<Order>()` call reuses the frozen configuration correctly —
  matching this plan's Execution Flow section. Uses the same hand-written
  `ICompositionPlan<T>`/`CollectionPlanCache<T>` fake convention every other
  `Compono.Tests` file uses (per `testing.md`, `Compono.Tests` never references the
  real generator).
- **Test-seam review: `CreateRootForTesting`/`CreateManyForTesting` retained
  unchanged.** Audited every current call site (`ComposerConfigurationTests`,
  `ComposerCreateManyTests`, `CompositionEndToEndTests`,
  `CompositionRandomIntegrationTests`, and the `Providers/*Tests` files) — every one
  exercises pure seed/randomness determinism or built-in provider behavior that
  predates and is orthogonal to configuration entirely (Milestone 2 scope), plus one
  deliberate parity test (`ComposerConfigurationTests`, Phase 0) that specifically
  needs both the public path and the raw seam side by side to prove they agree. None
  of the current usage overlaps with what a `Register`/`AddProfile`/`.For<T>()`/
  `WithCollectionSize` test needs to prove, so nothing qualified for removal or
  narrowing under this plan's own "retain only where the public API genuinely can't
  reach it" bar. No source or test changes made for this task; this note is the
  recorded reasoning it called for.
- **Manual verification used the real published package, not a fresh local
  `dotnet pack`.** Phase 4 itself adds no new `src/Compono`/`src/Compono.Generators`
  production code (only tests, this plan, and doc reconciliation) — every Milestone 3
  feature this phase needed to verify already shipped in the already-published
  `Compono 0.1.0-alpha.19` NuGet package, so re-packing locally would have verified
  nothing a fresh pack didn't already cover identically. Verified instead against the
  existing external dogfooding project this repo already uses for this exact purpose
  (`ncipollina/lightsaber-skill`, Milestone 1 Phase 5 precedent), on its
  already-prepared `chore/dogfood-compono` branch: bumped `Directory.Packages.props`'
  `Compono` version from `0.1.0-alpha.8` to `0.1.0-alpha.19`, added
  `test/Lightsaber.Skill.Tests/Compono/ComponoMilestone3VerificationTests.cs`
  combining a profile-sourced `Register<Crystal>`, an `IServiceProvider` fallback for
  an unregistered `Logger`, a `.For<Lightsaber>().Member(x =>
  x.Color).Use("Violet")` member rule, and both a global and a member-scoped
  `WithCollectionSize` override, composed through `Lightsaber`'s real
  source-generated plan (not a hand-written fake, unlike `Compono.Tests`) — all
  assertions passed. Restoring alpha.19 and building also surfaced that the
  project's pre-existing `ComponoVerificationTests.Create_NestedType_...` test had
  gone stale: it asserted the Milestone-1-era placeholder behavior
  (`NotSupportedException("*Milestone 2*")`) for resolving a nested constructor
  argument, which Milestone 2's real provider pipeline (shipped since alpha.8) no
  longer produces — the nested `Crystal` now composes successfully. Fixed in the same
  commit (asserts the nested value composes, instead of asserting the old
  placeholder throw). Both changes committed to `chore/dogfood-compono` (not pushed,
  per that repo's own owner's direction) — this repo's own git history is
  unaffected, since `lightsaber-skill` is a separate repository.
- No `docs/mvp.md`/`docs/architecture.md`/`docs/public-api.md` changes were needed
  for this phase's own reconciliation pass — every phase from Phase 0 through Phase 3
  already updated its own relevant doc section in the same change it shipped in (see
  each phase's own Notes above), so by Phase 4 no doc described Milestone 3 behavior
  as "not yet implemented" or otherwise stale. Audited `grep`-style across all three
  docs to confirm before treating this task as done, rather than assuming the prior
  phases' discipline held.
- `Compono.Tests` now has 179 tests (up from 166 recorded at Phase 3's own close-out
  — the difference is the several small regression tests PR #19's later review
  rounds added after that note was written, plus this phase's one new
  `ComposerEndToEndConfigurationTests` file/test), `Compono.Generators.Tests` has 77
  (unchanged — Phase 4 added no generator-facing code). Counted across both
  `net10.0`/`net11.0` runs, matching this plan's existing per-phase counting
  convention: 512 total (up from 510), all passing on both target frameworks.
