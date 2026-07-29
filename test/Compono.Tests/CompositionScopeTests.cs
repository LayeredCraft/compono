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
}
