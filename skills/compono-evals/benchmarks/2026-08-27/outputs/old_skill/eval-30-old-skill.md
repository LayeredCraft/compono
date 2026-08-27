No — don’t write recording fakes just because the old tests used NSubstitute vocabulary.

If the members are eligible non-overloaded interface members and the project intentionally moved to `Compono.TestDoubles`, migrate the vocabulary to generated test-double APIs:

- `Arg.Any<T>()` / `Arg.Is<T>(...)` → usually **remove them**. Generated doubles are argument-independent.
- `Received(1)` → `Verify().Member().Once()`
- `Received(2)` → `Verify().Member().Exactly(2)`
- `DidNotReceive()` → `Verify().Member().Never()`
- return/throw setup → `Configure().Member().Returns(...)` / `.Throws(...)`

Example shape:

```csharp
secretsManager.Configure()
    .GetSecretValueAsync()
    .Returns(response);

await sut.DoWorkAsync();

secretsManager.Verify()
    .GetSecretValueAsync()
    .Once();
```

Use `[Shared]` when the test needs to configure/verify the same double that is injected into the SUT:

```csharp
[Theory]
[Compose<GeneratedTestDoubleProfile>]
public async Task Test(
    [Shared] IAmazonSecretsManager secretsManager,
    MyProvider sut)
{
    secretsManager.Configure()
        .GetSecretValueAsync()
        .Returns(response);

    await sut.LoadAsync();

    secretsManager.Verify()
        .GetSecretValueAsync()
        .Once();
}
```

Only consider hand-written recording fakes if the old assertions depended on behavior `Compono.TestDoubles` intentionally does **not** support, such as argument-specific matching, different returns per argument, or call-order verification. If those aren’t needed, generated doubles are the intended replacement for `IAmazonSecretsManager`, `IConfigurationBuilder`, and `ILoggerFactory`.
