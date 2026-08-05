# Naming Conventions

## Test methods: name the behavior, not the mechanism

This repository's own test suite follows
`MethodName_ExpectedBehavior_WhenCondition`, and it's a good default for
your own composed tests too — a name like `ServiceUsesTheSharedRepository`
in [Your First Composed Theory](../getting-started/first-test.md) is
readable without opening the method. Avoid encoding Compono mechanics
into the name (`ComposesAndAssertsOnRepository`) — the fact that a value
was composed is an implementation detail of *how* the test built its data,
not part of what the test is actually verifying.

## Profiles: name after the concern, not the consumer

Covered in full in [Organizing Profiles](organizing-profiles.md) — the
short version: `InfrastructureProfile`, not `OrderServiceTestsProfile`. A
profile's name should describe what it configures so a reader can guess
its contents without opening it.

## `[Shared]` parameters: name for what they represent, not that they're shared

`[Shared]` is metadata on the parameter, not part of its meaning — name
the parameter the same way you'd name it without `[Shared]`
(`repository`, not `sharedRepository`). The attribute already makes the
sharing visible at the declaration site; repeating it in the name is
redundant and drifts if the parameter later stops being shared.

## Test-only domain types: keep them honest about scope

A type that exists only to give a test something to compose (like
`Repository`/`OrderService` in the Getting Started walkthrough) should be
named for the domain concept it represents, not decorated as a test
artifact (`TestRepository`, `FakeOrderService`) — Compono composes plain
application types, and a type doesn't need to look different just because
a test happens to be the thing constructing it. Reserve a
`Fake`/`Stub`/`Mock` prefix for a type that's genuinely a hand-written
test double, to keep that signal meaningful.

## Next

- See these conventions applied together in a real, buildable project →
  [Samples](../samples/index.md).
