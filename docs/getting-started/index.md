# Getting Started

Compono composes complete test environments — object graphs, shared
dependencies, test doubles, and semantic fake data — from a source-generated
composition plan, instead of building each dependency by hand or relying on
runtime reflection to invent one.

## What "composition" means here

Most .NET test helpers focus on *object generation*: given a type, produce
an instance of it, usually by reflecting over its constructor and filling in
plausible values. Compono starts from a different question: what does this
*test* need — an object graph, a couple of test doubles, a shared value
reused across several parameters, some semantically-realistic data — and how
do all of those pieces fit together into one deterministic result?

A `Composer` is built once per test (or once per profile, reused across
tests) from a declarative configuration: registrations, type/member rules,
profiles, and provider extensions like `UseNSubstitute()`/`UseBogus()`. From
that configuration, Compono's source generator produces the actual
construction code at compile time — there's no reflection-based fallback in
the default path, so what runs in your test is regular, debuggable,
AOT-friendly C#, not a runtime specimen builder walking your type via
`Activator.CreateInstance`.

```csharp
var composer = Composer.Create(builder =>
{
    builder.UseNSubstitute();
    builder.UseBogus();
});

var customer = composer.Create<Customer>();
```

## How this differs from AutoFixture or hand-written setup

- **Hand-written setup** — you write every `new Customer(...)` call, every
  `Substitute.For<IRepository>()`, every fake `DateTimeOffset`, by hand, in
  every test that needs one. Explicit, but repetitive, and it's easy for
  test setups to drift out of sync with the type they're building.
- **AutoFixture** — a runtime specimen engine invents values reflectively,
  which is fast to start with but can produce recursion/omission surprises
  on cyclic or ambiguous graphs, and gives you a customization API layered
  on top of that reflective core. See the
  [AutoFixture migration guide](../migrating-from-autofixture.md) if that's
  your starting point.
- **Compono** — you declare configuration once (registrations, rules,
  profiles), Compono's generator turns that into real construction code at
  compile time, and a failed composition reports a readable path and a
  reproducible seed (see
  [Determinism and Seeding](../concepts/determinism-and-seeding.md)) instead
  of a generic reflection stack trace.

None of this requires giving up AutoFixture wholesale on day one — Compono
is adopted incrementally, one test class or profile at a time.

## Next

- Never touched Compono before? Start with [Installation](installation.md),
  then [Your First Composed Theory](first-test.md).
- Want a curated path for your specific situation (migrating from
  AutoFixture, already using xUnit, etc.)? See [Learning Paths](learning-paths.md).
- Already through the basics? [Next Steps](next-steps.md) branches out to
  Concepts, How-to Guides, and the Cookbook.
