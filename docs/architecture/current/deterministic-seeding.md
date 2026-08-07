# Deterministic Seeding

Resolved by [ADR-0012](../../adr/0012-composition-path-identity-and-deterministic-random-forking.md)
and [ADR-0026](../../adr/0026-deterministic-seed-derivation-for-providers.md).
[Concepts: Determinism and Seeding](../../concepts/determinism-and-seeding.md)
covers what "deterministic by design" means for a test author; this page
is the derivation algorithm itself.

## Path-derived forking

The root context owns the seed. Random sources are forkable by stable
keys:

```text
root seed
└── test parameter: command
    └── Customer
        └── Email
```

`CompositionPath` is a chain of structured `PathSegment`s — not just
types — so two constructor parameters or members of the same type
(`Customer(string FirstName, string LastName)`) fork independently
instead of colliding on an identical key. Forking hashes the structured
segment data directly (a per-kind tag plus its `Ordinal`/index — a
constructor parameter's position in the selected constructor, or a
required member's generator-assigned declaration-order index; never
`Name`, which exists on the segment for diagnostic display only) via
FNV-1a, never a formatted display string — this is what makes the fork
key collision-free by construction rather than by careful
string-escaping. This is a reproducibility *contract*, not an
implementation detail: renaming a constructor parameter or required
member (with no reordering) never changes its derived value — only
reordering does.

That structured state feeds a small Compono-owned PRNG (not
`System.Random`), so the byte-for-byte output sequence is something
Compono controls rather than an inherited BCL implementation detail. The
stability guarantee is explicit: the same seed produces the same output
for a given `Compono` package version — cross-version stability across a
`Compono` upgrade is not promised.

## `CreateMany` seed derivation

`CreateMany<T>(count)` derives each item's independent root seed by
forking the batch's root seed through a stable `"CreateMany"` key, then
by the item's index — so item `i`'s output depends only on the batch root
and `i`, never on `count`. Items 0–2 of `CreateMany<T>(3)` and
`CreateMany<T>(10)` (same root seed) are byte-for-byte identical.

## `DeriveSeed()` for providers and registration factories

A provider or registration/configuration-rule factory that needs its own
deterministic randomness — `Compono.Bogus`'s `BogusMemberNameProvider`, or
its `UseBogus(...)`/`UseBogus<T>(...)` sugar — calls
`context.DeriveSeed()`: an on-demand, path-derived `int` a public
provider or factory can use for its own randomness, without exposing the
engine's own internal `IRandomSource` or path representation. It reuses
the same path-hash mechanism above internally, so it's just as
reproducible as the engine's own built-in resolution.

## Failure reporting

A composition failure's message ends with `Seed: {value}`, matching the
[Provider Pipeline](provider-pipeline.md#diagnostics)'s Diagnostics
example — a successful row does not surface its seed anywhere by default,
to keep passing-test output unchanged.
`CompositionException.WithSeedInMessage(original, seed)` is a static
factory for the one case where a `CompositionException` has no
`Diagnostic` to render a seed line from on its own (e.g. a generated
`HashSet<T>`/`Dictionary` collection plan's unique-value-exhaustion
failure): it returns a copy of `original` whose `Message` has the seed
appended directly, with `original` preserved as `InnerException`.
`Compono.XunitV3`'s own `[Compose]` binding algorithm uses this to
guarantee every composition failure's message carries a pasteable seed,
not only ones that happen to have a `Diagnostic`.
