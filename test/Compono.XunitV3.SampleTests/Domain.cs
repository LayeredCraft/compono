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

// No provider, registration, [Composable], or generated plan can ever satisfy this - reserved for
// FailingCompositionTests, which needs a genuine (pipeline-propagated) composition failure rather
// than one of Compono.XunitV3's own pre-composition validation failures (PR #26 review).
//
// Deliberately never used as a [Compose]-attributed parameter's own type directly - an
// interface/abstract/delegate root always reports CMP0003 at compile time (a documented, deliberate
// PLAN-0004 Open Item: ComposedTypeAnalyzer's root-level check is stricter than its nested-member
// check), so an abstract root here would be a compile error, not the runtime composition failure this
// fixture needs. GatewayConsumer below wraps it as a nested constructor parameter instead, which uses
// the more lenient member-level check and is left as a runtime Resolve<T> call.
public interface IUnregisteredGateway;

public sealed class GatewayConsumer
{
    public GatewayConsumer(IUnregisteredGateway gateway)
    {
        Gateway = gateway;
    }

    public IUnregisteredGateway Gateway { get; }
}
