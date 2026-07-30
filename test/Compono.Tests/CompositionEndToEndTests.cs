namespace Compono.Tests;

/// <summary>
/// Milestone 2's exit-criteria pass: the <c>Customer</c>/<c>Address</c> nested graph from
/// <c>docs/plans/0002-milestone-2-core-composition-engine.md</c>'s Execution Flow section, composed
/// end to end via <see cref="Composer.Create{T}"/>/<see cref="Composer.CreateMany{T}"/>. Uses
/// hand-written <see cref="ICompositionPlan{T}"/> test doubles rather than the real generator, per
/// Milestone 1's Phase 0 note (<c>Compono.Tests</c> doesn't reference <c>Compono.Generators</c> as
/// an analyzer) - real generator dispatch is covered separately by
/// <c>Compono.Generators.Tests</c>' <c>GeneratedCollectionPlanExecutionTests</c>.
/// </summary>
public sealed class CompositionEndToEndTests
{
    [Fact]
    public void Create_ComposesTheNestedCustomerAddressGraph_WithEveryLeafValuePresent()
    {
        RegisterPlans();

        try
        {
            var composer = Composer.Create();

            var customer = composer.Create<Customer>();

            customer.FirstName.Should().NotBeNullOrEmpty();
            customer.LastName.Should().NotBeNullOrEmpty();
            customer.HomeAddress.Street.Should().NotBeNullOrEmpty();
            customer.HomeAddress.City.Should().NotBeNullOrEmpty();
        }
        finally
        {
            UnregisterPlans();
        }
    }

    [Fact]
    public void CreateMany_ComposesIndependentCustomers_DeterministicallyFromTheSameBatchSeed()
    {
        RegisterPlans();

        try
        {
            var seed = new CompositionSeed(4219);

            var first = Composer.CreateManyForTesting<Customer>(3, seed);
            var second = Composer.CreateManyForTesting<Customer>(3, seed);

            first.Should().Equal(second);
            first.Select(c => c.FirstName).Distinct().Should().HaveCount(3);
        }
        finally
        {
            UnregisterPlans();
        }
    }

    private static void RegisterPlans()
    {
        PlanCache<Customer>.Instance = new CustomerPlan();
        PlanCache<Address>.Instance = new AddressPlan();
    }

    private static void UnregisterPlans()
    {
        PlanCache<Customer>.Instance = null;
        PlanCache<Address>.Instance = null;
    }

    private static CompositionRequestDescriptor Descriptor(int ordinal, string name) =>
        new(CompositionRequestKind.ConstructorParameter, ordinal, name, declaringType: null, Nullability.NotNullable);

    private sealed record Address(string Street, string City);

    private sealed record Customer(string FirstName, string LastName, Address HomeAddress);

    private sealed class AddressPlan : ICompositionPlan<Address>
    {
        public Address Compose(ICompositionContext context) =>
            new(
                context.Resolve<string>(Descriptor(0, "street")),
                context.Resolve<string>(Descriptor(1, "city")));
    }

    private sealed class CustomerPlan : ICompositionPlan<Customer>
    {
        public Customer Compose(ICompositionContext context) =>
            new(
                context.Resolve<string>(Descriptor(0, "firstName")),
                context.Resolve<string>(Descriptor(1, "lastName")),
                context.Resolve<Address>(Descriptor(2, "homeAddress")));
    }
}
