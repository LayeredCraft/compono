namespace Compono.TUnit.SampleTests;

// A finite-choice profile configuration argument uses an enum, not a string - ADR-0036's "no
// stringly typed configuration" principle, mirroring Compono.XunitV3.SampleTests.ConfigProfileTests
// exactly (PLAN-0040 Phase 2's own instance of that scenario, proving [Compose<TProfile, TConfig>]
// through the real packaged Compono.TUnit -> Compono dependency chain, not just
// Compono.TUnit.Tests' ProjectReference-based GetData checks).
public enum RepositoryKind
{
    Player,
    Game,
}

public sealed record RepositoryTestConfig(RepositoryKind Repository);

// Composed only as ConfigProfileTests' own [Compose<TProfile, TConfig>]-attributed test methods'
// parameter type - no other Compose-family use of it anywhere else in this project. Proves a
// concrete parameter type reached only this way gets a real generated plan through the packaged
// dependency chain (Compono.XunitV3.SampleTests.RepositoryConsumer's own comment records the exact
// discovery gap this shape once caught for that package - PR #65).
public sealed class RepositoryConsumer
{
    public RepositoryConsumer(string repositoryName) => RepositoryName = repositoryName;

    public string RepositoryName { get; }
}

public sealed class RepositoryTestProfile : ICompositionProfile
{
    public RepositoryTestProfile(RepositoryTestConfig config) => Config = config;

    public RepositoryTestConfig Config { get; }

    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => Config.Repository switch
        {
            RepositoryKind.Player => "player-repository",
            RepositoryKind.Game => "game-repository",
            _ => throw new ArgumentOutOfRangeException(nameof(Config)),
        });
}

public sealed class ConfigProfileTests
{
    [Test]
    [Compose<RepositoryTestProfile, RepositoryTestConfig>(RepositoryKind.Player)]
    public async Task ComposesTheProfileBuiltFromConfigArguments(RepositoryConsumer consumer)
    {
        await Assert.That(consumer.RepositoryName).IsEqualTo("player-repository");
    }

    [Test]
    [Compose<RepositoryTestProfile, RepositoryTestConfig>(RepositoryKind.Game)]
    public async Task DifferentConfigArguments_ProduceADifferentlyConfiguredProfile(RepositoryConsumer consumer)
    {
        await Assert.That(consumer.RepositoryName).IsEqualTo("game-repository");
    }
}
