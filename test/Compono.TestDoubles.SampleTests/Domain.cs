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

// Reached only through GeneratedDoubleTests' own [Compose<GeneratedTestDoubleProfile>] theory
// parameters - no [Composable], no Create<T>()/CreateMany<T>() call site anywhere else in this
// project. Proves a real generated double, reached only through the packaged
// Compono -> Compono.TestDoubles dependency chain, satisfies an interface leaf and is reused (via
// [Shared]) into a constructor parameter exactly like Compono.XunitV3.SampleTests' own
// SharedTests.SharedRepositoryIsReusedByTheService proves for a concrete type.
public sealed class OrderService
{
    private readonly IRepository _repository;

    public OrderService(IRepository repository) => _repository = repository;

    public async Task<Order> PlaceAsync(int amount)
    {
        var count = await _repository.CountAsync();
        _repository.Save(amount);
        return new Order(count + amount, _repository.UtcNow);
    }
}
