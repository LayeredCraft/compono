namespace Compono.TestDoubles.Tests;

/// <summary>
/// <see cref="GeneratedTestDoubleProvider.TryProvide"/> unit coverage, exercised through a real
/// <see cref="Composer"/> (<see cref="CompositionProviderResult"/>'s <c>Value</c>/<c>IsHandled</c> are
/// internal to <c>Compono</c>, so a provider's own outcome is only observable from outside through the
/// pipeline it feeds - matching Compono.NSubstitute.Tests' own <c>NSubstituteProviderTests</c> pattern.
/// Real generated-code coverage (a real <c>[ModuleInitializer]</c> populating
/// <see cref="GeneratedTestDoubleRegistry"/>) is Phase 2's job, per PLAN-0043 - here the registry is
/// populated by hand, exactly the shape a generated module initializer would produce.
/// </summary>
public sealed class GeneratedTestDoubleProviderTests
{
    [Fact]
    public void TryProvide_ProducesTheRegisteredFactorysValue_ForARegisteredInterface()
    {
        var expected = new FakeRepository();
        GeneratedTestDoubleRegistry.RegisterFactory<IRepository>(() => expected);
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new GeneratedTestDoubleProvider()));

        var value = composer.Create<IRepository>();

        value.Should().BeSameAs(expected);
    }

    [Fact]
    public void TryProvide_DoesNotHandle_AnInterfaceWithNoRegisteredFactory()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new GeneratedTestDoubleProvider()));

        var act = () => composer.Create<IUnregisteredRepository>();

        act.Should().Throw<CompositionException>();
    }

    public interface IRepository;

    private sealed class FakeRepository : IRepository;

    public interface IUnregisteredRepository;
}
