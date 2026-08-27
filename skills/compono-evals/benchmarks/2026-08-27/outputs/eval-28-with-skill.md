Use a generated `IConfigurationBuilder` double as a shared parameter, configure `Add(...)` to return the builder, then verify with `Match.Is<T>(...)`:

```csharp
using Compono;
using Compono.XunitV3;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SecretsManagerConfigurationBuilderExtensionsTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void AddSecretsManager_adds_secrets_manager_configuration_source(
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure()
            .Add(Match.Any<IConfigurationSource>())
            .Returns(configurationBuilder);

        configurationBuilder.AddSecretsManager();

        configurationBuilder.Verify()
            .Add(Match.Is<IConfigurationSource>(
                source => source is SecretsManagerConfigurationSource))
            .Once();
    }
}
```

Key migration points:

```csharp
Arg.Is<IConfigurationSource>(...)
```

becomes:

```csharp
Match.Is<IConfigurationSource>(...)
```

and:

```csharp
configurationBuilder.Received(1).Add(...)
```

becomes:

```csharp
configurationBuilder.Verify().Add(...).Once();
```

Because `IConfigurationBuilder.Add` returns non-nullable `IConfigurationBuilder`, configuring it to return `configurationBuilder` avoids an unconfigured generated-double return failure and preserves the fluent builder behavior.
