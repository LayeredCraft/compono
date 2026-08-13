using NSubstitute.Core;

namespace Compono.TestDoubles.Tests;

/// <summary>
/// ADR-0043's "Runtime activation and precedence": <c>UseGeneratedTestDoubles()</c> registered before
/// <c>UseNSubstitute()</c> means a generated double wins over a generic substitute for an interface
/// both could satisfy - stage 6 providers are tried in registration order (the same "first to report a
/// value wins" rule <see cref="CompositionBuilder.AddTestDoubleProvider"/>'s own doc already states),
/// so this is a direct consequence of that rule, not special-cased logic in either provider. Proven
/// both directions - registration order, not provider identity, decides the winner.
/// </summary>
public sealed class PrecedenceTests
{
    [Fact]
    public void GeneratedDouble_WinsOverNSubstitute_WhenRegisteredFirst()
    {
        var expected = new FakeRepository();
        GeneratedTestDoubleRegistry.RegisterFactory<IRepository>(() => expected);
        var composer = Composer.Create(builder => builder
            .UseGeneratedTestDoubles()
            .UseNSubstitute());

        var value = composer.Create<IRepository>();

        value.Should().BeSameAs(expected);
    }

    [Fact]
    public void NSubstitute_WinsOverAGeneratedDouble_WhenRegisteredFirst()
    {
        var generated = new FakeGateway();
        GeneratedTestDoubleRegistry.RegisterFactory<IGateway>(() => generated);
        var composer = Composer.Create(builder => builder
            .UseNSubstitute()
            .UseGeneratedTestDoubles());

        var value = composer.Create<IGateway>();

        value.Should().NotBeSameAs(generated);
        var act = () => SubstitutionContext.Current.GetCallRouterFor(value!);
        act.Should().NotThrow();
    }

    public interface IRepository;

    private sealed class FakeRepository : IRepository;

    public interface IGateway;

    private sealed class FakeGateway : IGateway;
}
