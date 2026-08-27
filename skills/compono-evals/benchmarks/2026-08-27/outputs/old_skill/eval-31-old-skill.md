No — don’t try to migrate that setup to `Compono.TestDoubles`.

`Compono.TestDoubles` is intentionally not a general mocking framework:

- no `Arg.Any<T>()` equivalent
- no `Match.Any<T>()` / `Match.Is<T>()` for generated doubles
- no callback-style `Returns(callInfo => ...)`
- no argument-aware behavior
- overload parameters on `Configure()` are only for C# overload selection; the values are discarded

So a setup that must:

1. capture the actual `RequestHandlerDelegate` argument,
2. invoke it,
3. run side effects before/after,
4. return that delegate’s result,

cannot be expressed with generated test doubles.

Keep a local fake for that pipeline collaborator, or keep that particular interface on NSubstitute if you still want mock-style callback behavior.

Example shape:

```csharp
private sealed class RecordingPipeline<TResponse> : IPipeline<TResponse>
{
    public bool BeforeCalled { get; private set; }
    public bool AfterCalled { get; private set; }

    public async Task<TResponse> InvokeAsync(
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        BeforeCalled = true;

        var result = await next(cancellationToken);

        AfterCalled = true;
        return result;
    }
}
```

Use `Compono.TestDoubles` for simple configured returns/throws and call counts. For callback-through behavior like this, a fake is the right migration target.
