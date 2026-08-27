Use the generated double’s `Configure()` surface and Compono matchers:

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(request => request.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

Full shape in a Compono setup:

```csharp
var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());

var secretsManager = composer.Create<IAmazonSecretsManager>();

secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(request => request.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

If this double is injected into a composed SUT/test row, make sure the same `IAmazonSecretsManager` instance is shared with the SUT, e.g. with `[Shared]` in `Compono.XunitV3`/`Compono.TUnit`.

Mapping from the old NSubstitute call:

- `Arg.Is<GetSecretValueRequest>(...)` → `Match.Is<GetSecretValueRequest>(...)`
- `Arg.Any<CancellationToken>()` → `Match.Any<CancellationToken>()`
- `.Returns(Task.FromResult(response))` stays the terminal configuration call.
