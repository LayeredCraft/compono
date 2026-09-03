using NUnit.Framework;

namespace Compono.NUnit.SampleTests;

// Deliberately no [TestFixture] here - discoverability via [Compose] alone (ADR-0059 §7) IS part of
// the proof, run for real through the actual packaged Compono.NUnit -> Compono dependency chain.
public class CompositionTests
{
    [Compose]
    public void ComposedTypeIsGeneratedThroughThePackagedDependency(OrderService service)
    {
        Assert.That(service, Is.Not.Null);
        Assert.That(service.Repository, Is.Not.Null);
    }
}
