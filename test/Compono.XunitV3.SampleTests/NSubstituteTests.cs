namespace Compono.XunitV3.SampleTests;

// PLAN-0005 Phase 2's real-runner proof: composing through the actual packaged
// Compono.NSubstitute -> Compono dependency chain under a real xUnit v3 runner, not just
// Compono.NSubstitute.Tests' own direct Composer calls - matching PLAN-0004 Phase 3's real-packaged-
// consumer testing strategy (see this project's pack-to-local-feed.sh).
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
// Program.cs would (coding-standards.md's application-level-wiring rule) - reached only through
// NSubstituteTests.Saves_order's own [Compose<TProfile>] theory parameter.
public sealed class NSubstituteTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseNSubstitute();
}

public sealed class NSubstituteTests
{
    // This plan's own Goal-section example, run for real: repository is a real NSubstitute
    // substitute, reused as the exact same instance inside handler's own composed IOrderRepository
    // constructor parameter, with no manual Substitute.For<T>() call anywhere in this test.
    [Theory]
    [Compose<NSubstituteTestProfile>]
    public async Task Saves_order([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command)
    {
        await handler.Handle(command);

        await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }
}
