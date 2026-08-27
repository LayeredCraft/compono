Use the generated double’s `Configure()` surface and Compono matchers:

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(request => request.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

Typical current Compono setup:

```csharp
public sealed class GeneratedTestDoublesProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.UseGeneratedTestDoubles();
    }
}
```

And in the test, make the double shared with the composed system under test:

```csharp
[Theory]
[Compose<GeneratedTestDoublesProfile>]
public async Task Loads_secret(
    [Shared] IAmazonSecretsManager secretsManager,
    /* other composed parameters */)
{
    var response = new GetSecretValueResponse
    {
        SecretString = """{"SomeKey":"SomeValue"}"""
    };

    secretsManager.Configure()
        .GetSecretValueAsync(
            Match.Is<GetSecretValueRequest>(request => request.SecretId == secretName),
            Match.Any<CancellationToken>())
        .Returns(Task.FromResult(response));

    // act/assert
}
```

Mapping from the old NSubstitute setup:

- `Arg.Is<GetSecretValueRequest>(...)` → `Match.Is<GetSecretValueRequest>(...)`
- `Arg.Any<CancellationToken>()` → `Match.Any<CancellationToken>()`
- `.Returns(Task.FromResult(response))` stays the same
- no NSubstitute substitute or hand-written fake is needed for this eligible non-overloaded member.
