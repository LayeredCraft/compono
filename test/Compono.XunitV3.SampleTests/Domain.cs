namespace Compono.XunitV3.SampleTests;

// Reached only through SharedTests.SharedRepositoryIsReusedByTheService's own [Compose]-attributed
// theory parameters - no [Composable], no Create<T>()/CreateMany<T>(), no direct CompositionRow call
// site anywhere else in this project. Proves Compono.Generators.ComposeMethodDiscovery (Phase 1)
// generates a real plan through the packaged Compono.XunitV3 -> Compono dependency, not just an
// isolated Compono.Generators.Tests snapshot test.
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
