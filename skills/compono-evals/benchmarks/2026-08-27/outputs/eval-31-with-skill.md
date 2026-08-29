Use a local fake (or keep NSubstitute for that seam if the project allows it).

`Match.Any<T>()` / `Match.Is<T>()` only express **argument matching** for configuration/verification, e.g. “when called with any delegate and any token, return this fixed value” or “verify it was called once with a delegate matching this predicate.”

They do **not** support invocation-aware callbacks such as:

- inspect the actual invocation as a call object
- invoke the actual `RequestHandlerDelegate` argument
- run side effects before/after that invocation
- return the delegate’s actual result

So this NSubstitute shape:

```csharp
pipeline.InvokeAsync(
    Arg.Any<RequestHandlerDelegate>(),
    Arg.Any<CancellationToken>())
.Returns(call =>
{
    // use actual delegate argument
});
```

doesn’t translate to `Compono.TestDoubles` matchers.

A small local fake is the right migration boundary here, e.g. a fake pipeline that records before/after, calls the supplied `RequestHandlerDelegate`, and returns its `Task`/result. Use TestDoubles for plain matched returns and filtered verification; use a fake when the behavior depends on executing the invocation itself.
