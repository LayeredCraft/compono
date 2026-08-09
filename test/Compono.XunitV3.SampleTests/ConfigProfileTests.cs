namespace Compono.XunitV3.SampleTests;

// A finite-choice profile configuration argument uses an enum, not a string - ADR-0036's "no
// stringly typed configuration" principle, modeled on the real trivia-platform PersistenceAutoData
// shape RESEARCH-0002 Finding 1 is drawn from.
public enum RepositoryKind
{
    Player,
    Game,
}

public sealed record RepositoryTestConfig(RepositoryKind Repository);

// Reached only through ConfigProfileTests' own [Compose<TProfile,TConfig>] theory parameters - proves
// ComposeAttribute<TProfile,TConfig> actually binds profile configuration arguments and applies the
// resulting profile through the real packaged pipeline, not just Compono.XunitV3.Tests' in-process
// GetData checks.
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

// Deliberately has no constructor accepting a RepositoryTestConfig - reserved for
// MismatchedProfileConstructorShape_FailsBeforeTheTestExecutes below, which needs
// ConfigProfileBinder's own pre-composition constructor-shape failure, not a genuine composition
// failure (mirrors FailingCompositionTests' distinction for the ordinary [Compose]/[Compose<TProfile>]
// forms).
public sealed class ProfileWithNoMatchingConstructor : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
    }
}

public sealed class ConfigProfileTests
{
    [Theory]
    [Compose<RepositoryTestProfile, RepositoryTestConfig>(RepositoryKind.Player)]
    public void ComposesTheProfileBuiltFromConfigArguments(string repositoryName)
    {
        repositoryName.Should().Be("player-repository");
    }

    [Theory]
    [Compose<RepositoryTestProfile, RepositoryTestConfig>(RepositoryKind.Game)]
    public void DifferentConfigArguments_ProduceADifferentlyConfiguredProfile(string repositoryName)
    {
        repositoryName.Should().Be("game-repository");
    }

    // Deliberately fails, on every run, via ConfigProfileBinder's own pre-composition constructor-shape
    // validation (ProfileWithNoMatchingConstructor has no constructor accepting a RepositoryTestConfig)
    // - not a genuine composition failure. Proves the diagnostic reaches a real xUnit v3 runner's
    // actual output before the test body ever executes, through the real packaged pipeline, mirroring
    // FailingCompositionTests' existing pattern (this whole project is deliberately excluded from
    // Compono.slnx/CI for exactly this reason - it's packaged-consumer verification, run manually, not
    // an automated gate).
    [Theory]
    [Compose<ProfileWithNoMatchingConstructor, RepositoryTestConfig>(RepositoryKind.Player)]
    public void MismatchedProfileConstructorShape_FailsBeforeTheTestExecutes(string repositoryName)
    {
        repositoryName.Should().BeNull("GetData throws before this body ever runs - this line never executes");
    }
}
