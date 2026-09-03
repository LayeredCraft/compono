using Compono.Http;
using Compono.XunitV3;

namespace Compono.Samples.AspNetApi.Tests;

// Compono.Http: a composed, [Shared] TestHttpHandler stands in for the real carrier API
// ShippingClient calls through a real HttpClient pipeline (handler.CreateClient) - no application
// interface substituted, no reflection, the exact "handler-based testing of an outbound
// HTTP-calling service" scenario ADR-0033 Amendment 2 scopes for this sample.
public sealed class ShippingClientTests
{
    // OnPost("/v1/labels") - the exact-path overload, not Match.Any<string>() - so this test
    // actually constrains the endpoint ShippingClient calls, not just its HTTP method: a regression
    // to any other path would leave this registration unmatched and fail with
    // UnmatchedHttpRequestException instead of silently passing.
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task RequestLabelAsync_PostsToTheLabelsEndpointAndReturnsTheParsedLabel(
        [Shared] TestHttpHandler handler, Order order, ShippingLabel label)
    {
        var registration = handler.OnPost("/v1/labels").RespondJson(label);
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
        handler.OnPost("/v1/labels").Respond(System.Net.HttpStatusCode.ServiceUnavailable);
        using var client = handler.CreateClient(new Uri("https://shipping.example.com/"));
        var shippingClient = new ShippingClient(client);

        var act = () => shippingClient.RequestLabelAsync(order, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // Codex PR #130 review: ReadFromJsonAsync<ShippingLabel> can return null even for a successful
    // response whose body is the literal JSON "null" - proves RequestLabelAsync never lets that null
    // escape as a violation of its own non-null Task<ShippingLabel> contract, throwing
    // HttpRequestException instead.
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task RequestLabelAsync_ThrowsWhenTheCarrierRespondsWithANullBody(
        [Shared] TestHttpHandler handler, Order order)
    {
        handler.OnPost("/v1/labels").RespondText("null", "application/json");
        using var client = handler.CreateClient(new Uri("https://shipping.example.com/"));
        var shippingClient = new ShippingClient(client);

        var act = () => shippingClient.RequestLabelAsync(order, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
