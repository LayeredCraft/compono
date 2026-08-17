# [ADR-0045] Compono.TestDoubles: Configuration-Required Members for Non-Deterministic-Default Returns

**Status:** Accepted

**Date:** 2026-08-17

**Decision Makers:** Nick Cipollina, Claude (design deep dive)

## Context

[RESEARCH-0004](../research/0004-lightsaber-skill-testdoubles-v2-dogfood.md)
re-ran the `lightsaber-skill` dogfooding migration against the shipped
`Compono.TestDoubles` v2 package ([ADR-0044](0044-compono-testdoubles-v2-overloads-generics-verification.md),
`Accepted`, [PLAN-0044](../plans/0044-compono-testdoubles-v2.md), `Done`)
and found v2's overload/generic-method/verification work landed exactly as
designed — `ILogger<T>` now generates cleanly, direct proof the generic-
method Requirement works — but it wasn't the suite's actual dominant
blocker. Six of the suite's seven load-bearing interfaces
(`IResponseBuilder`, `IAmazonS3`, `ISkillMediator`, `IOptions<T>`,
`ILambdaContext`, `IHandlerInput`) are still rejected outright by
`CMP0025` ("Unsupported test-double return shape") — a rule that predates
ADR-0044 entirely (`Compono.TestDoubles` v1, [ADR-0043](0043-compono-generated-test-doubles-design.md)
Amendment 5, Finding K): any member (property or method, including
recursively through `Task<T>`/`ValueTask<T>`'s inner `T`) that returns a
non-nullable reference type with no deterministic default rejects the
*entire* interface at generation time, regardless of whether a test author
would have configured that specific member with `Returns(...)`/`Throws(...)`
before ever calling it. Practical result: zero tests in the suite can drop
`Compono.NSubstitute`, because every test using the now-working `ILogger<T>`
also uses at least one still-`CMP0025`-rejected interface.

This ADR does **not** reopen ADR-0044 or PLAN-0044 — both stand exactly as
`Accepted`/`Done`. Their three Requirements (overloaded members, return-
type-independent generic methods, minimal call verification) are real,
implemented, and validated. This is a new decision about a different,
pre-existing constraint that RESEARCH-0004's evidence shows is now the
more consequential one.

`CMP0025` is a single descriptor currently covering four distinct sub-
cases with genuinely different implementability properties (see
`TestDoubleAnalyzer.cs` around the return-shape checks): a by-ref return, a
pointer/function-pointer return, a ref-like (`ref struct`) return, and a
non-nullable reference return with no deterministic default. The first
three have no possible `ReturnConfig<T>` representation at all — a `ref
struct` cannot be used as a generic type argument in C#, and a by-ref
return has no storable value to hold — these are hard language
constraints, not missing-default gaps. The fourth sub-case is different:
the member is perfectly implementable (a plain property getter or method
body returning a `T`), Compono just doesn't know a *safe unconfigured*
value for it. This ADR is about that fourth sub-case only.

## Decision Drivers

- Real, evidenced dogfooding data (RESEARCH-0004), per
  [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
  evidence-over-prediction bias — not a hypothetical gap.
- Must not reintroduce what [ADR-0042](0042-compono-owned-source-generated-test-doubles.md)'s
  Non-Goals and ADR-0043 Finding K already rejected: manufacturing an
  arbitrary non-null value, or any reflection-based fallback (ADR-0001,
  "no reflection-by-default").
- AOT-safety is a hard requirement, proven not assumed (ADR-0001, the same
  standard PLAN-0044 Phase 3 already met for v2's three shapes together).
- Zero semantic drift for already-shipped behavior — a member that already
  has a safe deterministic default (`bool`, `int`, a nullable reference,
  `Task`, a known collection shape) must keep behaving exactly as it does
  today; this is an additive expansion of which interfaces can generate,
  not a rewrite of v1/v2's existing dispatch semantics.
- Minimal new runtime surface — prefer reusing `Compono`'s existing
  `ReturnConfig<T>`/`ReturnConfigBuilder<T>` state machinery
  (`src/Compono/ReturnConfig.cs`, `ReturnConfigBuilder.cs`) over inventing
  a parallel state type, if the existing one already fits.
- Diagnostic honesty — a compile-time diagnostic should describe what's
  actually true about the generated code (whether an interface generates
  at all; whether a specific member needs configuration before use), not
  conflate two different claims under one code.

## Considered Options

1. **Configuration-required member** — a member with no deterministic
   default still generates; an unconfigured invocation throws a new,
   clearly-messaged exception instead of the interface being rejected.
2. **Status quo** — keep `CMP0025` as whole-interface rejection for this
   sub-case, unchanged.
3. **Manufacture or compose a return value** — construct *some* non-null
   instance (via a public parameterless constructor, reflection, or
   recursive nested-double generation) rather than requiring
   configuration.
4. **Special-case certain return shapes** — e.g., default a fluent, self-
   returning method (`IResponseBuilder Speak(...)`) to `return this;`
   while every other non-nullable reference return stays configuration-
   required.

## Decision Outcome

Chosen option: **"Configuration-required member" (Option 1)**, with
`CMP0025` narrowed to keep meaning "whole-interface rejection" only for
the three genuinely unimplementable return shapes (by-ref, pointer, ref-
like), and a new diagnostic, `CMP0032`, introduced for the configuration-
required sub-case. Option 4's fluent self-return special case is
considered and explicitly **rejected** — see "Fluent self-returning
members" below; a self-returning method is configuration-required exactly
like any other non-nullable reference return, no special case.

### Why Option 1, and not the alternatives

**Option 3 (manufacture/compose a value) is rejected outright**, per the
Context section's own framing — this is not a close call. It would
require either runtime reflection (a public-parameterless-constructor
guess, violating ADR-0001's no-reflection-by-default default) or
recursively generating a nested double for every reference-typed return
(reintroducing the general-purpose object-composition scope ADR-0042's
Non-Goals explicitly excluded — `Compono.TestDoubles` is deliberately not
AutoFixture). Worse, a manufactured, default-valued `SkillResponse` (or
similar) is *more* likely to produce a silent false pass than a loud,
immediate exception — the failure mode gets worse, not better, exactly the
scenario ADR-0043 Finding K already rejected once.

**Option 4 (special-case fluent self-return) is rejected.** The generator
can only observe that a method's return type is syntactically identical to
the interface being doubled — it cannot know the member is *conventionally*
fluent (some interfaces declare a same-typed return that isn't meant to be
`this`, e.g. a factory-shaped method). Defaulting to `return this;` would
be a *behavioral* guess, categorically different from the existing neutral
defaults (`0`, `false`, `[]`, `Task.CompletedTask`) that are inert rather
than assertive. It's also unnecessary: Option 1 alone already unblocks
`IResponseBuilder` without it, and Option 4 was explicitly flagged as a
temptation to adopt "merely because it makes the dogfood pass greener" —
it doesn't need to be adopted for that, so it isn't.

**Option 2 (status quo) is directly falsified by RESEARCH-0004** — it's
the option that produced the evidence motivating this ADR in the first
place.

**Option 1 wins** because it's the only alternative that (a) doesn't
violate the "don't manufacture a value" principle, (b) doesn't need a
behavioral guess about interface semantics the generator can't verify, and
(c) is implementable almost entirely by *reusing* existing shipped
infrastructure — see "Member-level dispatch rule" below.

### Member-level dispatch rule

A member with no deterministic default gets exactly the dispatch order
requested and confirmed during this design pass:

```csharp
Task<SkillResponse> ISkillMediator.Send(SkillRequest request, CancellationToken cancellationToken)
{
    __send.RecordCall();

    if (__send.HasConfiguredException)
        throw __send.ConfiguredException;

    if (__send.HasConfiguredValue)
        return __send.ConfiguredValue;

    throw new global::Compono.TestDoubleNotConfiguredException(
        "ISkillMediator.Send(SkillRequest, CancellationToken) has no deterministic default and " +
        "must be configured before invocation - call Configure().Send(...).Returns(...) or " +
        "Configure().Send(...).Throws(...) first.");
}
```

This is **not a new dispatch shape** — it's the exact order every existing
generated member already checks (`RecordCall()`, then
`HasConfiguredException`, then `HasConfiguredValue`), with the *only*
change being what happens when neither is configured: today's deterministic-
default members return a computed default; a configuration-required member
throws instead. The backing field type is `Compono.ReturnConfig<T>` for
the member's own real return type `T` — unchanged from every other member,
because `ReturnConfig<T>`/`ReturnConfigBuilder<T>` already carry
`HasConfiguredValue`/`ConfiguredValue`/`HasConfiguredException`/
`ConfiguredException` generically, with no dependency on `T` having a
default at all. **No new runtime state type is needed** — only a new
exception type (see "Exception API" below).

### Interface generation: `CMP0025` narrowed, `CMP0032` introduced

`CMP0025` keeps its current meaning — whole-interface rejection, "this
leaf falls back to the ordinary runtime-provider path" — for exactly the
three return shapes with no possible `ReturnConfig<T>` representation at
all: a by-ref return, a pointer/function-pointer return, a ref-like
(`ref struct`) return. These stay genuinely unimplementable; nothing in
this ADR changes them.

A new diagnostic, **`CMP0032`** ("Test-double member requires explicit
configuration"), `DiagnosticSeverity.Info`, replaces `CMP0025` for the
fourth sub-case (non-nullable reference return, or property, with no
deterministic default). Unlike `CMP0025`, `CMP0032` is **member-scoped,
not whole-interface** — the interface still generates, every other member
keeps whatever disposition it already had, and `CMP0032`'s message makes
clear the member is real and usable, just requires configuration first
("...generates with a configuration-required member; call
`Configure().{1}(...)` before this member is invoked, or it throws
`TestDoubleNotConfiguredException`."). This mirrors the existing pattern
`CMP0022`/`CMP0029`/`CMP0030` already establish (a member-scoped info
diagnostic that doesn't reject the whole leaf) rather than inventing a
new diagnostic category.

This deliberately does **not** relax whole-interface rejection for any
other currently-unsupported shape — pointer parameters, `ref`/`out`/`in`
parameters without a sibling overload, unconstrained `T?` type parameters,
a generic method whose return type depends on its own type parameter, and
so on all keep exactly their current disposition. This ADR's scope is the
one specific gap RESEARCH-0004's evidence identified: a return-shape
problem, not a general "make more things generate" mandate.

### Properties

Identical treatment, via the property getter — `IOptions<LightsaberOptions>.Value`
and `ILambdaContext.AwsRequestId` (RESEARCH-0004's own acceptance cases)
both become configuration-required members once this ships:

```csharp
string ILambdaContext.AwsRequestId
{
    get
    {
        __awsRequestId.RecordCall();

        if (__awsRequestId.HasConfiguredException)
            throw __awsRequestId.ConfiguredException;

        if (__awsRequestId.HasConfiguredValue)
            return __awsRequestId.ConfiguredValue;

        throw new global::Compono.TestDoubleNotConfiguredException(
            "ILambdaContext.AwsRequestId has no deterministic default and must be configured " +
            "before invocation - call Configure().AwsRequestId().Returns(...) or " +
            "Configure().AwsRequestId().Throws(...) first.");
    }
}
```

Orthogonal to, and unaffected by, `CMP0027` (set-only property rejection)
and the existing get-only/`init` accessor-kind handling (ADR-0043
Amendments 7/9) — a property's *setter* story is untouched; this only
changes what an unconfigured *getter* does when it has no deterministic
default.

### Async returns (`Task<T>`/`ValueTask<T>`)

**No separate implementation is needed for this case.** `ReturnConfig<T>`
is already generic over the member's *real* declared return type — for
`ISkillMediator.Send` that's `Task<SkillResponse>` itself, not
`SkillResponse`; `Configure().Send(request).Returns(Task.FromResult(response))`
already configures the whole `Task<SkillResponse>` value today (see
`docs/packages/compono-testdoubles.md`'s own `CountAsync()` example, which
predates this ADR). `TestDoubleDefaults.TryGetDefaultExpression`'s
recursion into `Task<T>`'s inner `T` only mattered for computing a
*default* value — once "no default" means "configuration-required"
instead of "reject the interface," that recursion simply stops mattering
for this case: the member's dispatch body is `ReturnConfig<Task<SkillResponse>>`-shaped
exactly like every synchronous non-nullable-reference member, with no
`async`/`await` involved (the same "return an already-completed `Task`,
no state machine" shape v1/v2 already use throughout). `ValueTask<T>`
follows identically. This is confirmed by real test coverage (Phase 1),
not asserted from design alone — but no new *generator* code path is
predicted to be necessary, only new *test* coverage proving the existing
one behaves correctly for this shape.

This ADR makes **no change** to `Throws(...)`'s existing synchronous-throw
semantics (ADR-0043 Amendment 7) — the new
`TestDoubleNotConfiguredException` throws synchronously at invocation,
exactly like a configured `Throws(...)` already does for every member type,
sync or async.

### Fluent self-returning members

Covered under "Why Option 1, and not the alternatives" above — rejected.
`IResponseBuilder.Speak(...)` (and every other self-returning fluent
method) is configuration-required like any other non-nullable reference
return; a test that needs the fluent chain to keep working configures each
step's return, `Configure().Speak(text).Returns(mockResponseBuilder)`,
exactly as the existing NSubstitute-based tests already do today (see
RESEARCH-0004's cited `HandlerHelpers.CreateDefaultMockResponseBuilder()`
for the equivalent existing pattern).

### Exception API

`Compono.TestDoubleNotConfiguredException` — a new `sealed` exception type
in the core `Compono` package, alongside the existing
`TestDoubleVerificationException` and following its exact shape (a plain
message-only constructor, no reflection needed since the generated call
site supplies a fully literal string — interface name, member name, and
signature are all known at generation time):

```csharp
namespace Compono;

/// <summary>
/// Thrown when a generated test-double member with no deterministic default is invoked without
/// first being configured via <c>Configure().Member().Returns(...)</c> or <c>.Throws(...)</c>.
/// </summary>
public sealed class TestDoubleNotConfiguredException : Exception
{
    public TestDoubleNotConfiguredException(string message) : base(message)
    {
    }
}
```

The generated message names the interface, the member (with its real
parameter list, matching how overload-scoped diagnostics already identify
a member precisely), states the reason ("has no deterministic default"),
and states the fix (`Configure().Member(...).Returns(...)` /
`.Throws(...)`) — all supplied as generated string literals, matching
`TestDoubleVerificationException`'s existing "no reflection to build the
message" property.

### AOT

Native AOT/trimming remains a hard requirement, proven the same way
PLAN-0044 Phase 3 proved v2's three shapes together: a real
`dotnet publish -p:PublishAot=true` execution, not static analysis alone.
This ADR's implementation plan (PLAN-0045) includes extending
`test/Compono.TestDoubles.AotSmokeTest` to exercise at least one
configuration-required synchronous member, one property, and one
`Task<T>`-returning member — both the configured-success path and the
throws-when-unconfigured path — under a real AOT-published binary,
matching this repo's "prove it, don't assume it" standard
([ADR-0001](0001-source-generation-first.md)). The change itself is
expected to be trivially AOT-safe (a generated conditional branch plus a
generated `throw new TestDoubleNotConfiguredException(literalString)` —
no reflection, no dynamic code), but that expectation is a hypothesis to
verify, not a substitute for the real publish-and-run proof.

### Performance

Expected negligible — one extra `bool` field read and a branch on an
already-branchy dispatch body (every generated member already checks
`HasConfiguredException` then `HasConfiguredValue` before reaching its
final fallback; this only changes what the final fallback does). Per
[ADR-0034](0034-benchmark-suite-strategy-and-redesign.md)'s "benchmark
only if it measures a real risk, don't invent competitive benchmarks"
policy, this ADR does **not** call for a new benchmark class — if
implementation surfaces an actual measured concern, that's an amendment,
not a speculative benchmark added up front.

### Positive Consequences

- Unblocks generation for interfaces the ecosystem actually has —
  RESEARCH-0004's six blocked interfaces each become individually
  assessable rather than blanket-rejected; whether the double is
  *useful* for a given test still depends on whether that test configures
  the members it calls, which is now a per-test question instead of a
  permanently-closed door.
- Reuses 100% of the existing `ReturnConfig<T>`/`ReturnConfigBuilder<T>`
  state machinery — the only new runtime surface is one small exception
  type, minimizing implementation and review risk.
- `CMP0025`'s remaining scope (by-ref/pointer/ref-like) is now honestly
  described — a whole-interface-fallback diagnostic that only fires for
  shapes with literally no storable value, not conflated with "I don't
  know a good default."
- Sets up PLAN-0045 Phase 4's re-dogfood to test the real acceptance bar
  RESEARCH-0004 established — whether real tests can drop
  `Compono.NSubstitute`, not just how many interfaces generate.

### Negative Consequences

- A genuinely new test-double failure mode: an unconfigured invocation
  that would previously have meant "this interface never generated at
  all, you're using `Compono.NSubstitute` for it and know that going in"
  now means "this interface generated, but *this specific call* needed
  configuration you didn't provide" — a runtime surprise for a consumer
  who assumed "it compiled, therefore every path is safe." Mitigated by
  the exception message naming the exact fix, and by this being the same
  failure shape NSubstitute users already experience today for an
  unconfigured `Substitute.For<T>()` member returning a default-of-`T`
  that's often equally unhelpful — this isn't a worse experience than the
  status quo alternative, just a different one.
- Documentation surface grows: `CMP0025`'s narrowed scope and `CMP0032`'s
  new entry both need updates across `docs/packages/compono-testdoubles.md`,
  `docs/reference/diagnostics.md`, `skills/compono/references/diagnostics.md`,
  and `skills/compono/references/testdoubles.md` — tracked as its own
  PLAN-0045 phase so it doesn't lag the code that introduces it.

## Pros and Cons of the Options

### Option 1 — Configuration-required member

The interface still generates; a member with no deterministic default
throws `TestDoubleNotConfiguredException` if invoked before being
configured.

- Good, because it reuses existing `ReturnConfig<T>` machinery unchanged.
- Good, because it fails loudly and precisely at the exact call site that
  needed configuration, rather than failing silently (a wrong value) or
  failing globally (the whole interface never existing).
- Good, because it's directly evidenced to help against RESEARCH-0004's
  six blocked interfaces.
- Bad, because it introduces a new runtime failure mode consumers haven't
  seen from `Compono.TestDoubles` before (mitigated — see Negative
  Consequences).

### Option 2 — Status quo (keep `CMP0025` whole-interface rejection)

- Good, because it's zero additional implementation risk.
- Bad, because RESEARCH-0004 directly falsifies it as adequate for real
  usage — this is the option that produced the evidence motivating this
  ADR.
- Bad, because it conflates "cannot implement this member's C# body at
  all" with "can implement it, don't know a safe default" under one
  all-or-nothing rejection.

### Option 3 — Manufacture or compose a return value

- Good, because every generated double would behave more like a
  populated real object graph.
- Bad, because it requires reflection (violates ADR-0001) or recursive
  nested-double generation for arbitrary types (reintroduces the general-
  purpose object-composition scope ADR-0042's Non-Goals excluded).
- Bad, because a manufactured, default-valued object is more likely to
  produce a silent false pass than a loud configuration-required
  exception — a worse failure mode than either Option 1 or the status
  quo.

### Option 4 — Special-case certain return shapes (fluent self-return)

- Good, because it would make `IResponseBuilder`'s chained-builder pattern
  slightly more ergonomic by default.
- Bad, because the generator cannot verify a same-typed return is
  actually meant to be `this` — a behavioral guess, not a neutral
  default, and not one the analyzer can prove correct.
- Bad, because Option 1 alone already unblocks `IResponseBuilder` without
  it — the special case buys nothing this ADR's evidence actually needs.

## Amendment 1 (2026-08-17): `CMP0032` scoped to one diagnostic per interface, not one per member

Raised before Phase 0 implementation began, once this ADR's core decision
was confirmed: emitting `CMP0032` once per configuration-required *member*,
as the original Decision Outcome specified, risks real diagnostic noise on
a large real-world interface. `IAmazonS3` — one of this ADR's own
motivating interfaces — declares dozens of members; a meaningful fraction
of any AWS-SDK-shaped interface returns a non-nullable `string`/response
type, so a per-member `CMP0032` could emit dozens of informational
diagnostics for a single `Configure()` call site, even when a given test
only ever invokes or configures a handful of them. This is exactly the
"noisy to consume on real production interfaces" failure mode a Compono
diagnostic is supposed to avoid — an Info diagnostic that shows up by the
dozen trains consumers to ignore the whole category, defeating its own
purpose.

**Alternatives considered:**

- **Info per member (original Decision Outcome)** — most precise (each
  diagnostic names exactly one member), but scales linearly with an
  interface's member count regardless of what a given test actually
  touches — the noise problem above.
- **No compile-time diagnostic at all** — zero noise, but a real
  departure from this repo's established diagnostic philosophy: every
  other unsupported/limited-support shape (`CMP0020`-`CMP0031`) gets
  *some* generation-time visibility rather than a silent gap a consumer
  only discovers via `docs/*.md` or a runtime surprise. Going fully silent
  for this one case would be inconsistent with that pattern for no reason
  stronger than "avoid noise," when a less drastic fix (below) achieves
  the same noise reduction without giving up discoverability entirely.
- **Interface-scoped, count-only summary (chosen)** — one `CMP0032` per
  interface, not per member, firing once even if that interface has many
  configuration-required members. Its message states *how many* members
  require configuration and points at
  `docs/packages/compono-testdoubles.md`'s "Configuration-required
  members" section, without enumerating every member by name — the exact
  member identity is exactly what `TestDoubleNotConfiguredException`
  already supplies precisely, at the one point it actually matters (a
  real unconfigured invocation), so the diagnostic doesn't need to
  duplicate that detail to stay useful. This keeps compile-time
  discoverability (a consumer scanning build output learns "this
  interface has N members you may need to configure") while capping
  noise at one line per interface regardless of that interface's size.
- **Capped member-name enumeration** ("first 5 names, and N more") —
  more informative than the count-only summary, but adds truncation-
  formatting complexity for a benefit the runtime exception already
  covers on demand. Not chosen — the count-only summary is simpler and
  already resolves the noise concern; this option's extra detail wasn't
  worth its extra implementation surface.

**Decision:** `CMP0032` becomes **interface-scoped** (cardinality: one
diagnostic per interface, fired once even if that interface has many
configuration-required members), not member-scoped. This is a diagnostic-
*cardinality* change only — not to be confused with `CMP0025`'s
whole-interface-*rejection* semantics, which `CMP0032` still never has:
every configuration-required member keeps generating and keeps its own
`Configure()`/`Verify()` surface; only how many `CMP0032` info messages a
build emits changes. `TestDoubleAnalyzer` collects every configuration-
required member found while walking an interface's members (the same pass
that already decides per-member disposition) and, if the collected count
is nonzero, emits one `CMP0032` for the interface with that count in its
message — mirroring the existing "collect across the member-walk, emit
one summary diagnostic" shape `CMP0028` (conflicting test-double metadata
across discoveries) already uses in this codebase, rather than inventing a
new aggregation mechanism.

This is a refinement of the diagnostic-UX detail only — the underlying
decision (a member with no deterministic default generates as
configuration-required rather than rejecting its interface) is unchanged.
PLAN-0045's Phase 0 task list is updated in the same pass as this
Amendment to reflect the interface-scoped shape.

## Amendment 2 (2026-08-17): corrected non-overloaded `Configure()` call-site examples

Codex review on the PR carrying this ADR caught two of its own examples
using a call shape that doesn't actually compile. Recorded here rather
than edited in place, per this ADR's own immutability rule and this
repo's established precedent for exactly this class of fix (ADR-0044
Amendment 9: "corrected overload-selection example").

**Finding:** `ISkillMediator.Send` has no sibling overload, so per the
existing generated-`Configure()`-extension convention this ADR itself
relies on elsewhere (an overloaded member's extension takes its real
discriminator parameter list; a **non**-overloaded member's extension is
always zero-argument — exactly the convention
`docs/packages/compono-testdoubles.md`'s own `CountAsync()` example
already documents, and the same `is_overloaded` branch `TestDouble.scriban`
already implements), the real generated call is `Configure().Send()`, not
`Configure().Send(request)`. Two places in this ADR's original text used
the wrong (parameterized) form:

- The "Member-level dispatch rule" example's thrown-exception hint text
  read `Configure().Send(...).Returns(...)` / `Configure().Send(...).Throws(...)`
  — corrected reading: `Configure().Send().Returns(...)` /
  `Configure().Send().Throws(...)`.
- The "Async returns" section's example read
  `Configure().Send(request).Returns(Task.FromResult(response))` —
  corrected reading: `Configure().Send().Returns(Task.FromResult(response))`.

An *overloaded* configuration-required member's real generated message
and call site would instead show that member's real discriminator
argument list, matching how `Configure()` already works for any
overloaded member today (ADR-0044 Amendment 1) — this Amendment doesn't
change that established convention, only corrects this ADR's own
non-overloaded example against it.

Documentation/example correction only — no change to the dispatch-order
rule, the `CMP0032` decision (Amendment 1), or PLAN-0045's scope. When
Phase 0 implementation actually generates a configuration-required
member's exception message, it should use the member's real generated
call shape (zero-argument if not overloaded, its real parameter list if
it is) rather than either of the incorrect literal strings above.

## Amendment 3 (2026-08-17): a member with no configuration surface for an unrelated reason stays whole-interface-rejected, not configuration-required

Codex review on the PR carrying this ADR (before Phase 0 implementation
began) caught a gap in the Decision Outcome: it describes configuration-
required treatment as applying to "a member with no deterministic
default," without qualifying that against a member that *also* has no
`Configure()`/`Verify()` surface for a separate, unrelated reason.

**Finding — combining "no deterministic default" with "no configuration
surface" produces an unconfigurable member that throws forever, which is
worse than today's whole-interface rejection.** Three existing shapes
already withhold a member's configuration surface independently of its
return type: a diamond-colliding identity (the same signature reached
through two different base interfaces), a same-named zero-argument-
extension collision (`CMP0029`), and an overloaded `ref`/`out`/`in`
parameter (`CMP0030`, which withholds only that one overload's surface
while its sibling keeps theirs). If such a member's return type *also*
has no deterministic default, naively applying this ADR's Decision
Outcome would generate a member with `RequiresConfiguration = true` but
no `Configure()` extension to ever satisfy it — every invocation throws
`TestDoubleNotConfiguredException` unconditionally, with no way to stop
it. That's strictly worse than today's shipped behavior, which already
whole-interface-rejects this exact combined shape via `CMP0025` (the
return-type default check runs unconditionally, before any of the three
surface-withholding checks get a chance to matter) — a consumer at least
learns at compile time that the interface doesn't generate, instead of
discovering an unconfigurable member at runtime.

**Corrected:** configuration-required treatment (this ADR's Option 1)
applies **only when the member would otherwise receive a real, usable
configuration surface** — i.e., only when `HasConfigurationSurface` would
independently evaluate to `true` for that member (unaffected by this
ADR's own change). A member combining "no deterministic default" with "no
configuration surface for an unrelated reason" gets **no change from this
ADR** — it stays exactly as it is today, whole-interface `CMP0025`
rejection. This preserves the one property every configuration-required
member must have: it's always possible to make it stop throwing, by
configuring it.

Mechanically, `TestDoubleAnalyzer` already computes each of the three
surface-withholding conditions (`isDiamondCollision`, `isZeroArgCollision`,
`hasRefOutInParameter` for methods) before reaching the return-type
default check for a method; for a property, the same two collision checks
(`isDiamondCollision`, `isZeroArgCollision`) are likewise already computed
earlier in its own branch. The fix is to consult whichever of these is
already in scope at the default-lookup-failure point, not to add new
detection logic — the gate is "would this member have had a surface
anyway," using data the analyzer already has.

PLAN-0045's Phase 0 task list is updated in the same pass as this
Amendment: the `TestDoubleAnalyzer.cs` task now states this gate
explicitly, and a new regression-test task covers exactly this
combination (an overloaded `ref`/`out`/`in` member, and a diamond-
colliding member, each with a no-default return type on the same
interface as other genuinely configuration-required members) — confirming
`CMP0025` still fires unchanged for these, unaffected by every other
member's new disposition.

## Amendment 4 (2026-08-17): `CMP0025`'s message text is not narrowed — only the condition for reaching it changes

Codex review caught a direct contradiction this Amendment's own predecessor
introduced: the original Decision Outcome ("Interface generation:
`CMP0025` narrowed, `CMP0032` introduced") states `CMP0025`'s message
narrows to describe only the three genuinely-unimplementable return
shapes (by-ref, pointer, ref-like) — but Amendment 3, immediately above,
requires `CMP0025` to keep firing, unchanged, for a member whose return
type has no deterministic default *and* which also lacks a configuration
surface for an unrelated reason (a diamond collision, a zero-argument-
extension collision, an overloaded `ref`/`out`/`in` parameter). If
`CMP0025`'s message text were actually narrowed to only three shape
descriptions, firing it for that fourth, Amendment-3-preserved case would
either use an inaccurate reason or require a second descriptor — exactly
the contradiction Codex flagged.

**Corrected:** `CMP0025`'s descriptor and message text are **not
narrowed at all** — `UnsupportedTestDoubleReturnShape` keeps describing
all four of its original shape sub-cases verbatim, unchanged code and
unchanged text, including "a non-nullable reference type with no
deterministic default." What changes is only the **condition** under
which the analyzer reaches that fourth branch for a given member:

- A member with no deterministic default that would **have** a real
  configuration surface (the ordinary case) no longer reaches `CMP0025`
  at all — it takes the new configuration-required path (`CMP0032`,
  Amendment 1) instead.
- A member with no deterministic default that would **not** have a real
  configuration surface anyway (Amendment 3's combined-shape case) still
  reaches `CMP0025`, with its existing, unnarrowed "a non-nullable
  reference type with no deterministic default" message — completely
  unchanged from today's shipped text and behavior for that specific
  combination.

So `CMP0025`'s scope is unchanged in the literal diagnostic-descriptor
sense (same four shape descriptions, same message format) — what
actually narrows is *how often* a member reaches the fourth branch, not
what the branch says when it's reached. This ADR's earlier "Interface
generation" section's claim that `CMP0025`'s message narrows is
superseded by this Amendment; the code-level task list (PLAN-0045 Phase
0) is corrected in the same pass to stop describing a message-text
change and instead describe the condition change this Amendment states.

## Amendment 5 (2026-08-17): the object-member-collision check is a fourth surface-withholding condition, not covered by Amendment 3's named gate — but already safe by construction

Codex review caught that Amendment 3's gate — "would `HasConfigurationSurface`
independently be `true`," checked via `isDiamondCollision`/
`isZeroArgCollision`/`hasRefOutInParameter` — omits a fourth existing
condition that also withholds a member's configuration surface: the
object-member-collision check (`TestDoubleObjectMemberCollision`,
`CMP0024` — a method shaped like `ToString`/`GetHashCode`/`GetType`/
`Equals` colliding with `object`'s own member). A method combining "no
deterministic default return" with an object-member collision (e.g. a
hypothetical `Stream ToString()`) would, under Amendment 3's gate as
literally stated, be provisionally marked configuration-required (none of
the three named flags are true), before the object-collision check —
which runs *later* in `TestDoubleAnalyzer`'s per-member sequence, and is
itself gated on `hasConfigurationSurface` being true — has a chance to
run and reject it via `CMP0024`.

**Finding — this is already safe by construction, but only because of an
invariant this ADR hadn't stated explicitly.** `TestDoubleAnalyzer`'s
`Failure(...)` helper (`TestDoubleAnalyzer.cs:995-996`) constructs a
brand-new `DiscoveredTestDoubleInfo` with an *empty* member list,
completely independent of whatever `members`/`infoDiagnostics`/the
per-interface configuration-required count had already accumulated in the
enclosing method before `Failure(...)` was called. Every rejection in
`TestDoubleAnalyzer`, including the object-collision check, is a
`return Failure(...)` statement — so if the object-collision check fires
*after* a member was provisionally marked configuration-required, that
provisional marking is discarded along with everything else the moment
`Failure(...)` returns; the whole interface rejects via `CMP0024`,
unchanged from today, exactly as if the provisional marking never
happened. The two states can't both survive to the final result — only
one `return` statement ever executes for a given `Analyze` call.

**Decision:** no design change — Amendment 3's gate doesn't need a fourth
named flag, because the object-collision check's own unconditional
`Failure(...)` already makes any upstream provisional marking moot. This
Amendment exists to state that invariant explicitly (so a future reader —
including whoever implements Phase 0 — doesn't have to re-derive it from
`Failure(...)`'s implementation) and to add regression coverage proving
it empirically rather than resting on this Amendment's own reasoning
alone. PLAN-0045's Phase 0 combined-shape regression tests gain a third
case: an object-member-collision-shaped method (`ToString`/`GetHashCode`/
`GetType`/`Equals`) with a no-default return type, confirming `CMP0024`
still fires unchanged and `CMP0032`'s count is unaffected.

## Amendment 6 (2026-08-17): withdraw Amendment 5's "already safe" conclusion — the object-collision predicate must be hoisted into the gate, not left to a downstream `Failure(...)`

Codex review caught that Amendment 5's own reasoning was wrong, using
Amendment 5's own evidence against it. Withdrawn, not edited in place,
per this ADR's immutability rule — the same "withdraw a previous
Amendment's conclusion" pattern ADR-0044 Amendment 9 already used in this
repo.

**Finding — Amendment 5 mischaracterized *today's* disposition for the
combined shape it analyzed.** `TestDoubleAnalyzer`'s per-member checks run
in a fixed sequence, and the *first* one to fail wins (via an immediate
`return Failure(...)`). Today, the return-type default-lookup check
(`TestDoubleAnalyzer.cs:453-456`) runs *before* the object-collision check
(lines 524-555) — so a method combining "no deterministic default" with
an object-member-collision shape (`ToString`/`GetHashCode`/`GetType`/
`Equals`) is rejected **today** via `CMP0025`, not `CMP0024` — the
object-collision check never even runs, because the return-type check
already returned. Amendment 5 claimed the *opposite*: that letting
execution continue past the (now-gated) return-type check to reach the
object-collision check later "confirms it's already safe by construction
... unchanged from today." That's backwards. Under Amendment 3's gate as
stated, this combined member would newly reach the object-collision check
(since none of Amendment 3's three named flags apply to it) and get
rejected via `CMP0024` **instead of** `CMP0025` — a real, consumer-visible
diagnostic-identity change for an unchanged input shape, exactly the kind
of stability regression `AGENTS.md`'s diagnostic-consistency guidance
rules out. `Failure(...)` discarding provisional local state (Amendment
5's actual finding) is true and irrelevant to this specific claim — it
explains why no member ever ends up in an inconsistent *partial* state,
not which diagnostic code the consumer ultimately sees.

**Corrected:** the object-member-collision predicate must be evaluated
*before* the return-type default-lookup check, as a fourth named
condition in Amendment 3's gate — not left to the object-collision
check's own later, unconditional `Failure(...)` to "catch" retroactively.
Every input the collision check's condition depends on
(`hasConfigurationSurface`, `method.IsGenericMethod`, `isOverloaded`,
`method.Name`, `method.Parameters`) is already available earlier in
`TestDoubleAnalyzer`'s per-member sequence (before line 449) — the fix is
to hoist the *predicate itself* (would this method's name/arity collide
with `object`, exactly the same logic the existing check at lines 524-555
already computes) into a boolean evaluated alongside
`isDiamondCollision`/`isZeroArgCollision`/`hasRefOutInParameter`, reusing
that single computed value both at the new gate and at the object-
collision check's original location (replacing its own ad hoc
re-computation there) — not duplicating the logic in two places that
could drift apart. With this fix, the combined shape keeps its exact
current `CMP0025` disposition, unchanged, matching every other combined
shape Amendment 3 already covers.

PLAN-0045's Phase 0 analyzer task and its combined-shape regression test
are corrected in the same pass: the object-collision case now asserts
`CMP0025` (not `CMP0024`, as Amendment 5 incorrectly stated), and the
analyzer task describes hoisting the predicate rather than relying on
`Failure(...)`'s discard behavior.

## Amendment 7 (2026-08-17): the object-collision hoist (Amendment 6) is a method-only fix — a property's object-collision check already runs before its own return-type check, unaffected either way

Codex review caught that Amendment 6's fix, and this ADR's Goal-level
description of it, don't distinguish methods from properties — and the
two shapes have *opposite* check ordering today, so the fix that's
necessary for one is a no-op for the other.

**Finding — for a property, the object-collision check already runs
*before* the return-type default-lookup check, the reverse of a method's
ordering.** `TestDoubleAnalyzer`'s property branch checks
`property.Name is "ToString" or "GetHashCode" or "GetType"`
(`TestDoubleAnalyzer.cs:723-737`, unconditional on the property's return
type) *before* it ever reaches the return-type default-lookup check
(lines 766-769). A property named `ToString`/`GetHashCode`/`GetType`
therefore already rejects via `CMP0024` today, regardless of whether its
type has a deterministic default — the return-type check for that
property is never even reached, with or without this ADR's changes.
Amendment 6's fix (hoisting the object-collision predicate ahead of the
return-type check) is specific to the *method* branch, where the
ordering is reversed (return-type check first, object-collision check
later) — applying it to properties would be redundant at best, and
describing it in shape-agnostic language risked an implementer "fixing"
the property branch's ordering to match the method branch's, which would
be a real regression: it would flip a colliding property's diagnostic
from today's `CMP0024` to `CMP0025`.

**Corrected:** Amendment 6's gate applies to the *method* branch only.
The property branch needs no corresponding change — its existing
check order already produces the correct, unchanged `CMP0025`-never
disposition (always `CMP0024` for a colliding property name, independent
of return-type default) with zero code change. PLAN-0045's Phase 0
analyzer task and Goal section are corrected in the same pass to state
this explicitly, so Phase 0's implementation and regression tests don't
touch the property branch's check order at all.

## Links

- [RESEARCH-0004](../research/0004-lightsaber-skill-testdoubles-v2-dogfood.md) —
  the dogfooding evidence this ADR responds to.
- [ADR-0044](0044-compono-testdoubles-v2-overloads-generics-verification.md) —
  the v2 work this ADR does not reopen; stays `Accepted` unchanged.
- [ADR-0043](0043-compono-generated-test-doubles-design.md) Amendment 5,
  Finding K — the v1 origin of `CMP0025`'s current whole-interface scope,
  narrowed (not reversed) by this ADR.
- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) — the
  Non-Goals (no general object composition, no reflection) that rule out
  Option 3.
- [ADR-0001](0001-source-generation-first.md) — the no-reflection-by-
  default rule, and the "prove it, don't assume it" AOT-verification
  standard this ADR's implementation plan must meet.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the evidence-over-prediction bias this ADR follows, same as ADR-0044.
- [ADR-0034](0034-benchmark-suite-strategy-and-redesign.md) — the
  benchmark-only-if-real-risk policy this ADR's "Performance" section
  follows.
- `src/Compono/ReturnConfig.cs`, `ReturnConfigBuilder.cs` — the existing
  state machinery this ADR reuses unchanged.
- [PLAN-0045](../plans/0045-testdoubles-configuration-required-members.md) —
  the phased implementation plan for this decision.
