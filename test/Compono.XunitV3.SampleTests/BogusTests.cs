namespace Compono.XunitV3.SampleTests;

// PLAN-0006 Phase 3's real-runner proof - this plan's own Goal scenario, run through the actual
// packaged Compono.Bogus/Compono.NSubstitute -> Compono dependency chain under a real xUnit v3 theory,
// matching NSubstituteTests.cs's own real-packaged-consumer strategy (PLAN-0004 Phase 3/PLAN-0005
// Phase 2). Reuses IOrderRepository/Order/PlaceOrder/CreateOrderHandler from NSubstituteTests.cs -
// coexistence is the point, not a second parallel domain model.
public sealed class Customer
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }
}

// Fixed pipeline precedence (semantic before test-double) needs no special ordering between
// UseBogus()/UseNSubstitute() - this profile calls them in the opposite order from
// NSubstituteTestProfile's own UseNSubstitute()-only configuration, proving call order doesn't matter.
public sealed class BogusTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseBogus().UseNSubstitute();
}

public sealed class BogusTests
{
    // Customer's FirstName/LastName/Email come from BogusMemberNameProvider's real, deterministically-
    // seeded Faker values; repository is a real NSubstitute substitute, reused by handler's own
    // constructor parameter with no manual Substitute.For<T>() call anywhere in this test - the same
    // graph shape NSubstituteTests.Saves_order proves for NSubstitute alone, now composed alongside
    // Bogus with zero interaction between the two packages' code.
    [Theory]
    [Compose<BogusTestProfile>]
    public async Task Saves_order([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command, Customer customer)
    {
        customer.FirstName.Should().NotBeNullOrWhiteSpace();
        customer.LastName.Should().NotBeNullOrWhiteSpace();
        customer.Email.Should().Contain("@");

        await handler.Handle(command);

        await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }
}
