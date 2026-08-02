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
        // implementation could coincidentally repeat a name from Bogus's own finite name list. A
        // Guid-valued rule removes that finite-cardinality ambiguity entirely - two independent
        // per-item seeds practically never collide on a Guid, so "all 5 are pairwise distinct" is a
        // real, high-cardinality assertion of independent seeding, not a "usually passes anyway" one.
        //
        // But distinctness alone still isn't sufficient - a regression that assigns every item the
        // *same* derived seed would produce identical items (never satisfying distinctness, so that
        // part alone would already catch it), while a regression that derives each item's seed from
        // (total count, index) rather than index alone would satisfy distinctness within a single call
        // but never reproduce a shared prefix across two different total counts. Only the two
        // assertions together - independently distinct AND shared-prefix-across-counts - pin down the
        // real ADR-0012 CreateMany contract: item i's seed depends only on the batch seed and its own
        // index, never on the total count requested. The shared-prefix half mirrors Compono.Tests' own
        // ComposerCreateManyTests.CreateManyForTesting_ProducesByteForByteIdenticalItems_ForTheSharedPrefixOfTwoDifferentCounts,
        // through the public Composer.CreateMany<T>() surface instead of the internal test seam.
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .UseBogus<Customer>(faker => faker.RuleFor(c => c.FirstName, f => f.Random.Guid().ToString())));

        var three = composer.CreateMany<Customer>(3).Select(c => c.FirstName).ToArray();
        var five = composer.CreateMany<Customer>(5).Select(c => c.FirstName).ToArray();

        five.Should().OnlyHaveUniqueItems();
        five.Take(3).Should().Equal(three);
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
