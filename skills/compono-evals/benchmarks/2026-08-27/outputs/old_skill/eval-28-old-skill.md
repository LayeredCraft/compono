With `Compono.TestDoubles`, don’t translate `Arg.Is<T>(...)`; generated doubles are argument-independent. Since `IConfigurationBuilder.Add` is non-overloaded, verify the member call itself:

```csharp
[Theory]
[Compose<YourCompositionProfile>]
public void Adds_secrets_manager_configuration_source(
    [Shared] IConfigurationBuilder configurationBuilder)
{
    // IConfigurationBuilder.Add returns non-nullable IConfigurationBuilder,
    // so configure the fluent return before exercising the extension.
    configurationBuilder.Configure().Add().Returns(configurationBuilder);

    configurationBuilder.AddSecretsManagerProvider(); // or the actual extension under test

    configurationBuilder.Verify().Add().Once();
}
```

If the extension composes another SUT that receives `IConfigurationBuilder`, keep the builder parameter `[Shared]` so the test body and SUT use the same generated double.

The important migration is:

```csharp
configurationBuilder.Received(1).Add(
    Arg.Is<IConfigurationSource>(source => source is SecretsManagerConfigurationSource));
```

becomes:

```csharp
configurationBuilder.Verify().Add().Once();
```

`Compono.TestDoubles` cannot assert the argument predicate. If verifying that the exact argument was a `SecretsManagerConfigurationSource` is essential, that specific assertion still requires `Compono.NSubstitute`/NSubstitute or a different observable seam.
