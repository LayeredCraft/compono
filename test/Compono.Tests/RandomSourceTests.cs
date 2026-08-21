namespace Compono.Tests;

public sealed class RandomSourceTests
{
    [Fact]
    public void Fork_ProducesIdenticalChildState_ForSameSeedAndSameSegment()
    {
        var parentA = RandomSource.FromSeed(new CompositionSeed(4219));
        var parentB = RandomSource.FromSeed(new CompositionSeed(4219));

        var childA = parentA.Fork(new PathSegment.ConstructorParameter(0, "firstName"));
        var childB = parentB.Fork(new PathSegment.ConstructorParameter(0, "firstName"));

        childA.NextUInt64().Should().Be(childB.NextUInt64());
    }

    [Fact]
    public void Fork_IsUnaffectedByName_ButDiffersByOrdinal()
    {
        var parent = RandomSource.FromSeed(new CompositionSeed(4219));

        var sameOrdinalNameA = parent.Fork(new PathSegment.ConstructorParameter(0, "a")).NextUInt64();
        var sameOrdinalNameB = parent.Fork(new PathSegment.ConstructorParameter(0, "b")).NextUInt64();
        var differentOrdinal = parent.Fork(new PathSegment.ConstructorParameter(1, "a")).NextUInt64();

        sameOrdinalNameA.Should().Be(sameOrdinalNameB);
        sameOrdinalNameA.Should().NotBe(differentOrdinal);
    }

    [Fact]
    public void Fork_ProducesDistinctOutput_ForEachSegmentKindAtSameOrdinalOrIndex()
    {
        var parent = RandomSource.FromSeed(new CompositionSeed(4219));

        var outputs = new[]
        {
            parent.Fork(new PathSegment.ConstructorParameter(0, "x")).NextUInt64(),
            parent.Fork(new PathSegment.RequiredMember(0, "x")).NextUInt64(),
            parent.Fork(new PathSegment.CollectionElement(0)).NextUInt64(),
            parent.Fork(new PathSegment.DictionaryKey(0)).NextUInt64(),
            parent.Fork(new PathSegment.DictionaryValue(0)).NextUInt64(),
            parent.Fork(new PathSegment.ManualResolve(0)).NextUInt64(),
            parent.Fork(new PathSegment.TestParameter(0, "x")).NextUInt64(),
            parent.Fork(new PathSegment.ConfiguredResolution(0)).NextUInt64(),
        };

        outputs.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void NextUInt64_DoesNotAffectThisNodesOwnFutureForks_RegardlessOfHowManyValuesAreDrawn()
    {
        var nodeA = RandomSource.FromSeed(new CompositionSeed(4219)).Fork(new PathSegment.ConstructorParameter(0, "node"));
        var childOfA = nodeA.Fork(new PathSegment.ConstructorParameter(0, "child"));
        var expectedChildValue = childOfA.NextUInt64();

        var nodeB = RandomSource.FromSeed(new CompositionSeed(4219)).Fork(new PathSegment.ConstructorParameter(0, "node"));
        nodeB.NextUInt64();
        nodeB.NextUInt64();
        nodeB.NextUInt64();
        var childOfB = nodeB.Fork(new PathSegment.ConstructorParameter(0, "child"));

        childOfB.NextUInt64().Should().Be(expectedChildValue);
    }

    [Fact]
    public void Fork_DoesNotCollapseToAFixedZeroState_FromAZeroSeedAtOrdinalZero()
    {
        // Regression test: chaining Fnv1a.Combine straight from the parent's own state (rather than
        // FNV-1a's real offset basis) has a degenerate fixed point at state 0, tag 0 (the
        // ConstructorParameter tag), all-zero payload bytes - every mix step stays 0, so a seed of 0
        // followed by any number of ConstructorParameter(0, ...) forks would collapse every one of
        // those distinct structural positions to the same derived state and the same NextUInt64()
        // output.
        var node = RandomSource.FromSeed(new CompositionSeed(0));

        var firstFork = node.Fork(new PathSegment.ConstructorParameter(0, "a"));
        var secondFork = firstFork.Fork(new PathSegment.ConstructorParameter(0, "a"));
        var thirdFork = secondFork.Fork(new PathSegment.ConstructorParameter(0, "a"));

        new[] { firstFork.NextUInt64(), secondFork.NextUInt64(), thirdFork.NextUInt64() }
            .Should().OnlyHaveUniqueItems();
    }
}
