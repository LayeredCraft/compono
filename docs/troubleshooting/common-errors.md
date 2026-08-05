# Common Errors

## By diagnostic code (compile-time)

Every `CMP0001`–`CMP0012` error is a compile-time diagnostic from
`Compono.Generators` — see [Reference: Diagnostics](../reference/diagnostics.md)
for the full message/cause/fix for each code. The two you'll hit most
often in practice:

- **`CMP0001` (ambiguous construction path)** — the type you're composing
  has more than one accessible constructor. Most common real-world trigger:
  composing a BCL type you don't own directly (e.g. `HttpClient`, which
  has 3 accessible constructors). Fix: compose an interface/wrapper around
  it instead — see
  [Migrating from AutoFixture](../migrating-from-autofixture.md) for a
  real worked example of exactly this.
- **`CMP0003` (type cannot be constructed)** — usually means an interface
  parameter with no provider configured for it. Fix: add
  `builder.UseNSubstitute()` if you want an automatic substitute, or
  register the type explicitly with `Register<T>`.

## By symptom (runtime)

### Runtime composition failures {#runtime-composition-failures}

This is a *runtime* failure — no compiled code was rejected, but nothing
in the resolution pipeline could satisfy a request at composition time.
Unlike the `CMP` codes above, there's no diagnostic code here — only a
path-annotated message and a reproducible seed, via
`exception.Diagnostic` (a `CompositionDiagnostic`):

```text
Unable to compose CreateOrderHandler.

CreateOrderHandler
└── IOrderProcessor processor
    └── OrderValidator validator
        └── IRuleProvider rules

No registration, semantic provider, test-double provider, built-in
provider, or generated plan could satisfy IRuleProvider.

Seed: 8451203967726193045
```

Read the tree from the root down — it shows exactly which nested
dependency failed, not just the top-level type you asked for. To
reproduce the exact same composed values while debugging, copy the printed
seed into `[Compose(Seed = ...)]` (or `builder.WithSeed(...)` outside
xUnit) — see
[Determinism and Seeding](../concepts/determinism-and-seeding.md).

The most common cause is a missing provider for an interface — the same
underlying issue as `CMP0003` above, except discovered at a call site the
generator couldn't statically prove would fail (e.g. a registration that
only conditionally applies). Add the missing registration, or install the
package (`Compono.NSubstitute`/`Compono.Bogus`) that supplies it.

**A genuine construction cycle always fails this way too, immediately** —
Compono has no `OmitOnRecursionBehavior`-style opt-out; a self-referencing
object graph fails fast with the same path-annotated message rather than
silently omitting the cycling member. See
[Migrating from AutoFixture](../migrating-from-autofixture.md) if you're
coming from AutoFixture's recursion-behavior configuration — there's
nothing to configure here, by design.

### "My test throws `CompositionConfigurationException`"

This is a *configuration* error, thrown when `Composer.Create(...)`
returns — not a composition failure for a specific value. It means your
configuration callback itself is contradictory: two `Register<T>` calls
for the same type, two `WithSeed` calls, or a similar conflict. Every
conflict found is collected and reported together, not just the first
one. Fix: remove the duplicate/contradictory configuration call — there is
no last-write-wins fallback to rely on instead.

### "A composed value doesn't look realistic" (looks like an anonymous string)

`Compono.Bogus` isn't installed, or `UseBogus()` wasn't called, or the
member's name isn't on the built-in convention list — see
[`Compono.Bogus`'s Package Guide](../packages/compono-bogus.md) for the
exact list of matched names and how to extend it.

### "My substitute doesn't return what I expected"

`Compono.NSubstitute` never auto-configures a substitute's members — this
is deliberate, not a bug. See
[`Compono.NSubstitute`'s Package Guide](../packages/compono-nsubstitute.md#what-it-deliberately-doesnt-do)
for why, and how to stub the member explicitly.

### "Two composed parameters of the same type aren't the same instance"

That's the default — composition is independent per parameter unless you
opt in with `[Shared]`. See [Shared Values](../concepts/shared-values.md).

## Next

- [FAQ](faq.md) — design-decision questions that aren't a specific error.
- [Reference: Diagnostics](../reference/diagnostics.md) — full detail for
  every `CMP` code.
