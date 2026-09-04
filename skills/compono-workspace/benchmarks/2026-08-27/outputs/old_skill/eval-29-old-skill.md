Use the generated-double `Configure()` surface, not NSubstitute argument matchers:

```csharp
secretsManager
    .Configure()
    .GetSecretValueAsync()
    .Returns(Task.FromResult(response));
```

Current Compono setup should look like this:

```csharp
public sealed class GeneratedTestDoubleProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.UseGeneratedTestDoubles();
}
```

And the test needs the shared generated double so the configured instance is the same one injected into the SUT:

```csharp
[Theory]
[Compose<GeneratedTestDoubleProfile>]
public async Task Reads_secret(
    [Shared] IAmazonSecretsManager secretsManager,
    MySecretsProvider sut)
{
    var secretName = "my-secret";

    var response = new GetSecretValueResponse
    {
        SecretString = """{"key":"value"}"""
    };

    secretsManager
        .Configure()
        .GetSecretValueAsync()
        .Returns(Task.FromResult(response));

    // act/assert...
}
```

Important difference from NSubstitute: `Compono.TestDoubles` is argument-independent. This setup returns `response` for every `GetSecretValueAsync(...)` call. Since `GetSecretValueAsync` is non-overloaded here, the generated configuration member takes no arguments.
