using NSubstitute;

namespace Compono.TUnit.SampleTests;

// PLAN-0040 Phase 1's own Goal-section scenario, run for real: composing through the actual
// packaged Compono.NSubstitute -> Compono.TUnit -> Compono dependency chain under a real TUnit
// runner, not just Compono.TUnit.Tests' own direct GetDataRowsAsync calls. Mirrors
// Compono.XunitV3.SampleTests/NSubstituteTests.cs exactly.
public interface IOrderRepository
{
    Task SaveAsync(Order order, CancellationToken cancellationToken);
}

public sealed record Order;

public sealed record PlaceOrder(string CustomerName, int Quantity);

public sealed class CreateOrderHandler
{
    public CreateOrderHandler(IOrderRepository repository)
    {
        Repository = repository;
    }

    public IOrderRepository Repository { get; }

    public Task Handle(PlaceOrder command) => Repository.SaveAsync(new Order(), CancellationToken.None);
}

// Applies UseNSubstitute() to this row's own CompositionBuilder, exactly like an application's
// Program.cs would - reached only through NSubstituteTests.Saves_order's own [Compose<TProfile>]
// method parameter.
public sealed class NSubstituteTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseNSubstitute();
}

public sealed class NSubstituteTests
{
    // This plan's own Goal-section example, run for real: repository is a real NSubstitute
    // substitute, reused as the exact same instance inside handler's own composed IOrderRepository
    // constructor parameter, with no manual Substitute.For<T>() call anywhere in this test.
    [Test]
    [Compose<NSubstituteTestProfile>]
    public async Task Saves_order([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command)
    {
        await handler.Handle(command);

        await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }
}
