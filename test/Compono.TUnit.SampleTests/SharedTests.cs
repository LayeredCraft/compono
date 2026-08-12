namespace Compono.TUnit.SampleTests;

// Real, running-TUnit end-to-end proof of Compono.TUnit's [Shared] binding, using the no-profile
// shape (a plain composed domain object, not NSubstitute - PLAN-0040 Phase 1's scope, not this
// phase's). Mirrors Compono.XunitV3.SampleTests/SharedTests.cs's own Repository-reuse proof.
public sealed class SharedTests
{
    [Test]
    [Compose]
    public async Task SharedRepositoryIsReusedByTheService([Shared] Repository repository, OrderService service)
    {
        await Assert.That(service.Repository).IsSameReferenceAs(repository);
    }
}
