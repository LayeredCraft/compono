No — not just because the old tests used NSubstitute vocabulary.

If the migrated members are eligible, non-overloaded interface members and the project intentionally uses `Compono.TestDoubles`, prefer generated doubles:

- `Arg.Any` / `Arg.Is` usually disappears because generated doubles are argument-independent.
- `Received(1)` → `double.Verify().Member().Once()`
- `Received(2)` → `double.Verify().Member().Exactly(2)`
- `DidNotReceive()` → `double.Verify().Member().Never()`
- return setup → `double.Configure().Member().Returns(...)`
- exception setup → `double.Configure().Member().Throws(...)`

Use `[Shared]` when the test needs to configure/verify the same double that was injected into the SUT:

```csharp
[Theory]
[Compose<GeneratedTestDoubleProfile>]
public async Task Example(
    [Shared] IAmazonSecretsManager secretsManager,
    AwsSecretsManagerProvider sut)
{
    secretsManager.Configure()
        .GetSecretValueAsync()
        .Returns(Task.FromResult(response));

    await sut.LoadAsync();

    secretsManager.Verify()
        .GetSecretValueAsync()
        .Once();
}
```

Only write recording fakes if the test truly needs behavior `Compono.TestDoubles` does not provide, such as:

- argument-specific returns,
- argument-specific verification,
- call-order assertions,
- capturing arguments for assertions,
- unsupported interface/member shapes.

For simple migrated `Arg.Any`, `Received(n)`, and `DidNotReceive` against eligible non-overloaded members, generated `Configure()`/`Verify()` is the intended replacement, not hand-written fakes.
