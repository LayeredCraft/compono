Use the generated double’s `Configure()`/`Verify()` surface, not `Received`/`Arg`.

```csharp
[Theory]
[Compose<GeneratedTestDoubleProfile>]
public void Adds_secrets_manager_configuration_source(IConfigurationBuilder configurationBuilder)
{
    // IConfigurationBuilder.Add returns IConfigurationBuilder, so configure the
    // generated double to return itself when the extension method calls Add.
    configurationBuilder.Configure()
        .Add(Match.Any<IConfigurationSource>())
        .Returns(configurationBuilder);

    configurationBuilder.AddSecretsManager(/* test args */);

    configurationBuilder.Verify()
        .Add(Match.Is<IConfigurationSource>(
            source => source is SecretsManagerConfigurationSource))
        .Once();
}
```

If the `IConfigurationBuilder` is also injected into a composed SUT/collaborator, mark it shared:

```csharp
public void Test([Shared] IConfigurationBuilder configurationBuilder, SomeService sut)
```

But for a direct extension-method test, the key migration is:

```csharp
configurationBuilder.Verify()
    .Add(Match.Is<IConfigurationSource>(
        source => source is SecretsManagerConfigurationSource))
    .Once();
```
