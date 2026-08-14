namespace Compono.TestDoubles.SampleTests;

// IRepository extends IClock rather than declaring every member itself - proves the generator walks
// the full transitive base-interface closure (ADR-0043 Amendment 11 Finding Z), not just IRepository's
// own declared members. The generated double must implement UtcNow too, even though IRepository never
// mentions it.
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IRepository : IClock
{
    Task<int> CountAsync();

    void Save(int amount);
}

public sealed record Order(int Total, DateTimeOffset PlacedAt);

// Reached through GeneratedDoubleTests' own [Compose<GeneratedTestDoubleProfile>] theory
// parameters (repository as a sibling [Shared] parameter, reused into OrderService's own
// constructor - same shape Compono.XunitV3.SampleTests' own
// SharedTests.SharedRepositoryIsReusedByTheService proves for a concrete type) and, separately, a
// direct composer.Create<OrderService>() call (Create_OrderService_test.cs) that never composes
// IRepository as a call-site parameter at all - proving the composition engine discovers and
// satisfies OrderService's own nested IRepository dependency purely through the generated-double
// provider. Repository is exposed as a public property (mirroring
// Compono.XunitV3.SampleTests.OrderService's own identical property) so both proofs can configure
// the exact double instance actually wired into the service.
public sealed class OrderService
{
    public OrderService(IRepository repository) => Repository = repository;

    public IRepository Repository { get; }

    public async Task<Order> PlaceAsync(int amount)
    {
        var count = await Repository.CountAsync();
        Repository.Save(amount);
        return new Order(count + amount, Repository.UtcNow);
    }
}
