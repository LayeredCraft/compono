namespace Compono.Tests;

/// <summary>
/// Exercises <see cref="GeneratedTestDoubleRegistry"/> in isolation - the core, <see cref="Type"/>-keyed
/// registry a generated <c>[ModuleInitializer]</c> populates, and the runtime provider reads. Uses a
/// distinct marker interface per test to avoid cross-test key collisions, since the registry is
/// process-wide static state.
/// </summary>
public sealed class GeneratedTestDoubleRegistryTests
{
    [Fact]
    public void TryCreate_NothingRegisteredForType_ReturnsFalse()
    {
        var found = GeneratedTestDoubleRegistry.TryCreate(typeof(IUnregisteredMarker), out var value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void RegisterFactory_ThenTryCreate_InvokesFactoryAndReturnsItsResult()
    {
        var expected = new RegisteredMarker();
        GeneratedTestDoubleRegistry.RegisterFactory<IRegisteredMarker>(() => expected);

        var found = GeneratedTestDoubleRegistry.TryCreate(typeof(IRegisteredMarker), out var value);

        found.Should().BeTrue();
        value.Should().BeSameAs(expected);
    }

    [Fact]
    public void RegisterFactory_CalledTwiceForSameType_FirstRegistrationWins()
    {
        var first = new FirstWinsMarker();
        var second = new FirstWinsMarker();
        GeneratedTestDoubleRegistry.RegisterFactory<IFirstWinsMarker>(() => first);

        GeneratedTestDoubleRegistry.RegisterFactory<IFirstWinsMarker>(() => second);
        GeneratedTestDoubleRegistry.TryCreate(typeof(IFirstWinsMarker), out var value);

        value.Should().BeSameAs(first);
    }

    private interface IUnregisteredMarker;

    private interface IRegisteredMarker;

    private sealed class RegisteredMarker : IRegisteredMarker;

    private interface IFirstWinsMarker;

    private sealed class FirstWinsMarker : IFirstWinsMarker;
}
