Use a local fake (or keep NSubstitute for that seam if the project allows it).

`Match.Any<T>()` / `Match.Is<T>()` in `Compono.TestDoubles` are argument **matching** APIs for configured responses and filtered verification. They do not provide invocation-aware callbacks or argument capture.

So this kind of NSubstitute setup:

```csharp
pipeline.InvokeAsync(
    Arg.Any<RequestHandlerDelegate>(),
    Arg.Any<CancellationToken>())
.Returns(call =>
{
    // get actual delegate argument
    // run side effects before/after
    // invoke delegate
    // return its result
});
```

is outside current generated TestDoubles capability, because it needs:

- access to the actual invocation arguments,
- callback side effects,
- invoking a delegate argument,
- returning that delegate’s result.

`Match.Any<RequestHandlerDelegate>()` can match “any delegate” for a fixed `.Returns(...)`, but it cannot say “call the delegate argument and return what it returns.”

For migration, keep a small project-local fake for this pipeline behavior. Use generated TestDoubles for collaborators where the setup is ordinary matching/verification, but don’t force this callback-style middleware/pipeline seam into `Match<T>`.
