namespace Compono.TUnit.SampleTests;

public sealed class CompositionTests
{
    [Test]
    [Compose]
    public async Task ComposedTypeIsGeneratedThroughThePackagedDependency(OrderService service)
    {
        await Assert.That(service).IsNotNull();
        await Assert.That(service.Repository).IsNotNull();
    }
}
