# Compono.XunitV3.Aot

xUnit v3 **Native AOT** integration — the `[Compose]` theory data attribute
for consumers publishing their test project with `PublishAot=true`, using
xUnit v3's own official Native AOT package family
(`xunit.v3.aot.mtp-v2` and friends). See
[ADR-0066](../adr/0066-compono-xunitv3-aot-package-architecture.md) for the
full architecture decision and
[RESEARCH-0031](../research/0031-native-aot-framework-native-testing-admission-research.md)/
[RESEARCH-0032](../research/0032-compono-xunitv3-native-aot-package-architecture.md)
for the evidence behind it.

## `Compono.XunitV3` vs. `Compono.XunitV3.Aot` — which do I install?

These are **two separate, mutually exclusive packages**, not one package
with a mode switch — never reference both in the same project.

| | `Compono.XunitV3` | `Compono.XunitV3.Aot` |
|---|---|---|
| Use when | Your test project runs under ordinary reflection-mode xUnit v3 (the default for almost every consumer today) | Your test project publishes with `PublishAot=true` using xUnit v3's own AOT package family |
| xUnit package it pairs with | `xunit.v3.mtp-v2` | `xunit.v3.aot.mtp-v2` |
| Minimum xUnit v3 version | Whatever `Compono.XunitV3` already supports | **4.0.0+** — the version that introduced xUnit's Native AOT support at all |
| Minimum .NET | Compono's ordinary floor | **.NET 9+** — xUnit's own stated Native AOT requirement |
| `[Compose]` surface | Full: inline values, `[Shared]`, `[Compose<TProfile>]`, `[Compose<TProfile, TConfig>]` | Plain `[Compose]`, `[Compose<TProfile>]`, `[Compose<TProfile, TConfig>]` — no inline values or `[Shared]` yet |
| `[ComposeData]` surface | Full: `[ComposeData]`, `[ComposeData<TProfile>]`, `[ComposeData<TProfile, TConfig>]`, row `[Shared]` | Full: the same three forms and row `[Shared]` — composed theory rows are at parity across both engines |

**Why two packages, not one:** xUnit v3 ships its reflection-mode and
Native AOT surfaces as genuinely separate, non-interchangeable assemblies
— referencing both `xunit.v3.mtp-v2` and `xunit.v3.aot.mtp-v2` (or
`Compono.XunitV3` and `Compono.XunitV3.Aot`) in the same project fails to
compile (`CS0433`, a duplicate `TheoryAttribute`/`DataAttribute`
identity). This is a hard technical constraint, not a design choice — see
ADR-0066 for the full account of why a single package supporting both
modes isn't achievable through any supported NuGet/MSBuild mechanism.

You can, however, have **both packages installed across different test
projects in the same solution** — one project running JIT with
`Compono.XunitV3`, another publishing AOT with `Compono.XunitV3.Aot` —
that's a normal, supported setup.

## When to install

You publish your xUnit v3 test project as Native AOT (`PublishAot=true`)
and want theory parameters composed automatically, the same way
`Compono.XunitV3` does for ordinary JIT tests:

```bash
dotnet add package Compono
dotnet add package Compono.XunitV3.Aot
dotnet add package xunit.v3.aot.mtp-v2
```

## What it gives you (Phase 1)

- **`[Compose]`** — every theory parameter is composed. Usage is
  textually identical to `Compono.XunitV3`'s own `[Compose]`:

  ```csharp
  using Compono.XunitV3.Aot;
  using Xunit;

  public sealed class WidgetTests
  {
      [Theory]
      [Compose]
      public void Widget_is_composed_and_test_passes(Widget widget, string leaf)
      {
          Assert.False(string.IsNullOrEmpty(widget.Name));
          Assert.False(string.IsNullOrEmpty(leaf));
      }
  }
  ```

  Only the `using` (`Compono.XunitV3.Aot` instead of `Compono.XunitV3`)
  and the referenced packages change when moving a test project from JIT
  to Native AOT — the `[Theory]`/`[Compose]` source itself doesn't.

## Profile-based composition (Phase 2)

`[Compose<TProfile>]` and `[Compose<TProfile, TConfig>]` — added in Phase 2
(ADR-0067/PLAN-0067), after Phase 1 shipped plain `[Compose]` only:

- **`[Compose<TProfile>]`** — applies `TProfile` (`where TProfile :
  ICompositionProfile, new()`) before composing every parameter, textually
  identical to `Compono.XunitV3.ComposeAttribute<TProfile>`:

  ```csharp
  [Theory]
  [Compose<MyProfile>]
  public void Test_uses_profile(Widget widget) { ... }
  ```

  The generated registration constructs `TProfile` via a direct,
  compile-time-closed `Composer.Create(b => b.AddProfile<TProfile>())` call
  — no reflection, since `TProfile`'s `new()` constraint is already
  enforced by the C# compiler at the attribute use site.

- **`[Compose<TProfile, TConfig>]`** — constructs `TConfig` from this
  attribute's own constructor arguments, then `TProfile` from that
  `TConfig`, then applies it — textually identical to
  `Compono.XunitV3.ComposeAttribute<TProfile, TConfig>`:

  ```csharp
  [Theory]
  [Compose<MyProfile, MyConfig>(MyConfigValue.Foo, "widget")]
  public void Test_uses_configured_profile(Widget widget) { ... }
  ```

  See [ADR-0067](../adr/0067-compono-xunitv3-aot-profile-support.md) for
  the full design — the compile-time-verified-construction mechanism this
  form uses, and how it differs from `Compono.XunitV3`'s runtime
  `ConfigProfileBinder`.

## Using xUnit fixtures in profiles

`TestContext.Current.RequireFixture<TFixture>()` is available here too —
the same method, textually identical to `Compono.XunitV3`'s, in namespace
`Compono.XunitV3.Aot`. Call it **inside a `Register` factory** to register
a value xUnit already initialized (a class or collection fixture):

```csharp
using Compono.XunitV3.Aot;
using Xunit;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        ConnectionString = "Server=localhost;Database=app";
        await Task.Yield();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed record DatabaseSettings(string ConnectionString);

public sealed class DatabaseProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Register(
        () => new DatabaseSettings(
            TestContext.Current.RequireFixture<DatabaseFixture>().ConnectionString));
}

public sealed class DatabaseTests : IClassFixture<DatabaseFixture>
{
    [Theory]
    [Compose<DatabaseProfile>]
    public void Settings_come_from_the_fixture(DatabaseSettings settings)
    {
        Assert.Equal("Server=localhost;Database=app", settings.ConnectionString);
    }
}
```

The same rules apply as in `Compono.XunitV3`: call it inside the `Register`
factory, matching is **exact-type only** (xUnit's `GetFixture` returns
`null` when the requested exact fixture type isn't available;
`RequireFixture` throws a descriptive exception instead), and it **never
waits** for initialization (throwing immediately if the fixture isn't
ready). The fixture stays xUnit's — `RequireFixture` never disposes it or
changes its lifetime. As in `Compono.XunitV3`, Compono doesn't own or
dispose composed values; dispose what you create. See the
[Compono.XunitV3 Package Guide](compono-xunitv3.md#using-xunit-fixtures-in-profiles)
and [ADR-0069](../adr/0069-xunitv3-require-fixture-accessor.md).

### Known limitation: assembly fixtures

Class and collection fixtures (`IClassFixture<T>`, `ICollectionFixture<T>`)
work fully. An **assembly fixture** — `[assembly: AssemblyFixture(typeof(T))]`
— currently fails to *compile* under xUnit v3's Native AOT generator
(4.0.1 / 4.0.2) with `CS1593` in the generator-emitted
`AssemblyFixtureAttribute*.g.cs`, before Compono is involved at all. This is
an upstream xUnit issue ([xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664))
— reported fixed upstream in xUnit **4.0.3-pre.19**, but not yet validated
by Compono, and no stable release carrying the fix is inside Compono's
supported range yet. Until one reaches your package graph, use a class or
collection fixture instead.

## Composed theory rows (`[ComposeData]`)

`Compono.XunitV3.Aot` supports the full `[ComposeData]` family —
`[ComposeData]`, `[ComposeData<TProfile>]`, and
`[ComposeData<TProfile, TConfig>]` — plus the row `[Shared]` behavior
(ADR-0070, issue #163; `Compono.XunitV3.Aot.SharedAttribute` is the AOT
package's own `[Shared]` marker, since it cannot reference the JIT
package's — ADR-0070 Amendment 3). The rows come from the same public
static member shapes, bind the same leading-positional way, merge the same
row/attribute metadata, and follow the same seed rules as
[`Compono.XunitV3`'s `[ComposeData]`](compono-xunitv3.md#composed-theory-rows-composedata).
This is full JIT/AOT parity for ComposeData: moving a `[ComposeData]` theory
from a JIT project to an AOT project changes only the `using` and the
referenced packages — the same three examples live in both
`test/Compono.XunitV3.SampleTests/ComposeDataTests.cs` and
`test/Compono.XunitV3.Aot.SampleTests/ComposeDataTests.cs`. (This is unlike
`[Compose]`, whose inline values and `[Shared]` remain AOT follow-ups — see
[Not yet supported](#not-yet-supported).)

Because the AOT package's attributes are thin markers with no runtime
`GetData` path, `Compono.Generators` reads the member and validates every
row shape **at compile time**, then emits a
`Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)`
registration whose factory composes each row through real,
compile-time-closed-generic `CompositionRow.Resolve<T>()` calls. A shape the
generator can't name or call is a build error, not a silent omission — see
[ComposeData diagnostics on Native AOT](#composedata-diagnostics-on-native-aot).

### ComposeData vs. `Compono.XunitV3`: where they differ

A `[ComposeData]` theory behaves identically across both engines except where
a shape is unreachable from generated code. The differences:

- **Diagnosis time.** An unsupported shape is a compile-time `CMP` diagnostic
  on AOT, reported against the test method; the JIT path reports the
  equivalent condition at run time, except the AOT-only cases in ADR-0070
  Amendments 2 and 4, which JIT accepts.
- **Inherited theory methods.** JIT resolves the row member against the
  concrete reflected test class; AOT resolves it against the method's
  **declaring** class (ADR-0070 Amendment 2). A member that exists only on a
  derived class is rejected on AOT (`CMP0051`). Set `MemberType` to name the
  holder explicitly, but note it selects one fixed holder type — it does not
  reproduce JIT's per-derived-class selection.
- **Inaccessible nested holders.** A `private`/`protected` nested
  `MemberType` or `SkipType` holder may work under JIT reflection but is
  rejected on AOT (`CMP0044`) and must never be silently skipped. Make it at
  least `internal`.
- **Assembly fixtures.** As with `[Compose]`, only class and collection
  fixtures work under xUnit's Native AOT generator today
  ([xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664)); an
  assembly-fixture-backed row member won't compile. See
  [Known limitation: assembly fixtures](#known-limitation-assembly-fixtures).
- **Disposal.** xUnit's JIT runner disposes `IDisposable` theory arguments;
  its Native AOT core does not. Don't pass a fixture-owned disposable as a
  theory parameter on either engine — see [`Compono.XunitV3`'s
  disposal note](compono-xunitv3.md#disposal). A composed disposable you
  *do* own, such as a `[Shared] IMeterFactory`, must be disposed in the test
  body here (`using var _ = meters;`), because nothing else disposes it on
  AOT.

The register-the-row-config recipe for reading a row's `TConfig` in the test
body, and the wrapper-profile pattern for profile constructor arguments, work
unchanged on this package. See [`Compono.XunitV3`'s per-row
configuration](compono-xunitv3.md#per-row-configuration) and
[Use Profiles](../how-to/use-profiles.md#a-profile-whose-constructor-takes-runtime-objects).

## Not yet supported

`Compono.XunitV3.Aot.ComposeAttribute`'s three forms don't support inline
values (`[Compose(42, "widget")]`) or `[Shared]` parameter reuse yet — real,
additional generator work deferred to a later phase. There is no syntax to
attempt either (a real compile error results, not silent incorrect
behavior). See
[PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md)/
[PLAN-0067](../plans/0067-compono-xunitv3-aot-profile-support-impl-plan.md).

Composed theory rows (`[ComposeData]`, issue #163) **are** fully supported
under AOT — see
[Composed theory rows (`[ComposeData]`)](#composed-theory-rows-composedata)
above for the three forms, the row `[Shared]` behavior, the compile-time
diagnostics, and the JIT/AOT differences. This doesn't change `[Compose]`'s
limits above.

## `[Compose<TProfile, TConfig>]` diagnostics differ from `Compono.XunitV3` — by design

`Compono.XunitV3.ComposeAttribute<TProfile, TConfig>` validates `TConfig`'s/
`TProfile`'s constructor shape and the supplied profile configuration
arguments **at runtime**, via `ConfigProfileBinder` reflection (the first
time the attribute's `GetData` runs), because C#'s generic-constraint
system cannot express "has a constructor accepting exactly this type."
`Compono.XunitV3.Aot` has no runtime `GetData` path at all to check this
through, so `Compono.Generators` performs the identical checks **at
compile time** instead, against the real declared symbols and this
attribute's own compile-time-constant constructor arguments:

| Diagnostic | Condition |
|---|---|
| `CMP0041` | `TConfig` does not have exactly one *usable* public constructor - isn't a usable named type at all, has zero or more than one public constructor, or its sole public constructor has a by-ref (`ref`/`out`/`in`) parameter, a `dynamic`-typed parameter, or is marked `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`/`[RequiresAssemblyFiles]` |
| `CMP0042` | `TProfile` does not have exactly one *usable* public constructor accepting exactly one `TConfig`-typed parameter - same additional exclusions as `CMP0041` (by-ref parameter, or a prohibited AOT attribute) applied to the matching constructor |
| `CMP0043` | A supplied constructor argument's count/nullability/type doesn't match `TConfig`'s single constructor's parameters |
| `CMP0044` | `TProfile`, `TConfig`, a `typeof(...)`/enum-typed argument's own type, or (for an array-typed argument) its declared element type or any `typeof(...)`/enum-typed value recursively embedded in it isn't accessible from the generated top-level registration (commonly: a profile/config type nested `private` inside the attributed test class) |
| `CMP0045` | More than one Compose-family attribute (`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`) on one test method |
| `CMP0046` | The selected `TConfig`/`TProfile` constructor doesn't satisfy the type's `required` members |
| `CMP0047` | The selected `TConfig`/`TProfile` constructor is marked `[Obsolete("...", error: true)]`, `[Experimental("...")]`, or non-optional `[CompilerFeatureRequired("...")]`, making any use of it a compiler error |
| `CMP0048` | An accessible sibling constructor with a higher `[OverloadResolutionPriority]` could silently supersede the selected `TConfig`/`TProfile` constructor at the generated call site |
| `CMP0049` | `[Compose<TProfile>]`'s `TProfile` has a public parameterless constructor marked `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`/`[RequiresAssemblyFiles]` |

This is a deliberate, documented divergence — not accidental drift between
the two packages: earlier, IDE-visible feedback instead of a
test-execution-time failure, and a byproduct of `Compono.Generators` being
able to see everything it needs at compile time for this attribute family.
See [ADR-0067](../adr/0067-compono-xunitv3-aot-profile-support.md)'s "One
accepted, explicit divergence" section. `Compono.XunitV3.Aot.ComposeAttribute<TProfile,
TConfig>`'s generated code constructs both types via a direct `new`
call with no reflection and no `[DynamicallyAccessedMembers]` annotation
anywhere in the path — strictly stronger AOT-safety than the JIT-mode path
it mirrors.

## Unsupported method/parameter shapes (`CMP0040`)

Because `ComposeAttribute` is a marker only (see below) with no runtime
fallback, an unsupported `[Compose]`-attributed method shape is rejected
at compile time with **`CMP0040`** instead:

- A generic test method — rejected before any registration is built, so
  without this diagnostic the method would simply never be discovered by
  xUnit's Native AOT pipeline, with no diagnostic anywhere.
- A `ref`/`out`/`in` or `params` parameter — same silent-non-discovery
  failure mode as above.
- A parameter type that can never be a legal `CompositionRow.Resolve<T>()`
  type argument — an open generic parameter, a `ref struct`, a pointer, a
  function pointer, or an unsupported array shape. Without this
  diagnostic, code generation would proceed and emit a registration
  containing an illegal `CompositionRow.Resolve<T>()` call, so the
  consumer would see a generated-code compiler error instead of an
  actionable diagnostic pointing at their own test method.

See [CMP0040](../reference/diagnostics.md#cmp0040-unsupported-componoxunitv3aot-attributed-method-signature)
for the full diagnostic reference entry.

## ComposeData diagnostics on Native AOT

Because `[ComposeData]` is validated at compile time under AOT (there is no
runtime member read to fail late), every unsupported shape is a `CMP`
diagnostic against the test method:

| Diagnostic | Condition |
|---|---|
| `CMP0051` | The `[ComposeData]` member wasn't found, or more than one inherited/hidden static member matches (ambiguous) — set `MemberType` to disambiguate, or rename the member |
| `CMP0052` | The member isn't public and static, requires arguments, or is a generic method; **or** a selected member (or an active skip-condition property) is a `static abstract`/`static virtual` interface member the generated code cannot call |
| `CMP0053` | The member's return type, or a typed row's element type, can never be a supported collection/row shape; **or** the member's type implements `IAsyncEnumerable<T>` for more than one element type, where the JIT path's interface choice isn't deterministic |
| `CMP0054` | `MemberType` is an open generic type; **or** the selected member (or an active skip-condition property) is declared on an open generic type the generated code cannot name — set `MemberType`/`SkipType` to a non-generic or closed generic type that declares it |
| `CMP0055` | `[ComposeData<TProfile, TConfig>]`: a typed row's element 0 can never be the `TConfig` configuration value |
| `CMP0056` | A typed row supplies more values than the method has parameters, or a row value can never be assigned to its parameter |
| `CMP0057` | An active skip condition is invalid — both `SkipUnless` and `SkipWhen` are set, or the named property isn't a public static `bool` property on the holder (or a base type) |
| `CMP0040` | The `[ComposeData]`-attributed method's signature is unsupported (a generic method, a `ref`/`out`/`in`/`params` parameter, an uncomposable parameter type, or two `[Shared]` parameters sharing a type) |
| `CMP0042` | `[ComposeData<TProfile, TConfig>]`'s `TProfile` doesn't have exactly one usable public constructor accepting a single `TConfig` parameter |
| `CMP0044` | A type referenced by `[ComposeData<...>]` — a profile/config type or a `MemberType`/`SkipType` holder — isn't accessible from the generated top-level registration |
| `CMP0045` | More than one Compose-family attribute (including `[ComposeData…]`) on one test method |
| `CMP0046`–`CMP0048` | The same required-members / prohibited-constructor / overload-supersede conditions as the `[Compose<TProfile, TConfig>]` table above, applied to `[ComposeData<TProfile, TConfig>]`'s selected `TProfile(TConfig)` constructor |
| `CMP0049` | `[ComposeData<TProfile>]`'s `TProfile` has a public parameterless constructor marked `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`/`[RequiresAssemblyFiles]` |
| `CMP0047` (members) | A selected member (or its getter) — or an active skip property (or its getter) — is marked `[Obsolete(error: true)]`/`[Experimental]`/non-optional `[CompilerFeatureRequired]` |

### AOT-only exclusions

ADR-0070 Amendments 2 and 4 (with §7a) approve **eight** shapes that JIT
accepts but generated AOT code cannot name or call. Each is rejected with a
compile-time `CMP` diagnostic — never a silently omitted test — and each has a
workaround:

1. A member that exists only on a derived class of an inherited theory method
   (`CMP0051`) — set `MemberType` to a fixed holder, or move the member to the
   declaring class (Amendment 2 §1).
2. A `private`/`protected` nested `MemberType`/`SkipType` holder (`CMP0044`) —
   make it at least `internal` (Amendment 2 §2).
3. A member or active skip-condition holder that contains a type parameter,
   e.g. declared on a generic base (`CMP0054`) — set `MemberType`/`SkipType` to
   a non-generic or closed holder (Amendment 4 §2a).
4. A test parameter whose type contains the generic base's type parameter, e.g.
   `List<T>` (`CMP0040`) (Amendment 4 §2b).
5. A selected member/getter or active skip property/getter marked
   `[Obsolete(error: true)]`/`[Experimental]`/non-optional
   `[CompilerFeatureRequired]` (`CMP0047`) — remove the marking or move the data
   (Amendment 4 §2c).
6. A `static virtual` interface member with a default body selected through an
   interface `MemberType` (`CMP0052`) — move the data to a class/struct static
   member (Amendment 4 §2d).
7. A member whose type implements `IAsyncEnumerable<T>` more than once with no
   deterministic JIT choice (`CMP0053`) — see the supported mixed shape below
   (Amendment 4 §2e).
8. An active skip-condition property that is a `static virtual` interface
   property (`CMP0052`) — move it to a class/struct static property or a
   non-virtual static interface property (Amendment 4 §7a).

A `static abstract` interface member — data member or skip property — gets the
same `CMP0052` twin but is **not** an exception: JIT fails it at run time too
(Amendment 4 §2d, §7b).

**Supported mixed async shape.** A member type that implements
`IAsyncEnumerable<T>` with one reference-type element plus any number of
value-type implementations is supported on both engines — variance never
applies to value types, so the reference rows are always read (Amendment 4
§2e). Only the ambiguous cases above are rejected.

**Accepted residual (C-4).** AOT reports an invalid *active* attribute-level
skip condition at compile time even when no row ends up evaluating it — a
known, accepted compile-time-versus-run-time difference (Amendment 2 §3,
PLAN-0071 B4.9).

**Generic test-class bases are supported** — a `[ComposeData]` theory declared
on a generic base is registered against the open-generic base and resolved to
the closed test class at runtime, with no IL/AOT warnings. See
[ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md)
Amendments 2 and 4 for the full account and
[the diagnostics reference](../reference/diagnostics.md) for every code.

## How it actually works under the hood

Unlike `Compono.XunitV3.ComposeAttribute`, this package's `ComposeAttribute`
carries **no runtime composition logic** — it's a thin marker attribute,
matching the shape xUnit's own official Native AOT extensibility samples
(`AotCsvDataSource`, `AotRetryFact`, `AotTraitExtensibility`) use.
`Compono.Generators` discovers every `[Compose]`-attributed method at
compile time and emits a
`Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)`
registration — xUnit v3's own official Native AOT theory-data mechanism —
whose factory closure calls real, compile-time-closed-generic
`CompositionRow.Resolve<T>()` calls, exactly the same core `Compono`
composition machinery `Compono.XunitV3` uses. `DataAttribute.GetData` is
never invoked under this path at all; xUnit's AOT pipeline calls the
registered factory directly.

## Native AOT publish and run

```bash
dotnet publish YourTestProject.csproj -c Release -f net10.0 \
  -p:RuntimeIdentifier=<rid> -p:SelfContained=true -p:PublishAot=true -p:UseAppHost=true
./bin/Release/net10.0/<rid>/publish/YourTestProject
```

Pass `RuntimeIdentifier`/`SelfContained` as `-p:` MSBuild properties, not
the `-r <rid>`/`--self-contained` CLI shorthand flags — the shorthand form
can fail this exact publish shape with `MSB3030: Could not copy ... apphost
... because it was not found` in some project configurations. The
resulting native binary is a real, self-hosting xUnit v3 test executable —
run it directly; there is no `dotnet test` step for a published Native AOT
binary.

## Compatibility

- **xUnit v3 4.0.0+** required — earlier versions have no Native AOT
  support to integrate with at all.
- **.NET 9.0+** required — matches xUnit's own stated floor.
- **`Compono.XunitV3` is completely unaffected** by this package's
  existence — same package ID, same dependency on the reflection-mode
  `xunit.v3.extensibility.core`, same behavior. Installing
  `Compono.XunitV3.Aot` in a new project never changes anything about an
  existing `Compono.XunitV3` project elsewhere in your solution.
