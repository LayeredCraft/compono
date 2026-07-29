namespace Compono.Tests;

public sealed class CompositionScopeTests
{
    [Fact]
    public void ResolveSharedForTesting_ReusesTheSameValue_ForASecondSharedRequestOfTheSameType()
    {
        var provider = new CountingProvider();
        var context = new CompositionContext([], [], [], [provider]);

        var first = context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");
        var second = context.ResolveSharedForTesting<Widget>(ordinal: 1, name: "b");

        second.Should().BeSameAs(first);
        provider.CallCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_NeverReadsFromScope_EvenWhenTheSameTypeWasAlreadyShared()
    {
        var provider = new CountingProvider();
        var context = new CompositionContext([], [], [], [provider]);
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 1, "b", Nullability.NotNullable);

        var shared = context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");
        var notShared = context.Resolve<Widget>(descriptor);

        notShared.Should().NotBeSameAs(shared);
        provider.CallCount.Should().Be(2);
    }

    [Fact]
    public void ResolveSharedForTesting_Throws_WhenTheFirstProducedValueIsNullForANonNullableRequest()
    {
        var provider = new StubProvider(value: null);
        var context = new CompositionContext([], [], [], [provider]);

        var act = () => context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");

        act.Should().Throw<CompositionException>().WithMessage("*shared value*");
    }

    [Fact]
    public void ResolveSharedForTesting_Throws_WhenTheFirstProducedValueHasAnIncompatibleRuntimeType()
    {
        var provider = new StubProvider(value: "not-a-widget");
        var context = new CompositionContext([], [], [], [provider]);

        var act = () => context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");

        act.Should().Throw<CompositionException>().WithMessage("*shared value*");
    }

    [Fact]
    public void ResolveSharedForTesting_DoesNotCacheAnInvalidFirstValue_SoASecondSharedRequestCanStillSucceed()
    {
        var provider = new OnceInvalidThenValidProvider();
        var context = new CompositionContext([], [], [], [provider]);

        var act = () => context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");
        act.Should().Throw<CompositionException>();

        var second = context.ResolveSharedForTesting<Widget>(ordinal: 1, name: "b");

        second.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSharedForTesting_Throws_WhenAGeneratedPlanProducesAnIncompatibleFirstValue()
    {
        PlanCache<Widget>.Instance = new NullWidgetPlan();

        try
        {
            var context = new CompositionContext();

            var act = () => context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");

            act.Should().Throw<CompositionException>().WithMessage("*shared value*");
        }
        finally
        {
            PlanCache<Widget>.Instance = null;
        }
    }

    private sealed record Widget;

    private sealed class CountingProvider : ICompositionProvider
    {
        internal int CallCount { get; private set; }

        public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context)
        {
            if (request.RequestedType != typeof(Widget))
                return CompositionResult.NotHandled.Instance;

            CallCount++;
            return new CompositionResult.Success(new Widget());
        }
    }

    private sealed class StubProvider(object? value) : ICompositionProvider
    {
        public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context) =>
            request.RequestedType == typeof(Widget)
                ? new CompositionResult.Success(value)
                : CompositionResult.NotHandled.Instance;
    }

    private sealed class OnceInvalidThenValidProvider : ICompositionProvider
    {
        private bool _invoked;

        public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context)
        {
            if (request.RequestedType != typeof(Widget))
                return CompositionResult.NotHandled.Instance;

            if (!_invoked)
            {
                _invoked = true;
                return new CompositionResult.Success(null);
            }

            return new CompositionResult.Success(new Widget());
        }
    }

    // Simulates a buggy generated/hand-written plan producing null for a non-nullable request - the
    // case StoreSharedAndReturn's validate-before-cache fix must catch on the generated-plan path too.
    private sealed class NullWidgetPlan : ICompositionPlan<Widget>
    {
        public Widget Compose(ICompositionContext context) => null!;
    }
}
