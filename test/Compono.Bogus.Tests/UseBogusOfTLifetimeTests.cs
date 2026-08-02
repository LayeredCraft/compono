using Bogus;

namespace Compono.Bogus.Tests;

/// <summary>
/// <c>UseBogus&lt;T&gt;()</c>'s per-request <c>Faker&lt;T&gt;</c> lifetime (ADR-0027's Model 3) -
/// proving the corrected design actually holds under repeated and concurrent
/// <see cref="Composer.Create{T}"/>/<see cref="Composer.CreateMany{T}"/> calls, not just that it
/// compiles - PLAN-0006 Phase 3.
/// </summary>
public sealed class UseBogusOfTLifetimeTests
{
    [Fact]
    public void ConfigureCallback_RunsOncePerResolvedObject_NotOnceAtRegistrationTime()
    {
        var invocationCount = 0;
        var composer = Composer.Create(builder => builder
            .UseBogus<Widget>(faker =>
            {
                invocationCount++;
                faker.RuleFor(w => w.Value, f => f.Random.Int());
            }));

        invocationCount.Should().Be(0);

        composer.Create<Widget>();
        composer.Create<Widget>();
        composer.Create<Widget>();

        invocationCount.Should().Be(3);
    }

    [Fact]
    public void TwoSeparateCreateCalls_ReceiveTwoDistinctFakerInstances_WithNoSharedState()
    {
        var seenFakers = new List<object>();
        var composer = Composer.Create(builder => builder
            .UseBogus<Widget>(faker =>
            {
                seenFakers.Add(faker);
                faker.RuleFor(w => w.Value, f => f.Random.Int());
            }));

        composer.Create<Widget>();
        composer.Create<Widget>();

        seenFakers.Should().HaveCount(2);
        seenFakers[0].Should().NotBeSameAs(seenFakers[1]);
    }

    [Fact]
    public void ParallelCreateMany_ForAUseBogusOfTRegisteredType_ProducesCorrectNonCorruptedResults()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .UseBogus<Widget>(faker => faker.RuleFor(w => w.Value, f => f.Random.Int())));
        var expected = composer.CreateMany<Widget>(50).Select(w => w.Value).ToArray();

        var results = new int[50][];
        Parallel.For(0, 10, i => results[i] = composer.CreateMany<Widget>(50).Select(w => w.Value).ToArray());

        // Every parallel CreateMany(50) run against the same seeded composer reproduces the exact same
        // 50 values, in order - strong evidence against shared mutable Faker<T> state observable across
        // threads, not just an absence of exceptions.
        for (var i = 0; i < 10; i++)
            results[i].Should().Equal(expected);
    }

    [Fact]
    public void SameSeedAndRequestPath_ReproducesTheSameGeneratedObject_AcrossSeparateComposerInstances()
    {
        static int ComposeRoot() =>
            Composer.Create(builder => builder
                    .WithSeed(4219)
                    .UseBogus<Widget>(faker => faker.RuleFor(w => w.Value, f => f.Random.Int())))
                .Create<Widget>()
                .Value;

        var first = ComposeRoot();
        var second = ComposeRoot();

        first.Should().Be(second);
    }

    public sealed class Widget
    {
        public int Value { get; set; }
    }
}
