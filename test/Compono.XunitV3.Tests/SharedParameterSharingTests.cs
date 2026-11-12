namespace Compono.XunitV3.Tests;

public interface ISharedStandIn
{
}

public sealed class SharedStandIn : ISharedStandIn
{
}

public sealed class SharedStandInConsumer(ISharedStandIn dependency)
{
    public ISharedStandIn Dependency { get; } = dependency;
}

public sealed class SharedStandInRoot(SharedStandInConsumer first, SharedStandInConsumer second)
{
    public SharedStandInConsumer First { get; } = first;
    public SharedStandInConsumer Second { get; } = second;
}

public sealed class SharedStandInProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<ISharedStandIn>(() => new SharedStandIn());
}

public sealed class SharedParameterSharingTests
{
    [Theory]
    [Compose<SharedStandInProfile>]
    public void SharedParameter_DeclaredAfterDependent_IsSharedAcrossTwoNestedConsumers(
        SharedStandInRoot sut,
        [Shared] ISharedStandIn shared)
    {
        ReferenceEquals(shared, sut.First.Dependency).Should().BeTrue();
        ReferenceEquals(shared, sut.Second.Dependency).Should().BeTrue();
    }
}