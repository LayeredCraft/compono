With generated Compono test doubles, don’t use `Received`/`Arg.Is`. Verification is argument-independent, and because `IConfigurationBuilder.Add` is non-overloaded, the generated verification member is parameterless:

```csharp
[Theory]
[Compose<AwsSecretsManagerProviderCompositionProfile>]
public void Adds_secrets_manager_configuration_source(
    [Shared] IConfigurationBuilder configurationBuilder)
{
    configurationBuilder.AddSecretsManager();

    configurationBuilder.Verify().Add().Once();
}
```

If the extension/method under test uses the return value of `IConfigurationBuilder.Add(...)`, configure the fluent return first:

```csharp
[Theory]
[Compose<AwsSecretsManagerProviderCompositionProfile>]
public void Adds_secrets_manager_configuration_source(
    [Shared] IConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Configure().Add().Returns(configurationBuilder);

    configurationBuilder.AddSecretsManager();

    configurationBuilder.Verify().Add().Once();
}
```

Key migration point: `Compono.TestDoubles` cannot express the old argument predicate:

```csharp
Arg.Is<IConfigurationSource>(source =>
    source is SecretsManagerConfigurationSource)
```

So `Verify().Add().Once()` verifies that `Add` was called once, but not which `IConfigurationSource` instance/type was passed. If asserting the argument type is essential, keep that particular test on NSubstitute or test through a real/recording `IConfigurationBuilder` instead.
