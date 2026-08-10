# [ADR-0037] netstandard2.1 Compatibility Floor

**Status:** Superseded by [ADR-0038](0038-net8-net9-explicit-multi-target.md)

**Date:** 2026-08-10

**Decision Makers:** solo

## Context

All four Compono packages (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`,
`Compono.Bogus`) target `net10.0;net11.0` only. [ADR-0031](0031-public-preview-release-and-versioning-policy.md)
records this as a deliberate rolling two-TFM window — current GA plus the
next release in development, tracked continuously ahead of GA — not an
oversight.

That policy blocks any consumer on an older-but-still-supported .NET
version. Concretely: an attempt to dogfood Compono in the
`structured-logging` repo (targets `.NET 8`/`.NET 9`) failed to restore,
because neither TFM satisfies `net10.0;net11.0`. `.NET 8` and `.NET 9` are
both still within Microsoft's own support window; a preview-stage test
library refusing to install there is a real adoption barrier, not a
theoretical one.

C# language version is not the constraint — `LangVersion` is independent
of target framework and has been since C# 8, so C# 14 syntax already
compiles fine when targeting an older TFM. The actual constraint is BCL
surface: a handful of net6.0+-only APIs are in use in `Compono`'s own
source (see Decision Outcome).

## Decision Drivers

- Unblock consumers on currently-supported-but-not-latest .NET versions
  (.NET 8, .NET 9, and current and future .NET implementations that
  support .NET Standard 2.1) without re-litigating TFM policy on every new
  consumer.
- Keep ADR-0031's rolling-ahead window intact for `net10.0`/`net11.0` as
  the packages' primary, actively-tracked TFMs — this decision adds a
  floor underneath that window, it doesn't replace it.
- Minimize BCL-shim surface — prefer solutions that don't require
  rewriting working, correct code just to satisfy an older compile target.
- Verify the integration packages' own third-party dependencies
  (`xunit.v3.extensibility.core`, `NSubstitute`, `Bogus`) don't
  independently block a lower floor before committing to one.

## Considered Options

1. Status quo — `net10.0;net11.0` only, direct consumers to upgrade.
2. Explicit multi-target to the two blocking TFMs (`net8.0;net9.0;net10.0;net11.0`).
3. Add `netstandard2.1` as an additional floor TFM alongside `net10.0;net11.0`.

## Decision Outcome

Chosen option: **3 — add `netstandard2.1` as a floor TFM**, because it
covers `.NET 8`/`.NET 9` and current and future .NET implementations that
support .NET Standard 2.1 (including .NET Core 3.0+) through one extra
target instead of enumerating consumer TFMs one at a time, and its BCL gap
versus `net10.0`/`net11.0` is narrow and fully accounted for:

- `Random.Shared` (net6.0+) — used in `CompositionSeed.cs` (`Generate()`/
  `GenerateRowSeed()`, each called once per root `Composer.Create`/
  `CreateRow` call, not on a per-item hot path — `CreateMany`'s per-item
  forking goes through `CompositionSeed.Fork`, a deterministic FNV-1a
  combine with no `Random` involved at all). No available polyfill
  implements `Random.Shared` (verified against the `Polyfill` package's
  source directly — it does not stub `Random.Shared`, only instance
  extension methods like `NextInt64`/`Shuffle`, since a shared static
  instance needs real thread-safe state, not a type stub). Resolved with
  `#if NET6_0_OR_GREATER` around the existing call, falling back under
  `netstandard2.1` to a single shared `Random` instance guarded by a
  `lock` — not `ThreadLocal<Random>`. Given the call frequency above (once
  per root composition, not per element), lock contention is not a
  realistic concern; a single locked instance is trivially thread-safe, has
  no per-thread state or disposal lifecycle to reason about, and is the
  simplest fallback that satisfies `Random.Shared`'s actual semantic
  requirement (safe concurrent access to a shared generator) without
  reproducing its internal implementation strategy. If seed generation
  ever becomes demonstrably contended, that's a reason to revisit this
  choice with evidence, not a reason to preempt it here. `net10.0`/`net11.0`
  builds are unaffected — the fallback only compiles into the
  `netstandard2.1` output.
- `ArgumentNullException.ThrowIfNull` (net6.0+) — used at ~20 call sites
  across `Compono`. Resolved via the `Polyfill` (SimonCropp) source-only
  package, which ships a `netstandard2.1`-specific implementation of this
  exact API (verified against the package's source).
- `required` members (C# 11, needs `RequiredMemberAttribute`/
  `CompilerFeatureRequiredAttribute` recognized by the compiler) — resolved
  via the `PolySharp` source-only package, the standard mechanism for this
  exact gap; no runtime behavior involved, pure compiler-recognized
  attribute stubs.

Each integration package's own third-party dependency was checked for a
netstandard2.1-compatible build before committing to this option:
`xunit.v3.extensibility.core` ships `netstandard2.0` (a strict subset of
2.1, so automatically satisfied), `NSubstitute` ships both `netstandard2.0`
and `netstandard2.1` explicitly, `Bogus` ships `netstandard1.3` through
`2.1`. None of the three integration packages' dependencies block this
floor. That check covers only each integration package's *third-party
dependency* — it doesn't prove `Compono.XunitV3`/`Compono.NSubstitute`/
`Compono.Bogus`'s own source has no further net6.0+-only BCL usage beyond
the three gaps identified in `Compono` itself. PLAN-0037 treats compiling
each integration package's own source against `netstandard2.1` as an
explicit compatibility-audit task, not an assumption that `Compono`'s
three known gaps are exhaustive across the whole solution.

`Compono.Generators` already targets `netstandard2.0` (required for Roslyn
analyzer/source-generator host compatibility, per ADR-0031's own note) —
unaffected by this decision, since it was never gated by the `net10.0;net11.0`
consumer policy in the first place.

This decision **amends, not supersedes,** ADR-0031: the rolling
current-plus-next-GA window for `net10.0`/`net11.0` remains policy exactly
as written there — those two TFMs are still the packages' primary,
actively-tracked targets, get first access to newer BCL APIs directly
(via the `#if NET6_0_OR_GREATER`-style guards above), and the older TFM in
that pair is still dropped only on a minor-version bump per that ADR's
existing rule. `netstandard2.1` is a third, independent floor TFM that is
not itself tracked release-to-release the way the two-TFM window is — it
stays as long as it keeps unblocking real consumers and doesn't meaningfully
constrain what the primary TFMs can use directly.

### Positive Consequences

- Unblocks any consumer on .NET 8+, .NET Core 3.0+, or another
  netstandard2.1-capable runtime, without a new TFM add per consumer.
- No loss of C# language version on the primary TFMs — `net10.0;net11.0`
  keep using new BCL APIs directly; the `#if` guards only add an
  alternate code path for the floor build, they don't downgrade the
  primary one.
- All three integration packages' dependencies already support this
  floor — no forced dependency downgrade anywhere in the stack.

### Negative Consequences

- One additional build output per package (four additional package/TFM
  outputs total, one `netstandard2.1` build per package) — slightly
  larger nupkg, one more leg in CI's build matrix and the packed-consumer
  smoke test.
- Two new external source-only dependencies (`PolySharp`, `Polyfill`) on
  `Compono` itself, scoped to the `netstandard2.1` build only (both are
  compile-time-only, no runtime assembly shipped) — a small addition to
  the supply-chain surface `references/security.md` cares about, worth a
  one-line note there.
- `Random.Shared`'s lock-based fallback is new code with its own (thin)
  test-coverage need, isolated to the `netstandard2.1` build.
- Two more genuinely-different BCL surfaces to keep correct over time
  (any future net6.0+-only API added to `Compono` needs the same
  `#if`-guard treatment, or a new polyfill check) — an ongoing tax, not a
  one-time cost.

## Pros and Cons of the Options

### 1. Status quo

Keep `net10.0;net11.0` only; consumers on older TFMs upgrade or don't
adopt Compono yet.

- Good, because zero implementation cost, zero new supply-chain surface.
- Good, because it's the simplest position to keep consistent with
  ADR-0031's stated policy.
- Bad, because it left a real dogfooding attempt (`structured-logging`)
  blocked today, and blocks every other consumer still on .NET 8/9 for as
  long as they stay there — a meaningful adoption barrier during the
  public-preview phase this project is explicitly trying to grow.

### 2. Explicit multi-target (`net8.0;net9.0;net10.0;net11.0`)

Add the two specific blocking TFMs directly, no netstandard involved.

- Good, because each TFM gets the real, un-shimmed BCL for its own
  version — `net8.0`/`net9.0` builds could use their own native
  `Random.Shared`/`ArgumentNullException.ThrowIfNull` directly, no
  polyfill needed at all.
- Bad, because it only covers exactly `net8.0`/`net9.0` — a consumer on
  `net7.0`, `.NET Core 3.1`, or any future TFM this doesn't enumerate is
  still blocked, and each newly-blocked consumer becomes its own future
  ADR amendment instead of already being covered.
- Bad, because four TFMs per package (sixteen build outputs across all
  four packages) is a larger CI/package surface than three TFMs with one
  shimmed floor, for narrower coverage.
- Bad, because it reads as directly contradicting ADR-0031's "never one
  release behind" framing in a way `netstandard2.1`-as-floor doesn't (a
  floor is explicitly a different axis from the tracked window; an
  explicit `net8.0`/`net9.0` target is that same window, just widened
  backward).

### 3. `netstandard2.1` floor (chosen)

- Good, because one additional TFM covers `.NET 8`/`.NET 9`/`.NET Core
  3.0+`, and current and future .NET implementations that support .NET
  Standard 2.1, not just two named versions.
- Good, because all three integration packages' real dependencies already
  support it — verified, not assumed.
- Good, because the BCL gap versus `net10.0`/`net11.0` is exactly three
  known items, each with a scoped, already-identified fix.
- Bad, because `netstandard2.1` predates `required` members, forcing a
  polyfill dependency that a direct `net8.0`/`net9.0` target wouldn't need
  (both natively support `required`, being net7.0+).
- Bad, because it's an unfamiliar-to-some-readers axis (netstandard vs.
  TFM) sitting alongside ADR-0031's TFM-window language, which needs this
  ADR to make the relationship explicit rather than leaving it implicit.

## The floor is cheap, not a design target

`netstandard2.1` is a compatibility floor, not a lowest-common-denominator
design target. The primary `net10.0`/`net11.0` TFMs may continue using
newer BCL capabilities directly — nothing about this decision requires
writing `Compono`'s primary-path code to the older surface. Compatibility
shims for the floor should stay small and isolated (as the three items
above already are); if maintaining the floor later requires substantial
conditional implementation, or begins constraining what the primary TFMs
can use directly, retaining the floor should be re-evaluated rather than
treated as a permanent compatibility promise regardless of maintenance
cost.

## Links

- [ADR-0031: Public Preview Release and Versioning Policy](0031-public-preview-release-and-versioning-policy.md) — the rolling two-TFM window this ADR adds a floor underneath, not supersedes.
- [ADR-0001: Source-Generation First](0001-source-generation-first.md) — `Compono.Generators`'s existing `netstandard2.0` target for unrelated reasons (analyzer host compatibility), referenced here only to distinguish it from this decision.
