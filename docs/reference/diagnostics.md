# Diagnostics

Every `CMP` diagnostic code Compono's source generator can report, one
entry each. All are compile-time errors raised by `Compono.Generators`
during a normal build — they appear in your IDE's error list and fail
`dotnet build`, the same as any other compiler error. For a *runtime*
composition failure (a `CompositionException` thrown from
`composer.Create<T>()`), see
[Troubleshooting: Common Errors](../troubleshooting/common-errors.md#runtime-composition-failures)
instead — runtime failures have no diagnostic code, only a path-annotated
message.

Constructor-selection diagnostics (`CMP0001`–`CMP0007`, `CMP0009`) are
governed by [ADR-0002](../adr/0002-constructor-selection-algorithm.md);
the collection/discovery-conflict diagnostics (`CMP0010`–`CMP0012`) by the
generator's own discovery-merge logic; `CMP0013` by
[ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)'s row-binding
dispatch-eligibility guard.

## CMP0001 — Ambiguous construction path

**Message:** `{Type} has {N} accessible constructors and no way to
disambiguate them`

**Cause:** The type you're composing (directly, or as a nested dependency)
has more than one accessible constructor. Compono's generator resolves
constructor selection entirely at compile time from the type's own shape —
it has no way to guess which constructor you meant, and no runtime
fallback to disambiguate.

**Fix:** Reduce the type to exactly one accessible constructor, or compose
an interface/factory instead of the concrete type directly (an interface
is always resolved by a provider, never by constructor selection — see
[Providers](../concepts/providers.md)). This is the workaround needed for
BCL types you don't own, like `HttpClient` — see
[Migrating from AutoFixture](../migrating-from-autofixture.md) for a real
example.

## CMP0002 — No accessible constructor

**Message:** `{Type} has no accessible instance constructor Compono can invoke`

**Cause:** The type has no public/internal instance constructor at all
(e.g. only a `private` constructor, or it's `static`).

**Fix:** Give the type an accessible constructor, or compose a different
type/interface that Compono can actually construct.

## CMP0003 — Type cannot be constructed

**Message:** `{Type} is {reason} and cannot be constructed directly`

**Cause:** Historically reported for an abstract type or delegate the
generator couldn't construct directly. Interfaces, abstract classes, and
delegates are now always classified as provider-resolved instead — at
both root and member position — so this specific compile-time failure
isn't reachable through ordinary composition. A missing provider for one
of them surfaces as a *runtime* `CompositionException` instead — see
[Troubleshooting: Common Errors](../troubleshooting/common-errors.md#runtime-composition-failures).

**Fix:** Install/configure a provider that can supply the type
(`Compono.NSubstitute`'s `UseNSubstitute()`, or an explicit
`Register<T>`/`For<T>()` rule) at runtime — there's no compile-time fix to
make here, since this diagnostic doesn't fire for that scenario.

## CMP0004 — Unsupported constructor parameter kind

**Message:** `{Type} takes parameter '{name}' {kind}, which Compono cannot
compose a value for`

**Cause:** A constructor parameter has a kind Compono's generator doesn't
support composing a value for — a `ref`, `out`, or `ref readonly`
parameter (no argument expression can be written for them), a ref struct
(ref-like) parameter type, or a pointer/function-pointer parameter type.
`in` parameters are supported and don't trigger this — Compono can pass an
ordinary value to them with no modifier.

**Fix:** Remove or change the unsupported parameter kind, or register an
explicit factory (`Register<T>`) that constructs the type by hand instead
of relying on generated construction.

## CMP0005 — Type argument is not closed

**Message:** `'{Type}' is not a closed type`

**Cause:** You called `Create<T>()`/`CreateMany<T>()` (or composed a
member) with a type argument that still contains an unresolved type
parameter from an enclosing generic method or type — Compono requires a
fully closed, concrete type at every composition root.

**Fix:** Supply a concrete, closed type argument. Open generic
registrations are an explicit [MVP non-goal](../mvp.md#mvp-non-goals) —
there's no configuration that makes an open type composable.

## CMP0006 — Unsupported type argument shape

**Message:** `'{Type}' is not a type Compono can compose`

**Cause:** The type argument isn't a named type (class, struct, record, or
interface) and isn't one of the supported collection root shapes either.
A rank-1 array (`Create<int[]>()`) and the other built-in collection
shapes (`List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`, and similar)
are all valid composition roots on their own — this diagnostic is for
what's left over: a pointer/function-pointer type, or an unsupported array
rank (e.g. `Create<int[,]>()`).

**Fix:** Compose a named type, a rank-1 array, or one of the other
supported collection root shapes instead — see
[Collections](../concepts/collections.md).

## CMP0007 — Unsupported required member kind

**Message:** `{Type} has required member '{name}' {kind}, which Compono
cannot compose a value for`

**Cause:** Either of two distinct problems with a `required` member
(property or field):

- **Unsupported type**, the same category of restriction as CMP0004 — a
  ref struct (ref-like) or pointer/function-pointer member type, which
  can't be used as `Resolve<T>()`'s generic type argument.
- **Not assignable from generated code** — a required property with no
  accessible `init`/`set` accessor, or a required field that's `readonly`
  or itself inaccessible. The C# compiler never lets a C#-authored type
  declare a required member in this shape, but `required` is ultimately
  just metadata Roslyn reads off any assembly — a non-C# or hand-authored
  assembly can expose one anyway. Generated code assigning to it would
  fail to compile (`CS0272`/`CS0191`), so it's reported here instead.

**Fix:** For an unsupported type, change the member's type to a supported
shape, or make it non-required and set it via a `Register<T>` factory
instead. For an inaccessible/unassignable member, give it an accessible
`init`/`set` accessor (or make the field non-`readonly` and accessible),
or satisfy it from a `[SetsRequiredMembers]`-annotated constructor
instead — a constructor with that attribute is treated as already
satisfying every required member itself, so none of them reach this
check.

## CMP0008 — Assembly-level `[Composable]` has no target type

**Message:** `Assembly-level [Composable] requires a type argument
identifying the type to compose`

**Cause:** `[assembly: Composable]` was applied without a type argument.

**Fix:** Supply the target type: `[assembly: Composable(typeof(SomeType))]`.

## CMP0009 — Type argument is a ref struct

**Message:** `'{Type}' is a ref struct (ref-like type), which cannot be
used as a type argument for Compono's generated
ICompositionPlan<T>/PlanCache<T>`

**Cause:** `ref struct` types (e.g. `Span<T>`) can't be used as a generic
type argument at all in C# — Compono's generated plan cache is generic
over the composed type, so a `ref struct` can never be a composition root
or member.

**Fix:** There is no workaround for composing a `ref struct` directly —
compose the non-ref-struct type that produces or wraps the value you need
instead.

## CMP0010 — Conflicting composition metadata across discoveries

**Message:** `'{Type}' was discovered multiple times with different
composition metadata`

**Cause:** The same type is requested with inconsistent nullability in
different places in the same compilation (e.g. `Create<Box<string>>()` in
one file and `Create<Box<string?>>()` in another) — Compono generates
exactly one plan per type and can't produce two variants.

**Fix:** Make every request for that type use the same nullability.

## CMP0011 — Conflicting collection metadata across discoveries

**Message:** `'{Type}' was discovered multiple times with different
element/key nullability`

**Cause:** The same closed collection type is requested with inconsistent
element/key nullability (e.g. a `List<string>` member here, a
`List<string?>` member there) — the same underlying constraint as CMP0010,
applied to generated collection plans specifically.

**Fix:** Make every member/parameter of that collection type use the same
element/key nullability.

## CMP0012 — Collection element or key type is not accessible

**Message:** `'{Type}' cannot be an element or key type of a generated
collection plan for '{Collection}'`

**Cause:** Every generated collection plan is emitted as a top-level type
outside any containing type, so a `private`/`protected` element or key
type can never be referenced from it — even from a call site that could
otherwise see the private type.

**Fix:** Use a collection of an accessible (`public`/`internal`) element
or key type, or widen the element/key type's own accessibility.

## CMP0013 — Compose-attributed parameter type is not accessible

**Message:** `'{Type}' cannot be registered for row-binding dispatch on
'{Method}'`

**Cause:** A `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile,
TConfig>]`-attributed test method has a parameter whose type is
`private`/`protected` — the generated `RowInvokerRegistry` registration
that lets `Compono.XunitV3` dispatch into it at runtime is always emitted
as a top-level type outside any containing type, so it can never reference
a private/protected parameter type, even from a test method that could
otherwise see it. The same accessibility-domain problem CMP0012 solves for
collection element/key types, applied here to a bare `[Compose]`-family
parameter.

**Fix:** Use a parameter of an accessible (`public`/`internal`) type, or
widen the parameter type's own accessibility.

## Next

- [Troubleshooting: Common Errors](../troubleshooting/common-errors.md) —
  indexed by symptom as well as code, with full worked examples.
- [Glossary](glossary.md) — terms these diagnostics reference (composition
  root, composition plan, provider, and similar).
