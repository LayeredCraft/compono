No — don’t try to express that with `Match.Any<T>()` / `Match.Is<T>()` in `Compono.TestDoubles`.

`Compono.TestDoubles` is intentionally not a general mocking framework:

- no argument matchers
- no argument-aware behavior
- no callback/`Returns(callInfo => ...)` equivalent
- configuration is per member/overload, not per argument value
- it cannot invoke one of the actual arguments and return that result

For this shape:

```csharp
pipeline.InvokeAsync(
    Arg.Any<RequestHandlerDelegate>(),
    Arg.Any<CancellationToken>())
```

where the fake must:

1. capture the real `RequestHandlerDelegate`
2. run side effects before/after
3. invoke that delegate
4. return the delegate’s result

you should keep a local fake/stub implementation, or keep using `Compono.NSubstitute` for that specific interface if you still want mocking-framework behavior there.

A hand fake is usually clearest:

```csharp
private sealed class RecordingPipeline : IPipeline
{
    public bool BeforeCalled { get; private set; }
    public bool AfterCalled { get; private set; }

    public async Task<Response> InvokeAsync(
        RequestHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        BeforeCalled = true;
        var result = await next();
        AfterCalled = true;
        return result;
    }
}
```

Use generated TestDoubles for simple “return this” / “throw this” collaborators. For callback-style middleware/pipeline behavior, a local fake is the right migration target.
