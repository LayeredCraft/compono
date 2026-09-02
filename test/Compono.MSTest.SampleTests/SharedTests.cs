using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.SampleTests;

// Real, running-MSTest end-to-end proof of Compono.MSTest's [Shared] binding, using the no-profile
// shape (a plain composed domain object, not NSubstitute). Mirrors Compono.TUnit.SampleTests'/
// Compono.XunitV3.SampleTests' own Repository-reuse proof.
[TestClass]
public sealed class SharedTests
{
    [TestMethod]
    [Compose]
    public void SharedRepositoryIsReusedByTheService([Shared] Repository repository, OrderService service)
    {
        Assert.AreSame(repository, service.Repository);
    }
}
