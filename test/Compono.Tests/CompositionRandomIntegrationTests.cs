namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 2 Phase 1's seed/path-identity/random-forking wiring through the real
/// <see cref="Composer"/>/<see cref="CompositionContext"/> pipeline, via
/// <see cref="Composer.CreateRootForTesting{T}"/> and a capturing <see cref="ICompositionPlan{T}"/>
/// test double (no built-in provider consumes <see cref="IRandomSource"/> yet - that's Phase 2).
/// </summary>
public sealed class CompositionRandomIntegrationTests
{
    [Fact]
    public void CreateRootForTesting_ProducesIdenticalOutput_ForSameSeedAndSamePath()
    {
        PlanCache<RandomProbeValue>.Instance = new RandomCapturingPlan();
        try
        {
            var seed = new CompositionSeed(4219);

            var first = Composer.CreateRootForTesting<RandomProbeValue>(seed);
            var second = Composer.CreateRootForTesting<RandomProbeValue>(seed);

            first.Should().Be(second);
        }
        finally
        {
            PlanCache<RandomProbeValue>.Instance = null;
        }
    }

    [Fact]
    public void Resolve_ForksIndependently_ForSiblingRequestsWithDifferentOrdinals()
    {
        PlanCache<RandomProbeValue>.Instance = new RandomCapturingPlan();
        PlanCache<RandomPairProbe>.Instance = new RandomPairPlan();
        try
        {
            var result = Composer.CreateRootForTesting<RandomPairProbe>(new CompositionSeed(4219));

            result.First.Should().NotBe(result.Second);
        }
        finally
        {
            PlanCache<RandomProbeValue>.Instance = null;
            PlanCache<RandomPairProbe>.Instance = null;
        }
    }

    [Fact]
    public void Resolve_IsUnaffectedByRenaming_ButChangedByReordering()
    {
        PlanCache<RandomProbeValue>.Instance = new RandomCapturingPlan();
        try
        {
            var seed = new CompositionSeed(4219);

            PlanCache<RandomPairProbe>.Instance = new RandomPairPlan();
            var original = Composer.CreateRootForTesting<RandomPairProbe>(seed);

            PlanCache<RandomPairProbe>.Instance = new RandomPairPlanRenamed();
            var renamed = Composer.CreateRootForTesting<RandomPairProbe>(seed);

            PlanCache<RandomPairProbe>.Instance = new RandomPairPlanReordered();
            var reordered = Composer.CreateRootForTesting<RandomPairProbe>(seed);

            renamed.Should().Be(original);
            reordered.First.Should().Be(original.Second);
            reordered.Second.Should().Be(original.First);
        }
        finally
        {
            PlanCache<RandomProbeValue>.Instance = null;
            PlanCache<RandomPairProbe>.Instance = null;
        }
    }

    [Fact]
    public void Resolve_UnaffectedByUnrelatedSiblingAddedElsewhere()
    {
        PlanCache<RandomProbeValue>.Instance = new RandomCapturingPlan();
        try
        {
            var seed = new CompositionSeed(4219);

            PlanCache<RandomPairProbe>.Instance = new RandomPairPlan();
            var pair = Composer.CreateRootForTesting<RandomPairProbe>(seed);
            PlanCache<RandomPairProbe>.Instance = null;

            PlanCache<RandomTripleProbe>.Instance = new RandomTriplePlan();
            var triple = Composer.CreateRootForTesting<RandomTripleProbe>(seed);
            PlanCache<RandomTripleProbe>.Instance = null;

            triple.First.Should().Be(pair.First);
            triple.Second.Should().Be(pair.Second);
        }
        finally
        {
            PlanCache<RandomProbeValue>.Instance = null;
        }
    }

    private static CompositionRequestDescriptor Descriptor(int ordinal, string name) =>
        new(CompositionRequestKind.ConstructorParameter, ordinal, name, Nullability.NotNullable);

    private sealed record RandomProbeValue(ulong Value);

    private sealed record RandomPairProbe(RandomProbeValue First, RandomProbeValue Second);

    private sealed record RandomTripleProbe(RandomProbeValue First, RandomProbeValue Second, RandomProbeValue Third);

    private sealed class RandomCapturingPlan : ICompositionPlan<RandomProbeValue>
    {
        public RandomProbeValue Compose(ICompositionContext context) =>
            new(((CompositionContext)context).Random.NextUInt64());
    }

    private sealed class RandomPairPlan : ICompositionPlan<RandomPairProbe>
    {
        public RandomPairProbe Compose(ICompositionContext context) =>
            new(
                context.Resolve<RandomProbeValue>(Descriptor(0, "first")),
                context.Resolve<RandomProbeValue>(Descriptor(1, "second")));
    }

    private sealed class RandomPairPlanRenamed : ICompositionPlan<RandomPairProbe>
    {
        public RandomPairProbe Compose(ICompositionContext context) =>
            new(
                context.Resolve<RandomProbeValue>(Descriptor(0, "renamedFirst")),
                context.Resolve<RandomProbeValue>(Descriptor(1, "renamedSecond")));
    }

    private sealed class RandomPairPlanReordered : ICompositionPlan<RandomPairProbe>
    {
        public RandomPairProbe Compose(ICompositionContext context) =>
            new(
                context.Resolve<RandomProbeValue>(Descriptor(1, "first")),
                context.Resolve<RandomProbeValue>(Descriptor(0, "second")));
    }

    private sealed class RandomTriplePlan : ICompositionPlan<RandomTripleProbe>
    {
        public RandomTripleProbe Compose(ICompositionContext context) =>
            new(
                context.Resolve<RandomProbeValue>(Descriptor(0, "first")),
                context.Resolve<RandomProbeValue>(Descriptor(1, "second")),
                context.Resolve<RandomProbeValue>(Descriptor(2, "third")));
    }
}
