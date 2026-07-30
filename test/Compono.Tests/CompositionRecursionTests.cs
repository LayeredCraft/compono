namespace Compono.Tests;

public sealed class CompositionRecursionTests
{
    [Fact]
    public void ResolveRoot_Throws_WhenAGeneratedPlanRecursesIntoItsOwnTypeWithNoTerminatingValue()
    {
        PlanCache<Node>.Instance = new RecursingNodePlan();

        try
        {
            var context = new CompositionContext();

            var act = () => context.ResolveRoot<Node>();

            act.Should().Throw<CompositionException>()
                .WithMessage("*Node*")
                .Where(e => e.Message.Contains("[0]"));
        }
        finally
        {
            PlanCache<Node>.Instance = null;
        }
    }

    private sealed record Node(List<Node> Children);

    // Simulates the shape a real generated List<Node> collection plan would produce for a
    // self-referencing Node type: each element is requested via a CollectionElement(0) descriptor,
    // recursively dispatching back into PlanCache<Node>.Instance with no registration/shared value
    // anywhere in the loop to terminate it.
    private sealed class RecursingNodePlan : ICompositionPlan<Node>
    {
        public Node Compose(ICompositionContext context)
        {
            var descriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.CollectionElement, ordinal: 0, name: "", declaringType: null, Nullability.NotNullable);
            var child = context.Resolve<Node>(descriptor);

            return new Node([child]);
        }
    }
}
