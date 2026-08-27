Current Compono.TestDoubles setup:

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(request => request.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

Mapping from the old NSubstitute setup:

```csharp
Arg.Is<GetSecretValueRequest>(...)  -> Match.Is<GetSecretValueRequest>(...)
Arg.Any<CancellationToken>()        -> Match.Any<CancellationToken>()
.Returns(...)                       -> .Returns(...)
```

Since `GetSecretValueAsync` is eligible/non-overloaded here and `IAmazonSecretsManager` resolves as a generated double, you don’t need NSubstitute or a hand-written fake.
