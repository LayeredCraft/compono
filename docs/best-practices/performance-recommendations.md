# Performance Recommendations

Practical guidance — what to actually do. For the methodology and measured
numbers behind these recommendations, see [Performance](../performance.md).

## Let source generation do its job

Compono's construction plans are generated at compile time specifically so
composing a type at runtime costs a dispatch to generated code, not
reflection (`docs/adr/0001-source-generation-first.md`). You don't need to
do anything to get this benefit for an ordinary composed type — it's the
default, not an opt-in, for any type reached through a recognized call
site (`Create<T>()`, `CreateMany<T>()`, a `[Compose]` theory parameter).
See [The Composition Model](../concepts/composition-model.md) for what
makes a call site recognized.

## Keep collection sizes proportional to what the test actually needs

A larger composed collection means more nested composition work,
multiplied by every test that composes it. Set an explicit, smaller size
for a specific member with
`For<T>().Member(x => x.Y).WithCollectionSize(n)` rather than inflating
the suite-wide default to accommodate one test's unusual need — see
[Collections](../concepts/collections.md) for the global-vs-per-member
distinction.

## Don't reach for `[Shared]` to avoid recomposition

`[Shared]` exists to make a test assert against or configure a specific
*instance*, not as a performance optimization — composing an ordinary
value is cheap by design (see "Let source generation do its job" above).
Reaching for `[Shared]` where independence was actually intended trades a
real correctness property (two composed values of the same type are
independent unless told otherwise) for a performance concern that
generated composition doesn't actually have.

## Measure before optimizing further

If a composed test suite's runtime genuinely becomes a bottleneck,
profile it rather than guessing — the overwhelming majority of a test
suite's wall-clock time in practice is I/O (a real HTTP call, a real
database), not composition. `benchmarks/Compono.Benchmarks`
(`BenchmarkDotNet`) is the same tool this project's own maintainers use to
validate Compono's own construction performance, and the same discipline
(measure the specific claim, don't extrapolate from a different one)
applies to your own suite.

## Next

- The measured numbers and methodology behind "generated construction
  avoids reflection overhead" → [Performance](../performance.md).
- Collection composition in depth → [Collections](../concepts/collections.md).
- Applies at scale → [Large Test Suites](large-test-suites.md).
