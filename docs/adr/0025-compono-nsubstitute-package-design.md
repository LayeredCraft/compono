# [ADR-0025] Compono.NSubstitute Package Design

**Status:** Accepted

**Date:** 2026-07-31

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

[ADR-0024](0024-public-provider-extensibility-model.md) settles the mechanism a
package like `Compono.NSubstitute` plugs into: `ICompositionValueProvider`,
registered via `CompositionBuilder.AddTestDoubleProvider(...)`, compiled into
stage 6. This ADR settles everything specific to the package itself —
`docs/mvp.md`'s Milestone 5 scope: "Test-double provider contract, Interface
substitutes, Optional abstract-class substitutes, Shared substitute reuse,
Integration-specific configuration, Clear diagnostics when substitution is
unsupported," and its explicit non-goals: "Recursive auto-configuration of
substitute members, NSubstitute API wrappers, Pinning NSubstitute versions in the
core package." `docs/public-api.md` already sketches the intended activation
shape:

```csharp
builder.UseNSubstitute();

builder.UseNSubstitute(options =>
{
    options.SubstituteAbstractClasses = false;
});
```

This ADR turns that sketch into a real design: what `NSubstituteProvider` actually
matches (interface, abstract class, delegate — the "delegate handling" and
"abstract-class support"/"interface substitution" items this milestone's design
task called out explicitly), what `NSubstituteOptions` exposes, how "shared
substitute reuse" is satisfied (answer: entirely by ADR-0024/existing engine
mechanism, no new code), and what "clear diagnostics when substitution is
unsupported" means concretely.

## Decision Drivers

- `docs/mvp.md`'s explicit non-goal: "Recursive auto-configuration of substitute
  members" — `NSubstituteProvider` produces a bare substitute via NSubstitute's own
  `Substitute.For<T>(...)`/equivalent API and returns it; it does not call
  `context.Resolve<T>()` to configure the substitute's own members/return values.
- `docs/mvp.md`'s explicit non-goal: "NSubstitute API wrappers" — this package
  exposes activation (`UseNSubstitute`) and configuration (`NSubstituteOptions`)
  only; it does not re-expose or wrap NSubstitute's own `Arg`/`Received`/`Returns`
  surface, which a consumer already uses directly against the composed substitute.
- `docs/mvp.md`'s explicit non-goal: "Pinning NSubstitute versions in the core
  package" — trivially satisfied by [ADR-0024](0024-public-provider-extensibility-model.md)'s
  design: `Compono` core has no reference to `NSubstitute` at all; only
  `Compono.NSubstitute` does, per the existing package-dependency diagram.
  `Compono.NSubstitute`'s own `.csproj` still needs *some* version constraint on
  its `PackageReference`, but that's ordinary package versioning, not a core-package
  pin.
- Coding standard: exceptions are for the unexpected — an ordinary "this type isn't
  substitutable" outcome must be `NotHandled`, not a thrown exception (see
  [ADR-0024](0024-public-provider-extensibility-model.md)'s Provider Failure
  Semantics).
- `design-decisions.md` rule 4/[ADR-0001](0001-source-generation-first.md): no
  reflection-based fallback *in core*. `Compono.NSubstitute`'s own reliance on
  NSubstitute's internal (reflection/codegen-heavy) proxy generation is an
  integration-package concern this ADR treats as accepted, per this milestone's
  own explicit framing, not a relaxation of that rule.

## Considered Options

### What `NSubstituteProvider` treats as substitutable

1. **Defer entirely to what NSubstitute's own public API can construct**: any
   interface type, any delegate type, and (only if
   `NSubstituteOptions.SubstituteAbstractClasses` is `true`) any non-sealed
   abstract class — matching NSubstitute's own `Substitute.For<T>()` capability
   surface as closely as a static `Type` check reasonably can, rather than
   Compono re-deriving its own narrower notion of "substitutable."
2. **Interfaces only for the MVP** — no delegate or abstract-class support at all,
   deferring both to a later milestone.
3. **Interfaces plus abstract classes only** — omitting delegate types, which
   `docs/mvp.md`'s own scope list doesn't explicitly name (only this design task's
   investigation list does).

### Provider failure/diagnostics for an unsupported type

1. **Static, cheap pre-check; `NotHandled` for anything that doesn't match** — the
   provider never calls into NSubstitute at all for a type it can already tell,
   from `Type` reflection alone, it can't substitute (a sealed concrete class, a
   struct, `string`, etc.). The resulting "nothing could satisfy this" diagnostic
   is the engine's own existing generic failure message
   (`CompositionContext`'s `"No registration, configuration rule, semantic
   provider, test-double provider, built-in provider, or generated plan could
   satisfy '{type}'."`), which already names the exact failed type and its full
   path — no NSubstitute-specific message needed for the common case.
2. **A custom `NSubstituteDiagnosticException`** thrown for any unsubstitutable
   type, wrapping/replacing the engine's own diagnostic.

### Options plumbing

1. **A plain mutable options class, `NSubstituteOptions`, configured via
   `Action<NSubstituteOptions>? configure`** on `UseNSubstitute`, matching
   `UseBogus`'s existing sketch in `docs/public-api.md` exactly — no `IOptions<T>`/
   Microsoft.Extensions.Options dependency (core has none, and this package
   doesn't need one either; `coding-standards.md`'s `IOptions<T>` section applies
   only "if a configuration class is bound via `IOptions<T>`," which nothing here
   does).

## Decision Outcome

### Substitutable shapes — Option 1, defer to NSubstitute's own capability

```csharp
internal static bool IsSubstitutable(Type requestedType, NSubstituteOptions options) =>
    requestedType.IsInterface
    || typeof(Delegate).IsAssignableFrom(requestedType)
    || (options.SubstituteAbstractClasses && requestedType.IsAbstract && !requestedType.IsSealed && !requestedType.IsInterface);
```

**Corrected by Amendment 1 (below).** This original sketch has two defects a
pre-implementation review caught: the delegate check also matches the non-
substitutable framework base types `Delegate`/`MulticastDelegate` themselves, and
storing/reading a caller-owned mutable `NSubstituteOptions` directly conflicts with
[ADR-0024](0024-public-provider-extensibility-model.md)'s immutable-provider-
registration guarantee. See Amendment 1 for the corrected shape; this section's
prose (substitutability rules, precedence, rationale) is otherwise unchanged and
still accurate.

- **Interfaces**: always substitutable (`RequestedType.IsInterface`) — the
  milestone's headline case, and the pattern no closed-set `.For<T>()` rule could
  express (`docs/architecture.md`'s Open Architectural Decisions entry this
  milestone resolves).
- **Delegates**: always substitutable
  (`typeof(Delegate).IsAssignableFrom(requestedType)`) — NSubstitute natively
  supports `Substitute.For<TDelegate>()`; this design task's investigation list
  named delegate handling explicitly, `docs/mvp.md`'s non-goals don't exclude it,
  and it costs one extra branch in an already-cheap static check. Delegate
  substitution is included in Milestone 5's scope on that basis.
- **Abstract classes**: substitutable only when
  `NSubstituteOptions.SubstituteAbstractClasses` is `true` (default `true`, per
  `docs/public-api.md`'s existing sketch showing it explicitly set to `false`
  as the noteworthy case) — `docs/mvp.md`'s own wording, "Optional abstract-class
  substitutes," names this as configuration, not an always-on behavior.
- **Everything else** (a sealed concrete class, a struct, `string`, a generic type
  parameter already closed to something else entirely) — `NotHandled`,
  unconditionally, with no attempt to call into NSubstitute at all. This is a
  purely static check; it never allocates, never reflects beyond the handful of
  `Type` property reads already shown above, and runs before any real
  substitute-creation work.
- **Open generic behavior**: does not arise, per [ADR-0024](0024-public-provider-extensibility-model.md) —
  `CompositionProviderRequest.RequestedType` is always closed
  (`IRepository<Customer>`, never `IRepository<>`), so `Substitute.For<T>()` is
  always called (indirectly, via NSubstitute's non-generic `Substitute.For(Type[],
  object[])` overload, since `Compono.NSubstitute` only has a runtime `Type`, not a
  compile-time generic parameter) with a fully closed type either way.

```csharp
public sealed class NSubstituteProvider : ICompositionValueProvider
{
    private readonly NSubstituteOptions _options;

    public NSubstituteProvider(NSubstituteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (!IsSubstitutable(request.RequestedType, _options))
            return CompositionProviderResult.NotHandled;

        return CompositionProviderResult.Handled(Substitute.For([request.RequestedType], []));
    }
}
```

**Corrected by Amendment 1 (below):** this constructor retains a reference to the
caller-supplied, mutable `NSubstituteOptions` instance — since `NSubstituteProvider`
is `public`, a caller holding that same `options` reference can mutate it after
`UseNSubstitute`/construction, silently changing this provider's substitution
behavior after the fact. See Amendment 1 for the snapshotting fix.

No call to `context.Resolve<T>()` anywhere in this provider — confirming the
"recursive auto-configuration of substitute members" non-goal directly in the
shape of the code, not just as a stated intent. A bare substitute is returned
exactly as NSubstitute constructs it; a consumer configures its behavior
(`Returns`, `When`/`Do`) in their own test body against the composed instance,
same as any hand-written `Substitute.For<T>()` call today.

### Diagnostics — Option 1, static pre-check + the engine's existing failure message

No NSubstitute-specific exception type is introduced. `docs/mvp.md`'s "clear
diagnostics when substitution is unsupported" requirement is satisfied by the
static pre-check above returning `NotHandled` promptly (rather than attempting and
failing inside NSubstitute's own code) combined with the engine's own existing
terminal diagnostic — `CompositionContext`'s stage-9 failure message already names
the exact failed type and its full composition path
(`docs/architecture.md`'s Diagnostics example), which is precisely "clear
diagnostics" for the common case (a consumer accidentally requests a sealed class
where they meant an interface, say). The rarer residual case — NSubstitute's own
`Substitute.For(...)` throwing for a type that passed the static check but that
NSubstitute itself can't actually proxy for some internal reason — propagates
uncaught, per [ADR-0024](0024-public-provider-extensibility-model.md)'s Provider
Failure Semantics (an accepted, explicitly-stated limitation there, not
re-litigated here). No design attempts to catch and re-wrap that residual case
into a `CompositionException` for Milestone 5; revisit only if real usage shows
this rough edge actually matters in practice.

### Shared substitute reuse — no new code

Satisfied entirely by [ADR-0024](0024-public-provider-extensibility-model.md)'s
existing-engine-mechanism answer: a `[Shared] IRepository` parameter that
`NSubstituteProvider` satisfies is stored into the row's `CompositionScope`
exactly like any other stage's successful shared result, and a later ordinary
(non-`[Shared]`) request for the same type — including a generated plan's own
constructor parameter — transparently reuses it, per
[ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
unconditional stage-2 read. `Compono.NSubstitute` contributes zero code toward this
requirement; it falls out of registering into stage 6 at all.

### Configuration — Option 1, plain options class

```csharp
public sealed class NSubstituteOptions
{
    /// <summary>
    /// Whether an unsealed abstract class is substitutable, in addition to every interface and
    /// delegate type. Defaults to <see langword="true"/>.
    /// </summary>
    public bool SubstituteAbstractClasses { get; set; } = true;
}
```

```csharp
namespace Compono;

public static class CompositionBuilderExtensions
{
    extension(CompositionBuilder builder)
    {
        public CompositionBuilder UseNSubstitute() => builder.UseNSubstitute(static _ => { });

        public CompositionBuilder UseNSubstitute(Action<NSubstituteOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var options = new NSubstituteOptions();
            configure(options);
            return builder.AddTestDoubleProvider(new NSubstituteProvider(options));
        }
    }
}
```

Matches `docs/public-api.md`'s existing sketch exactly. `NSubstituteProvider`'s own
options are constructed and owned entirely inside `Compono.NSubstitute` — core
`Compono` never sees `NSubstituteOptions`, only the already-configured
`ICompositionValueProvider` instance `AddTestDoubleProvider` receives.

### Package boundary

`Compono.NSubstitute` depends on `Compono` and `NSubstitute` only, matching the
existing package-dependency diagram (`docs/architecture.md`) exactly —
`NSubstituteProvider`/`NSubstituteOptions`/the `UseNSubstitute` extension are the
package's entire public surface for Milestone 5. `Compono.NSubstitute` never
implements or references `Compono`'s internal `ICompositionProvider` — only the
public `ICompositionValueProvider` [ADR-0024](0024-public-provider-extensibility-model.md)
defines.

### Positive Consequences

- Matches `docs/mvp.md`'s exit criterion directly: "a typical service test can
  receive a shared substitute, a composed system under test, and a composed
  request with no manual setup" — the `[Shared] IRepository` example in
  `docs/public-api.md`'s xUnit v3 Experience section already demonstrates exactly
  this, and needs zero code change to keep working once `NSubstituteProvider`
  exists.
- No new diagnostic mechanism, no new exception type — reuses the engine's
  existing, already-good failure message.
- `IsSubstitutable`'s static check is trivially unit-testable in isolation, with
  no NSubstitute call involved for the negative cases.

### Negative Consequences

- Delegate substitution wasn't in `docs/mvp.md`'s own explicit scope list before
  this ADR — a small, deliberate scope addition, justified above; flagged here so
  it isn't mistaken for scope creep discovered later. `docs/mvp.md`'s Milestone 5
  section should be updated to mention it explicitly (a `PLAN-0005` doc task).
- The residual "NSubstitute itself throws for a superficially-matching type"
  case has no bespoke diagnostics — an accepted rough edge, not solved here (see
  Diagnostics above).

## Pros and Cons of the Options

### Substitutable shapes — defer to NSubstitute's capability (chosen)

- Good, because it tracks NSubstitute's own actual capability rather than a
  second, independently-maintained notion of "substitutable" that could drift
  from what NSubstitute really supports.
- Good, because each check is a cheap, well-understood `Type` reflection property
  read, not a runtime probe.
- Bad, because it means Milestone 5 quietly adds delegate support beyond
  `docs/mvp.md`'s original bullet list — mitigated by updating that doc in the
  same PR, per this repo's own documentation rule.

### Substitutable shapes — interfaces only

- Good, because it's the narrowest possible MVP scope.
- Bad, because it leaves "abstract-class support" (explicitly named in
  `docs/mvp.md`'s own Milestone 5 scope) undesigned, just deferred again with no
  new information gained by waiting.

### Substitutable shapes — interfaces + abstract classes, no delegates

- Good, because it matches `docs/mvp.md`'s original bullet list without any
  doc update needed.
- Bad, because delegate substitution costs one extra, cheap static-check branch
  and NSubstitute already supports it natively — excluding it buys nothing and
  leaves a real, easy capability on the table for no stated reason.

### Diagnostics — static pre-check + engine's existing message (chosen)

- Good, because it adds no new type, exception, or message format to maintain.
- Good, because the engine's existing diagnostic already names the exact type and
  path — genuinely "clear," not a placeholder.
- Bad, because the rare NSubstitute-internal-throw case still surfaces a raw,
  non-`CompositionException` error with no seed/path context — accepted, see
  Negative Consequences above.

### Diagnostics — custom `NSubstituteDiagnosticException`

- Bad, because it duplicates diagnostic machinery (`CompositionDiagnostic`,
  seed-in-message, path rendering) the engine's existing failure path already
  provides for free, for no case the pre-check-plus-`NotHandled` design doesn't
  already handle.

This package's own surface (`NSubstituteProvider`, `NSubstituteOptions`,
`UseNSubstitute`) is subject to the same alpha compatibility posture
[ADR-0024](0024-public-provider-extensibility-model.md)'s Alpha Compatibility
Policy states for `ICompositionValueProvider` itself — provisional during alpha,
revisable when Milestone 6/7 or real usage justifies it, any such change
explicitly documented per that policy and `PLAN-0005`'s breaking-change handling
section, never silently introduced.

## Amendment 1 (2026-07-31): Options snapshotting and delegate-type matching, corrected before implementation

A pre-implementation review of this ADR (before any `Compono.NSubstitute` code was
written) caught two defects in the sketches above. Both are corrected here, per
`design-decisions.md`'s Amendment mechanic — the original Decision Outcome text
above is left as written, since it's still accurate about *what* is substitutable
and *why*; only the two code sketches' exact shape is superseded.

**1. `NSubstituteProvider` must snapshot `NSubstituteOptions`, never retain the
caller-owned mutable instance.** [ADR-0024](0024-public-provider-extensibility-model.md)
commits to "immutable, reusable provider registration" as one of its architectural
guarantees — a provider, once constructed, must be safe to invoke repeatedly
(including concurrently) for the lifetime of the `Composer` it's registered on.
`NSubstituteProvider` is `public` (per this ADR's own package-boundary section), so
a consumer can construct it directly, meaning the original sketch's
`private readonly NSubstituteOptions _options` field is reachable and mutable after
registration — a consumer holding the same `options` reference (or one constructed
and reused across multiple `UseNSubstitute(options => ...)` calls) could change
`SubstituteAbstractClasses` after the fact, silently changing already-registered
behavior and violating the guarantee directly. Corrected shape: the constructor
reads `options.SubstituteAbstractClasses` once and stores the extracted `bool`,
never the `NSubstituteOptions` reference itself:

```csharp
public sealed class NSubstituteProvider : ICompositionValueProvider
{
    private readonly bool _substituteAbstractClasses;

    public NSubstituteProvider(NSubstituteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _substituteAbstractClasses = options.SubstituteAbstractClasses;
    }

    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (!IsSubstitutable(request.RequestedType, _substituteAbstractClasses))
            return CompositionProviderResult.NotHandled;

        return CompositionProviderResult.Handled(Substitute.For([request.RequestedType], []));
    }
}
```

`IsSubstitutable`'s signature changes to match — it now takes the already-extracted
`bool substituteAbstractClasses` rather than the mutable `NSubstituteOptions`
instance, so the static check itself can never observe a post-construction mutation
either:

```csharp
internal static bool IsSubstitutable(Type requestedType, bool substituteAbstractClasses) =>
    requestedType.IsInterface
    || requestedType.IsSubclassOf(typeof(MulticastDelegate))
    || (substituteAbstractClasses && requestedType.IsAbstract && !requestedType.IsSealed && !requestedType.IsInterface);
```

If `NSubstituteOptions` ever grows a second configurable value, the same rule
applies: snapshot every value the constructor needs into its own immutable field
(or into one small internal immutable settings record, if the number of extracted
values grows unwieldy as plain fields) — never retain the `NSubstituteOptions`
reference itself.

**2. Delegate matching must exclude the framework base types.**
`typeof(Delegate).IsAssignableFrom(requestedType)` also returns `true` for
`requestedType == typeof(Delegate)` and `requestedType == typeof(MulticastDelegate)`
themselves — neither is a concrete, declared delegate type NSubstitute can actually
substitute; both are the framework's own abstract base types every real delegate
type derives from. `requestedType.IsSubclassOf(typeof(MulticastDelegate))` is the
correct check: every real declared delegate type (`Action`, `Func<T>`, a
custom `delegate` declaration) is a subclass of `MulticastDelegate`, but
`MulticastDelegate`/`Delegate` are never subclasses of themselves, so `IsSubclassOf`
excludes exactly the two base types `IsAssignableFrom` wrongly included. Shown
combined with fix 1 above.

**Test coverage added to `PLAN-0005`'s Phase 2** for both fixes: negative
`IsSubstitutable` cases for `typeof(Delegate)` and `typeof(MulticastDelegate)`
themselves (alongside the existing sealed-class/struct/`string` negative cases), a
positive case for a real custom delegate type, and a mutation test proving that
mutating an `NSubstituteOptions` instance after `UseNSubstitute(options => ...)`
returns does not change an already-registered `NSubstituteProvider`'s behavior.

## Links

- [docs/mvp.md](../mvp.md) — Milestone 5 scope, non-goals, exit criterion
- [docs/public-api.md](../public-api.md) — NSubstitute Integration section
  (`UseNSubstitute`/`NSubstituteOptions` sketch this ADR finalizes)
- [docs/architecture.md](../architecture.md) — `Compono.NSubstitute` Package
  Boundaries entry, stage 6 (test-double providers)
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  the shared-scope mechanism "shared substitute reuse" relies on unchanged
- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md) —
  the unconditional stage-2 read that makes a `[Shared]` substitute visible to an
  ordinary nested constructor parameter
- [ADR-0024](0024-public-provider-extensibility-model.md) — the public provider
  contract, registration surface, and provider-failure-semantics rule this ADR
  implements against
