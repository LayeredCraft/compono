namespace Compono.TestDoubles.SampleTests;

// A direct composer.Create<OrderService>() call, deliberately never composing IRepository as its
// own call-site parameter anywhere in this test (unlike GeneratedDoubleTests' own [Shared]
// IRepository theory parameters) - the literal API PLAN-0043's own Phase 2 checklist names, proving
// the composition engine discovers and satisfies OrderService's nested IRepository dependency
// purely through the generated-double provider, not just that a sibling [Compose] theory parameter
// happens to compose correctly (PR #85 review).
public sealed class CreateOrderServiceTests
{
    [Fact]
    public async Task Create_OrderService_resolves_its_nested_IRepository_dependency_through_the_generated_double()
    {
        var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());

        var service = composer.Create<OrderService>();
        service.Repository.Configure().CountAsync().Returns(Task.FromResult(4));

        var order = await service.PlaceAsync(6);

        order.Total.Should().Be(10);
    }
}
