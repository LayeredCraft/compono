# Diagnostics

Every `CMP` diagnostic code Compono's source generator can report, one
entry each. `CMP0001`–`CMP0013` are compile-time **errors** raised by
`Compono.Generators` during a normal build — they appear in your IDE's
error list and fail `dotnet build`, the same as any other compiler error.
`CMP0020`–`CMP0031` are a separate, **informational** family — they only
apply if `<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>`
is set (see [`Compono.TestDoubles`](../packages/compono-testdoubles.md))
and never fail the build. Most (`CMP0020`, `CMP0021`, `CMP0023`–`CMP0028`,
`CMP0031`) report that an entire interface leaf's generated double
couldn't be emitted — the leaf falls back to the ordinary runtime-provider
path. A newer, narrower subset (v2, ADR-0044) is **scoped to one overload
or identity instead**: `CMP0022`, `CMP0029`, and `CMP0030` withhold just
that one overload's or identity's `Configure()` surface while the double
still generates and every other member is unaffected; each entry below
says which scope applies. For a *runtime* composition failure (a
`CompositionException` thrown from `composer.Create<T>()`), see
[Troubleshooting: Common Errors](../troubleshooting/common-errors.md#runtime-composition-failures)
instead — runtime failures have no diagnostic code, only a path-annotated
message.

Constructor-selection diagnostics (`CMP0001`–`CMP0007`, `CMP0009`) are
governed by [ADR-0002](../adr/0002-constructor-selection-algorithm.md);
the collection/discovery-conflict diagnostics (`CMP0010`–`CMP0012`) by the
generator's own discovery-merge logic; `CMP0013` by
[ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)'s row-binding
dispatch-eligibility guard; the generated-test-double diagnostics
(`CMP0020`–`CMP0031`) by
[ADR-0043](../adr/0043-compono-generated-test-doubles-design.md)'s design,
extended by [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)
for `CMP0022`, `CMP0029`, `CMP0030`, and `CMP0031`.

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

## CMP0020 — Test-double interface is not accessible

**Severity:** Informational — never fails the build.

**Message:** `'{Interface}' cannot have a generated test double`

**Cause:** The interface (or a `private`/`protected` nested interface
reached through it) isn't accessible to a top-level generated type — the
double is always emitted outside any containing type, so it can never
implement a private/protected interface, even from a call site that could
otherwise see it.

**Fix:** None needed to keep the interface working — it falls back to the
ordinary runtime-provider path (`UseNSubstitute()`, `Register<T>()`,
`.For<T>()`). Widen the interface's accessibility only if you specifically
want a generated double for it.

## CMP0021 — Unsupported test-double member kind

**Severity:** Informational — never fails the build.

**Message:** `'{Interface}' declares member '{Member}' {Kind}, which
Compono cannot generate a test double for`

**Cause:** The interface declares an indexer, event, static abstract
member, a variable-argument (`__arglist`) method, or another member-kind
shape outside v1's supported set. (A `ref`/`out`/`in`/pointer/function-
pointer *parameter* on an otherwise-supported method is `CMP0026`, not
this code; a generic method whose return type depends on its own type
parameter is `CMP0031`, not this code either — a generic method is
supported as of v2, ADR-0044 Requirement 2.)

**Fix:** None needed — falls back to the ordinary runtime-provider path.

## CMP0022 — Diamond-colliding test-double member

**Severity:** Informational — never fails the build. **Scope (v2,
ADR-0044): this one identity only** — every other member of the interface,
including any other overload sharing the same name, is unaffected.

**Message:** `'{Interface}' declares member '{Member}{Signature}', whose
signature is also independently declared by another base interface (a
diamond collision) - Compono can't tell the two identities apart, so
neither gets a Configure() surface`

**Cause:** The exact same full signature (parameter types, `ref`/`out`/`in`
kind, and generic arity) is declared by two different base interfaces
reached through the interface's transitive closure — a genuine C# overload
(two members of the same name but a *different* signature) is unaffected
by this diagnostic; it gets its own per-overload `Configure()`
surface instead — see
[`Compono.TestDoubles`](../packages/compono-testdoubles.md#overloaded-members)'s
"Overloaded members" section. Verification (`Verify()`) reuses this same per-overload
surface — a diamond-colliding identity has no `Verify()` surface either.

**Fix:** None needed — that one identity falls back to a deterministic
default; the rest of the double is unaffected.

## CMP0023 — Test-double interface member collides with `Configure()`/`Verify()`

**Severity:** Informational — never fails the build.

**Message:** `'{Interface}' declares its own member named '{Configure|Verify}'`

**Cause:** The interface declares its own `Configure` or `Verify` member
with a signature that would shadow the corresponding generated bridge —
any non-method member of that name (property/field/event, which always
wins over an extension), or a method callable with zero arguments.
"Callable with zero arguments" is broader than "zero-parameter":
`Configure(int mode = 0)` (all parameters optional) and
`Configure(params int[] modes)` (trailing `params`) both collide too, the
same applicability rule the C# compiler itself uses for overload
resolution — and likewise for a `Verify`-named member.

**Fix:** None needed — falls back to the ordinary runtime-provider path.

## CMP0024 — Test-double member collides with an inherited `object` member

**Severity:** Informational — never fails the build. **Scope: always the
whole interface** — an object-member collision has no constructible
fallback body at any granularity, the same disposition as any other
no-constructible-body shape.

**Message:** `'{Interface}' declares member '{Member}', whose generated
configuration extension collides with an inherited 'object.{Member}'
member of the same arity`

**Cause:** A member's generated configuration extension collides with an
inherited `object` member. For a non-overloaded member the extension is
always zero-argument — `ToString`, `GetHashCode`, and `GetType` collide;
`Equals` doesn't (`object.Equals(object)` takes one argument, so a
zero-argument generated `Equals` extension never collides with it). For an
**overloaded** member (v2, ADR-0044) the extension carries the real
overload's own parameter list instead: a genuinely zero-parameter overload
of `ToString`/`GetHashCode`/`GetType` still collides, and a non-generic,
single-*required*-parameter overload of `Equals` collides too — unless
that parameter's type is ref-like (e.g. `Span<T>`), which has no boxing or
reference conversion to `object` at all, or the parameter is the overload's
own trailing `params` array (`Equals(params int[] values)`), which keeps a
reachable spelling at every arity except exactly one.

**Fix:** None needed — falls back to the ordinary runtime-provider path.

## CMP0025 — Unsupported test-double return shape

**Severity:** Informational — never fails the build.

**Message:** `'{Interface}' declares member '{Member}' returning {Shape},
which Compono cannot generate a test double for`

**Cause:** The member's return type is ref-like (e.g. `Span<byte>`),
by-ref-returning, a pointer or function pointer, or a non-nullable
reference type (or a `Task<T>`/`ValueTask<T>` wrapping one) with no
deterministic default.

**Fix:** None needed — falls back to the ordinary runtime-provider path.

## CMP0026 — Unsupported test-double parameter shape

**Severity:** Informational — never fails the build. **Scope: always the
whole interface.** (A `ref`/`out`/`in` parameter on a member with a
same-named sibling is scoped instead — see `CMP0030` below, not this
code.)

- **A pointer or function-pointer parameter, at any nesting depth**
  (including inside an array, e.g. `int*[]`) — it requires the method to
  be declared `unsafe`, which this feature never emits.
- **A `ref`/`out`/`in` parameter on a *solo* member** (no same-named
  sibling at all) — "overload-set-internal partial support" presupposes
  an overload set; a solo member has no set to preserve, so it keeps v1's
  original disposition unchanged.
- **An `out` parameter whose own type has no deterministic default**
  (e.g. a non-nullable reference type) — there's no constructible
  fallback body at all, even for an overload with a surfaced sibling; same
  as any other no-deterministic-default case.
- **A generic method's own type parameter used as `T?` in a parameter**
  (v2, ADR-0044 Amendment 6 Finding 15, unified by Amendment 9) —
  constrained or unconstrained, regardless of which constraint. A `T?`
  usage can require a C# 9+ constraint restatement on the explicit
  interface implementation to disambiguate its inherited, oblivious
  reference-or-value-type meaning; correctly modeling exactly when that's
  *required*, and with which exact keyword, isn't attempted (two review
  rounds gave conflicting answers even for the constrained case), so
  every `T?`-using type parameter is diagnosed and excluded alike.

**Message:** `'{Interface}' declares member '{Member}' with parameter
'{Parameter}' {Shape}, which Compono cannot generate a test double for`

**Cause:** A method parameter is `ref`/`out`/`in`, a pointer, a function
pointer, or a generic type parameter used as `T?`.

**Fix:** None needed — falls back to the ordinary runtime-provider path.

## CMP0027 — Set-only test-double property is unsupported

**Severity:** Informational — never fails the build.

**Message:** `'{Interface}' declares set-only property '{Property}'`

**Cause:** The property has a setter but no getter, so there's no
`ReturnConfig<T>` value to read back — `Configure()` has nothing to
configure a return for. (`Verify()`'s call *count* is orthogonal to this;
a set-only property is unsupported because it has no readable value, not
because of anything to do with verification.)

**Fix:** None needed — falls back to the ordinary runtime-provider path.

## CMP0028 — Conflicting test-double metadata across discoveries

**Severity:** Informational — never fails the build.

**Message:** `'{Interface}' was discovered multiple times with different
generic-argument nullability`

**Cause:** The same interface was reached from two call sites with
different generic-argument nullability (e.g. a member typed
`IProvider<string>` and one typed `IProvider<string?>`). Compono generates
exactly one test double per interface and can't guarantee it correctly
reflects every discovery.

**Fix:** Request the interface with consistent nullability everywhere it's
composed. `ComponoGeneratedTestDoubles` is read once for the whole
compilation — there's no per-interface switch to disable it for just this
leaf; if you can't make every call site consistent, the interface will
need to fall back to a different provider (e.g. `UseNSubstitute()`) for
the whole project instead.

## CMP0029 — Test-double members generate colliding zero-argument extensions

**Severity:** Informational — never fails the build. **Scope (v2,
ADR-0044): the colliding identities only** — every other member of the
interface, including a genuine overload of the same name with its own
non-zero parameter list, is unaffected.

**Message:** `'{Interface}' declares member '{Member}', whose generated
configuration extension has no parameters to disambiguate it from another
same-named member's own generated extension`

**Cause:** Two or more same-named members inherited through the
interface's transitive closure don't share a full signature (so
`CMP0022`'s diamond-collision check doesn't catch them — a property vs. a
method, or two methods with a different real parameter list), but each
one's own generated configuration extension is genuinely zero-parameter —
a property's extension always is; a method's is unless it's part of a real
overload set with its own distinguishing parameter list. Two identical
zero-parameter extension signatures are an unresolvable `CS0111` collision
if both kept their surface.

**Fix:** None needed — the colliding identities fall back to a
deterministic default; any other overload of the same name that keeps a
real parameter list is unaffected.

## CMP0030 — Overload-scoped unsupported test-double parameter shape

**Severity:** Informational — never fails the build. **Scope (v2,
ADR-0044): this one overload only** — every other member of the
interface, including this overload's own dispatch body and its sibling
overloads, is unaffected. (This is the scoped counterpart to `CMP0026`'s
whole-interface `ref`/`out`/`in`-with-no-sibling case — the two are kept
as separate diagnostics rather than one message with two different
"does this fall back to the runtime-provider path" meanings.)

**Message:** `'{Interface}' declares member '{Member}' with parameter
'{Parameter}' as a ref/out/in parameter. This overload has no
Configure() surface, but it still dispatches via a deterministic default -
its sibling overloads, and the rest of the interface, are unaffected.`

**Cause:** A `ref`/`out`/`in` parameter on a member that has at least one
same-named sibling of any shape.

**Fix:** None needed — this overload still dispatches deterministically;
its sibling overloads keep their own `Configure()` surface.

## CMP0031 — Unsupported test-double generic return shape

**Severity:** Informational — never fails the build. **Scope: always the
whole interface** — same no-constructible-body bucket as `CMP0025`, under
its own code rather than reusing that one's "returning {Shape}" message
shape (a generic-return dependency is a relationship to the method's own
type parameters, not itself a return "shape").

**Message:** `'{Interface}' declares generic method '{Member}' whose
return type references its own type parameter, which Compono cannot
generate a deterministic default for. This leaf falls back to the
ordinary runtime-provider path.`

**Cause:** A generic method's return type references its own type
parameter anywhere in its symbol graph (`T Get<T>()`, `Task<T> GetAsync<T>()`,
`IEnumerable<T> Filter<T>()`) — there's no concrete slot type or
deterministic default to construct a body around, regardless of what the
caller ultimately closes the type parameter to. A generic method whose
return type *doesn't* depend on its own type parameter (`ILogger<T>`'s own
`Log<TState>`/`BeginScope<TState>`) is unaffected — see
[`Compono.TestDoubles`](../packages/compono-testdoubles.md#generic-methods).

**Fix:** None needed — falls back to the ordinary runtime-provider path.

## Next

- [Troubleshooting: Common Errors](../troubleshooting/common-errors.md) —
  indexed by symptom as well as code, with full worked examples.
- [Glossary](glossary.md) — terms these diagnostics reference (composition
  root, composition plan, provider, and similar).
