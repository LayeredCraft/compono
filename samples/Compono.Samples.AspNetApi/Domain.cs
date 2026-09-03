using System.Net.Http.Json;

namespace Compono.Samples.AspNetApi;

/// <summary>
/// A request to place an order for <paramref name="Quantity"/> units on behalf of
/// <paramref name="CustomerName"/>.
/// </summary>
public sealed record PlaceOrder(string CustomerName, int Quantity);

/// <summary>
/// A persisted order.
/// </summary>
public sealed record Order(Guid Id, string CustomerName, int Quantity);

/// <summary>
/// Persists placed orders.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Persists <paramref name="order"/>, returning the persisted instance.
    /// </summary>
    Task<Order> SaveAsync(Order order, CancellationToken cancellationToken);
}

/// <summary>
/// An in-memory <see cref="IOrderRepository"/> - good enough for this sample's own scope, real
/// persistence is out of scope (Compono usage, not application-architecture depth, is the point).
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    /// <inheritdoc />
    public Task<Order> SaveAsync(Order order, CancellationToken cancellationToken) => Task.FromResult(order);
}

/// <summary>
/// Places orders on behalf of the <c>/orders</c> endpoint.
/// </summary>
public sealed class OrderService
{
    private readonly IOrderRepository _repository;

    /// <summary>
    /// Creates a new <see cref="OrderService"/> backed by <paramref name="repository"/>.
    /// </summary>
    public OrderService(IOrderRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Places <paramref name="command"/>, persisting and returning the resulting <see cref="Order"/>.
    /// </summary>
    public Task<Order> PlaceAsync(PlaceOrder command, CancellationToken cancellationToken) =>
        _repository.SaveAsync(new Order(Guid.NewGuid(), command.CustomerName, command.Quantity), cancellationToken);
}

/// <summary>
/// A shipping label issued by the external carrier <see cref="ShippingClient"/> calls out to.
/// </summary>
public sealed record ShippingLabel(string TrackingNumber, string Carrier);

/// <summary>
/// Calls a real external HTTP carrier API to request a <see cref="ShippingLabel"/> for a placed
/// <see cref="Order"/> - the outbound HTTP dependency <c>Compono.Http</c>'s sample scenario
/// exercises. A typed client (see <c>Program.cs</c>'s <c>AddHttpClient&lt;ShippingClient&gt;</c>
/// registration), not a hand-rolled <see cref="HttpClient"/> wrapper - Compono usage stays the
/// point, not a bespoke HTTP abstraction.
/// </summary>
public sealed class ShippingClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a new <see cref="ShippingClient"/> over <paramref name="httpClient"/>.
    /// </summary>
    public ShippingClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Requests a <see cref="ShippingLabel"/> for <paramref name="order"/> from the carrier's
    /// <c>POST /v1/labels</c> endpoint.
    /// </summary>
    public async Task<ShippingLabel> RequestLabelAsync(Order order, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/v1/labels", new { orderId = order.Id, quantity = order.Quantity }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var label = await response.Content.ReadFromJsonAsync<ShippingLabel>(cancellationToken);
        return label ?? throw new HttpRequestException(
            $"The carrier responded successfully to 'POST /v1/labels' for order '{order.Id}' but the response body deserialized to null.");
    }
}
