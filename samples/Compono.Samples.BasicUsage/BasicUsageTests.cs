using Compono.XunitV3;

namespace Compono.Samples.BasicUsage;

// The core Compono workflow, demonstrated end to end - see docs/samples/basic-usage.md for the
// overview and docs/getting-started/ for the guided walkthrough this sample assumes.
public sealed class BasicUsageTests
{
    [Fact]
    public void Composer_Create_ComposesASingleInstance()
    {
        var composer = Composer.Create();

        var order = composer.Create<CreateOrder>();

        order.Should().NotBeNull();
    }

    [Fact]
    public void Composer_CreateMany_ComposesIndependentInstances()
    {
        var composer = Composer.Create();

        var orders = composer.CreateMany<CreateOrder>(3);

        orders.Should().HaveCount(3);
        // Reference-distinctness, not OnlyHaveUniqueItems() - CreateOrder is a record, so
        // value-equal-but-independently-composed instances would pass a value-equality uniqueness
        // check anyway, which doesn't actually verify the "independent instances" claim CreateMany's
        // own contract makes.
        orders.Distinct(ReferenceEqualityComparer.Instance).Should().HaveCount(3);
    }

    [Fact]
    public void Composer_AddProfile_AppliesTheProfilesRegistrationAndMemberRule()
    {
        var composer = Composer.Create(builder => builder.AddProfile<SampleApplicationProfile>());

        var customer = composer.Create<Customer>();

        customer.Email.Should().Be("sample.customer@example.com");
    }

    [Fact]
    public void Composer_WithSeed_IsDeterministic()
    {
        var first = Composer.Create(builder => builder.WithSeed(4219)).Create<CreateOrder>();
        var second = Composer.Create(builder => builder.WithSeed(4219)).Create<CreateOrder>();

        first.Should().Be(second);
    }

    [Theory]
    [Compose<SampleApplicationProfile>]
    public void SharedRepositoryIsReusedByTheService([Shared] Repository repository, OrderService service, CreateOrder command)
    {
        service.Repository.Should().BeSameAs(repository);
        command.Should().NotBeNull();
    }

    // [Compose(Seed = ...)] is the reproduction mechanism a failing composed theory's own reported
    // "Seed: <value>" gets pasted back into (docs/concepts/determinism-and-seeding.md) - a
    // *separate* seed path from Composer.WithSeed above (row-scoped vs. programmatic), not one to
    // cross-compare against it.
    [Theory]
    [Compose(Seed = 4219)]
    public void ComposeAttributeSeedReproducesTheSameRowOnEveryRun(CreateOrder order)
    {
        order.Should().NotBeNull();
    }
}
