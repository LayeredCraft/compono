namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 3 Phase 0's public configuration surface -
/// <see cref="Composer.Create(Action{CompositionBuilder})"/>, <see cref="CompositionBuilder.WithSeed"/>,
/// and the resulting <see cref="Composer"/>'s reuse across multiple <see cref="Composer.Create{T}"/>/
/// <see cref="Composer.CreateMany{T}"/> calls.
/// </summary>
public sealed class ComposerConfigurationTests
{
    [Fact]
    public void Create_WithNullConfigureCallback_Throws()
    {
        var act = () => Composer.Create(configure: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithEmptyConfigureCallback_ComposesSuccessfully()
    {
        PlanCache<ConfigurationProbe>.Instance = new FixedPlan(new ConfigurationProbe("from-empty-callback"));

        try
        {
            var composer = Composer.Create(_ => { });

            var result = composer.Create<ConfigurationProbe>();

            result.Value.Should().Be("from-empty-callback");
        }
        finally
        {
            PlanCache<ConfigurationProbe>.Instance = null;
        }
    }

    [Fact]
    public void Create_WithExplicitSeed_MatchesCreateRootForTesting_WithTheSameSeed()
    {
        PlanCache<RandomProbe>.Instance = new RandomCapturingPlan();

        try
        {
            var composer = Composer.Create(builder => builder.WithSeed(4219));

            var viaBuilder = composer.Create<RandomProbe>();
            var viaTestSeam = Composer.CreateRootForTesting<RandomProbe>(new CompositionSeed(unchecked((ulong)4219)));

            viaBuilder.Should().Be(viaTestSeam);
        }
        finally
        {
            PlanCache<RandomProbe>.Instance = null;
        }
    }

    [Fact]
    public void Create_CalledTwiceOnTheSameComposer_ProducesIdenticalOutput_WhenSeedIsExplicit()
    {
        PlanCache<RandomProbe>.Instance = new RandomCapturingPlan();

        try
        {
            var composer = Composer.Create(builder => builder.WithSeed(4219));

            var first = composer.Create<RandomProbe>();
            var second = composer.Create<RandomProbe>();

            first.Should().Be(second);
        }
        finally
        {
            PlanCache<RandomProbe>.Instance = null;
        }
    }

    [Fact]
    public void Create_WithNoExplicitSeed_GeneratesAFreshSeedPerCall()
    {
        PlanCache<RandomProbe>.Instance = new RandomCapturingPlan();

        try
        {
            var composer = Composer.Create();

            var first = composer.Create<RandomProbe>();
            var second = composer.Create<RandomProbe>();

            first.Should().NotBe(second);
        }
        finally
        {
            PlanCache<RandomProbe>.Instance = null;
        }
    }

    [Fact]
    public void Create_CalledConcurrently_ProducesTheSameCorrectOutput_WhenSeedIsExplicit()
    {
        PlanCache<RandomProbe>.Instance = new RandomCapturingPlan();

        try
        {
            var composer = Composer.Create(builder => builder.WithSeed(4219));
            var expected = Composer.CreateRootForTesting<RandomProbe>(new CompositionSeed(unchecked((ulong)4219)));
            var results = new RandomProbe[50];

            Parallel.For(0, results.Length, i => results[i] = composer.Create<RandomProbe>());

            // Every concurrent call landing on the exact same, independently-verified-correct value is
            // strong evidence against shared mutable state bleeding between concurrent root operations -
            // a leak would show up as at least one divergent result, not just an exception.
            results.Should().AllSatisfy(result => result.Should().Be(expected));
        }
        finally
        {
            PlanCache<RandomProbe>.Instance = null;
        }
    }

    [Fact]
    public void CreateMany_WithExplicitSeed_MatchesCreateManyForTesting_WithTheSameSeed()
    {
        PlanCache<RandomProbe>.Instance = new RandomCapturingPlan();

        try
        {
            var composer = Composer.Create(builder => builder.WithSeed(4219));

            var viaBuilder = composer.CreateMany<RandomProbe>(3);
            var viaTestSeam = Composer.CreateManyForTesting<RandomProbe>(3, new CompositionSeed(unchecked((ulong)4219)));

            // A dedicated CreateMany-through-configuration assertion, not just Create<T>() - per
            // testing.md's own recorded lesson, pairing only Create<T>() with the configured-seed
            // path would leave CreateMany<T>()'s own wiring to _configuration.Seed unverified, the
            // exact shape of gap that already hid a real CreateMany<T>() discovery bug once before.
            viaBuilder.Should().BeEquivalentTo(viaTestSeam);
        }
        finally
        {
            PlanCache<RandomProbe>.Instance = null;
        }
    }

    [Fact]
    public void CreateMany_CalledTwiceOnTheSameComposer_ProducesIdenticalOutput_WhenSeedIsExplicit()
    {
        PlanCache<RandomProbe>.Instance = new RandomCapturingPlan();

        try
        {
            var composer = Composer.Create(builder => builder.WithSeed(4219));

            var first = composer.CreateMany<RandomProbe>(3);
            var second = composer.CreateMany<RandomProbe>(3);

            first.Should().BeEquivalentTo(second);
        }
        finally
        {
            PlanCache<RandomProbe>.Instance = null;
        }
    }

    [Fact]
    public void WithSeed_CalledTwice_ThrowsWithOneDuplicateConfigurationOptionErrorNamingBothSources()
    {
        var act = () => Composer.Create(builder => builder.WithSeed(1).WithSeed(2));

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateConfigurationOption>().Which;
        error.OptionName.Should().Be("WithSeed");
        error.Sources.Should().HaveCount(2);
        error.Sources.Should().AllSatisfy(source => source.Should().Be(ConfigurationSource.Direct));
    }

    private sealed record ConfigurationProbe(string Value);

    private sealed record RandomProbe(ulong Value);

    private sealed class FixedPlan(ConfigurationProbe value) : ICompositionPlan<ConfigurationProbe>
    {
        public ConfigurationProbe Compose(ICompositionContext context) => value;
    }

    private sealed class RandomCapturingPlan : ICompositionPlan<RandomProbe>
    {
        public RandomProbe Compose(ICompositionContext context) =>
            new(((CompositionContext)context).Random.NextUInt64());
    }
}
