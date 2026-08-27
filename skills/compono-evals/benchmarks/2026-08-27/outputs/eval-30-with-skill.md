No — not solely because the old tests used NSubstitute APIs.

For eligible non-overloaded interface members, migrate the vocabulary directly to `Compono.TestDoubles` generated doubles:

```csharp
Arg.Is<T>(p)        -> Match.Is<T>(p)
Arg.Any<T>()       -> Match.Any<T>()
Received(1)        -> Verify().Member(...).Once()
Received(2)        -> Verify().Member(...).Exactly(2)
DidNotReceive()    -> Verify().Member(...).Never()
```

Examples relevant to AWS Secrets Manager Provider:

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(r => r.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

```csharp
configurationBuilder.Verify()
    .Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSource))
    .Once();
```

Use `[Shared]` when the test parameter double must be the same instance injected into the SUT:

```csharp
public async Task Test(
    [Shared] IAmazonSecretsManager secretsManager,
    SecretsManagerProvider sut)
```

Only write a hand recording fake if the migrated test genuinely needs something generated doubles do not provide, such as argument capture for later arbitrary inspection, invocation-aware callbacks/side effects, ordered-call assertions, sequential responses, unsupported members, etc. But `Arg.Is`, `Arg.Any`, `Received`, and `DidNotReceive` by themselves are not evidence that recording fakes are needed.
