using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.SampleTests;

[TestClass]
public sealed class CompositionTests
{
    [TestMethod]
    [Compose]
    public void ComposedTypeIsGeneratedThroughThePackagedDependency(OrderService service)
    {
        Assert.IsNotNull(service);
        Assert.IsNotNull(service.Repository);
    }
}
