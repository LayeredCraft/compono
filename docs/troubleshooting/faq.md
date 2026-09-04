# FAQ

Questions that come up repeatedly but don't map to a single diagnostic
code or error message.

## Why does a construction cycle fail instead of just omitting the cycling member?

AutoFixture (and similar tools) default to throwing on a self-referencing
object graph, but some configurations opt into silently omitting the
cycling member instead (AutoFixture's `OmitOnRecursionBehavior`). Compono
has no equivalent opt-out — a genuine construction cycle always fails
fast, immediately, with a path-annotated `CompositionException` (see
[Common Errors: Runtime Composition Failures](common-errors.md#runtime-composition-failures)).

This is deliberate: an omitted member is a silently incomplete object —
the test gets a value back, but part of its graph is missing without any
signal that happened. Compono's explicit-over-implicit design principle
(the same one behind `Compono.NSubstitute`'s no-auto-configuration
default) treats that as worse than a loud failure with a clear,
reproducible seed. If your object graph is genuinely self-referencing,
break the cycle explicitly with a `Register<T>` factory that supplies the
recursive member directly, rather than relying on generated default
construction to handle it.

## Why is a substitute's method returning `null`/`default` instead of a sensible value?

`Compono.NSubstitute` never auto-configures a substitute's members — see
[`Compono.NSubstitute`'s Package Guide](../packages/compono-nsubstitute.md#what-it-deliberately-doesnt-do).
Stub the specific member you depend on explicitly.

## Why can't I compose `HttpClient` (or another BCL type) directly?

`HttpClient` has multiple accessible constructors, so composing it
directly fails with `CMP0001` — see
[Reference: Diagnostics — CMP0001](../reference/diagnostics.md#cmp0001-ambiguous-construction-path).
The general workaround is to compose an interface wrapper around the BCL
type instead of the type itself — see
[Migrating from AutoFixture](../migrating-from-autofixture.md) for a real,
complete example (`IHttpClientProvider`).

## Can I stack `[Compose]` with `[Compose<TProfile>]` on the same test method?

No — only one Compose-family attribute per method is supported; stacking
throws a `CompositionException` at data-binding time. See
[`Compono.XunitV3`'s Package Guide](../packages/compono-xunitv3.md#what-it-deliberately-doesnt-do).

## Why did two composed values of the same type turn out identical when I expected them different (or vice versa)?

Composition is independent by default — two parameters of the same type
get two separate composed instances, even if they look alike. `[Shared]`
opts a specific type into being reused across a test row instead — see
[Shared Values](../concepts/shared-values.md). If you're seeing unexpected
*sameness* without using `[Shared]`, check whether a profile registered a
singleton-style factory (`Register<T>` that captures and returns the same
instance every call) rather than a fresh one.

## Is there a stable `1.0` release yet?

Not yet, but most packages already have a stable `0.9.0` release — a plain
`dotnet add package` picks it up with no extra flag. `Compono.MSTest` and
`Compono.NUnit` are still preview-only (`0.x.y-preview.N`, `--prerelease`
required) until their own first stable release ships. See
[Installation](../getting-started/installation.md) for exact commands,
[MVP Non-goals](../mvp.md#mvp-non-goals), and
[ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)
for the compatibility policy this implies. Note ADR-0031's own Amendment 5:
a breaking change now bumps the major version (the ordinary SemVer
`breaking-change → major` mapping), not the minor version — the ADR's
original "bumps the minor version until `1.0`" override was a deliberate,
temporary `0.x`-era guard that's since been lifted now that Compono is
ready to leave the `0.x` line.

## Next

- [Common Errors](common-errors.md)
- [Reference: Diagnostics](../reference/diagnostics.md)
