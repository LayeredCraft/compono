# Compono Manifesto

## Why Compono Exists

Compono exists because test composition in .NET deserves a design grounded in the capabilities and expectations of modern .NET.

AutoFixture demonstrated the value of automatic object creation, declarative test data, shared instances, and test-framework integration. It also enabled a style of testing that many teams came to depend on.

But the platform has changed.

Modern .NET now includes:

- Records
- Primary constructors
- Required members
- Nullable reference types
- Source generators
- Trimming
- Native AOT
- Modern test frameworks
- Better compile-time analysis
- Stronger expectations around determinism and diagnostics

Compono asks a fresh question:

> If a test composition framework were designed for modern .NET today, what should it look like?

Compono is not intended to reproduce AutoFixture feature-for-feature. It is a new product focused on composing complete test environments.

## What Test Composition Means

Object generation is only one part of preparing a test.

A test may require:

- A system under test
- Constructor dependencies
- Shared instances
- Test doubles
- Anonymous values
- Realistic semantic data
- Reusable project conventions
- Framework-specific parameter binding
- Deterministic reproduction of failures

Compono treats those needs as parts of a single composition problem.

Tests declare what they require. Compono determines how those requirements are satisfied.

## Core Principles

### Composition over generation

Compono is not primarily a fake-data generator or object factory.

Its purpose is to coordinate every contributor involved in preparing a test.

### Predictability over magic

Convenience should not come at the cost of understanding.

Resolution order must be deterministic and documented. When composition fails, Compono should explain:

- What was requested
- Why it was requested
- Which providers were considered
- Which provider was selected
- Where resolution failed
- How the failure can be reproduced

### Source-generated first

Compono should discover object construction metadata at compile time and generate composition plans wherever possible.

Runtime execution should focus on executing known plans rather than repeatedly inspecting types.

Runtime reflection remains an explicit architectural decision. It may eventually be:

- Disallowed
- Supported as a compatibility fallback
- Available only through an opt-in package or mode

Until that decision is finalized, reflection must not become an accidental dependency of the core design.

### Deterministic by default

A composition should be reproducible from its seed and configuration.

A failed test should report enough information to recreate the generated values and object graph.

### Modular by design

The core package must not depend on:

- xUnit
- NUnit
- MSTest
- NSubstitute
- Moq
- FakeItEasy
- Bogus

Those capabilities belong in integration packages built on stable extension contracts.

### Modern .NET first

Compono should prefer a small, coherent design for modern .NET over broad compatibility with legacy runtimes.

### Performance is a feature

Test infrastructure runs constantly.

Composition startup time, allocations, generated-plan execution, and test discovery overhead all matter.

Performance should be measured and protected rather than treated as a later optimization.

### Diagnostics are a product feature

Composition failures should be understandable without stepping through framework internals.

A useful failure is better than a clever fallback.

## What Compono Should Avoid Becoming

Compono should not become:

- An AutoFixture compatibility layer
- A monolithic testing toolkit
- A service locator hidden inside tests
- A reflection-heavy runtime
- A collection of unrelated convenience APIs
- A system whose behavior depends on mutable global state
- A framework where providers silently override one another
- A source of random, irreproducible test failures
- A feature-complete wrapper over every third-party integration

## Product Priorities

When design goals compete, Compono should prioritize:

1. A coherent test composition model
2. Predictable behavior and diagnostics
3. Performance through source-generated execution
4. Broad compatibility and convenience

This order may be refined as the project evolves, but convenience should not override clarity or architectural integrity.

## The North Star

> Test authors declare what a test needs. Compono satisfies those needs through explicit, deterministic, replaceable composition providers operating within a composition context.
