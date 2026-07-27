# Coding standards

These are the conventions for this codebase. `src/Compono` doesn't have
much code in it yet, so most of these are the standard to hold new code
to from the start, not a description of an established pattern already in
use — where that's the case, it's called out explicitly rather than
implied as "already how it's done here."

**Access modifiers**
- Default to the most restrictive access modifier that satisfies the
  requirement — don't make something `public` because it's convenient
  right now, make it as narrow as the actual callers require. This matters
  more than usual for Compono specifically: `Compono` is a published NuGet
  package, and every `public` member becomes part of a compatibility
  surface consumers can take a dependency on — something `internal` costs
  nothing to change later, something `public` is a breaking change to
  remove or reshape.
- Within a project, types and members are `internal` by default. Only
  what forms that project's actual public contract — the surface another
  package or a consuming project is meant to call — should be `public`.
  `docs/public-api.md`'s stated goal ("easy to discover, small enough to
  learn") is a reason to keep the public surface deliberately small, not
  just an access-modifier technicality.
- To give a test project access to `internal` members, use
  `InternalsVisibleTo` on the project being tested, targeting its
  `.Tests` project — don't widen a member to `public` just so a test can
  reach it.

**Naming**

| Element | Convention | Example |
|---|---|---|
| Classes, records, structs, enums | `PascalCase` | `CompositionContext`, `CompositionRequest` |
| Interfaces | `I`-prefixed `PascalCase` | `ICompositionProvider`, `ICompositionScope` |
| Public properties and methods | `PascalCase` | `IsHandled`, `TryComposeAsync()` |
| Private fields | `_camelCase` | `_random` |
| Protected fields | `_camelCase` | `_scope` |
| Local variables and method parameters | `camelCase` | `compositionRequest`, `cancellationToken` |
| Generic type parameters | `T` prefix + `PascalCase` | `TValue`, `TResult` |
| Constants (`const`) | `PascalCase` | `DefaultCollectionSize` |
| Static readonly fields | `PascalCase` | `DefaultSeed` |
| Configuration/options classes | `PascalCase` + `Options` suffix | `CompositionOptions`, `BogusOptions` |
| Async methods | end in `Async`, return `Task`/`Task<T>` (or `ValueTask`/`ValueTask<T>` on the resolution hot path — see wrinkle below) | `ResolveAsync()`, `TryComposeAsync()` |
| Boolean properties/variables | positive assertion | `IsValid`, `HasErrors`, `CanCompose` (not `NotValid`, `NoErrors`) |
| Abbreviations in names | treated as ordinary words, `PascalCase`/`camelCase` applies through them | `NSubstituteProvider` (not `NSUBSTITUTEProvider`), `xmlWriter` (not `XMLWriter`) |

A couple of these have a repo-specific wrinkle beyond the table:

- `ValueTask`/`ValueTask<T>` is reserved for the composition resolution hot
  path — methods called once per resolved value on every composition
  (`ICompositionProvider.TryComposeAsync`, `ICompositionContext.ResolveAsync`,
  per `docs/architecture.md`) — where the allocation `Task<T>` costs on a
  usually-synchronous path is worth avoiding. Everywhere else, async
  methods return `Task`/`Task<T>` even though the naming convention
  (`...Async`) is the same either way; don't reach for `ValueTask` as a
  general-purpose "slightly faster `Task`" default; it has sharper rules
  around awaiting exactly once that aren't worth taking on outside a
  genuine hot path.
- Classes are `sealed` by default — it's not just a style preference,
  sealed types avoid virtual-dispatch overhead and let the JIT devirtualize
  calls, which matters for a library sitting on the hot path of every
  composed test. Only leave a class unsealed if it's genuinely designed
  for extension (e.g. a base type an integration package is meant to
  derive from, like a `CompositionProfile` base class).
- Test classes: `sealed class <TypeUnderTest>Tests`. Test methods:
  `MethodName_ExpectedBehavior_WhenCondition`.

**Type inference and instantiation**

- Prefer `var` when the type is obvious from the right-hand side:
  ```csharp
  var context = new CompositionContext(seed);   // type is right there
  var provider = registrations.GetProvider<ICustomerProvider>();   // method name says it
  ```
  Use an explicit type when the right-hand side doesn't make it obvious
  (e.g. a method call whose return type isn't clear from its name).
- Prefer target-typed `new()` when the left-hand side already states the
  type — don't repeat it:
  ```csharp
  CompositionSeed seed = new(4219);
  private readonly List<ICompositionProvider> _providers = new();
  ```

**`IOptions<T>` configuration classes**

If a configuration class is bound via `IOptions<T>` (relevant to any
package that integrates with `Microsoft.Extensions.DependencyInjection`,
e.g. a hosting/test-framework integration), it must:

- Use the `Options` suffix, per the naming table above.
- Declare a `public const string SectionName` whose value matches the class
  name with the suffix stripped (e.g. `BogusOptions.SectionName = "Bogus"`).
  This keeps the binding self-contained and refactor-safe — renaming the
  class doesn't silently orphan a hardcoded section-string elsewhere.
- Be registered with the Configuration Binder pattern:
  `services.Configure<TOptions>(configuration.GetSection(TOptions.SectionName))`,
  not a hand-rolled `GetSection(...).Bind(opt)` scattered at the call site.

**Nullable reference types**
- `<Nullable>enable</Nullable>` is on for every csproj — keep it on for any
  new project.
- Prefer `required` init-only properties for mandatory state
  (`public required Type RequestedType { get; init; }`) over constructor
  boilerplate or nullable-and-checked-later fields.
- Use the null-forgiving operator (`!`) only with an explanatory comment —
  never use it without one; an uncommented `!` just hides the question of
  who's actually guaranteeing the value is non-null. This applies
  everywhere, including the common "a framework sets this post-construction"
  case (e.g. a source generator emitting a member assignment after
  construction) — the comment should say which framework/mechanism
  guarantees it.
- Guard arguments at method entry with `ArgumentNullException.ThrowIfNull(param)`,
  not `throw new ArgumentNullException(nameof(param))` — same effect, less
  boilerplate:
  ```csharp
  public void Register(ICompositionProvider provider)
  {
      ArgumentNullException.ThrowIfNull(provider);
      // ...
  }
  ```
- Use guard clauses / early returns at the top of a method rather than
  nesting the real logic inside a conditional — flatten the happy path
  instead of indenting it.

**Async**
- Thread `CancellationToken` as an explicit last parameter through public
  async APIs — don't drop it partway through a call chain. `docs/architecture.md`'s
  `ICompositionProvider.TryComposeAsync` signature already models this.
- Be deliberate about `ConfigureAwait`: `Compono` and its integration
  packages are redistributed libraries consumed by arbitrary application
  and UI code, which is exactly the case `ConfigureAwait(false)` exists
  for — use it on library-internal awaits that don't need to resume on a
  captured context. This is the opposite default from an application's own
  `Program.cs`/composition-root code, which has no captured context to
  avoid deadlocking on in the first place.
- Always `await` — never block on async code with `.Result`, `.Wait()`, or
  `.GetAwaiter().GetResult()`. Blocking on async code from a sync context is
  a deadlock risk and defeats the point of the async chain you just wrote.
- No `async void`, ever, including event handlers — exceptions thrown from
  `async void` can't be caught by the caller, they just crash the process.
  Use `async Task` (or a `Task`-returning handler if the framework allows
  it); if a genuinely fire-and-forget signature is unavoidable, that's a
  design question worth raising, not a default to reach for.
- Don't use `Task.Run()` to push work onto the thread pool as a substitute
  for a real async API or to "make something async" — resolving a
  composition graph is CPU-bound, in-process work; wrapping it in
  `Task.Run` just adds a thread-pool hop without a concrete need for it.
  Reserve `ValueTask`/`Task` return types on the resolution pipeline for
  genuine cases (an integration provider that does real I/O, e.g. a
  network-backed semantic data source), not to make synchronous code look
  async.
- Prefer `IAsyncEnumerable<T>` for streaming data sources instead of
  buffering everything into a `List<T>` first — reach for it whenever a
  caller can start processing composed items as they arrive (e.g.
  `CreateMany<T>()` over a large collection) rather than waiting for the
  whole set to materialize.

**DI / composition**
- **Application-level wiring** (a consumer's own `Program.cs` or test
  project setup): register providers/profiles inline via the builder API
  (`Composer.Create(builder => builder.UseNSubstitute().UseBogus())`), not
  a bespoke wiring layer per project.
- **Package-level composition roots** (an integration package's own public
  entry point, e.g. `Compono.NSubstitute`'s `UseNSubstitute()` extension
  method on the builder): a DI-extension/builder pattern is the sanctioned
  shape — `docs/public-api.md` already models this for `UseNSubstitute()`/
  `UseBogus()`. A published package's entry point is a different concern
  from an application's internal wiring: consumers need a stable,
  documented way to opt into what the package provides, the same reason
  `AddControllers`/`AddAuthentication`-style extension methods exist in
  the wider .NET ecosystem.
- No static singletons, ever. Composition state belongs on a
  `CompositionContext`/`ICompositionScope` instance
  (`docs/architecture.md`), not behind a static/`XManager.Instance`
  accessor — the whole point of an explicit context is avoiding mutable
  global state (`docs/public-api.md`'s "free of mutable global state"
  goal).
- All dependencies are injected via the constructor into `private readonly`
  fields (`protected readonly` if a base class exposes the dependency to
  subclasses). Don't inject via public settable properties or method
  parameters — the one exception is where a framework requires it (e.g.
  `[FromServices]` in a minimal-API-hosted integration).
- Don't reach for `IServiceProvider` / the service-locator pattern to pull a
  dependency out of thin air. The only legitimate uses are a genuine
  composition root and a factory class whose *entire, explicit* purpose is
  resolving something at runtime (e.g. resolving a per-request provider
  instance) — if you're tempted to inject `IServiceProvider` anywhere
  else, that's a sign the real dependency should just be
  constructor-injected instead.
- **Constructor style**: use traditional constructors with explicit
  `private readonly` field assignments in application/library code, not
  primary constructors:
  ```csharp
  public sealed class NSubstituteProvider : ICompositionProvider
  {
      private readonly ISubstituteFactory _factory;

      public NSubstituteProvider(ISubstituteFactory factory)
      {
          _factory = factory;
      }
  }
  ```
  Primary constructors are reserved for test classes (fixtures, test data
  builders), where the extra brevity doesn't cost you a place to put
  validation or additional setup logic later.

**Immutability and object modeling**
- Default to immutable: properties are `init`-only unless the type has a
  concrete reason to mutate after construction.
- DTOs, commands, queries, and value objects **must** be immutable — model
  them as `record` (reference-type data with structural equality) or
  `readonly record struct` (small value objects with value semantics):
  ```csharp
  public sealed record CompositionRequest(Type RequestedType, string? Name);
  public readonly record struct CompositionSeed(int Value);
  ```
- Types with real identity and lifecycle (`CompositionContext`,
  `ICompositionScope` implementations) are regular `class`es, not records —
  they have identity, not structural equality, and any state change goes
  through a domain method, never a public setter.
- **Required properties vs. constructor parameters**: use `required`
  properties for DTOs and configuration objects, where the shape is "all of
  these must be set, in whatever order." Use constructor parameters for
  types where construction is itself a meaningful operation with
  invariants to enforce (a `CompositionContext` being handed its seed and
  scope at creation), not just data assembly.

**Expression-bodied members and pattern matching**
- Prefer expression-bodied members for simple, single-line implementations:
  `public bool IsHandled => Result is not CompositionResult.NotHandled;`
  reads better than the equivalent four-line property.
- Prefer pattern matching wherever it makes the code more readable — that's
  the bar, not "use pattern matching everywhere":
  - Prefer switch *expressions* over switch *statements* when every branch
    just produces a value — this fits the `NotHandled`/`Success`/`Failure`
    result shape from `docs/architecture.md` particularly well.
  - Use `is` pattern matching for null checks and type checks (`if (result
    is { } r)`, `if (provider is INamedProvider named)`) instead of
    `!= null` plus a separate cast.

**LINQ**
- Method/fluent syntax exclusively — `providers.Where(p => ...).Select(...)`,
  never query syntax (`from x in providers where ... select ...`).
- Keep chains readable: break a chain across multiple lines, one method
  per line, once it's more than a method or two long. A short chain
  (`_providers.OfType<T>().FirstOrDefault()`) is fine to keep on one line.

**Methods**
- Four parameters, max (not counting a trailing `CancellationToken`, which
  doesn't count against the limit). A method that needs a 5th parameter
  takes a parameter object instead — a `record`, per **Immutability and
  object modeling** above, not a loose 5th argument bolted on.
- No boolean flag parameters that change what the method *does*. A `bool`
  that makes a method take two different code paths internally is a sign
  the method is actually two methods wearing one name — split it into two
  well-named ones instead. This doesn't apply to a `bool` that's genuinely
  just the data being set (a setter whose value happens to be a boolean)
  — that's fine as-is.

**Collection types on API surfaces**
- Default to `IEnumerable<T>` for method parameters and return types when
  the caller only needs to iterate — it keeps the implementation flexible
  (a `List<T>`, a LINQ query, a lazily-generated sequence can all satisfy
  it) and defers execution instead of forcing materialization the caller
  may not need.
- Use `IReadOnlyList<T>` or `IReadOnlyCollection<T>` when the caller needs
  indexed access or `Count` without being able to mutate.
- Use `IList<T>`/`ICollection<T>` only when callers genuinely need to
  mutate the collection through that reference — don't reach for a mutable
  interface just because it's more permissive.
- Never expose a raw `List<T>`, `Dictionary<TKey, TValue>`, or other
  concrete collection type on a `public` or `internal` API surface — always
  the appropriate interface from the tiers above.
- Inside domain types, back the collection with a `private List<T>` (or
  `Dictionary<TKey, TValue>`, etc.) field and expose it as
  `IReadOnlyCollection<T>`/`IReadOnlyList<T>` — mutation happens through a
  named method (`AddProvider`, `Register`), not by handing the caller the
  backing collection to mutate directly.

**Collections, strings, time, and concurrency**
- Prefer collection expressions (C# 12) over `new`-based initialization:
  ```csharp
  int[] values = [1, 2, 3];
  List<string> names = [];
  IReadOnlyList<ICompositionProvider> providers = [.. baseProviders, extra];
  ```
- Prefer string interpolation (`$"..."`) for one-off string construction;
  reach for `StringBuilder` only when concatenating in a loop or other hot
  path. Never use `string.Format` — interpolation covers the same cases
  more readably. Use raw string literals (`"""..."""`) for embedded
  multi-line text (generated-code snippets, test fixtures) instead of
  escaped `\n`/`\"` soup.
- Anything whose behavior depends on "now" (a deterministic seed's
  timestamp component, a time-based semantic value provider) takes a
  `TimeProvider` via constructor injection instead of calling
  `DateTime.UtcNow`/`DateTimeOffset.UtcNow` directly — that's what makes it
  deterministically testable (fake/advance time in a test, `TimeProvider.System`
  in the real registration), and determinism is a stated product goal
  (`README.md`'s "Deterministic by design"). Code that doesn't have
  time-dependent *behavior* to test (a one-off log timestamp, say) doesn't
  need the abstraction.
- For shared mutable state, default to lock-free options first —
  `ConcurrentDictionary`, `Interlocked`, or an immutable snapshot swap —
  before reaching for an explicit lock. When a real critical section is
  unavoidable, use `System.Threading.Lock` (.NET 9), not `lock (someObject)`
  on an arbitrary reference type.

**Error handling**

Core principle: **exceptions are for the unexpected.** An exception signals
a bug or an environmental failure — something that should never happen
during normal operation (a null that violates an invariant, a generator
hitting a case it doesn't recognize). An *expected* outcome — a request
that no provider can satisfy, a value that fails validation — is not a
bug, and must be communicated through a return value, not an exception.
Using exceptions for control flow hides intent: the method signature no
longer tells the caller what can go wrong, callers have to guess (or read
the implementation) to find out, and it bypasses the type system doing the
job it's good at.

`docs/architecture.md`'s own resolution-pipeline design already models
this: a provider reports `NotHandled`/`Success`/`Failure` rather than
throwing when it can't satisfy a request ("This avoids exception-driven
provider selection and preserves meaningful failures"). The rules below
make that explicit and give it a name, rather than introducing a new
direction:

- Never throw for an expected failure. Outcomes like "no provider could
  satisfy this request" or "the requested type has no compatible
  constructor" are represented as a diagnostic result the caller checks,
  not an exception the caller catches.
- A `CompositionResult`-style outcome type (or equivalent `Result<T>`) is a
  **domain/composition-logic** concept. Genuinely infrastructure-adjacent
  code (file I/O, a network-backed semantic provider) can still use
  nullable as its "not found" contract where that fits better — don't
  force every layer into the same result-wrapping shape if a plain
  nullable already says what's needed.
- Catch the base `Exception` type only in a top-level boundary handler
  (e.g. the generator's own top-level diagnostic-reporting catch, or a
  composition root's top-level handler). Everywhere else, catch specific
  exception types you actually know how to handle — a broad
  `catch (Exception)` outside the top-level boundary hides bugs instead of
  handling them.
- When re-throwing, always use bare `throw;` — never `throw ex;`, which
  resets the stack trace and erases where the exception actually happened.
- Never swallow an exception silently. At an absolute minimum, log it and
  re-throw; if you're catching it, you owe the next person a record of what
  happened.
- Log an exception once, at the boundary where it's actually handled — not
  at every layer it passes through on the way there. A caught-logged-rethrown
  exception logged again three layers up just duplicates the same incident
  in the logs with no new information.
- Use `throw new ArgumentOutOfRangeException(...)` for exhaustiveness
  guards on switches over enums — this is exactly the "should never
  happen" case exceptions are for, and it applies whether the switch is a
  statement or an expression (a `_ => throw new
  ArgumentOutOfRangeException(...)` discard arm on a switch expression).

Adopting a `Result<T>`-style type (whether a small hand-rolled one or a
NuGet package like FluentResults) is itself a design decision, not a
drive-by choice — per `design-decisions.md`, that gets a light design dive
and a decision record the first time a real composition-outcome case needs
it, rather than reaching for a package or rolling a custom type inline the
first time the need comes up.

**Code structure and organization**
- No God classes. If a class has grown enough unrelated responsibility that
  it's hard to describe in one sentence, that's the signal to split it —
  the provider/plan composition model (`docs/architecture.md`) exists
  specifically to avoid this by keeping each provider narrowly scoped, so
  reaching for a God class usually means fighting the grain of the
  existing design rather than following it.
- No `#region` blocks, anywhere. If a class feels like it needs regions to
  stay navigable, the class is too large; split it instead of organizing
  the sprawl.
- No static classes except true stateless utility helpers (pure functions,
  no state). Static **mutable** state is never permitted, full stop —
  that's just a singleton wearing a different hat, and it's covered by the
  same "no static singletons, ever" rule as **DI / composition** above.
- Extension methods use C# 14 extension block syntax, not the legacy
  `static class` + `this`-parameter pattern:
  ```csharp
  public static class CompositionBuilderExtensions
  {
      extension(CompositionBuilder builder)
      {
          public CompositionBuilder UseNSubstitute() => ...;
      }
  }
  ```
  Call sites don't change (`builder.UseNSubstitute()` either way) — this is
  purely about how the extension is declared.

  A few more rules on extension classes specifically:
  - Name the class after the extended type plus `Extensions`
    (`CompositionBuilderExtensions` for `CompositionBuilder`) — don't fold
    unrelated extensions for different types into one shared "Extensions"
    grab-bag class.
  - One extended type per file, one static class per file, filename
    matches the class name.
  - The extension class follows the same access-modifier rules as any
    other type — `internal` if it's not meant to cross an assembly
    boundary, `public` if it is (an integration package's
    `UseX()`-style extension method is exactly the case that needs to be
    `public`).
  - When extending a type from a third-party or BCL assembly (not a type
    this repo owns), the extension class lives in the project that owns
    the *integration* with that dependency, not wherever the first caller
    happens to be — e.g. an extension on an NSubstitute type belongs in
    `Compono.NSubstitute`, not in `Compono` core (which must not reference
    NSubstitute at all — see `design-decisions.md` rule 3).
- Don't qualify instance member access with `this.` unless it's required
  to resolve a naming ambiguity (e.g. a constructor parameter shadowing a
  field of the same name).

**Member ordering within a class**

Order members top-to-bottom by access modifier, and within each access
level by kind:

1. Public constants and static members
2. Public properties
3. Public constructors
4. Public methods
5. Internal members (same sub-order as above: constants/static, properties,
   constructors, methods)
6. Protected members (same sub-order)
7. Private members (same sub-order)

Separate each group with a single blank line. The point is that anyone
reading the class top-down sees its public contract first and its
implementation detail last, in a consistent place every time — not
alphabetical order, not chronological-by-when-it-was-added.

**File / namespace organization**
- Namespace matches folder path exactly, file-scoped namespace syntax.
- One public type per file, filename == type name.
- Organize by feature/concern folder (e.g. `Composition/`, `Providers/`,
  `Generators/`), not by technical layer.

There's no `Directory.Build.props` yet, so `Nullable`, `ImplicitUsings`,
and `TargetFramework` will need to be set per-csproj until one exists — if
you add a new project before that gap is closed, copy the settings from
`src/Compono/Compono.csproj` rather than introducing a fresh variant, and
consider adding `Directory.Build.props` at that point if you're already
touching multiple project files.
