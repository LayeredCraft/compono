No — not solely because the old tests used NSubstitute APIs.

For eligible non-overloaded interface members, migrate the NSubstitute vocabulary directly to `Compono.TestDoubles` generated-double APIs:

```csharp
Arg.Is<T>(predicate)      -> Match.Is<T>(predicate)
Arg.Any<T>()              -> Match.Any<T>()
Received(1).Member(...)   -> Verify().Member(...).Once()
Received(2).Member(...)   -> Verify().Member(...).Exactly(2)
DidNotReceive().Member(...) -> Verify().Member(...).Never()
```

Examples for the AWS Secrets Manager Provider shapes:

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(r => r.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

```csharp
configurationBuilder.Verify()
    .Add(Match.Is<IConfigurationSource>(
        source => source is SecretsManagerConfigurationSource))
    .Once();
```

Use hand-written recording fakes only if the test genuinely needs something outside current generated doubles, such as:

- true argument capture for later arbitrary inspection,
- invocation-aware callbacks/side effects,
- call-order verification,
- sequential/call-count-based responses,
- unsupported member shapes.

If the members are eligible non-overloaded interface members and the project has `Compono.TestDoubles`, `UseGeneratedTestDoubles()`, and `<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>`, generated doubles are the intended migration path. Also remember to use `[Shared]` when the test needs to configure/verify the same double instance that is injected into the composed SUT.
