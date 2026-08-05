---
title: Compose a Substitute With One Method Stubbed
description: Compose a real NSubstitute substitute and configure just the one call your test cares about.
packages: [Compono, Compono.XunitV3, Compono.NSubstitute]
concepts: [shared-values, providers]
---

# Compose a Substitute With One Method Stubbed

## Problem

A composed interface dependency is fine as an ordinary substitute for most
of the test — every call it doesn't need to care about can behave however
NSubstitute's own defaults behave — except for one specific call, whose
return value the test needs to control.

## Solution

```csharp
public interface IOrderRepository
{
    Task<Order> SaveAsync(Order order, CancellationToken cancellationToken);
}
```

```csharp
[Theory]
[Compose<ApplicationTestProfile>] // a profile that calls builder.UseNSubstitute()
public async Task Handle_ReturnsTheRepositorysSavedOrder(
    [Shared] IOrderRepository repository, OrderHandler handler, PlaceOrder command, Order savedOrder)
{
    repository.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(savedOrder);

    var result = await handler.Handle(command);

    result.Should().Be(savedOrder);
}
```

`repository` is a real, composed NSubstitute substitute — `[Shared]` makes
it the exact instance `OrderHandler`'s own constructor received, so
stubbing `SaveAsync` here affects the call `handler.Handle(...)` actually
makes. Every other member of `IOrderRepository` (if there were more) stays
an ordinary, unconfigured substitute call.

## Discussion

Configure the stub *before* the composed value is used, not after —
NSubstitute setup only affects calls made after `Returns(...)` runs, the
same as configuring any other NSubstitute substitute.

Want to verify the call happened, not control its return value? Use
`Received(...)` the same way, on the same `[Shared]` instance — the two
aren't mutually exclusive; stub the call's return value and verify it was
called exactly once in the same test if both matter.

## See also

- [`Compono.NSubstitute` Package Guide](../packages/compono-nsubstitute.md)
  — the full provider mechanics, abstract-class substitution, and how it
  fits the resolution pipeline.
- [Shared Values](../concepts/shared-values.md) — why `[Shared]` is what
  makes stubbing/verifying a composed substitute possible at all.
- The same pattern working end to end in a real, buildable project →
  [Sample: ASP.NET API](../samples/aspnet-api.md).
