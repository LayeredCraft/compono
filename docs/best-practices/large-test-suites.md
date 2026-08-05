# Large Test Suites

## Let independence stay the default

Composed parameters are independent by default — two composed values of
the same type in the same row are two different instances unless you mark
one `[Shared]`. As a suite grows, resist reaching for `[Shared]` out of
habit: it's for the specific case where a test needs to assert against or
configure the *same instance* a dependency received, not a general
"make things consistent" tool. Overusing it couples composed values
together in ways that make failures harder to isolate — a test with three
unrelated `[Shared]` parameters is a test that's stopped testing three
independent things.

## Prefer many small profiles over shared giant fixtures

A suite that's grown large usually has grown its setup along with it —
resist consolidating that growth into one shared, all-purpose profile or
fixture. See [Organizing Profiles](organizing-profiles.md) for the shape
that scales instead: several small, focused profiles, composed together
only where a given test actually needs more than one.

## Keep collection sizes intentional at scale

Compono's default composed collection size is deliberately small. If a
large suite's tests need a specific size for a specific member (e.g.
exercising pagination), set it explicitly and locally —
`builder.For<Order>().Member(x => x.LineItems).WithCollectionSize(2)` — or
suite-wide via `builder.WithCollectionSize(n)` if the suite has a real,
consistent need for a different default. Don't inflate the global default
"just in case" — larger composed collections cost real composition time
multiplied across every test that touches the type, and most assertions
don't need more than a couple of elements to be meaningful.

## A flaky-looking failure is a signal, not noise

Compono's composition is deterministic by construction — see
[Deterministic, Non-Brittle Tests](deterministic-and-non-brittle-tests.md).
In a large suite, a test that only fails "sometimes" almost never means
Compono produced a different value from the same seed; it means something
outside composition (shared mutable state between tests, real
non-determinism in the system under test, test ordering) is the actual
cause. Chase that down rather than adding a retry — reproducing the exact
composed values via the failure's own reported seed is usually the fastest
way to tell which one it is.

## Next

- The mechanics `[Shared]` actually relies on →
  [Shared Values](../concepts/shared-values.md).
- Collection composition in depth → [Collections](../concepts/collections.md).
- Reproducing a specific failure deterministically →
  [Determinism and Seeding](../concepts/determinism-and-seeding.md).
