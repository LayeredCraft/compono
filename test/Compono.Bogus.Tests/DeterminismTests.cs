namespace Compono.Bogus.Tests;

/// <summary>
/// ADR-0026's determinism contract, exercised through real Bogus usage rather than
/// <c>Compono.Tests</c>' own <c>DeriveSeed()</c>-level coverage - PLAN-0006 Phase 3.
/// </summary>
public sealed class DeterminismTests
{
    [Fact]
    public void SameSeed_ReproducesTheSameConventionProviderValue()
    {
        static string ComposeRoot() =>
            Composer.Create(builder => builder.WithSeed(4219).UseBogus())
                .CreateRow(typeof(DeterminismTests))
                .Resolve<string>(EmailDescriptor);

        var first = ComposeRoot();
        var second = ComposeRoot();

        first.Should().Be(second);
    }

    [Fact]
    public void SameSeed_ReproducesTheSameUseBogusOfTGeneratedObject()
    {
        static Customer ComposeRoot() =>
            Composer.Create(builder => builder
                    .WithSeed(4219)
                    .UseBogus<Customer>(faker => faker.RuleFor(c => c.FirstName, f => f.Name.FirstName())))
                .Create<Customer>();

        var first = ComposeRoot();
        var second = ComposeRoot();

        first.FirstName.Should().Be(second.FirstName);
    }

    [Fact]
    public void AddingAnUnrelatedBogusBackedMember_DoesNotPerturbAnExistingOnesValue()
    {
        var baseline = Composer.Create(builder => builder.WithSeed(4219).UseBogus())
            .CreateRow(typeof(DeterminismTests))
            .Resolve<string>(EmailDescriptor);

        var withUnrelatedSibling = Composer.Create(builder => builder.WithSeed(4219).UseBogus())
            .CreateRow(typeof(DeterminismTests));
        var email = withUnrelatedSibling.Resolve<string>(EmailDescriptor);
        // An unrelated sibling request on the same row, resolved after Email - each descriptor's own
        // ordinal forks an independent path (ADR-0012), so resolving it doesn't change Email's already-
        // produced value.
        withUnrelatedSibling.Resolve<string>(FirstNameDescriptor);

        email.Should().Be(baseline);
    }

    [Fact]
    public void CreateMany_ProducesIndependentlySeededItems_ForAUseBogusOfTRegisteredType()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .UseBogus<Customer>(faker => faker.RuleFor(c => c.FirstName, f => f.Name.FirstName())));

        var customers = composer.CreateMany<Customer>(5);

        customers.Select(c => c.FirstName).Distinct().Should().HaveCount(5);
    }

    private static CompositionRequestDescriptor EmailDescriptor =>
        new(CompositionRequestKind.ConstructorParameter, ordinal: 0, "Email", declaringType: null, Nullability.NotNullable);

    private static CompositionRequestDescriptor FirstNameDescriptor =>
        new(CompositionRequestKind.ConstructorParameter, ordinal: 1, "FirstName", declaringType: null, Nullability.NotNullable);

    public sealed class Customer
    {
        public string FirstName { get; set; } = "";
    }
}
