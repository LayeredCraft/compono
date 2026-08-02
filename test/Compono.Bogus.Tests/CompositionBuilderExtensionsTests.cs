using Bogus;

namespace Compono.Bogus.Tests;

/// <summary>
/// <c>CompositionBuilderExtensions</c>'s <c>UseBogus()</c>/<c>UseBogus(Action{BogusOptions})</c>/
/// <c>UseBogus&lt;T&gt;(...)</c> wiring into a real <see cref="Composer"/> - PLAN-0006 Phase 3.
/// </summary>
public sealed class CompositionBuilderExtensionsTests
{
    [Fact]
    public void UseBogus_WithNoConfiguration_WiresAWorkingProvider_ForAnAllowlistedMemberName()
    {
        // CompositionRow.Resolve(descriptor), not Composer.Create<Customer>() - Compono.Bogus.Tests
        // doesn't reference the source generator (testing.md's own established pattern), so a
        // manually-descriptored member request is what exercises the wired provider without needing a
        // generated plan for a whole class.
        var descriptor = EmailDescriptor;
        var withoutProvider = Resolve<string>(descriptor, static builder => builder.WithSeed(4219));
        var withProvider = Resolve<string>(descriptor, static builder => builder.WithSeed(4219).UseBogus());

        withProvider.Should().NotBe(withoutProvider);
    }

    [Fact]
    public void UseBogus_Configured_EnableMemberNameConventionsFalse_NeverRegistersTheConventionProvider()
    {
        var descriptor = EmailDescriptor;
        var withoutProvider = Resolve<string>(descriptor, static builder => builder.WithSeed(4219));
        var withProviderDisabled = Resolve<string>(
            descriptor, static builder => builder.WithSeed(4219).UseBogus(options => options.EnableMemberNameConventions = false));

        // Equivalent to the "no provider at all" fallback - proving the provider truly isn't
        // registered, not just configured to look different.
        withProviderDisabled.Should().Be(withoutProvider);
    }

    [Fact]
    public void UseBogus_Configure_ThrowsForANullDelegate()
    {
        var act = () => Composer.Create(builder => builder.UseBogus(null!));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseBogusOfT_ProducesAFullyBogusGeneratedInstance_IncludingACorrelatedRule()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .UseBogus<Customer>(faker => faker
                .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                .RuleFor(c => c.FullName, (f, c) => $"{c.FirstName} Prefixed")));

        var customer = composer.Create<Customer>();

        customer.FullName.Should().Be($"{customer.FirstName} Prefixed");
    }

    [Fact]
    public void UseBogusOfT_DuplicateRegistrationForTheSameType_ThrowsCompositionConfigurationException()
    {
        var act = () => Composer.Create(builder => builder
            .UseBogus<Customer>(faker => faker.RuleFor(c => c.FirstName, f => f.Name.FirstName()))
            .UseBogus<Customer>(faker => faker.RuleFor(c => c.FirstName, f => f.Name.FirstName())));

        act.Should().Throw<CompositionConfigurationException>();
    }

    [Fact]
    public void UseBogusOfT_ConfigureFakerThatEagerlyDrawsFromTheSeededRandomizer_IsStillDeterministicForTheSameSeed()
    {
        // ADR-0027 Amendment 1 regression: Faker<T> exposes no public Random accessor (only the
        // member-rule sugar's plain Faker does), so the only way configureFaker can eagerly draw from
        // this request's seeded Randomizer - rather than a lazy f => f.Name.FirstName() factory
        // Generate() evaluates later - is registering a genuinely random-valued rule, then calling
        // faker.Generate() itself before returning to force that rule to evaluate immediately,
        // consuming randomness right there rather than later. (Calling Generate() with no rule
        // registered first - a prior version of this test's mistake, caught by review - just returns
        // Customer's default, constant property values regardless of seed, proving nothing.) The
        // eagerly-drawn value is then pinned as the final rule, so the extension's own later
        // Generate() call returns that exact already-drawn value. This eager draw must still be
        // deterministic for the same Compono seed, proving UseSeed(...) runs before configureFaker,
        // not after.
        static Customer ComposeRoot() =>
            Composer.Create(builder => builder
                    .WithSeed(4219)
                    .UseBogus<Customer>(faker =>
                    {
                        faker.RuleFor(c => c.FirstName, f => f.Random.Guid().ToString());
                        var eager = faker.Generate();
                        faker.RuleFor(c => c.FirstName, eager.FirstName);
                    }))
                .Create<Customer>();

        var first = ComposeRoot();
        var second = ComposeRoot();

        first.FirstName.Should().Be(second.FirstName);
    }

    private static CompositionRequestDescriptor EmailDescriptor =>
        new(CompositionRequestKind.ConstructorParameter, ordinal: 0, "Email", declaringType: null, Nullability.NotNullable);

    private static TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor, Action<CompositionBuilder> configure) =>
        Composer.Create(configure).CreateRow(typeof(CompositionBuilderExtensionsTests)).Resolve<TValue>(descriptor);

    public sealed class Customer
    {
        public string FirstName { get; set; } = "";

        public string FullName { get; set; } = "";

        public string Email { get; set; } = "";
    }
}
