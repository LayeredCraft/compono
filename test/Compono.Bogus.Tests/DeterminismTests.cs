using System.Collections.Concurrent;
using Bogus;

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

    [Fact]
    public void TryProvide_ProducesCorrectValues_WhenCalledConcurrently_OnOneSharedProviderInstance()
    {
        // BogusMemberNameProvider caches one Faker per thread (ADR-0027's Amendment) instead of
        // constructing one per request - this proves that reuse is actually safe under real
        // concurrent access, not just assumed safe because no test caught a problem.
        //
        // A Parallel.ForEachAsync-driven version of this test was tried first, but its per-item
        // body had no genuine await point (the composition call is fully synchronous), so nothing
        // guaranteed the runtime actually dispatched work across multiple threads at the same
        // instant rather than running items back-to-back - a shared-Faker race could pass this
        // test serially, purely by luck of the scheduler. Real System.Threading.Thread instances
        // plus a Barrier remove that luck entirely: every worker blocks at the barrier until all
        // of them have arrived, then releases simultaneously, so the composition calls below are
        // guaranteed to genuinely overlap in wall-clock time on one shared provider instance.
        const int workerCount = 16;
        var provider = new BogusMemberNameProvider("en");
        var barrier = new Barrier(workerCount);
        var results = new ConcurrentBag<(int Seed, string Value)>();

        var workers = Enumerable.Range(0, workerCount)
            .Select(seed => new Thread(() =>
            {
                barrier.SignalAndWait();
                var value = Composer.Create(b => b.WithSeed(seed).AddSemanticProvider(provider))
                    .CreateRow(typeof(DeterminismTests))
                    .Resolve<string>(EmailDescriptor);

                results.Add((seed, value));
            }))
            .ToArray();

        foreach (var worker in workers)
            worker.Start();
        foreach (var worker in workers)
            worker.Join();

        // Each seed's concurrently-produced value must match what a fresh, independent,
        // single-threaded resolution for that same seed produces - proving no cross-call state
        // leaked between threads sharing the reused Faker, not just that nothing threw.
        results.Should().HaveCount(workerCount);
        foreach (var (seed, value) in results)
        {
            var expected = Composer.Create(b => b.WithSeed(seed).AddSemanticProvider(new BogusMemberNameProvider("en")))
                .CreateRow(typeof(DeterminismTests))
                .Resolve<string>(EmailDescriptor);

            value.Should().Be(expected);
        }
    }

    [Fact]
    public void CustomConventionMutatingFakerState_DoesNotPerturbALaterBuiltInRequestOnTheSameThread()
    {
        // A custom AddConvention delegate that reassigns Faker.DateTimeReference (a completely
        // ordinary thing to do - not a hostile/adversarial delegate) must not leave that mutation
        // behind for a later, unrelated built-in-convention request to observe. Built-in
        // conventions reuse the thread's cached Faker (BogusConventions.IsBuiltIn); custom
        // conventions get their own single-use Faker instead, specifically so this can't happen -
        // see ADR-0027's Amendment.
        var conventions = new Dictionary<string, Func<Faker, string>>
        {
            ["Custom"] = faker =>
            {
                faker.DateTimeReference = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return faker.Date.Recent().ToString("O");
            },
            ["Email"] = f => f.Internet.Email(),
        };
        var provider = new BogusMemberNameProvider("en", conventions);

        var customDescriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Custom", declaringType: null, Nullability.NotNullable);

        Composer.Create(b => b.WithSeed(4219).AddSemanticProvider(provider))
            .CreateRow(typeof(DeterminismTests))
            .Resolve<string>(customDescriptor);

        // Same provider instance (so the same thread's cached Faker, if a built-in request were
        // ever to touch the same instance the custom one just mutated) resolves Email afterward -
        // must match an independently-computed reference value exactly, proving the custom
        // convention's DateTimeReference mutation never reached the built-in convention's Faker.
        var afterCustom = Composer.Create(b => b.WithSeed(4219).AddSemanticProvider(provider))
            .CreateRow(typeof(DeterminismTests))
            .Resolve<string>(EmailDescriptor);
        var expected = Composer.Create(b => b.WithSeed(4219).AddSemanticProvider(new BogusMemberNameProvider("en", conventions)))
            .CreateRow(typeof(DeterminismTests))
            .Resolve<string>(EmailDescriptor);

        afterCustom.Should().Be(expected);
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
