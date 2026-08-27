# [RESEARCH-0012] AWS Secrets Manager Provider Compono Skill Regression

**Status:** Done

**Date:** 2026-08-27

## Scope

This records two focused follow-up findings from the real AWS Secrets Manager
Provider migration from AutoFixture/AutoFixture.Xunit3/NSubstitute to
`Compono.XunitV3` + `Compono.TestDoubles`.

This is not a product design record and creates no ADR. It applies the existing
ADR-0029/ADR-0042 evidence rules to classify what the migration actually showed.

## Finding A: installed skill missed shipped argument matching support

During the migration, the installed Compono skill initially led the agent to
write hand-made recording fakes for `IAmazonSecretsManager`,
`IConfigurationBuilder`, and `ILoggerFactory`, asserting that
`Compono.TestDoubles` intentionally did not support argument matchers/capture.
That was incorrect for ordinary matching and filtered verification.

The final migration used the shipped `Compono.TestDoubles` surface directly:

```csharp
configurationBuilder.Configure()
    .Add(Match.Any<IConfigurationSource>())
    .Returns(configurationBuilder);

configurationBuilder.Verify()
    .Add(Match.Is<IConfigurationSource>(predicate))
    .Once();
```

and:

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(predicate),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

The migrated suite passed across `net8.0`, `net9.0`, `net10.0`, and `net11.0`
(62/62 on each TFM), with AutoFixture/NSubstitute/`Compono.NSubstitute` absent
from direct and transitive dependencies.

Classification: **skill/docs regression**, not a runtime `Compono.TestDoubles`
capability gap. Current `Compono.TestDoubles` supports literal equality
matching, `Match.Any<T>()`, `Match.Is<T>(predicate)`, argument-filtered
`Never()`/`Once()`/`Exactly(n)`, and multi-entry argument-distinguished response
configuration for eligible member shapes. True capture/callback behavior remains
a separate boundary.

## Finding B: `TestConfigurationProvider` is existing project-local test architecture

The migration retained a local `TestConfigurationProvider : ConfigurationProvider`.
This is not new evidence for abstract-class generated doubles.

Pre-migration evidence from the AWS Secrets Manager Provider test project:

```csharp
public class ConfigurationProviderSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type type && type == typeof(ConfigurationProvider))
        {
            return new TestConfigurationProvider();
        }

        return new NoSpecimen();
    }
}
```

```csharp
public class TestConfigurationProvider : ConfigurationProvider
{
    public override void Set(string key, string value)
    {
        Data[key] = value;
    }
}
```

The fixture registered that specimen builder before `AutoNSubstituteCustomization`:

```csharp
fixture.Customizations.Add(new ConfigurationProviderSpecimenBuilder());

fixture.Customize(new AutoNSubstituteCustomization
{
    GenerateDelegates = true
});
```

Search of the pre-migration state found no `Substitute.For<ConfigurationProvider>()`
and no NSubstitute-backed substitution of `ConfigurationProvider`.

Classification: **existing project-local test architecture**, not a Compono
capability gap. Compono preserves the same design through:

```csharp
builder.Register<ConfigurationProvider>(_ => new TestConfigurationProvider());
```

ADR-0042 Amendment 2 does not apply because there is no evidenced
`Compono.NSubstitute`/NSubstitute-can vs. `Compono.TestDoubles`-cannot
replacement case. No roadmap item, ADR, or abstract-class support design is
created from this evidence.
