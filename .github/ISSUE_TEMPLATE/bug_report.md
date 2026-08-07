---
name: Bug report
about: Something isn't working the way it should
title: "[Bug]: "
labels: ["type: fix"]
assignees: []
---

## Describe the bug

A clear, concise description of what's wrong.

## Reproduction

Minimal steps or a minimal code sample that reproduces the issue. A small
`Composer.Create(...)`/`[Compose]` snippet is more useful than a
description — see
[`Compono.Samples.BasicUsage`](https://github.com/LayeredCraft/compono/tree/main/samples/Compono.Samples.BasicUsage)
for the shape a minimal repro can follow.

```csharp
// paste a minimal repro here
```

## Expected behavior

What you expected to happen instead.

## Actual behavior

What actually happened — include the full exception message and, if
present, the `CompositionDiagnostic`/seed from a
`CompositionException` (see
[Troubleshooting](https://layeredcraft.github.io/compono/troubleshooting/)
for what these look like).

## Environment

- Compono version(s): (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`,
  `Compono.Bogus` — whichever apply)
- .NET SDK version:
- OS:

## Additional context

Anything else that might help — did this used to work in an earlier
version, does it only reproduce under a specific TFM, etc.
