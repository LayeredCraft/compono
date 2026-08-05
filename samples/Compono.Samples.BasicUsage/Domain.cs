namespace Compono.Samples.BasicUsage;

// Plain application types - nothing here is Compono-specific. Compono composes ordinary
// constructors and record parameters; it doesn't require attributes, base classes, or interfaces
// on the types it builds. Mirrors docs/getting-started/first-test.md's own example so a reader who
// just finished Getting Started recognizes this sample immediately.
public sealed class Repository;

public sealed class OrderService
{
    public OrderService(Repository repository)
    {
        Repository = repository;
    }

    public Repository Repository { get; }
}

public sealed record CreateOrder(string CustomerName, int Quantity);

public sealed record Customer
{
    public required string Name { get; init; }

    public required string Email { get; init; }
}
