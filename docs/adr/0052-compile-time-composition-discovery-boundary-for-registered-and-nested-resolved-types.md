# [ADR-0052] Compile-Time Composition-Discovery Boundary for Registered and Nested-Resolved Types

**Status:** Partially Accepted (2026-08-25) — **Part B (explicit constructor
selection) is `Accepted` and shipped** (`CompositionTypeRuleBuilder<T>.UseConstructor<...>()`,
`CMP0033`/`CMP0034`, ADR-0002 Amendment 3). **Part A (registration-aware
discovery) remains `Proposed`/deferred** — not solved by this decision; the
real `HttpClient` evidence (RESEARCH-0010 §10) stays open, tracked
separately. **Finding B** (nested `context.Resolve<T>()` discovery) also
remains `Proposed`/untouched. This single ADR intentionally carries mixed
status across its two named capabilities rather than a clean split, per
explicit product direction — a future formal split into a dedicated Part-B
ADR number is a legitimate follow-up if this hybrid status proves confusing
in practice, not done here to avoid breaking this document's own extensive,
already-recorded evidence trail and cross-references under time pressure.

**Date:** 2026-08-24 (design dive: 2026-08-25; Part B accepted: 2026-08-25)

**Decision Makers:** solo

## Product decision (2026-08-25): explicit constructor selection is required pre-1.0

This ADR's original text (2026-08-24) recorded Findings A and B as roadmap
candidates and deliberately deferred the design work to a future dive,
per ADR-0029's own restraint for that classification. That deferral is now
lifted for Finding A specifically: **the product requirement is that
Compono must provide a practical way to compose a concrete type with
multiple accessible constructors, before 1.0.** The question this ADR
answers is no longer *whether* explicit constructor selection should
exist — it's *what the mechanism is*. "Design space investigated" below is
the dive itself.

**Correction (2026-08-25, same day): this ADR's first design pass
conflated two related but distinct capabilities, caught before being
accepted.** They are kept separate throughout the rest of this document:

- **(A) Registration-aware discovery** — the walker recognizing that a
  statically-visible `Register<T>(...)` supplies `T` externally, and
  therefore not attempting structural constructor discovery for `T` on
  that composition path at all. `Register<T>`'s factory is the consumer's
  own code; Compono contributes nothing to *how* `T` gets built, only to
  what gets passed into the factory if it asks (via `context.Resolve<...>()`
  calls the consumer writes themselves).
- **(B) Explicit constructor selection** — Compono is still expected to
  *compose* `T` (resolve each of the selected constructor's parameters
  itself, exactly as it already does for an unambiguous single-constructor
  type today) — the consumer's only contribution is identifying *which*
  constructor to use. Manually writing `new Foo(context.Resolve<IBar>(),
  context.Resolve<IBaz>())` is (A)'s shape, not (B)'s — it's the consumer
  doing Compono's normal parameter-composition work by hand, which is
  exactly the workaround cost Finding A's evidence documents as a real,
  material loss (RESEARCH-0010 §10's `CreateClient(...)` hand-construction
  workaround), not a solution to it.

**(A) likely closes the AlexaVoxCraft `HttpClient` evidence specifically**
(that consumer already has a working `Register<HttpClient>(...)` — it
supplies the whole object, and that's a fine answer for `HttpClient`
specifically, since nothing about `HttpClient`'s own constructor
parameters needs Compono's composition). **(B) is the actual pre-1.0
product requirement** — a first-class way to say "compose `Foo` normally,
but through *this* constructor" for a type where hand-writing the whole
factory is not an acceptable answer (a type with many constructor
parameters, or one the consumer wants Compono to keep composing
transitively as that constructor's own shape evolves). (B) is required
regardless of whether (A) ships — they are independently useful, and
(A) alone does not satisfy the product requirement.

Two binding requirements shape every option considered:

- `[CompositionConstructor]` (or any Compono-specific attribute) on the
  *target production type* is **not an acceptable primary solution** — a
  test-composition library must not require production code to carry
  test-only annotations. This was already a stated constraint (see
  "Constraints already established" below); it's restated here because
  ADR-0002's own original text treated an attribute on the type as the
  presumed future disambiguation mechanism, and that presumption is now
  explicitly rejected, not merely deprioritized.
- This is **not** license to fall back to a guessing heuristic (greediest
  constructor, longest, first-resolvable). ADR-0002's Decision Outcome
  rejected that on principle (`docs/manifesto.md`'s "predictability over
  magic"), and nothing in the evidence gathered since — more dogfooding,
  not a change of principle — undoes that reasoning. The design space
  below is a genuine third category: **consumer-controlled, source-generated
  construction policy**, evaluated on its own merits, not "guessing
  reconsidered."

Finding B (nested `context.Resolve<T>()` inside a registration factory)
remains a roadmap candidate whose relationship to Finding A this dive
still has to determine — see "Do Findings A and B need one mechanism or
two?" below. This ADR is not pre-committing to solving both with the same
change.

## Context

Per [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
Gap decision rubric, applied below, `alexa-vox-craft`'s real migration
(PLAN-0051 Task 10, [RESEARCH-0010](../research/0010-alexa-vox-craft-compono-ecosystem-migration.md)
§10-11) surfaced two findings that both trace to the same underlying
question, without yet proving they need the same answer: **what should
`Compono.Generators`' compile-time `TransitiveClosureWalker` do when it
reaches a type it cannot itself compose, but that a registration factory
(`Register<T>(...)`) or a nested `context.Resolve<T>()` call could resolve
at runtime?** Today the walker only knows two things about a type it
reaches structurally: compose it (walk its constructor), or treat it as a
provider-resolved leaf (`LeafTypeClassifier.IsProviderResolved`, a fixed
list of BCL primitives/value types). It never consults the registration
table, and it never treats an arbitrary lambda body (a registration
factory's own code) as a discovery root.

**Finding A** ([RESEARCH-0010 §10](../research/0010-alexa-vox-craft-compono-ecosystem-migration.md#10-finding-a-ambiguous-constructor-selection-has-no-registert-escape-hatch-when-reached-through-another-composed-types-constructor)):
`AlexaInteractionModelClient(HttpClient client, ILogger<...> logger)`,
composed as a theory parameter, hits `CMP0001` on `HttpClient`'s 3
accessible constructors — a **compile-time** diagnostic — even though the
active profile registers `Register<HttpClient>(context => ...)`. The
walker structurally descends into `HttpClient`'s constructor parameters
before any registration is consulted, because `HttpClient` is not
provider-resolved by `LeafTypeClassifier`'s fixed rules. This is the first
real evidence of [ADR-0002](0002-constructor-selection-algorithm.md)
Amendment 1's `HttpClient` case (previously only synthetic, "a capability
preserved for hypothetical future tests, not a real pre-existing call
site") occurring as a genuine, real, migration-driven blocker, and — new
information Amendment 1 didn't have — occurring specifically as a *nested*
constructor parameter of another composed type, not `HttpClient` composed
at the root.

**Finding B** ([RESEARCH-0010 §11](../research/0010-alexa-vox-craft-compono-ecosystem-migration.md#11-finding-b-a-type-reachable-only-via-nested-contextresolvet-inside-a-registration-factory-may-have-no-generated-plan)):
`SmapiHttpTestProfile`'s registration factory —
`Register<IOptions<T>>(context => Options.Create(context.Resolve<SmapiDeveloperAccessTokenOptions>()))`
— fails at **runtime**, not compile time, with `CompositionException: No
registration, ..., or generated plan could satisfy
'SmapiDeveloperAccessTokenOptions'`. The record type is never reached
through a `[Compose]` root or another discovered type's constructor — the
only place it's mentioned is inside the registration factory's own lambda
body, which the walker doesn't treat as a discovery root at all. No plan
is ever generated for it, so the runtime resolution path (which normally
falls back to a generated plan) has nothing to fall back to.

Both findings were explicitly captured as evidence only, not fixed, in
PLAN-0051, per product direction that Compono.Http not be blocked on a
core-Compono design question. RESEARCH-0010 §11 deliberately declines to
conclude whether they're the same architectural problem — this ADR
records that same open question rather than resolving it prematurely.

## Applying ADR-0029's Gap decision rubric

**Finding A**

1. *Observed frequency*: one real site (`AlexaInteractionModelClient`
   composed as a theory parameter), hit while writing the natural,
   idiomatic version of a real migrated test — not a hypothetical or
   preserved-for-future-tests capability. This is a materially different
   evidence quality than ADR-0002 Amendment 1's `cosmere-tracker` case,
   which explicitly did not clear this bar.
2. *Was it ever intended to work?* No. ADR-0002's `CMP0001` on multiple
   accessible constructors is documented, intended behavior for a type
   reached structurally, and `Register<T>` has never been documented or
   `Accepted` as a compile-time escape hatch for structural discovery —
   `skills/compono/SKILL.md`'s "When not to use Compono" already names
   this exact class of type. Not a bug; proceed.
3. *Workaround cost*: real, shown before/after (RESEARCH-0010 §10) — the
   consumer stops composing the client type at all and hand-constructs it
   from `TestHttpHandler.CreateClient(...)` plus a manually-supplied
   logger, losing Compono's automatic composition of that constructor's
   own parameters. Material, not zero.
4. *Principle alignment*: closing this does not obviously require
   reflection or hidden state — a mechanism that lives in
   registration/profile/test configuration (not on the type itself) is
   consistent with [ADR-0001](0001-source-generation-first.md) and the
   explicit-over-implicit bias. No principle blocks a solution from
   existing; this points toward roadmap candidate rather than intentional
   design difference.

**Classification: Roadmap candidate.**

**Finding B**

1. *Observed frequency*: one real site
   (`SmapiHttpTestProfile.Configure`'s `IOptions<...>` registration),
   also hit writing the natural version of a real migrated registration,
   not synthesized to demonstrate the gap.
2. *Was it ever intended to work?* No `Accepted` ADR or documented
   behavior claims an arbitrary `context.Resolve<T>()` call inside a
   registration factory's lambda body participates in compile-time
   discovery — `docs/concepts/registrations-and-rules.md`'s own worked
   nested-`Resolve` example only ever nests a provider-resolved BCL value
   type (`DateTimeOffset`), never a user-defined composable type, so it
   never actually exercises this path. Not a bug; proceed.
3. *Workaround cost*: real, shown before/after (RESEARCH-0010 §11) — the
   factory body is rewritten to resolve three primitives
   (`context.Resolve<string>()` ×3) and hand-construct the record inline,
   instead of one `context.Resolve<SmapiDeveloperAccessTokenOptions>()`
   call, moving the record's own shape knowledge out of its declaration
   and into every factory that needs it. Material, though narrower in
   blast radius than Finding A (confined to factory bodies, not test
   signatures).
4. *Principle alignment*: a solution that requires the generator to
   inspect arbitrary lambda-body source (rather than a statically
   declared, strongly-typed dependency list) would sit uncomfortably
   against explicit-over-implicit and would be far harder to keep
   deterministic — but RESEARCH-0010 §11 and this ADR's own open design
   questions below note that an explicit-declaration-based alternative
   (rather than magic lambda-body inspection) is plausible. This does not
   rule out a solution existing within the repo's principles; it does
   raise the bar on what shape that solution can take.

**Classification: Roadmap candidate.**

Per ADR-0029's own classification rules, "Roadmap candidate... A new
`Proposed` ADR records the problem only... for a future milestone's
design pass" — that is exactly this ADR's scope, and no further.

## One roadmap entry, two evidence cases, deliberately unmerged

Findings A and B are recorded as **one** roadmap candidate — a single
compile-time composition-discovery boundary question — rather than two,
because both concretely reduce to the same question above ("what does the
walker do at a type it can't itself compose, that something else might
resolve"), and forcing two separate roadmap entries before that's answered
would risk designing two solutions to what turns out to be one problem.

This is **not** a decision that they *are* the same capability. RESEARCH-0010
§11 explicitly declines to conclude that, and this ADR preserves that
uncertainty rather than resolving it in either direction: Finding A is a
**compile-time** `CMP0001` about an ambiguous-constructor type reached
structurally; Finding B is a **runtime** `CompositionException` about a
type reached only from inside a lambda body, with no compile-time signal
at all. A future design pass may conclude they need one mechanism, two
independent mechanisms, or that one is in scope for a nearer milestone and
the other isn't. That determination is explicitly deferred to the design
dive this ADR queues, not decided here.

## Constraints already established (not re-derived here)

Carried forward from RESEARCH-0010 §10 and ADR-0002, binding on the future
design dive unless it produces compelling evidence to amend one of them:

- no Compono-specific constructor-selection attributes on production
  types — a test-composition library must not require production code to
  carry test-only annotations;
- no "greediest constructor" / longest-constructor / first-resolvable-
  constructor guessing (per ADR-0002's Decision Outcome);
- no reflection (per [ADR-0001](0001-source-generation-first.md));
- preserve source-generation, Native AOT, and trimming goals;
- any selection/configuration mechanism belongs in Compono
  composition/profile/test configuration, not on the type being composed;
- if explicit constructor selection turns out to be necessary at all, the
  consumer should be able to identify the intended construction path while
  Compono continues composing that constructor's own parameters normally
  — not fall back to hand-constructing the whole graph merely to
  disambiguate one entry point;
- do not special-case `HttpClient` — it is the evidence, not the
  architecture;
- do not assume a fix for Finding A is a fix for Finding B, or vice versa,
  before the design dive actually determines that.

## Architectural questions this ADR originally queued (2026-08-24)

Recorded verbatim as the starting brief for the design dive below — not
re-derived, but every one of these is now answered by "Design space
investigated" that follows:

1. What does an `alexa-vox-craft`-shaped consumer naturally want to
   express for a registered-but-structurally-ambiguous type, and for a
   registration factory's own nested dependencies?
2. Is Finding A actually a constructor-selection problem, or is the
   deeper problem that compile-time discovery doesn't understand
   registration/profile configuration at all when deciding whether to
   walk a type structurally?
3. Could `Register<T>(...)` itself already provide sufficient compile-time
   evidence that `T` is supplied by configuration and therefore should not
   be structurally constructor-walked? If so, how would the generator
   observe that statically and predictably (registrations are runtime
   `CompositionBuilder` calls today, not currently visible to the
   generator's compile-time walk)?
4. What are the consequences of treating "registered" types as discovery
   graph boundaries — analyzed against, at minimum: registrations present
   in only some profiles; `[Compose]` vs. `[Compose<TProfile>]`; direct
   `Composer.Create<T>()`; generated plans reused across
   configuration/profile shapes; nested object graphs; registration
   precedence; registration factories that themselves have dependencies;
   generic types/registrations; generator visibility across assemblies?
5. "Registered == leaf" must not be assumed sound for every applicable
   composition path merely because it resolves Finding A's specific case
   — it needs to be proven, or explicitly scoped to where it's proven.
6. Does `context.Resolve<T>()` inside a registration factory need to
   participate in compile-time discovery at all, or can a registration
   factory instead be given a statically expressible, strongly-typed
   dependency declaration of its own (avoiding arbitrary lambda-body
   inspection)?
7. Only after 2-6 are answered: is an explicit constructor-selection
   mechanism still necessary, or does an improved `Register<T>` model
   already express the required intent? If still necessary, what is the
   smallest strongly-typed profile/composition-level mechanism that lets a
   consumer select the intended constructor without annotating production
   code and without forcing a full hand-constructed graph?

## Design space investigated (2026-08-25)

### Current generator mechanics — why Finding A and Finding B fail the way they do

Traced directly in `src/Compono.Generators/Discovery/` (not inferred):

- `ConstructorSelector.Select` (`ConstructorSelector.cs`) is purely
  arity-based: 0 accessible constructors → `CMP0002`, exactly 1 → select
  it, `> 1` → `CMP0001` (`AmbiguousConstructor`), unconditionally. There is
  no hook here for a caller-supplied preference — the type it's given, and
  a `location`/`path` for diagnostics, are its entire input.
- `TransitiveClosureWalker.EnqueueMember`/`EnqueueRoot`
  (`TransitiveClosureWalker.cs`) decide whether a type reaches
  `ConstructorSelector` at all via exactly one gate:
  `LeafTypeClassifier.IsProviderResolved`. That method
  (`LeafTypeClassifier.cs`) is a **closed, fixed classification**: not an
  `INamedTypeSymbol`, abstract, enum, delegate, a built-in simple type
  (`bool`/`int`/`string`/...), one of six recognized BCL value types
  (`DateTime`, `DateTimeOffset`, `Guid`, `TimeSpan`, `DateOnly`,
  `TimeOnly`), or `Nullable<T>` over one of those. **It has no parameter
  for "is this type registered" and no access to any registration data at
  all** — `Register<T>(...)` is an ordinary instance method on
  `CompositionBuilder` (`src/Compono/CompositionBuilder.cs`), called at
  *runtime* from inside a profile's `Configure(CompositionBuilder)` method
  body or a test's own `Composer.Create(builder => ...)` lambda. The
  generator's compile-time walk and the runtime `CompositionBuilder` are
  two entirely separate systems today; nothing connects them. This is
  exactly why Finding A fires `CMP0001` even with a matching
  `Register<HttpClient>(...)` in scope — the walker never gets far enough
  to notice the registration exists.
- Finding B's root cause is different in kind, not degree:
  `TransitiveClosureWalker.Walk` only ever starts from an explicit root
  (`Composer.Create<T>()`'s type argument, or a `[Compose]`-attributed
  theory parameter — see `ComposedTypeAnalyzer`/`CreateInvocationDiscovery`,
  not walked in this session). A type mentioned only inside a registration
  factory's own lambda body (`context.Resolve<SmapiDeveloperAccessTokenOptions>()`)
  is never a discovery root and is never reached by the walker at all — no
  plan is generated, so the *runtime* resolution path (which normally
  falls back to a generated plan when no registration matches) has nothing
  to fall back to. This isn't "the walker refuses to compose it," it's
  "the walker never knew the type existed."

### Interaction with `Register<T>` today

`Register<T>` **is** the existing escape hatch for a type the walker would
otherwise refuse or mishandle — but only once the walker has *stopped
trying to compose the type itself* and fallen through to the runtime
resolution chain (registrations → semantic providers → test-double
providers → built-in providers → generated plan). For a type the walker
descends into *structurally* (any concrete, non-abstract, non-leaf class —
`HttpClient` included), `Register<T>` never gets a chance to run: the
`CMP0001` diagnostic is emitted at compile time, before the generated code
that would consult the registration table at runtime is ever reached. So
the accurate claim is not "`Register<T>` doesn't solve constructor
selection" in the abstract — it solves it completely for any type the
walker treats as a leaf (an interface, an abstract class, a delegate) —
but it does **not** solve it for a concrete, structurally-composable type
with more than one constructor, because the walker commits to structural
composition (and fails) before registration is ever consulted. This is
Finding A exactly, and it's a **discovery-boundary** gap, not a
`Register<T>` capability gap.

### Do Findings A and B need one mechanism or two?

Both ultimately trace to the same question this ADR's Context poses: what
should the walker do at a type it can't itself compose but something else
might resolve? But they differ in exactly where they fail (compile-time
vs. runtime) and exactly what "something else" means (a registration
lookup vs. an arbitrary lambda-body expression) — reason enough that this
ADR has deliberately not assumed they're one problem.

Having now traced the actual mechanics above, they resolve to **two
distinct gaps in the same underlying system, plausibly closable by one
piece of shared infrastructure, but not by one identical code change**:

- Finding A needs the walker to **stop** descending into a type it would
  otherwise structurally compose, when something establishes that the type
  is externally supplied.
- Finding B needs the walker to **start** at a root it doesn't currently
  know exists — a type reachable only from inside a registration factory's
  own body.

Both needs are satisfiable by the same underlying capability — teaching
the generator to statically recognize call sites inside a profile's
`Configure` method (see Part A's A4 below, the mechanism Part B's own
discovery also reuses) — used two different ways: as a *stop signal* for
Finding A (`T` is registered → don't structurally walk it) and as an
*additional discovery root* for Finding B (a registration factory body's
own `context.Resolve<T>()` calls become new roots to walk from). That's a
real, evidenced architectural link, not a forced merge — but it's a
**hypothesis a compiler spike must confirm**, not a decision made here:
whether a profile's `Configure` method body is reliably, deterministically
analyzable this way for both shapes (a direct `.Register<T>(...)`/
`.UseConstructor(...)` call, and a nested `context.Resolve<T>()` inside
one of those calls' own lambda argument) is exactly the kind of claim this
repo requires evidence for before committing to. **Recommendation: scope
the first implementation to Part A and Part B only** (the pre-1.0 product
requirement, addressing Finding A in full), design the shared static-scan
mechanism so Finding B's shape isn't foreclosed, and treat "does the same
mechanism actually close Finding B too" as a follow-up spike/finding once
Part A/Part B are validated — not a joint deliverable that blocks the
pre-1.0 requirement on Finding B's harder, less-evidenced shape (one real
site vs. Finding A's now-clearer product mandate).

### Part A: registration-aware discovery — design options

*(Naming note: "Part A"/"Part B" below name this section's two designed
**capabilities** — registration-aware discovery and constructor selection,
respectively. They are not the same axis as "Finding A"/"Finding B" above,
which name the two original **evidence reports** from RESEARCH-0010.
Finding A is closed by Part A below; Finding B is the separate, still-open
nested-`Resolve<T>()` question, untouched by Part B.)*

Closes the case where the consumer wants to supply the *whole object*
(`HttpClient`, constructed however the factory body says) and simply needs
the walker to stop treating that type as something it must structurally
compose itself.

**A1. Deterministic convention (e.g. greediest-satisfiable, primary-constructor-first).**
Rejected as the *sole* mechanism, for either Part A or Part B. ADR-0002's
Decision Outcome already weighed and rejected "greediest constructor" and
"primary-constructor-first, greedy fallback" on principle — no new
evidence changes that reasoning (a heuristic that silently changes its
answer when a constructor overload set changes shape remains exactly the
"clever fallback" the manifesto warns against).

**A2. Existing `Register<T>(Func<ICompositionContext, T>)`, recognized by
the walker.**
```csharp
builder.Register<HttpClient>(context =>
    new HttpClient(context.Resolve<HttpMessageHandler>()));
```
Already a complete, strongly-typed, reflection-free way for a consumer to
supply the whole object — the gap is not that this API is missing, it's
that the walker never treats a *structurally composable* `HttpClient`
parameter as a leaf, so this registration is never consulted (see
"Interaction with `Register<T>` today"). No new public API needed for this
part; what's needed is making the walker recognize the registration as a
compile-time stop signal (A4). **This closes the `HttpClient` evidence
specifically, and any other case where the consumer is content to hand-
write the whole factory** — it does not, by itself, satisfy the
constructor-selection requirement (Part B below), and must not be
presented as though it does.

**A3. Convention + explicit override.** Not adopted as a *default-composes-
without-configuration* convention (A1's rejection applies), but the
underlying shape — most multi-constructor types don't need per-type
configuration, only the ones actually reached ambiguously do — is already
how both Part A and Part B behave: a single-constructor type needs zero
configuration; only a type reached with `> 1` accessible constructor needs
an explicit entry, and only in the profile(s) that actually compose it.

**A4. Registration as the walker's stop signal — the mechanism that
closes Finding A's `Register<T>`-covered cases.** The generator statically
scans each `[Compose<TProfile>]`-referenced profile's `Configure` method
body (and, for `Composer.Create(builder => ...)`, the inline lambda) for
direct `builder.Register<T>(...)` call expressions — an ordinary, bounded
Roslyn syntax/semantic-model query against source already in the same
compilation, not "arbitrary lambda-body inspection" in the sense Finding
B's own write-up worried about (it's looking for one specific,
syntactically recognizable call shape, not evaluating or dataflow-analyzing
arbitrary code). When a profile in scope for a given `[Compose<TProfile>]`
site registers `T`, the walker treats `T` as a leaf for that composition
path — exactly like an interface is today — and stops descending into its
constructor. This directly answers question 3 from the original queued
list: yes, `Register<T>(...)` can provide sufficient compile-time
evidence, observed by reading the profile's own declared source, not by
making `CompositionBuilder` itself compile-time-visible in some new way.

**A5/A6. External types and nested/transitive composition.** Both
requirements are satisfied by construction: the recognition happens inside
`TransitiveClosureWalker.EnqueueMember` (which already runs for every
nested constructor-parameter position, not just roots), and requires no
annotation on the target type or ownership of its source.

### Part B: explicit constructor selection — design options

The actual pre-1.0 requirement: Compono still composes `T`'s selected
constructor's own parameters (recursively, through the same discovery
machinery an unambiguous type already gets); the consumer only identifies
*which* constructor. Evaluated against: discoverability, compile-time
validation, overload discrimination, optional/`params` parameters,
accessibility, generic constructors/types, nested/transitive usage,
profile scoping, `[Compose]`/`[Compose<TProfile>]`/direct
`Composer.Create` interaction, generator visibility, generated-output
shape, AOT/trimming, and whether the option requires the consumer to
manually supply parameter *values* (A's shape) instead of merely selecting
the *path* (B's requirement).

**B1. Strongly-typed constructor lambda (recommended).**
```csharp
builder.For<Foo>()
    .UseConstructor(static (IBar bar, IBaz baz) => new Foo(bar, baz));
```
`For<T>()` returns a small navigator type scoped to `T`; `UseConstructor`
is generic in the lambda's own parameter types
(`UseConstructor<T1, T2>(Func<T1, T2, T> ctor)`, one overload per arity up
to a practical bound — real constructors rarely exceed single digits of
parameters). The lambda is real, ordinary C# — `new Foo(bar, baz)` either
compiles against a real accessible constructor of `Foo` or it doesn't;
**the C# compiler itself is the overload-discrimination and accessibility
validator**, for free, no new Compono diagnostic infrastructure needed for
that part. At runtime, Compono's generated code calls the delegate exactly
once, with each argument produced by the *same* `Resolve<T1>()`/`Resolve<T2>()`
machinery a constructor parameter already gets — no reflection anywhere
(the delegate is invoked directly, never inspected via `MethodInfo`).

Generated-code shape (conceptually, mirroring today's plain-constructor
codegen):
```csharp
// today, single unambiguous constructor:
return new Foo(context.Resolve<IBar>(), context.Resolve<IBaz>());

// with an explicit B1 selection registered for Foo:
return FooConstructorSelection.Instance(context.Resolve<IBar>(), context.Resolve<IBaz>());
```
i.e. the selection delegate *replaces* the plain `new Foo(...)` call the
generator would otherwise emit — everything else about the generated plan
(argument resolution, required-member assignment, nested discovery) is
unchanged. **This is the critical distinction from A2/`Register<T>`**: the
delegate's own declared parameter types (`IBar`, `IBaz`) participate in
`TransitiveClosureWalker`'s discovery exactly like real constructor
parameters do — a further-ambiguous `IBar` still gets its own `CMP0001` (or
its own B1 selection) — whereas `Register<T>`'s factory body is opaque to
the walker by design (Finding B's own root cause). A recommended
compile-time constraint: the lambda body must be a single object-creation
expression (`new Foo(...)`, parameters passed straight through in some
order) — a diagnostic, not silent acceptance, if the body does anything
else (extra logic, conditional construction) — keeping this a *selection*
mechanism, not a general escape hatch that quietly grows into A2's territory.

**B2. Constructor signature/type-list selection.**
```csharp
builder.For<Foo>().UseConstructor<IBar, IBaz>();
```
Compono itself emits the `new Foo(...)` call — the consumer supplies only
the ordered parameter-type list. The generator resolves this by matching
`Foo`'s real constructor symbols against the requested type list at
compile time (the same kind of symbol comparison `ConstructorSelector`
already does, now filtered by "matches this list" instead of "count == 1"
— not reflection, an ordinary Roslyn `IMethodSymbol.Parameters` comparison).
Pros over B1: no lambda body to write or to validate structurally; the
generator, not the consumer, owns the actual `new Foo(...)` call, so there
is no way for the selection to drift from "just pick a constructor" into
arbitrary logic. Cons: less discoverable from IntelliSense than a lambda
(a consumer has to already know `Foo`'s constructor's parameter types and
spell them out positionally, rather than getting them handed to them by
lambda parameter inference against a real overload); expresses "select
this constructor" less naturally than B1 for a constructor with `params`
or optional parameters (the type-list shape has no clean way to represent
"call the `params` overload with exactly these three arguments" the way a
real call expression does); high-arity constructors produce a long,
easy-to-miscount generic argument list.

**B3. Expression-based constructor selection.**
```csharp
builder.For<Foo>().UseConstructor(x => new Foo(x.Bar, x.Baz));
```
**Not recommended.** If spelled as `Expression<Func<...>>`, this either
requires `Expression.Compile()` at runtime (a real reflection-emit path,
directly conflicting with ADR-0001's no-reflection posture and a known
Native AOT hazard) or requires walking the expression tree's `NewExpression`/
`ConstructorInfo` at runtime to extract the constructor (metadata-based
reflection by another name, and itself a trimming/AOT risk without
explicit preservation hints). If instead spelled as a plain delegate whose
*source-level syntax* the generator reads via Roslyn (not the compiled
expression-tree object at runtime), this collapses to exactly B1 with
extra ceremony — there is no benefit to the `Expression<...>` wrapper once
the generator is already reading the lambda from source, only the cost.
Rejected as strictly dominated by B1.

**B4. A dedicated marker/configuration shape distinct from B1/B2.**
Investigated; no shape was found that isn't a re-spelling of B1 or B2 with
different ceremony (e.g. a params-array-of-`Type` overload is B2 without
the type-safety; a source-generator-only attribute-free "magic method
name" convention scoped to the profile type was considered and rejected —
it would require the generator to special-case method *names* inside a
profile body, a much less discoverable and more implicit mechanism than a
literal `.For<T>().UseConstructor(...)` call site, working against
`docs/manifesto.md`'s explicit-over-implicit bias for no real benefit over
B1/B2).

**B5. `Register<T>` for the "consumer supplies the whole object" case.**
Not an alternative to B1/B2 — this is A2, restated to make the boundary
explicit: use `Register<T>` when the consumer wants to control
construction entirely (including calling helper methods, doing
conditional logic, wrapping an existing instance); use B1/B2 when Compono
should keep composing `T`'s parameters normally and the consumer only
wants to pick the entry point. Conflating the two was this ADR's own
first-pass mistake (see the "Correction" note in "Product decision"
above) — the design going forward keeps them as two distinct call shapes
with two distinct discovery treatments, not one capability with two
spellings.

**Recommendation for Part B, revised after the compiler spike below: B2,
not B1.** The first-pass preference for B1 (recorded above, kept for the
record rather than deleted) does not survive contact with real spiking.
B2 achieves the "Compono emits the actual construction, not a stored
delegate" requirement **structurally** — there is no lambda body at all,
so there is nothing to validate, nothing that can drift toward A2/`Register<T>`
territory, and no rejection-diagnostic surface to design or maintain. B1
achieves the *same* end guarantee only through an elaborate compile-time
validation pass (reject constants, nested calls, side effects, block
bodies, unproven positional reordering — all confirmed real, necessary
cases by the spike below) that exists purely to police a syntax shape
that *looks* like a real callable but must never actually be invoked as
one — a meaningfully larger, more failure-prone implementation for no
behavioral gain a consumer would feel. B1 remains fully validated as
technically buildable (every one of Q1-Q10 below has a confirmed answer),
and is recorded as a credible follow-up if B2's weaker discoverability
(parameter names aren't visible at the call site, only types) proves a
real problem once consumers use it — but it is not the initial
recommendation.

### How the generator discovers and validates a Part B selection

- Same static-scan foundation as A4: a `[Compose<TProfile>]` site's
  resolved profile(s) (and a direct `Composer.Create(builder => ...)`
  lambda) are scanned for `.For<T>().UseConstructor(...)` call
  expressions, using the same bounded, syntactically-recognized-call-shape
  approach — not general dataflow analysis.
- `T`'s selected constructor becomes the constructor
  `TransitiveClosureWalker`/`ConstructorSelector` use for `T` on that
  composition path, in place of `ConstructorSelector.Select`'s own
  arity-based rule — see "Exact ADR-0002 amendment required" below for how
  this changes ADR-0002's own stated rule.
- No selection present, `T` reached with `> 1` accessible constructor:
  unchanged `CMP0001`, exactly as today.
- A selection present but its lambda/type-list doesn't match any real
  accessible constructor of `T`: for B1, this is simply a C# compile error
  inside the lambda body (no new Compono diagnostic needed); for B2, a new
  Compono-specific diagnostic is needed (there's no real call expression
  for the C# compiler to reject), reported at the `.UseConstructor<...>()`
  call site.
- Per-composition-path scoping (a selection registered in `ProfileA` but
  not `ProfileB` only applies to `ProfileA`-resolved paths) follows the
  same scoping requirement A4 already established — not re-derived here.

### Exact ADR-0002 amendment required

ADR-0002's Decision Outcome, as originally written, states the rule as:
exactly one accessible constructor → select it; more than one → `CMP0001`,
unconditionally. That is **no longer wholly accurate** once Part B ships —
claiming otherwise would misstate this ADR's own design. The corrected
rule, to be recorded as an ADR-0002 Amendment once Part B is implemented
(not accepted yet — recorded here as the exact text the amendment needs,
per this ADR's own deliverable list):

> A type with exactly one accessible constructor still selects it
> automatically, unchanged. A type with more than one accessible
> constructor and **no explicit Compono-side constructor selection in
> scope for the composition path reaching it** still reports `CMP0001`,
> unchanged. A type with more than one accessible constructor **and** an
> explicit selection in scope (ADR-0052's `UseConstructor`/equivalent) uses
> the selected constructor — Compono still composes that constructor's own
> parameters exactly as it would for an unambiguous type. This is not a
> reconsideration of the original Decision Outcome's rejection of a
> guessing heuristic (greedy/first-resolvable/etc.) — no heuristic is
> introduced; ambiguity absent an explicit selection still fails exactly as
> designed.

This amendment is **required**, not optional, once Part B ships — ADR-0002
must not be left describing a rule Compono no longer actually implements.

### Two acceptance cases (Finding A's evidence and Part B's own proof)

Both required before this design is considered validated - one alone does
not prove the other capability works:

1. **Registered external type** (proves Part A): `alexa-vox-craft`'s real
   `AlexaInteractionModelClient(HttpClient client, ILogger<...> logger)`,
   composed as a theory parameter, with `Register<HttpClient>(context =>
   context.Resolve<TestHttpHandler>().CreateClient(baseAddress))` in the
   active profile — must compose cleanly with no `CMP0001`, and `HttpClient`
   must resolve to the registered factory's own value, not a Compono-
   composed one.
2. **Compono-composed ambiguous type, no `Register<T>` involved** (proves
   Part B — this is the case that actually demonstrates constructor
   selection, not merely registration-aware leaf classification): a
   controlled sample/test type with exactly two accessible constructors,
   e.g.
   ```csharp
   public sealed class Foo
   {
       public Foo() { }
       public Foo(IBar bar, IBaz baz) { }
   }
   ```
   reached as both a direct `Composer.Create<Foo>()` root and as a nested
   constructor parameter of another composed type. **Without** an explicit
   selection, this must still report `CMP0001` (proving the rule change is
   additive, not a silent behavior change for every ambiguous type). **With**
   a profile-side `For<Foo>().UseConstructor<IBar, IBaz>()` (B2, the
   recommended API - see "Compiler spike results" below for why B1 is no
   longer preferred) in scope, Compono must compose `IBar` and `IBaz`
   itself (including recursively, if either has its own nested
   dependencies) and invoke that constructor — proving Compono, not the
   consumer, did the parameter-composition work.

## Real incremental-generator pipeline spike (2026-08-25) — a genuine blocker found, scoping model corrected

Before wiring anything into the real generator, traced how discovery
actually reaches a plan today (`ComposeMethodDiscovery.TransformMethod`,
`CreateInvocationDiscovery.Transform`, `DiscoveredTypeInfo`,
`DiscoveredCollectionInfo`, `DiscoveredTestDoubleInfo`,
`ComponoIncrementalGenerator.cs`'s own pipeline wiring) — not guessed from
this ADR's own earlier prose. **This surfaced a real architectural fact
that invalidates the "per-composition-path scoping" assumption this ADR's
design space was built on, both for Part A and Part B, and both original
findings A/B's own open question 4.**

**The finding, confirmed directly in source, not inferred:**
`[Compose<TProfile>]`'s own discovery
(`ComposeMethodDiscovery.TransformMethod`) never reads `TProfile` at all —
it walks the attributed *method's own parameter types*, completely
independent of which profile is attached to that method. `DiscoveredTypeInfo`
(the record a generated composition plan is built from) has **no profile
dimension whatsoever** — `PlanClassName => $"{TypeName}CompositionPlan"`,
one plan per *type*, globally, shared by every composition path that
reaches that type, regardless of which profile requested it.
`DiscoveredCollectionInfo`/`DiscoveredTestDoubleInfo` are the same shape —
this isn't an oversight in one code path, it's how Compono's entire
compile-time discovery model is built: **compile time answers "is this
type's shape structurally composable," runtime answers "what values/
registrations actually apply"** — a clean separation this design's
original "scope to the profile(s) resolved for this specific
`[Compose<TProfile>]` site" language quietly assumed away without
verifying against the real architecture.

**Consequence: per-composition-path scoping ("registered in `ProfileA`,
not `ProfileB`, must never leak") is not achievable at the granularity
this ADR originally specified, without a materially larger change — a
new per-(type, profile) plan-variant system, which is not what a "narrow
implementation spike" should attempt and was not what the product decision
asked for.** The realistic, architecture-consistent alternative, matching
the same granularity every other compile-time discovery decision in this
codebase already uses:

**Corrected scoping model: compilation-wide, not per-profile-path.** If a
statically-recognized `Register<T>(...)` (Part A) or
`.For<T>().UseConstructor<...>()` selection (Part B) appears **anywhere**
in **any** `ICompositionProfile`-implementing type or `Composer.Create(builder
=> ...)` lambda in the compilation, that decision applies to **every**
composition of `T` in that compilation — not scoped to the specific
profile(s) resolved for one `[Compose<TProfile>]` site. This is a real,
honest narrowing from what this ADR previously promised, not a free
substitution — recorded here explicitly rather than silently absorbed:

- **A composition path that does *not* actually apply the registering/
  selecting profile at runtime no longer gets a compile-time `CMP0001`**
  for that type — it now compiles, and fails at **runtime** instead (a
  `CompositionException` if nothing else satisfies `T` for that path) if
  the assumption doesn't hold for that specific test. This trades a
  compile-time diagnostic for a runtime one in the "registered in some
  profiles, not others, and a `ProfileB`-only path genuinely needed the
  old `CMP0001`" case — a real behavior change from the original design,
  not a refinement of it.
- **A genuine conflict — two different `UseConstructor` selections for
  the same `T` anywhere in the compilation — is now a real, load-bearing
  compile-time conflict** (there is exactly one plan for `T`; two
  contradictory selections can't both apply to it), not merely a hygiene
  diagnostic. This directly confirms the "duplicate/conflicting
  `UseConstructor` selections" case explicitly asked for below is not an
  edge case to spike defensively — it is a structurally necessary
  diagnostic this design cannot skip.
- Part A's `HttpClient` acceptance case and Part B's `Foo` acceptance case
  (below) are both **unaffected** by this correction — a real project
  typically has exactly one answer for "how is `HttpClient`/`Foo`
  constructed" across its whole test suite, which is exactly the
  compilation-wide granularity this corrected model provides. The
  narrowing only bites a consumer who deliberately wants *different*
  construction behavior for the same type in different profiles within
  one compilation — a real but materially rarer shape than the evidenced
  cases.

**This must be treated as a product decision, not absorbed silently**:
accept the compilation-wide granularity (recommended — it matches the
existing architecture, ships the pre-1.0 requirement, and both real
evidenced cases are satisfied), or treat true per-profile scoping as a
separate, larger, later architectural project (new plan-variant
infrastructure) that this ADR does not currently recommend attempting
before 1.0.

## Compiler spike results (2026-08-25)

Real Roslyn spike (`Microsoft.CodeAnalysis.CSharp` 5.9.0, in-memory
`CSharpCompilation`, not persisted in this repo — throwaway, same pattern
as this repo's other Amendment-driving spikes) compiling a small fixture
with several `ICompositionProfile`-shaped classes, then querying the
resulting `SemanticModel`. All results below are actual program output,
not predicted. (Written before the real-pipeline trace above; its
per-profile-scoping results are still accurate as *pure Roslyn syntax/
symbol queries* — the correction above is about how those results plug
into Compono's *existing plan-generation granularity*, not about whether
the queries themselves work.)

**Part A — `Register<T>` static recognition, confirmed:**
- A profile that calls `builder.Register<HttpClient>(...)` is correctly
  identified, with `HttpClient` extracted as the exact registered type,
  via `SemanticModel.GetSymbolInfo` on each `InvocationExpressionSyntax`
  bound to a method named `Register` — ordinary symbol resolution, no
  heuristic string/name matching needed.
- A sibling profile with an empty `Configure` body correctly reports no
  registrations — confirmed **per-class** (per-profile) scoping is
  achievable simply by scoping the syntax query to each profile's own
  `ClassDeclarationSyntax`, not a compilation-wide search.
- **Cross-assembly / fail-closed, confirmed real, not hypothetical**: a
  symbol for a method compiled into a *referenced* assembly (tested
  against `System.Console.WriteLine`, standing in for "a profile type
  defined in another project") has `DeclaringSyntaxReferences.Length == 0`
  — **no syntax tree is available at all** for a type outside the current
  compilation. This confirms the design constraint already recorded above
  is not optional: a profile type compiled into a different assembly
  cannot be statically scanned by this mechanism, and the walker must fail
  closed to today's unchanged `CMP0001`/structural-walk behavior for that
  case, not silently assume no registration exists (silence there would
  be indistinguishable from "genuinely not registered" and risk a false
  negative in the opposite, more dangerous direction — treating a real
  registration as absent is safe/conservative; the reverse would not be).

**Part B — B1 (strongly-typed constructor lambda), confirmed:**

| Case | Result |
|---|---|
| `(IBar bar, IBaz baz) => new Foo(bar, baz)` | **Accepted** — identity order, resolves to the real `Foo(IBar, IBaz)` constructor symbol via `GetSymbolInfo` |
| `(IBar bar, IBaz baz) => new Foo(baz: baz, bar: bar)` | **Accepted** — named arguments matching parameter names, semantically identity despite syntactic reordering |
| `(string a, string b) => new Widget(b, a)` (same-typed params, so the compiler *can't* reject it the way mismatched types would) | **Ambiguous — ruled reject-by-default**: no type-system proof this is selection rather than a deliberate transform |
| `(string a, string b) => new Widget(a, "literal")` | **Rejected** — constant argument, not a bare parameter reference |
| `(string a, string b) => new Widget(a.Trim(), b)` | **Rejected** — nested method-call argument |
| Block body (`{ Console.WriteLine(...); return new Foo(bar, baz); }`) | **Rejected outright**, even though the block's only executable effect is the same trivial construction — a block body is structurally capable of side effects, so it's refused as a category, not inspected statement-by-statement. **Consumer must use expression-bodied lambda syntax.** |
| Every accepted/rejected case | **Compiler-resolved constructor symbol is always readable** via `GetSymbolInfo` on the `ObjectCreationExpressionSyntax`, independent of whether the shape passes trivial-forwarding validation — answers Q7 (overloads differing only by assignable types): C# itself resolves the overload, Compono only reads the answer |

Answers to the numbered questions:

1. **Is the delegate actually invoked, or only a compile-time marker?**
   Recommended design: **compile-time-only marker, never invoked at
   runtime.** `CompositionBuilder.For<T>().UseConstructor(...)`'s *runtime*
   method body can be a genuine no-op — the compile-time-generated
   composition plan already embeds the selected constructor and its
   argument-resolution calls directly (the same way any other generated
   plan already does), so nothing at runtime ever needs to invoke the
   lambda as a delegate.
2. **How do we guarantee it doesn't become a disguised `Register<T>`?**
   Structurally, by never invoking it — but this makes it a **hard
   requirement, not an optimization**, that any lambda failing the
   trivial-forwarding validation is a **compile-time diagnostic error**,
   never silently accepted-but-unexecuted. Silent non-invocation of a
   consumer-written side-effecting lambda would be a confusing, silently-
   dead-code trap — exactly the kind of surprise `docs/manifesto.md`'s "a
   useful failure is better than a clever fallback" rules out.
3. **Can the shape be reliably enforced?** Yes, confirmed by the table
   above — every named constraint (direct forwarding only, no constants,
   no nested calls, no conditionals, no side effects) is a distinguishable
   syntax-tree shape a Roslyn analyzer can check deterministically.
4. **Do we want to allow reordering?** **Only via named arguments**
   (`new Foo(baz: baz, bar: bar)`), where the name itself proves intent
   unambiguously. **Bare positional reordering is rejected by default** —
   confirmed genuinely ambiguous by the spike (same-typed parameters mean
   the compiler can't catch a mistaken swap the way mismatched types
   would), and indistinguishable from a real transform without the named-
   argument signal.
5. **Optional parameters?** Work with no special design: a lambda that
   simply omits a trailing optional parameter compiles against the real
   constructor exactly as any hand-written call would (ordinary C#
   default-argument behavior) — not separately spiked, follows directly
   from B1's mechanism being real constructor-call syntax.
6. **`params` constructors?** Out of scope for the initial design — a
   selection would need to pass an explicit array, which means the
   *consumer* assembles the `params` array's contents by hand, working
   against the whole point of the capability. Recommend the same explicit
   rejection `ConstructorSelector.ValidateParameterKinds` already applies
   to other unsupported constructor-parameter shapes, not silent
   acceptance of a degraded case.
7. **Overloads differing only by assignable types?** Answered above — the
   C# compiler resolves this the same way it resolves any overloaded call;
   Compono reads the result, never re-implements overload resolution.
8. **Nullable annotations?** No special handling needed — ordinary C#
   argument-passing rules apply unchanged; not a distinct code path from
   any other generated `Resolve<T>()` call already handling nullability
   today.
9. **`ref`/`in`/`out` constructor parameters?** Recommend reusing
   `ConstructorSelector.ValidateParameterKinds`'s existing rejection
   unchanged — B1's lambda parameter would itself need matching
   `ref`/`out`/`in` modifiers to forward correctly, an exotic shape not
   worth supporting in the initial design.
10. **High arity?** Not measured to a specific bound, per instruction —
    `Func<T1, ..., T16, TResult>` is the BCL's own hard ceiling (no
    Compono-authored delegate family needed below that), which in practice
    exceeds any realistic constructor arity worth composing.

**Part B — B2 (type-list selection), confirmed:**
`builder.For<Foo>().UseConstructor<IBar, IBaz, Foo>()` resolves cleanly:
the requested type-argument list is compared directly against `Foo`'s real
constructor symbols' parameter types (`SymbolEqualityComparer`), finding
the exact matching `IMethodSymbol` — ordinary compile-time symbol
comparison, the same category of operation `ConstructorSelector` already
performs today, no reflection, no lambda, no validation-diagnostic surface
at all (there's no body to validate). A type list matching no real
constructor would need a **new** Compono-specific diagnostic (there's no
C# compiler error for "this constructor doesn't exist" on its own, since
`UseConstructor<T1,T2,TResult>` is a real, always-callable generic
method) — simpler than B1's validation surface (one diagnostic case, not
several).

**Third marker shape, investigated as requested:** no shape was found that
separates "constructor signature selection" from "runtime construction
delegate" more cleanly than B2 already does — B2 *is* that separation (a
bare type list, never a callable). A `Func<...>`-typed parameter that's
merely declared but never invoked (e.g. `UseConstructor(default(Func<IBar,
IBaz, Foo>))`) was considered and rejected as strictly worse than B2's own
syntax for the identical underlying idea, and a `delegate*` function-
pointer marker was rejected outright (function pointers have their own
generic-type-argument restrictions in C# and would complicate the API for
no benefit). **B2 is the recommended answer to this investigation, not a
new fourth shape.**

### Interaction with ADR-0052 Finding B

Deliberately not solved by this design as scoped (see "Do Findings A and B
need one mechanism or two?" above) — Finding B needs the *reverse*
direction of the same static-scan capability (an additional discovery
*root*, not a *stop signal*), which this ADR records as a strong candidate
for the same infrastructure but explicitly defers proving until Finding
A's mechanism exists and a dedicated spike confirms the scan generalizes
to a nested `context.Resolve<T>()` call inside a registration factory's
own lambda argument (a syntactically deeper pattern than a direct
`Register<T>(...)` call).

### Source-generation/discovery implications

- Parameters of the *dominant registration's own factory lambda* become
  new discovery-graph members exactly like an ordinary constructor
  parameter would — e.g. `Register<HttpClient>(context =>
  context.Resolve<HttpMessageHandler>() is used ...)` means
  `HttpMessageHandler` (or whatever the factory body itself composes)
  participates in the same closure the walker already builds, so it gets
  its own generated plan, leaf classification, or diagnostic exactly as
  any other discovered type would. This is a natural consequence of
  treating the factory body as ordinary discoverable source, not a new
  mechanism of its own.
- Registration precedence, multiple profiles registering the same type
  differently, and profiles applied only to some `[Compose<TProfile>]`
  sites all need the walk to be scoped **per composition-plan generation
  target** (per `[Compose<TProfile>]`/`Composer.Create` call site's
  resolved profile set), not globally cached across every profile in the
  compilation — a type registered in `ProfileA` but not `ProfileB` must
  still hit `CMP0001` on a `ProfileB`-only composition path. This is a
  real implementation constraint the spike must validate, not yet proven
  sound for every applicable path (question 5's own caution stands).
- Generic registrations (`Register<T>` called inside a generic profile
  method, or registering an open/constructed generic) and
  cross-assembly profile visibility are real edge cases flagged for the
  implementation spike, not resolved by this design pass — narrower
  scoping (fail closed to today's `CMP0001` behavior when the static scan
  can't confidently classify a registration) is preferred over guessing
  in either direction, consistent with "a useful failure is better than a
  clever fallback."

### AOT/trimming implications

**Part A** adds no new runtime mechanism at all — it only changes what the
*compile-time* walker does when a matching registration is statically
recognized (skip structural descent instead of reporting `CMP0001`). The
actual factory invocation at runtime is already the existing, ordinary
`Register<T>` call — no reflection before, none added now.

**Part B (B1)** invokes a plain delegate (`Func<T1, ..., TResult>`) with
generator-supplied arguments — a direct delegate call, not
`MethodInfo.Invoke`/`Activator.CreateInstance`, so it carries the same
AOT/trimming profile as any other delegate invocation already present in
generated composition plans today (none of them require metadata
preservation beyond the delegate's own declared signature, which the
linker already keeps because the generated code references it directly).
B2 would additionally need the generator to statically resolve which real
constructor symbol the type-list names — a compile-time-only operation
(Roslyn symbol comparison), never emitted as a runtime lookup, so it
carries no AOT/trimming cost either. Neither option introduces
`System.Reflection` or `System.Linq.Expressions` anywhere in generated
code or Compono's own runtime surface.

### Diagnostics behavior

- A type reached structurally with `> 1` accessible constructor and **no**
  statically-recognized registration for it, on that composition path,
  keeps today's exact `CMP0001` behavior, unchanged — this design narrows
  when the diagnostic fires, it doesn't change what it means or remove it.
- A **new** diagnostic is needed for the case a registration exists but
  its factory's own return-type/constructor call doesn't actually resolve
  to a real accessible constructor of `T` (e.g. a typo'd or since-removed
  constructor) — the C# compiler already reports this as an ordinary
  compile error inside the factory lambda itself (it's real C# code), so
  this may not need a *Compono-specific* diagnostic at all, only needs
  confirming during the spike.
- Recommend a new informational diagnostic when a registration is present
  for a type the walker would otherwise have composed *unambiguously*
  (a single accessible constructor) — not an error, but useful signal that
  the registration may be redundant, mirroring this repo's existing
  informational-diagnostic precedent (`CMP0022`/`CMP0030`) rather than
  silently accepting it.

## Real implementation spike results (2026-08-25, continued) — Part B built and proven end-to-end

Built directly against the real `Compono`/`Compono.Generators` source
(uncommitted, spike-only — see the file list at the end of this section),
run through the actual incremental-generator pipeline via this repo's own
`GeneratorTestHelpers.CompileAndExecute`/`VerifyFailure` test
infrastructure, not an isolated Roslyn query. **Adopted semantic, per
product direction: within one compilation, a type has at most one explicit
constructor selection** — matching the existing one-plan-per-type
architecture exactly, not a per-profile variant.

**Integration point correction found while building this:** `CompositionBuilder`
already has a real, shipped `For<T>()` returning `CompositionTypeRuleBuilder<T>`
(ADR-0020's composition configuration rules) — the spike's first attempt
to add a *new*, colliding `For<T>()` overload failed to compile (`CS0111`).
Corrected by adding `UseConstructor<T1, ...>()` overloads directly onto
the existing `CompositionTypeRuleBuilder<T>` instead of inventing a
parallel navigator type — a better outcome than originally sketched: one
`.For<T>()` entry point for every per-type configuration concern, not two.

**What was built** (spike-only, not proposed as final production code -
naming/XML docs/test coverage would need this repo's usual polish before
a real PR):
- `Compono.CompositionTypeRuleBuilder<T>.UseConstructor<T1>()` through
  `<T1, T2, T3, T4>()` — real no-op runtime methods (arity 1-4 only, for
  the spike; see arity discussion below).
- `Compono.Generators.Discovery.ConstructorSelectionScanner` — scans every
  syntax tree in the `Compilation` for a `UseConstructor<...>()` call bound
  to `CompositionTypeRuleBuilder<T>`, matches the requested type-argument
  list against `T`'s real constructor symbols (compile-time symbol
  comparison, no reflection), and produces one compilation-wide
  `(selection | conflict | invalid)` outcome per type. Cached per
  `Compilation` via `ConditionalWeakTable`, matching this codebase's
  existing `WellKnownTypes.GetOrCreate` pattern — **not yet a proper
  `IncrementalValueProvider`** (it re-scans on first access per
  compilation, not incrementally cached/reused the way a real
  implementation should structure it - a real implementation task, not a
  design question).
- `ConstructorSelector.Select` consults the scan **only when a type is
  already ambiguous** (`> 1` accessible constructor) — an unambiguous
  type's behavior is completely untouched.
- Two new diagnostics: `CMP0033` (conflicting selections for the same
  type) and `CMP0034` (a selection matching no real accessible
  constructor), both `Error` severity, matching `CMP0001`'s own category.
  An identical repeated selection (same real constructor symbol, via
  `SymbolEqualityComparer`) is treated as **idempotent, not a conflict** —
  confirmed reliable, per the product direction's own preference.

**Real end-to-end test results** (`ConstructorSelectionSpikeTests.cs`, 5
tests × 2 TFMs = 10/10 passing):
- No selection anywhere in the compilation → `CMP0001`, byte-identical to
  today's unchanged behavior.
- `Composer.Create<Foo>()` (root) with `.For<Foo>().UseConstructor<IBar, IBaz>()`
  registered elsewhere in the same compilation → compiles, and at runtime
  actually resolves `IBar`/`IBaz` through the ordinary composition path
  and constructs `Foo` through the selected 2-parameter constructor —
  proven by asserting the resolved instances are the real registered
  types, not just that composition didn't throw.
- `Outer(Foo foo)` (nested/transitive) with the same selection → the
  **same** generated `Foo` plan is reached through `Outer`'s own generated
  plan and composes correctly — proving Compono, not the consumer, resolved
  the nested dependencies (confirmed first by a deliberately-incomplete
  run that correctly threw `CompositionException` for the still-missing
  `IBar` registration - i.e. the *failure* mode itself proved the generated
  plan really was calling `context.Resolve<IBar>()` inside `Foo`'s own
  plan, not silently succeeding some other way).
- Two different `UseConstructor` selections for the same type anywhere in
  the compilation → `CMP0034` is reported precisely (not silently ignored,
  not crashing the generator), naming both conflicting constructors in the
  message.
- A `UseConstructor<...>()` type list matching no real constructor →
  `CMP0034`, precise message naming the requested (non-matching) type list.

**Edge cases (optional parameters, `params`, nullable annotations, `ref`/
`out`/`in`, overloads differing only by assignable types, high arity):**
not re-spiked against the real pipeline — the underlying mechanism proven
here (compile-time symbol-list-to-constructor matching) is exactly the
mechanism the pure-Roslyn spike above already validated for these cases,
and `ConstructorSelectionScanner`'s matching logic is a direct,
unmodified translation of that already-proven logic. No new evidence
changes those conclusions. **Arity**: shipped 1-4 for the spike, not yet
a considered product decision — see "Practical arity bound" note below.

**AOT/trimming**: not re-run through the real AOT smoke test project for
this spike (time-boxed) — structurally unaffected by construction, since
every `UseConstructor` overload is a literal empty method body (no
reflection, no new runtime code path at all) and the scanning/matching
work is 100% compile-time; this repeats the same reasoning already proven
for PLAN-0053's own AOT proof, not new territory.

**Practical arity bound**: not measured against real dogfood-repo
constructor arities in this pass (would require grepping
`alexa-vox-craft`/`trivia-platform`/`cosmere-tracker`'s own real
multi-constructor types, not done here due to time) — flagged as a real
open item for whoever picks up the actual implementation plan, not
resolved by this spike. 1-4 was chosen arbitrarily to prove the mechanism
generalizes across arity via ordinary generic-method overloading (each
arity is its own explicit overload, no `Func<>`-style variance games
needed since these methods take no delegate at all) — extending the
overload set to a higher bound is a mechanical, low-risk change once a
real bound is chosen.

**Spike files** (uncommitted, in the working tree — not part of this
report's own deliverable, listed for transparency): `src/Compono/CompositionTypeRuleBuilder.cs`
(new `UseConstructor` overloads), `src/Compono.Generators/Discovery/ConstructorSelectionScanner.cs`
(new file), `src/Compono.Generators/Discovery/ConstructorSelector.cs`
(modified `Select`), `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs`
(two new descriptors), `test/Compono.Generators.Tests/ConstructorSelectionSpikeTests.cs`
(new file) and its approved snapshot files.

## Part A recommendation under the corrected constraint

The product direction is explicit: **do not weaken compile-time
diagnostics globally merely to make `Register<T>` serve the `HttpClient`
case; shipping constructor selection alone is preferred over that
tradeoff.** Compared against that constraint:

1. **Compilation-wide registration stop signal.** Simplest, closes
   `HttpClient` — but exactly the tradeoff just ruled out: a composition
   path using a profile that does *not* register `T` silently loses its
   `CMP0001` and fails at runtime instead, for every path in the
   compilation, not just the one that has the registration. Rejected
   under this constraint, not because it's technically unsound (the
   earlier compiler spike proved the mechanics work) but because the
   product has now explicitly prioritized diagnostic integrity over
   closing this specific case immediately.
2. **Do not add registration-aware discovery yet.** **Recommended.** Keep
   `Register<T>`'s current, unchanged runtime/provider-stage-only scope;
   keep today's `CMP0001` behavior exactly as-is for every structurally-
   ambiguous type reached through a registered path; rely on `UseConstructor`
   for any ambiguous type Compono itself should keep composing.
   **Important, evidence-based caveat: this does *not* fully close Finding
   A as originally scoped.** Re-checked directly against RESEARCH-0010
   §10's exact evidence: `AlexaInteractionModelClient`'s real registration
   is `Register<HttpClient>(context => context.Resolve<TestHttpHandler>().CreateClient(baseAddress))`
   — a *specific, pre-configured* `HttpClient` instance built from a test
   fixture's own state, not something `UseConstructor` could compose from
   scratch (there is no constructor-parameter list that produces "the
   handler `TestHttpHandler.CreateClient` happens to configure"). **`UseConstructor`
   does not substitute for `Register<T>` in the actual evidenced case** —
   deferring Part A leaves the real `HttpClient` evidence open, still
   served only by RESEARCH-0010's existing project-local workaround
   (hand-constructing the client from `CreateClient(...)`), not by this
   design. Recorded here explicitly so this isn't mistaken for "Finding A,
   fully closed" once Part B ships.
3. **A narrower compile-time mechanism proving a registration applies
   without full per-profile plan variants.** Investigated, per instruction
   not to invent a large architecture: no clean option was found.
   `[Compose<TProfile>]`'s own discovery
   (`ComposeMethodDiscovery.TransformMethod`, confirmed above) never reads
   `TProfile` at all today, and `CreateInvocationDiscovery` only recognizes
   `Composer.Create<T>()`'s bare type argument, not any builder-configuration
   argument alongside it — there is no existing per-call-site signal to
   attach a "this path's profile(s) register T" fact to without either (a)
   extending `[Compose<TProfile>]`'s own wire-up to pass `TProfile` through
   to the generator (a cross-package change touching `Compono.XunitV3` too,
   not narrow) or (b) the full per-(type, profile) plan-variant system
   already ruled out as too large. **No third option exists at the
   requested narrowness — reporting this rather than inventing one.**

**Recommendation: ship Part B (constructor selection) alone for now.
Defer Part A (registration-aware discovery) as a separate, later
roadmap item — not solved by this design, not silently downgraded either,
explicitly left open with the real evidence gap stated above.**

## Part B: Accepted and shipped (2026-08-25)

The spike from "Real implementation spike results" above was reviewed and
hardened as production code, not reverted:

- `Compono.CompositionTypeRuleBuilder<T>.UseConstructor<T1>()` through
  `<T1, T2, T3, T4, T5, T6>()` — arity 1-6, chosen from a real survey of
  constructor arities across this repo's own dogfooding consumers
  (`alexa-vox-craft`, `trivia-platform`, `trivia-manager`,
  `cosmere-tracker`, `lightsaber-skill`): 5-parameter constructors are
  already rare, nothing observed above that; 6 leaves one parameter of
  headroom, not `Func<>`'s unrelated 16-parameter ceiling copied by
  default. A 7-parameter selection is a genuine C# compile error (no
  matching overload) — immediately actionable, never a silent fallback.
  Full XML documentation added, including the explicit `Register<T>` vs.
  `UseConstructor` distinction (see "Register<T> distinction" in the
  package docs, below).
- `Compono.Generators.Discovery.ConstructorSelectionScanner` — hardened
  with full XML documentation of its one responsibility (recognizes
  exactly one call shape, nothing else), its determinism/order-
  independence guarantee, and its malformed/incomplete-source behavior
  (an unresolvable call is skipped, never a generator crash). Also bound
  to Compono's real `CompositionTypeRuleBuilder<T>` symbol (resolved once
  per compilation, compared by identity) rather than matching the
  containing type's simple name/arity, and its constructor-parameter
  matching now excludes `ref`/`out`/`ref readonly` parameters so an
  unsupported-by-ref overload can never win a type-only match over a
  usable one regardless of declaration order (both real correctness bugs,
  code review, 2026-08-25).
  **Known, real, still-open cost — corrected from an earlier, inaccurate
  claim here that this merge-ready PR would fix it (code review,
  2026-08-25):** still uses `ConditionalWeakTable`-per-`Compilation`
  caching, not a proper `IncrementalValueProvider`. Every analysis of an
  ambiguous composed type re-walks every syntax tree in the whole
  compilation from scratch on a cache miss - and an IDE/live-analysis
  session creates a fresh `Compilation` instance on most edits, so this is
  a genuine, non-trivial per-keystroke cost near a composed type, not a
  cosmetic one. Not fixed in this PR: doing so correctly means threading
  an incremental syntax-provider-fed candidate list through this
  generator's `Initialize` wiring down to wherever `ConstructorSelector.Select`
  is invoked - a pipeline-plumbing change this remediation pass judged too
  large and too risky to make safely without its own dedicated design/test
  pass, not something to rush under review-response time pressure.
  Tracked here as an explicit, honest open item - not a design gap, but
  also no longer described as "the next PR will handle it" without a
  next PR actually doing so.
- `CMP0033`/`CMP0034` added to `AnalyzerReleases.Unshipped.md` alongside
  the rest of this generator's tracked rules.
- Test file promoted from `ConstructorSelectionSpikeTests.cs` to
  `ExplicitConstructorSelectionTests.cs`, expanded from 5 to 10 real
  end-to-end tests (20 with both TFMs), all passing against the real
  incremental-generator pipeline: no-selection `CMP0001` control, root
  composition, nested/transitive composition, `CMP0033` conflict,
  `CMP0034` invalid selection, an **idempotent identical repeated
  selection** (confirmed accepted, not a false conflict), an
  **inaccessible matching constructor** (a `private` constructor
  correctly excluded, falling through to `CMP0034` rather than a false
  match), a **constructed generic + nullable-annotated parameter type**
  (`Wrapper<string>`, `string?`) matched exactly, a selection on an
  **already-unambiguous type** (proven harmless no-op — the single-
  constructor path never even consults the scanner), and a **`params`
  constructor** proven not to confuse symbol matching into a wrong/silent
  match. Full `Compono.Generators.Tests` suite: 452/452, zero
  regressions. Optional-parameter and by-ref-like-parameter behavior
  were not independently re-tested here — they follow directly from the
  same "exact parameter-type-list symbol match" mechanism already proven
  above and from `ConstructorSelector.ValidateParameterKinds`'s existing,
  unmodified `ref`/`out`/ref-like rejection (still runs, unconditionally,
  after a selection resolves a constructor - see `ConstructorSelector.Select`).

See ADR-0002 Amendment 3 for the corresponding constructor-selection rule
change, and `docs/packages/compono.md`'s "Explicit constructor selection"
section (added alongside this ADR) for consumer-facing documentation.

### Real consumer proof (2026-08-25): `alexa-vox-craft`'s `AlexaInteractionModelClientTests`

Beyond the AOT fixture's synthetic `AmbiguousFoo`, Part B was proven
against the real, pre-existing case that motivated this whole design
thread — `AlexaVoxCraft.Smapi.Tests.Clients.AlexaInteractionModelClientTests`'s
own `CreateClient(TestHttpHandler)` hand-construction helper and its
"no registration-based escape hatch" comment (the original evidence in
"Context," above). Full detail and dogfood-gate counts recorded in
[RESEARCH-0011](../research/0011-alexa-vox-craft-mediatr-tests-testkit-migration-slice-1.md)'s
"Real consumer proof" section; summary:

- `builder.For<HttpClient>().UseConstructor<HttpMessageHandler, bool>()`
  closes `CMP0001` for `HttpClient` at compile time - confirmed this
  fires **regardless of a pre-existing `Register<HttpClient>`**, since
  compile-time discovery can't see a runtime registration (Part A's own
  boundary, restated with fresh real-consumer evidence).
- The consumer's pre-existing `Register<HttpClient>` (building the
  client from a specific `TestHttpHandler`) still supplies the actual
  runtime value - a registration outranks a generated plan, so
  `UseConstructor`'s generated construction path is never invoked here.
  `Register<T>` and `UseConstructor` are not competing in this case; one
  closes the compile-time gate, the other supplies the runtime value -
  exactly the division of labor this ADR's `Register<T>` vs.
  `UseConstructor` distinction predicts.
- `AlexaInteractionModelClient` (the SUT itself, not just `HttpClient`)
  now composes as an ordinary theory parameter across all 10 tests in
  that file; the hand-construction helper and the stale limitation
  comment are both deleted.
- Fresh-package dogfood gate re-run after the change: same 2784/2784
  passing, `PASS`.
- The same fix applied identically to `AlexaSkillInvocationClientTests.cs`
  (11 tests, same `SmapiHttpTestProfile`, same shape) - no new profile
  machinery beyond one more `Register<ILogger<T>>` line. Same 2784/2784,
  `PASS`, after this second change too.

An isolated spike (real `PackageReference` against fresh-packed
packages, not `ProjectReference` - a `ProjectReference` skips the
analyzer wiring a real consumer gets through the packaged
`analyzers/dotnet/cs` folder) also surfaced one adjacent, **separate**
finding while proving the pieces before touching consumer code:
`.For<HttpClient>().Member(x => x.BaseAddress).Use(...)` silently never
applies, because `.Member(...)` rules only ever fire for a constructor
parameter or a `required` member (confirmed against this ADR's own
"Member-rule matching identity" design, carried over from ADR-0020) -
`HttpClient.BaseAddress` is an ordinary settable property, neither, so
Compono's plan for `HttpClient` never visits it. **Classification
(ADR-0029): Acceptable Compono-native alternative**, not a bug and not a
roadmap candidate - `Register<T>`/an explicit post-composition
assignment remains the right tool for "construct a simple value, then
imperatively configure more of it"; expanding the composition walker to
visit arbitrary non-required settable properties would materially
broaden Compono's semantics for a case `Register<T>` already covers
cleanly. It didn't end up mattering for the real migration above, since
the consumer's own `Register<HttpClient>` already sets `BaseAddress`
correctly.

## Recommendation (final, 2026-08-25)

**Ship Part B only. Defer Part A.** Superseding the earlier draft of this
section (kept below struck through in spirit, not deleted, per this
repo's ADR-immutability convention — the reasoning that changed it is
recorded above in "Part A recommendation under the corrected constraint"):

1. **Ship Part B** (explicit constructor selection): `CompositionTypeRuleBuilder<T>.UseConstructor<T1, ...>()`
   (added to the *existing* `.For<T>()` entry point, not a new navigator
   type — see "Real implementation spike results" above for why), source-
   generator-recognized via a compilation-wide static scan, matched
   against `T`'s real constructor symbols at compile time — no lambda, no
   runtime delegate, no reflection. **Scoping is compilation-wide, not
   per-profile-path**: within one compilation, a type has at most one
   explicit constructor selection; a second, different selection for the
   same type anywhere in the compilation is `CMP0033`; an identical
   repeated selection is idempotent (accepted silently, proven reliable
   via `SymbolEqualityComparer`); a selection matching no real accessible
   constructor is `CMP0034`. Proven end-to-end against the real generator
   pipeline (10/10 real tests passing, both TFMs) — root composition,
   nested/transitive composition, the no-selection `CMP0001` control, and
   both new diagnostics.
2. **Defer Part A** (registration-aware discovery) as its own, separate,
   later roadmap item. Not solved by this design. The real `HttpClient`
   evidence (RESEARCH-0010 §10) stays open, served only by its existing
   project-local workaround — **`UseConstructor` does not substitute for
   it** (the real registration supplies a specific pre-configured
   instance, not something a constructor-parameter list could compose).
   This is a deliberate, evidence-based product tradeoff (diagnostic
   integrity over closing this one case immediately), not an oversight —
   see "Part A recommendation under the corrected constraint" for the
   full comparison of the three options considered and why none of them
   satisfies the constraint at a "narrow" scope.
3. **Do not attempt Finding B (nested `context.Resolve<T>()` discovery) in
   this change.** Untouched by this spike; remains its own, separately-
   scoped roadmap question.
4. **ADR-0002 needs a real Amendment once Part B ships** — see "Exact
   ADR-0002 amendment required" above for its exact text (still accurate:
   "ambiguity is `CMP0001` unless an explicit selection is in scope").
   `ConstructorSelector.Select`'s own arity-based logic for an
   *unambiguous* type is completely unchanged — no guessing heuristic is
   introduced anywhere in this design.
5. **This ADR should become `Accepted`** once the spike code above is
   turned into a real implementation PR (proper `IncrementalValueProvider`
   caching instead of the spike's `ConditionalWeakTable` re-scan, a
   considered arity bound, this repo's usual XML-doc/naming polish, and
   the deferred edge-case matrix re-verified against the real pipeline
   rather than carried forward from the pure-Roslyn spike) — not before.
   The mechanism itself is now proven, not merely designed.

## Evidence gathered since proposal (2026-08-25)

Corroborating frequency evidence, not from `alexa-vox-craft`:
[RESEARCH-0003](../research/0003-structured-logging-exception-constructor-ambiguity.md)
recorded the same `CMP0001` shape (`System.Exception`, 3 accessible
constructors) across 61 real call sites in an unrelated repo
(`structured-logging`), classified "Acceptable Compono-native alternative"
at the time under ADR-0029's ordinary frequency/cost weighting (the
per-site workaround was cheap and didn't touch this ADR's own scope). That
classification is **not reopened here** — it was reached fairly under the
rules that applied to it, and the product decision above doesn't
retroactively change past classifications, only the forward-looking
answer to "should an explicit mechanism exist." It's recorded here only as
additional, independent evidence that ambiguous-constructor BCL types are
a recurring shape across real Compono consumers, not an `alexa-vox-craft`-specific
oddity.

[RESEARCH-0011](../research/0011-alexa-vox-craft-mediatr-tests-testkit-migration-slice-1.md)
(`AlexaVoxCraft.MediatR.Tests`, the first real slice through
`AlexaVoxCraft.TestKit`, 154/154 under current Compono) neither reproduced
Finding A nor Finding B, and added one negative data point narrowing
question 6 above: a registration factory's nested
`context.Resolve<ILogger<SkillMediator>>()` call — `ILogger<SkillMediator>`
never independently discovered elsewhere, exactly Finding B's shape —
resolved cleanly with no `CompositionException`, because `ILogger<T>` is
satisfied by a test-double *provider* stage, which never needs a
discovery-root generated plan at all. Finding B's failure mode is
therefore specific to types needing a *generated composition plan*
(concrete/record types), not to nested `Resolve<T>()` calls in general.
Recorded as evidence for the design dive to weigh against question 6's
"can a registration factory be given a statically expressible dependency
declaration" — no change to this ADR's own status or open questions.

## Links

- [RESEARCH-0010](../research/0010-alexa-vox-craft-compono-ecosystem-migration.md)
  §10 (Finding A) and §11 (Finding B) — the migration evidence this ADR
  formalizes.
- [RESEARCH-0011](../research/0011-alexa-vox-craft-mediatr-tests-testkit-migration-slice-1.md) —
  the first real `AlexaVoxCraft.TestKit` migration slice; negative
  evidence narrowing Finding B (see "Evidence gathered since proposal"
  above).
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the Gap decision rubric and classification applied above.
- [ADR-0002](0002-constructor-selection-algorithm.md) and its Amendments 1
  and 2 — the governing constructor-selection decision (unamended by this
  ADR, see "Recommendation" above), its prior, weaker `HttpClient`
  evidence, and Amendment 2's own record that the bar Amendment 1 named
  has now been cleared, deferring the actual design to this ADR.
- [RESEARCH-0003](../research/0003-structured-logging-exception-constructor-ambiguity.md) —
  corroborating (not `alexa-vox-craft`) frequency evidence for the same
  `CMP0001` shape, classification unchanged (see "Evidence gathered since
  proposal" above).
- `src/Compono.Generators/Discovery/ConstructorSelector.cs`,
  `TransitiveClosureWalker.cs`, `LeafTypeClassifier.cs` — the exact current
  mechanics this design's "Current generator mechanics" section traces
  directly, not inferred from documentation.
- `docs/concepts/registrations-and-rules.md` — the existing nested-`Resolve`
  worked example that this ADR's Finding B shows doesn't generalize to
  user-defined composable types.
- [PLAN-0051](../plans/0051-compono-http-handler-based-testing-package-impl-plan.md)
  Task 10 — where both findings were originally captured as evidence,
  deliberately not fixed.
