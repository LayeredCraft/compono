using System.Net;
using System.Net.Http.Json;
using Compono.XunitV3;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Compono.Samples.AspNetApi.Tests;

// One integration-style endpoint test: a real ASP.NET Core host (WebApplicationFactory<Program>),
// with the composed, [Shared] IOrderRepository substitute swapped in for the app's own
// InMemoryOrderRepository registration - the request travels through real minimal-API routing and
// model binding, not a direct OrderService call like OrderServiceTests above.
public sealed class EndpointTests
{
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task PostOrders_PersistsThroughTheRepositoryAndReturnsCreated(
        [Shared] IOrderRepository repository, PlaceOrder command, Order savedOrder)
    {
        repository.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(savedOrder);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton(repository))));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", command, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<Order>(TestContext.Current.CancellationToken);
        order.Should().Be(savedOrder);
        await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }
}
