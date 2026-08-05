# Deterministic, Non-Brittle Tests

Keeping tests reproducible and resistant to unrelated changes as a suite
grows.

## Assert on shape, not on incidental composed values

A composed value that isn't pinned down with an inline value or a
registration is, deliberately, not meant to be predicted — asserting
`quantity.Should().Be(42)` against an ordinarily-composed `int` is brittle
in a way that has nothing to do with Compono being unreliable; it's
asserting on a value the test never actually constrained. Assert on shape
(`Should().NotBeNullOrWhiteSpace()`, `Should().BeOfType<int>()`) for
ordinarily-composed values, and reserve an exact-value assertion for a
value your test explicitly fixed — an inline `[Compose(42, "widget")]`
argument, a [member rule](../how-to/customize-a-member.md), or a
`[Shared]` instance you're checking reference equality against.

## Never assert against a bare seed value

Don't write a test that hardcodes "seed 24601 produces quantity 7" as an
assertion — the same seed producing the same output is guaranteed *for a
given version of Compono*, not across versions (a new release can change
generated values for the same seed, the same way any library's internal
generation details can shift). Seeds are for *reproducing a specific run*
you're actively debugging, not for encoding expected values permanently
into a test.

## Reach for a fixed seed to debug, not to write the test

`[Compose(Seed = ...)]`/`WithSeed(...)` exist to reproduce a specific
failure while you're investigating it, not as a general test-writing
habit. A test suite where every test pins a specific seed loses the actual
benefit of composed data — incidental variation surfacing an assumption
your code silently depended on. Leave seeds unset by default; reach for one
only when reproducing a reported failure (see
[Seed a Specific Failing Case for Reproduction](../cookbook/seed-a-specific-failing-case-for-reproduction.md)).

## Let `[Shared]` do the coupling explicitly

If a test's correctness genuinely depends on two composed values being the
*same instance*, say so with `[Shared]` rather than hoping composition
happens to produce equal-looking values — equality and identity are
different guarantees, and only `[Shared]` gives you the latter. A test
that silently relies on incidental equality between two independently
composed values is exactly the kind of brittleness that breaks the moment
an unrelated change alters how that type composes.

## A composition failure is not a flaky test

If a composed test fails to *build* its graph at all (a `CompositionException`,
not a failed assertion), that's not intermittent flakiness to retry past —
it's a real, reproducible gap (a missing registration, an unregistered
interface) that the failure's own reported seed can reproduce exactly. See
[Determinism and Seeding](../concepts/determinism-and-seeding.md) for why
this is true by construction, not just usually true.

## Next

- The mechanics underpinning all of the above →
  [Determinism and Seeding](../concepts/determinism-and-seeding.md).
- Reproducing a specific reported failure →
  [Seed a Specific Failing Case for Reproduction](../cookbook/seed-a-specific-failing-case-for-reproduction.md).
- `[Shared]` in depth → [Shared Values](../concepts/shared-values.md).
