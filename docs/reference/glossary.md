# Glossary

Every term Concepts and Architecture introduce, one line each,
alphabetical. Cross-linked to the page that actually teaches the concept —
this page defines, it doesn't explain.

**Composer** — the immutable, built-once entry point (`Composer.Create()`).
Owns the resolution pipeline configuration and is reused across
`Create<T>()`/`CreateMany<T>()` calls. See
[The Composition Model](../concepts/composition-model.md).

**Composing / composition** — the whole walk that produces a fully
constructed object graph for a requested type, not just constructing one
object — includes every nested dependency the resolution pipeline
satisfies along the way. See
[The Composition Model](../concepts/composition-model.md).

**Composition plan (`ICompositionPlan<T>`)** — the source-generated
construction code for a type, produced at compile time from its
constructor and required members. See
[The Composition Model](../concepts/composition-model.md#composition-is-generated-not-reflected).

**Composition profile (`ICompositionProfile`)** — a reusable, named unit of
configuration (registrations, rules, provider setup) applied via
`[Compose<TProfile>]` or `builder.AddProfile<TProfile>()`. See
[Profiles](../concepts/profiles.md).

**Composition root** — the type passed to `Create<T>()`/`CreateMany<T>()`
— the top of the object graph being composed for one call.

**`CompositionBuilder`** — the configuration surface passed to
`Composer.Create(builder => ...)`; the object `Register<T>`, `For<T>()`,
`UseNSubstitute()`, `UseBogus()`, and `AddProfile<T>()` are all called on.
See [Registrations and Rules](../concepts/registrations-and-rules.md).

**`CompositionDiagnostic`** — the structured detail behind a thrown
`CompositionException`: the failed type, the request path, the provider
attempts tried, the seed, and a remediation-oriented message. Available via
`exception.Diagnostic`. See
[Troubleshooting: Common Errors](../troubleshooting/common-errors.md#runtime-composition-failures).

**`CompositionException`** — thrown at runtime when no registration, rule,
provider, or generated plan can satisfy a request. Always fail-fast — there
is no partial/best-effort composed result. See
[Troubleshooting: Common Errors](../troubleshooting/common-errors.md#runtime-composition-failures).

**`ICompositionContext`** — what a registration factory, type/member rule,
or custom provider receives to resolve its *own* nested dependencies
(`context.Resolve<T>()`) rather than constructing them by hand. See
[The Composition Model](../concepts/composition-model.md).

**Deterministic seed** — the root value (`ulong`) every composed value in
one `Create<T>()`/`CreateMany<T>()` call is derived from — the same seed
always produces the same composed values. See
[Determinism and Seeding](../concepts/determinism-and-seeding.md).

**Provider (`ICompositionValueProvider`)** — a pluggable "maybe I can help"
rule tried when no explicit registration or rule claims a value — the
mechanism `Compono.Bogus`/`Compono.NSubstitute` use to add behavior
without the core package knowing either exists. See
[Providers](../concepts/providers.md).

**Registration (`Register<T>`)** — an exact, type-keyed factory: "whenever
anything needs a `T`, build it like this." See
[Registrations and Rules](../concepts/registrations-and-rules.md#registrations-registert).

**Resolution pipeline** — the ordered sequence of stages Compono tries to
satisfy a request: registrations/type-member rules, semantic providers,
test-double providers, built-in value providers, then generated default
construction. See [The Composition Model](../concepts/composition-model.md).

**Semantic provider** — a provider that produces a *meaningful* value
rather than an arbitrary one, e.g. `Compono.Bogus`'s member-name-convention
matching. See [Providers](../concepts/providers.md).

**`[Shared]`** — `Compono.XunitV3`'s attribute marking a `[Compose]` theory
parameter whose composed value is reused by type for every other composed
parameter/nested dependency in the same test row. See
[Shared Values](../concepts/shared-values.md).

**Test-double provider** — a provider that produces a test double instead
of a real value, e.g. `Compono.NSubstitute`'s substitute creation. See
[Providers](../concepts/providers.md).

**Type/member rule (`For<T>()`)** — a more targeted alternative to a
registration: scopable down to a single member of a type, and lower
precedence than a registration when both could apply. See
[Registrations and Rules](../concepts/registrations-and-rules.md#type-and-member-rules-fort).

## Next

- [Reference: Diagnostics](diagnostics.md) — every `CMP` compile-time
  error code.
- [Concepts](../concepts/index.md) — the full mental-model walkthrough
  each of these terms is drawn from.
