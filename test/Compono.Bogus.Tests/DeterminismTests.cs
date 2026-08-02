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
        // An unrelated sibling request on the same row, resolved BEFORE Email - each descriptor's own
        // ordinal forks an independent path (ADR-0012), so resolving it first must not perturb Email's
        // own value. Resolving it after Email (as a prior version of this test did) would pass even
        // under a broken, single-shared-sequential-randomizer implementation, since a later draw can't
        // retroactively change an already-produced value - this order is what actually exercises the
        // path-independence regression.
        withUnrelatedSibling.Resolve<string>(FirstNameDescriptor);
        var email = withUnrelatedSibling.Resolve<string>(EmailDescriptor);

        email.Should().Be(baseline);
    }

    [Fact]
    public void CreateMany_ProducesIndependentlySeededItems_ForAUseBogusOfTRegisteredType()
    {
        // "5 distinct names" doesn't actually prove per-item seed derivation: a broken shared
        // sequential randomizer would usually still produce 5 distinct draws, and a correct
        // implementation could coincidentally repeat a name from Bogus's own finite name list. The
        // real ADR-0012 CreateMany contract - item i's seed depends only on the batch seed and its own
        // index, never on the total count requested - is what Compono.Tests' own
        // ComposerCreateManyTests.CreateManyForTesting_ProducesByteForByteIdenticalItems_ForTheSharedPrefixOfTwoDifferentCounts
        // asserts, mirrored here through the public Composer.CreateMany<T>() surface: item 0..2 must be
        // byte-for-byte identical whether 3 or 5 total items are requested from the same seed.
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .UseBogus<Customer>(faker => faker.RuleFor(c => c.FirstName, f => f.Name.FirstName())));

        var three = composer.CreateMany<Customer>(3).Select(c => c.FirstName);
        var five = composer.CreateMany<Customer>(5).Take(3).Select(c => c.FirstName);

        five.Should().Equal(three);
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
