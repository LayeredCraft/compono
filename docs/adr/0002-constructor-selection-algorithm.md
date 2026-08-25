# [ADR-0002] Constructor Selection Algorithm

**Status:** Accepted

**Date:** 2026-07-27

**Decision Makers:** solo

## Context

The source generator has to pick exactly one constructor to invoke when
it emits a composition plan for a type (`docs/architecture.md`'s
"Source-Generated Composition Plans": "Selects the constructor... Invokes
the constructor directly"). Modern C# gives a type author several ways to
end up with more than one accessible constructor — explicit overloads, a
primary constructor plus a chained secondary one, a record's positional
constructor alongside a copy constructor — so the generator needs a
documented rule for what happens when more than one candidate exists, not
just a rule for the common single-constructor case. This blocks
Milestone 1's "constructor selection prototype" and "compile-time
diagnostics for unsupported or ambiguous construction" work items
directly, and is listed as an open decision in both `docs/mvp.md` and
`docs/architecture.md`.

## Decision Drivers

- `docs/manifesto.md`'s "Predictability over magic" — resolution behavior
  must be deterministic and documented, and a failure should be
  explainable rather than silently guessed around.
- `docs/manifesto.md`'s "A useful failure is better than a clever
  fallback" — directly informs how ambiguity should be handled.
- `docs/manifesto.md`'s explicit non-goal: "Do not reproduce AutoFixture
  terminology solely for familiarity" — a signal that copying
  AutoFixture's own constructor-selection behavior by default isn't
  automatically the right call just because it's familiar.
- `docs/architecture.md`'s "Generator responsibilities" already commits to
  surfacing "Ambiguous construction paths" as something the generator
  identifies — this decision is choosing *what counts as ambiguous* and
  *what happens when it is*.

## Considered Options

1. Single unambiguous constructor, diagnostic on ambiguity.
2. Greedy / most-parameters constructor (AutoFixture-style heuristic).
3. Primary-constructor-first, with a greedy fallback among remaining
   ordinary constructors.

## Decision Outcome

Chosen option: **"Single unambiguous constructor, diagnostic on
ambiguity."** If a type has exactly one accessible constructor, the
generator selects it — no ambiguity, nothing to decide. If a type has more
than one accessible constructor and nothing else disambiguates it, that is
a compile-time diagnostic ("ambiguous construction path", per
`docs/architecture.md`) rather than a guess. An explicit
disambiguation mechanism (the exact shape — e.g. an attribute like
`[CompositionConstructor]` — is left to be designed when Milestone 1
actually needs it) is the intended escape hatch for a type that
legitimately has more than one accessible constructor and needs one
specific one selected.

This is the option most directly aligned with "predictability over magic"
and "a useful failure is better than a clever fallback": it never silently
picks a constructor the type's author didn't clearly signal as the
intended one, and it turns a genuinely ambiguous case into a compile-time
diagnostic — the same kind of actionable, compile-time feedback the
generator is already committed to producing for unsupported types.

### Positive Consequences

- Fully deterministic: there is never a case where two engineers reading
  the same type disagree about which constructor gets used, because
  there's no heuristic to disagree about.
- Ambiguity surfaces at compile time, in the same place and the same way
  as any other unsupported-construction diagnostic — one mental model for
  "the generator couldn't figure out how to build this," not two.
- Leaves room to add an explicit disambiguation attribute later without
  it being a breaking change to existing single-constructor types (the
  common case is unaffected either way).

### Negative Consequences

- A type with multiple legitimate constructors and no disambiguation
  attribute yet available will hard-fail to compose until Milestone 1
  actually ships that attribute — mitigated by scoping the attribute's
  design into Milestone 1 itself rather than deferring it indefinitely,
  since real types with multiple constructors are expected to show up
  quickly once dogfooding starts (Milestone 7).
- Less immediately familiar to someone migrating from AutoFixture, who
  may expect a multi-constructor type to "just work" via the greedy
  heuristic — considered and accepted per the Decision Drivers above; this
  is an intentional product difference, not an oversight.

## Pros and Cons of the Options

### Single unambiguous constructor, diagnostic on ambiguity

- Good, because it's fully deterministic — no heuristic to reason about
  or be surprised by.
- Good, because it turns ambiguity into the same kind of compile-time
  diagnostic the generator already produces for other unsupported cases.
- Good, because it never silently does something the type's author didn't
  clearly signal.
- Bad, because it requires a disambiguation attribute to exist before a
  legitimately multi-constructor type can be composed at all.

### Greedy / most-parameters constructor

Picks the constructor with the most parameters, matching AutoFixture's
default behavior.

- Good, because it's familiar to anyone migrating from AutoFixture.
- Good, because it never blocks on ambiguity — there's always an answer.
- Bad, because "most parameters" is a heuristic that can pick a
  constructor the type's author didn't intend as primary (e.g. a
  test-only or deserialization-only constructor that happens to have more
  parameters).
- Bad, because it's exactly the kind of "clever fallback" the manifesto
  calls out as something to avoid.
- Bad, because it produces different composed results if a constructor
  overload set changes shape (adding a parameter to a secondary
  constructor could silently make it the new "winner").

### Primary-constructor-first, greedy fallback

Prefers a primary constructor or positional record constructor when one
exists; falls back to greedy selection among remaining ordinary
constructors otherwise.

- Good, because it matches idiomatic modern C# most of the time (a type
  with a primary constructor almost always intends it as the construction
  path).
- Good, because it reduces how often the greedy heuristic's downsides
  (see above) actually get triggered.
- Bad, because it still contains the greedy heuristic as a fallback,
  inheriting its unpredictability for any type without a primary
  constructor and more than one ordinary constructor.
- Bad, because it's a two-tier rule (prefer X, else heuristic Y) that's
  more complex to explain and diagnose than a single consistent rule.

## Links

- `docs/architecture.md`'s "Source-Generated Composition Plans" and
  "Generator responsibilities" sections.
- `docs/manifesto.md`'s "Predictability over magic" and "Diagnostics are a
  product feature" principles.
- `docs/mvp.md`'s Milestone 1 scope ("Constructor selection prototype",
  "Compile-time diagnostics for unsupported or ambiguous construction").

## Amendment 1 (2026-08-04): `CMP0001` observed against a real ambiguous BCL type, no change made

Milestone 7's dogfooding pass (`ncipollina/cosmere-tracker`, per
[ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md),
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md), Finding 7 of
[RESEARCH-0001](../research/0001-autofixture-comparison.md)) hit
`CMP0001` composing `HttpClient` directly (3 accessible constructors,
`ConstructorSelector.Select` correctly reports `AmbiguousConstructor` per
this ADR's own rule) — this ADR's "explicit disambiguation mechanism...
left to be designed when Milestone 1 actually needs it" is still
undesigned, and this is the first real evidence of a project needing it.

The evidence doesn't actually support designing that mechanism yet,
though. `HttpClient` is a BCL type `cosmere-tracker` doesn't own, so this
ADR's originally-anticipated `[CompositionConstructor]` attribute — an
attribute the type's *author* applies to a constructor — was never going
to close this specific case regardless of whether it ships; the real
candidate is generic disambiguation support for a *registered/external*
ambiguous type, a materially different (and undesigned) mechanism this
ADR never actually committed to. And the diagnostic itself only fired
while porting a capability (`ClientTestProfile`) that has zero real
pre-migration call sites in `cosmere-tracker` — no test needed to compose
`HttpClient` before this migration; the interface-wrapper workaround
(`IHttpClientProvider`, treated as a provider-resolved leaf, bypassing
constructor-selection entirely) already closes the one real case this
migration produced, at the cost that migration actually paid.

**No change to this ADR's decision.** Per ADR-0029's evidence-driven
restraint, a synthetic exercise (a capability preserved for hypothetical
future tests, not a real pre-existing call site hitting a wall) doesn't
justify designing a new disambiguation mechanism now. This is recorded
here as the evidence trail for a plausible future roadmap item — generic
support for disambiguating construction of a registered/external
ambiguous type — should a real pre-existing call site surface it later,
not as a decision to build that mechanism today.

## Amendment 2 (2026-08-24): the real pre-existing call site Amendment 1 anticipated has now surfaced

`alexa-vox-craft`'s migration (PLAN-0051 Task 10, per
[RESEARCH-0010](../research/0010-alexa-vox-craft-compono-ecosystem-migration.md)
§10) hit the same `HttpClient` `CMP0001` shape Amendment 1 recorded, but
this time as a genuine pre-existing call site: composing
`AlexaInteractionModelClient(HttpClient client, ...)` directly, the
natural way to write the migrated test, not a capability preserved for a
hypothetical future one. This clears the bar Amendment 1 explicitly said
the `cosmere-tracker` evidence didn't. **Still no change to this ADR's
decision** — per ADR-0029's process, evidence clearing that bar makes this
a roadmap candidate, recorded and classified in its own dedicated ADR
rather than folded into this one, since the registered/external-type
disambiguation mechanism Amendment 1 anticipated is a materially
different, not-yet-designed capability from anything this ADR decided. See
[ADR-0052](0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md).

## Amendment 3 (2026-08-25): explicit constructor selection ships - the rule genuinely changes, this ADR's core decision does not

[ADR-0052](0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md)'s
Part B (explicit constructor selection) is now `Accepted` and shipped -
`CompositionTypeRuleBuilder<T>.UseConstructor<T1, ...>()`. This ADR's
Decision Outcome, as originally written, stated the rule unconditionally:
"If a type has more than one accessible constructor and nothing else
disambiguates it, that is a compile-time diagnostic... rather than a
guess." That statement is **no longer wholly accurate** as of this
Amendment - claiming otherwise would misstate Compono's own shipped
behavior. The corrected rule:

> A type with exactly one accessible constructor still selects it
> automatically - unchanged. A type with more than one accessible
> constructor and **no explicit `UseConstructor<...>()` selection in
> scope** (compilation-wide - see ADR-0052) still reports `CMP0001`,
> unchanged. A type with more than one accessible constructor **and** an
> explicit selection in scope uses the selected constructor - Compono
> still composes that constructor's own parameters exactly as it would
> for an unambiguous type, recursively, through the same discovery graph.

**This is not a reversal of this ADR's Decision Outcome, and not the
disambiguation mechanism this ADR's original text anticipated.** The
original text presumed a future `[CompositionConstructor]`-style
attribute applied to the ambiguous type itself - product direction
explicitly rejected that shape during ADR-0052's design dive: a
test-composition library must not require production code to carry
test-only annotations, especially for a BCL/third-party type the consumer
doesn't own and can't annotate. `UseConstructor<...>()` lives entirely in
profile/composition-configuration code (`CompositionBuilder.For<T>()`),
never on the type being composed - satisfying this ADR's own "useful
failure is better than a clever fallback" and "predictability over
magic" principles exactly as originally reasoned: there is still no
heuristic anywhere in this rule, an unresolved ambiguity is still a
compile-time diagnostic, and the *only* new behavior is an explicit,
strongly-typed, source-generator-recognized way for a consumer to say
which constructor to use - see ADR-0052's own "Part A recommendation
under the corrected constraint" and "Real implementation spike results"
sections for the full design and proof.

**What this Amendment does not change:** `ConstructorSelector.Select`'s
own arity-based logic for a type with **no** explicit selection is
completely unmodified - single accessible constructor still selects
automatically, `>1` with no selection is still exactly today's `CMP0001`,
`0` accessible constructors is still `CMP0002`. `Register<T>` remains a
separate capability (supplying/controlling construction of `T` entirely)
and is explicitly **not** treated as a constructor-selection substitute -
ADR-0052's own real evidence (`AlexaInteractionModelClient`'s registered
`HttpClient`) proved `UseConstructor` cannot serve every case
`Register<T>` already does, and that gap (registration-aware discovery)
remains open, tracked separately in ADR-0052's own still-`Proposed` Part
A.

