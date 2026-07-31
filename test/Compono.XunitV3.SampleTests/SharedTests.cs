namespace Compono.XunitV3.SampleTests;

public sealed class SharedTests
{
    [Theory]
    [Compose]
    public void SharedRepositoryIsReusedByTheService([Shared] Repository repository, OrderService service, CreateOrder command)
    {
        service.Repository.Should().BeSameAs(repository);
        command.Should().NotBeNull();
    }
}
