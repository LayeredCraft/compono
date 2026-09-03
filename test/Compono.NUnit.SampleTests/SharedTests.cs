using NUnit.Framework;

namespace Compono.NUnit.SampleTests;

// Real, running-NUnit end-to-end proof of Compono.NUnit's [Shared] binding, using the no-profile
// shape (a plain composed domain object, not NSubstitute). Mirrors Compono.MSTest.SampleTests'/
// Compono.TUnit.SampleTests'/Compono.XunitV3.SampleTests' own Repository-reuse proof. Deliberately
// no [TestFixture] here either - same ADR-0059 §7 proof as CompositionTests.
public class SharedTests
{
    [Compose]
    public void SharedRepositoryIsReusedByTheService([Shared] Repository repository, OrderService service)
    {
        Assert.That(service.Repository, Is.SameAs(repository));
    }
}
