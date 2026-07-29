namespace Compono.Tests;

public sealed class CollectionPlanCacheDispatchTests
{
    [Fact]
    public void ResolveRoot_ReturnsCollectionPlanValue_WhenCollectionPlanCacheHasAPlan()
    {
        CollectionPlanCache<List<int>>.Instance = new FixedCollectionPlan([1, 2, 3]);

        try
        {
            var context = new CompositionContext();

            var result = context.ResolveRoot<List<int>>();

            result.Should().Equal(1, 2, 3);
        }
        finally
        {
            CollectionPlanCache<List<int>>.Instance = null;
        }
    }

    [Fact]
    public void ResolveRoot_PrefersBuiltInProviderOverCollectionPlanCache()
    {
        CollectionPlanCache<List<int>>.Instance = new FixedCollectionPlan([9, 9, 9]);
        var provider = new StubProvider([1]);

        try
        {
            var context = new CompositionContext(
                profileProviders: [],
                semanticProviders: [],
                testDoubleProviders: [],
                builtInProviders: [provider]);

            var result = context.ResolveRoot<List<int>>();

            result.Should().Equal(1);
        }
        finally
        {
            CollectionPlanCache<List<int>>.Instance = null;
        }
    }

    [Fact]
    public void ResolveRoot_PrefersCollectionPlanCacheOverGeneratedPlanDispatch()
    {
        // A List<int> has no ICompositionPlan<List<int>> at all in real code - PlanCache<T> is only
        // ever populated for ordinary composable types - but this proves ordering even if it did.
        CollectionPlanCache<List<int>>.Instance = new FixedCollectionPlan([7]);
        PlanCache<List<int>>.Instance = new FixedGeneratedPlan([8]);

        try
        {
            var context = new CompositionContext();

            var result = context.ResolveRoot<List<int>>();

            result.Should().Equal(7);
        }
        finally
        {
            CollectionPlanCache<List<int>>.Instance = null;
            PlanCache<List<int>>.Instance = null;
        }
    }

    private sealed class FixedCollectionPlan(List<int> value) : ICompositionPlan<List<int>>
    {
        public List<int> Compose(ICompositionContext context) => value;
    }

    private sealed class FixedGeneratedPlan(List<int> value) : ICompositionPlan<List<int>>
    {
        public List<int> Compose(ICompositionContext context) => value;
    }

    private sealed class StubProvider(List<int> value) : ICompositionProvider
    {
        public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context) =>
            new CompositionResult.Success(value);
    }
}
