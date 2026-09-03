using Compono.Http;
using Compono.XunitV3;

namespace Compono.Samples.AspNetApi.Tests;

// Compono.Http: a composed, [Shared] TestHttpHandler stands in for the real carrier API
// ShippingClient calls through a real HttpClient pipeline (handler.CreateClient) - no application
// interface substituted, no reflection, the exact "handler-based testing of an outbound
// HTTP-calling service" scenario ADR-0033 Amendment 2 scopes for this sample.
public sealed class ShippingClientTests
{
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task RequestLabelAsync_PostsTheOrderAndReturnsTheParsedLabel(
        [Shared] TestHttpHandler handler, Order order, ShippingLabel label)
    {
        var registration = handler.OnPost(Match.Any<string>()).RespondJson(label);
        using var client = handler.CreateClient(new Uri("https://shipping.example.com/"));
        var shippingClient = new ShippingClient(client);

        var result = await shippingClient.RequestLabelAsync(order, TestContext.Current.CancellationToken);

        result.Should().Be(label);
        registration.Verify().Once();
    }

    [Theory]
    [Compose<ApiTestProfile>]
    public async Task RequestLabelAsync_ThrowsWhenTheCarrierRespondsWithAFailureStatus(
        [Shared] TestHttpHandler handler, Order order)
    {
        handler.OnPost(Match.Any<string>()).Respond(System.Net.HttpStatusCode.ServiceUnavailable);
        using var client = handler.CreateClient(new Uri("https://shipping.example.com/"));
        var shippingClient = new ShippingClient(client);

        var act = () => shippingClient.RequestLabelAsync(order, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
