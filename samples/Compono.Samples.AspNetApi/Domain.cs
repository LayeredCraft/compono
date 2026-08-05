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
