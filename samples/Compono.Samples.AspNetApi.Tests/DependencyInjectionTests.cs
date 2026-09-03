using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Compono.Samples.AspNetApi.Tests;

// Compono.DependencyInjection: row.AsServiceProvider() bridges a Compono-configured
// IOrderRepository into the host's own IServiceCollection. The host still owns hosting, routing,
// and the registered descriptor's own lifetime (Singleton, decided here, not by Compono) - Compono
// only supplies the resolved instance behind that descriptor's factory. Compono composes nothing
// it doesn't own and disposes nothing it resolves, the same non-owning contract ADR-0047
// establishes - see docs/packages/compono-dependencyinjection.md.
public sealed class DependencyInjectionTests
{
    [Fact]
    public async Task PostOrders_ResolvesTheRepositoryThroughAComponoBackedServiceProvider()
    {
        var row = Composer.Create(builder => builder.AddProfile<ApiTestProfile>()).CreateRow(typeof(DependencyInjectionTests));
        var provider = row.AsServiceProvider();
        var repository = provider.GetRequiredService<IOrderRepository>();
        var savedOrder = new Order(Guid.NewGuid(), "Ada Lovelace", 3);
        repository.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(savedOrder);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton(typeof(IOrderRepository), _ => provider.GetRequiredService<IOrderRepository>()))));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new PlaceOrder("Ada Lovelace", 3), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<Order>(TestContext.Current.CancellationToken);
        order.Should().Be(savedOrder);
        await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }
}
