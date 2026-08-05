# Troubleshooting

Something's wrong and you want a fix, not a tutorial — start here rather
than reading top to bottom.

- **A build error with a `CMP` code** (e.g. `CMP0001: 'MyType' has 2
  accessible constructors...`) → [Common Errors](common-errors.md), or
  jump straight to the code's own entry in
  [Reference: Diagnostics](../reference/diagnostics.md).
- **A test throws `CompositionException`/`CompositionConfigurationException`
  at runtime**, with no `CMP` code → [Common Errors: Runtime Composition
  Failures](common-errors.md#runtime-composition-failures).
- **Something works but doesn't behave the way you expected** (a value
  looks wrong, a substitute doesn't do what you thought, a value isn't
  shared) → [FAQ](faq.md).
- **You're not sure whether something is a bug or intended behavior** →
  [FAQ](faq.md) covers the design decisions that come up most; if it isn't
  there, the relevant [Concepts](../concepts/index.md) page explains the
  reasoning behind the pipeline stage involved.

## Known limitations

Compono's `0.x` line has a small number of deliberate, documented
limitations rather than undiscovered gaps — see each Package Guide's own
"What it deliberately doesn't do" section
([`Compono.XunitV3`](../packages/compono-xunitv3.md#what-it-deliberately-doesnt-do),
[`Compono.NSubstitute`](../packages/compono-nsubstitute.md#what-it-deliberately-doesnt-do),
[`Compono.Bogus`](../packages/compono-bogus.md#what-it-deliberately-doesnt-do)),
plus the full [MVP Non-goals](../mvp.md#mvp-non-goals) list for what's out
of scope entirely for `1.0`.

## Next

- [Common Errors](common-errors.md)
- [FAQ](faq.md)
- [Reference: Diagnostics](../reference/diagnostics.md)
