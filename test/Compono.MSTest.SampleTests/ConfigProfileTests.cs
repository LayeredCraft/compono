using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.SampleTests;

// A finite-choice profile configuration argument uses an enum, not a string - ADR-0036's "no
// stringly typed configuration" principle, mirroring Compono.TUnit.SampleTests.ConfigProfileTests/
// Compono.XunitV3.SampleTests.ConfigProfileTests exactly - proves [Compose<TProfile, TConfig>]
// through the real packaged Compono.MSTest -> Compono dependency chain, not just
// Compono.MSTest.Tests' ProjectReference-based GetData checks.
public enum RepositoryKind
{
    Player,
    Game,
}

public sealed record RepositoryTestConfig(RepositoryKind Repository);

// Composed only as ConfigProfileTests' own [Compose<TProfile, TConfig>]-attributed test methods'
// parameter type - no other Compose-family use of it anywhere else in this project. Proves a
// concrete parameter type reached only this way gets a real generated plan through the packaged
// dependency chain.
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

[TestClass]
public sealed class ConfigProfileTests
{
    [TestMethod]
    [Compose<RepositoryTestProfile, RepositoryTestConfig>(RepositoryKind.Player)]
    public void ComposesTheProfileBuiltFromConfigArguments(RepositoryConsumer consumer)
    {
        Assert.AreEqual("player-repository", consumer.RepositoryName);
    }

    [TestMethod]
    [Compose<RepositoryTestProfile, RepositoryTestConfig>(RepositoryKind.Game)]
    public void DifferentConfigArguments_ProduceADifferentlyConfiguredProfile(RepositoryConsumer consumer)
    {
        Assert.AreEqual("game-repository", consumer.RepositoryName);
    }
}
