namespace Compono.TestDoubles.Tests;

/// <summary>
/// <c>CompositionBuilderExtensions</c>'s <c>UseGeneratedTestDoubles()</c> wiring a working
/// <see cref="GeneratedTestDoubleProvider"/> into a real <see cref="Composer"/> - matching
/// Compono.NSubstitute.Tests' own <c>CompositionBuilderExtensionsTests</c> pattern.
/// </summary>
public sealed class CompositionBuilderExtensionsTests
{
    [Fact]
    public void UseGeneratedTestDoubles_ComposesARegisteredInterface_UsingItsRegisteredFactory()
    {
        var expected = new FakeService();
        GeneratedTestDoubleRegistry.RegisterFactory<IService>(() => expected);
        var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());

        var value = composer.Create<IService>();

        value.Should().BeSameAs(expected);
    }

    public interface IService;

    private sealed class FakeService : IService;
}
