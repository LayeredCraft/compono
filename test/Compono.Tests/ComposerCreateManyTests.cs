namespace Compono.Tests;

/// <summary>
/// Milestone 2 Phase 4's <see cref="Composer.CreateMany{T}"/> - argument/return contract and
/// seed-stability, via <see cref="Composer.CreateManyForTesting{T}"/>'s fixed-seed seam
/// (<c>docs/plans/0002-milestone-2-core-composition-engine.md</c>'s Phase 4 test plan). Uses a
/// hand-written <see cref="ICompositionPlan{T}"/> test double rather than relying on the real
/// generator, per Milestone 1's Phase 0 note: <c>Compono.Tests</c> doesn't reference
/// <c>Compono.Generators</c> as an analyzer, so nothing in this project ever gets a real generated
/// plan.
/// </summary>
public sealed class ComposerCreateManyTests
{
    [Fact]
    public void CreateMany_Throws_WhenCountIsNegative()
    {
        var composer = Composer.Create();

        var act = () => composer.CreateMany<Leaf>(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateMany_ReturnsAnEmptyNonNullList_WhenCountIsZero()
    {
        var composer = Composer.Create();

        var result = composer.CreateMany<Leaf>(0);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateMany_ReturnsExactlyCountInstances()
    {
        PlanCache<Leaf>.Instance = new LeafPlan();

        try
        {
            var composer = Composer.Create();

            var result = composer.CreateMany<Leaf>(3);

            result.Should().HaveCount(3);
        }
        finally
        {
            PlanCache<Leaf>.Instance = null;
        }
    }

    [Fact]
    public void CreateManyForTesting_ProducesByteForByteIdenticalItems_ForTheSharedPrefixOfTwoDifferentCounts()
    {
        PlanCache<RandomLeaf>.Instance = new RandomLeafPlan();

        try
        {
            var seed = new CompositionSeed(4219);

            var three = Composer.CreateManyForTesting<RandomLeaf>(3, seed);
            var ten = Composer.CreateManyForTesting<RandomLeaf>(10, seed);

            ten.Take(3).Should().Equal(three);
        }
        finally
        {
            PlanCache<RandomLeaf>.Instance = null;
        }
    }

    [Fact]
    public void CreateRootForTesting_ProducesIdenticalOutput_AcrossTwoIndependentCallsWithTheSameSeed()
    {
        PlanCache<RandomLeaf>.Instance = new RandomLeafPlan();

        try
        {
            var seed = new CompositionSeed(4219);

            var first = Composer.CreateRootForTesting<RandomLeaf>(seed);
            var second = Composer.CreateRootForTesting<RandomLeaf>(seed);

            first.Should().Be(second);
        }
        finally
        {
            PlanCache<RandomLeaf>.Instance = null;
        }
    }

    private sealed record Leaf;

    private sealed record RandomLeaf(ulong Value);

    private sealed class LeafPlan : ICompositionPlan<Leaf>
    {
        public Leaf Compose(ICompositionContext context) => new();
    }

    private sealed class RandomLeafPlan : ICompositionPlan<RandomLeaf>
    {
        public RandomLeaf Compose(ICompositionContext context) =>
            new(((CompositionContext)context).Random.NextUInt64());
    }
}
