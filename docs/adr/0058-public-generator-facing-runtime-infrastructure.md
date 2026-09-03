# [ADR-0058] Public Generator-Facing Runtime Infrastructure

**Status:** Accepted

**Date:** 2026-09-03

**Decision Makers:** Jonas Ha (proposal, this branch), Codex (initial
draft), Claude (independent technical verification against
`src/Compono.Generators` and repo source), Nick Cipollina (product-owner
acceptance, 2026-09-03)

## Context

Some runtime members must be `public` solely because source generated code is
compiled into an arbitrary consumer assembly. It cannot access `internal`
members in a Compono package, and `InternalsVisibleTo` cannot name an unbounded
set of consumer assemblies. This is an intentional cross-assembly integration
mechanism, not an accidental expansion of the ordinary consumer API.

Compono historically leaves such hooks visible in IntelliSense. The existing
examples are `PlanCache<T>.Instance`, `CollectionPlanCache<T>.Instance`,
`RowInvokerRegistry.Register`, `GeneratedTestDoubleRegistry.RegisterFactory`,
and `LoggingFactoryRegistry.Register`. PR #118 introduced
`ReturnConfig<T>.ClearConfiguredResponse()` with
`[EditorBrowsable(EditorBrowsableState.Never)]`. That created two conventions
for the same class of public member.

The initial issue inventory is incomplete. Emitted test-double dispatch also
calls `ReturnConfig<T>`'s response-state properties, `RecordCall()`, and
`NextSequenceOutcome()`. Its generated configuration extensions construct
`ReturnConfigBuilder<T>` directly. A policy that classifies only
`ClearConfiguredResponse()` would leave the same generator/runtime boundary
inconsistent within one type.

This ADR decides the discoverability policy only. It does not reconsider the
public accessibility of generator hooks, ADR-0053's callback-response design,
or introduce a reflection or runtime-indirection alternative.

## Decision Drivers

- A rule must be simple enough to apply consistently to future generated-code
  hooks across Compono packages.
- Normal consumers should see the APIs intended for direct use; implementation
  hooks should not compete with the fluent configuration and composition APIs.
- The policy must preserve generated consumer-assembly code's direct,
  compile-time access to its runtime hooks.
- The resulting public API documentation and XML comments must explain why an
  infrastructure member is public when that is not obvious from its name.

## Considered Options

1. Leave all generator-facing infrastructure undecorated and remove the
   attribute from `ReturnConfig<T>.ClearConfiguredResponse()`.
2. Apply `[EditorBrowsable(EditorBrowsableState.Never)]` to each public member
   whose only supported caller is generated consumer-assembly code (recommended).
3. Hide only state-mutating internal mechanics while leaving registration,
   cache, and read-side hooks visible.

## Decision Outcome

**Chosen: Option 2.** Apply
`[EditorBrowsable(EditorBrowsableState.Never)]` to a public *member* when it
exists solely for Compono-emitted code to call across an assembly boundary and
is not a supported manually-authored consumer API. Do not apply the attribute
to a type merely because one of its members is such a hook. Do not use mutation
risk as the classifier.

`EditorBrowsable` is intentionally only a discoverability hint: it does not
change CLR accessibility, binary compatibility, or generated-code execution.
It is appropriate here precisely because the cross-assembly public members
remain necessary. The policy must not claim to make those members inaccessible
or safe against deliberate manual use.

The rule above is stated at member level because no current type in the
Evaluation Scope is *exclusively* generator-facing — `RowInvokerRegistry`,
`GeneratedTestDoubleRegistry`, and `LoggingFactoryRegistry` each keep a
`TryGet`/`TryCreate` member other Compono packages call directly, and
`ReturnConfigBuilder<T>` keeps its consumer-facing fluent methods. A future
type whose entire public surface exists solely for generated-code calls,
with no member any manually-authored code (consumer or another Compono
package) is meant to call, may apply `EditorBrowsableState.Never` at the
type level instead — the same underlying rule (intended caller, not
mutation risk), just with nothing left to leave visible.

### Positive Consequences

- Once accepted, every new generator-facing hook has a documented
  discoverability rule.
- Generated-code-only members no longer crowd IntelliSense for normal
  consumers, while types and members needed by supported runtime integrations
  remain visible.

### Negative Consequences

- The public members remain callable and must retain their compatibility
  contract; the attribute is not an access-control boundary.
- Retrofitting the policy changes IntelliSense presentation for already shipped
  APIs and requires API-reference regeneration.

## Pros and Cons of the Options

### 1. Leave all hooks undecorated

Continue the established convention and remove the attribute from
`ClearConfiguredResponse()`.

- Good, because it matches the existing cache and registry precedent and makes
  no new IntelliSense-visibility change for shipped hooks.
- Good, because it is a simple rule: required public generated-code hooks stay
  visible and documented.
- Bad, because normal consumers may encounter methods intended only for
  generated code.

### 2. Hide generated-code-only members

Apply `EditorBrowsableState.Never` to the individual members emitted code calls
but manually-authored consumer code is not meant to call.

- Good, because ordinary IntelliSense emphasizes APIs consumers call directly.
- Good, because it is objective: intended caller, not an arbitrary assessment
  of whether a method's state change is dangerous.
- Bad, because it changes the presentation of established APIs.
- Bad, because a consumer can still discover and call the members deliberately.

### 3. Differentiate by risk

Hide state-mutating internal mechanics but leave caches and registration hooks
visible.

- Good, because it can protect consumers from particularly surprising methods
  such as response-state resets.
- Bad, because registration hooks and cache setters also mutate state, making a
  reliable risk boundary impossible to state and apply consistently.
- Bad, because each new hook may require a subjective classification.

## Evaluation Scope

The implementation review must evaluate these direct generated-code hooks
together:

- `PlanCache<T>.Instance`
- `CollectionPlanCache<T>.Instance`
- `RowInvokerRegistry.Register`
- `LoggingFactoryRegistry.Register`
- `ReturnConfig<T>.ClearConfiguredResponse`
- `GeneratedTestDoubleRegistry.RegisterFactory`
- `ReturnConfig<T>`'s `HasConfiguredValue`, `HasConfiguredException`,
  `HasConfiguredSequence`, `ConfiguredValue`, `ConfiguredException`,
  `ConfiguredCallCount`, `RecordCall()`, and `NextSequenceOutcome()`
- `ReturnConfigBuilder<T>`'s constructor

The review must leave `RowInvokerRegistry.TryGet`,
`GeneratedTestDoubleRegistry.TryCreate`, and `LoggingFactoryRegistry.TryCreate`
visible: those are runtime-integration seams used by the framework packages,
not direct calls emitted into a consumer assembly. `ReturnConfigBuilder<T>`
itself and its fluent response methods also remain visible because they are the
consumer-facing return values of generated configuration extensions; only its
constructor is an emitted-code hook.

The implementation must append a cross-reference amendment to ADR-0041 —
`RowInvokerRegistry`'s undecorated convention was established in that
type's own doc comment, not in ADR-0041's text (ADR-0041 never discusses
`EditorBrowsable`), so the amendment records this ADR's policy rather than
correcting a decision ADR-0041 never made. ADR-0055's own text *does*
already discuss and decide to leave `LoggingFactoryRegistry` undecorated
(§ "No `[EditorBrowsable(EditorBrowsableState.Never)]` is added"), so its
amendment is a genuine update to a prior decision. Check whether ADR-0043
(`GeneratedTestDoubleRegistry.RegisterFactory`) needs the same treatment.
Also add the policy to `coding-standards.md`. A short plan is warranted
because the change spans multiple packages, generated-code contract tests,
and regenerated API reference.

## Links

- [Issue #123](https://github.com/LayeredCraft/compono/issues/123)
- [PR #118](https://github.com/LayeredCraft/compono/pull/118)
- [ADR-0041](0041-aot-safe-row-binding-dispatch.md)
- [ADR-0043](0043-compono-generated-test-doubles-design.md)
- [ADR-0053](0053-testdoubles-invocation-aware-callback-responses.md)
- [ADR-0055](0055-compono-logging-testing-support-package.md)
