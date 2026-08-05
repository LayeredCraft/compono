using Compono.XunitV3;

namespace Compono.Samples.AspNetApi.Tests;

public sealed class OrderServiceTests
{
    // [Shared] IOrderRepository is a real NSubstitute substitute, reused as the exact same instance
    // inside service's own composed IOrderRepository constructor parameter - no manual
    // Substitute.For<T>() call anywhere in this test. Explicit setup (Returns) on top of that
    // composed substitute, then verified via Received - the two substitute-usage styles
    // docs/packages/compono-nsubstitute.md covers.
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task PlaceAsync_PersistsTheOrderAndReturnsTheRepositorysResult(
        [Shared] IOrderRepository repository, OrderService service, PlaceOrder command, Order savedOrder)
    {
        repository.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(savedOrder);

        var result = await service.PlaceAsync(command, CancellationToken.None);

        result.Should().Be(savedOrder);
        await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    // Mixes an inline value (quantity) with composed values (everything else) in the same row -
    // see docs/concepts/composition-model.md's inline-vs-composed distinction.
    [Theory]
    [Compose(5)]
    public void PlaceOrder_MixesInlineAndComposedValues(int quantity, string customerName)
    {
        var command = new PlaceOrder(customerName, quantity);

        command.Quantity.Should().Be(5);
        command.CustomerName.Should().NotBeNullOrWhiteSpace();
    }

    // Customer's FirstName/LastName/Email come from Compono.Bogus's real, deterministically-seeded
    // Faker values, composed alongside NSubstitute's IOrderRepository substitute with zero
    // interaction between the two packages' code - the same "Bogus + NSubstitute together" shape
    // test/Compono.XunitV3.SampleTests/BogusTests.cs proves in isolation.
    [Theory]
    [Compose<ApiTestProfile>]
    public void ComposesARealisticCustomerAlongsideTheSubstitute([Shared] IOrderRepository repository, Customer customer)
    {
        customer.FirstName.Should().NotBeNullOrWhiteSpace();
        customer.LastName.Should().NotBeNullOrWhiteSpace();
        customer.Email.Should().Contain("@");
        repository.Should().NotBeNull();
    }

    // A fixed seed reproduces the exact same composed row every time it's run - the same
    // reproduction mechanic a real composition failure's reported `Seed: <value>` relies on
    // (docs/concepts/determinism-and-seeding.md).
    [Fact]
    public void ComposeWithAFixedSeedIsReproducible()
    {
        var first = Composer.Create(builder => builder.WithSeed(24601).AddProfile<ApiTestProfile>()).Create<Customer>();
        var second = Composer.Create(builder => builder.WithSeed(24601).AddProfile<ApiTestProfile>()).Create<Customer>();

        first.FirstName.Should().Be(second.FirstName);
        first.LastName.Should().Be(second.LastName);
        first.Email.Should().Be(second.Email);
    }
}
