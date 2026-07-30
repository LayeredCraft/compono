namespace Compono.Tests;

public sealed class CompositionRowTests
{
    [Fact]
    public void Resolve_ForksIndependently_ForSiblingParametersOfTheSameType()
    {
        var composer = Composer.Create(builder => builder.WithSeed(4219));
        var row = composer.CreateRow(typeof(CompositionRowTests));
        var first = TestParameterDescriptor(ordinal: 0, "first");
        var second = TestParameterDescriptor(ordinal: 1, "second");

        var firstValue = row.Resolve<string>(first);
        var secondValue = row.Resolve<string>(second);

        firstValue.Should().NotBe(secondValue);
    }

    [Fact]
    public void Resolve_ReturnsSharedValue_ForALaterOrdinaryRequestOfTheSameType()
    {
        PlanCache<ComposedMarker>.Instance = new FixedPlan(new ComposedMarker("composed"));

        try
        {
            var composer = Composer.Create();
            var row = composer.CreateRow(typeof(CompositionRowTests));
            var sharedDescriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.TestParameter, ordinal: 0, name: "repository", declaringType: typeof(CompositionRowTests), Nullability.NotNullable);
            var laterDescriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.TestParameter, ordinal: 1, name: "another", declaringType: typeof(CompositionRowTests), Nullability.NotNullable);

            var shared = row.ResolveShared<ComposedMarker>(sharedDescriptor);
            var later = row.Resolve<ComposedMarker>(laterDescriptor);

            later.Should().BeSameAs(shared);
        }
        finally
        {
            PlanCache<ComposedMarker>.Instance = null;
        }
    }

    [Fact]
    public void ShareExplicit_Throws_WhenValueIsNullForANonNullableRequest()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(CompositionRowTests));
        var descriptor = TestParameterDescriptor(ordinal: 0, "repository");

        var act = () => row.ShareExplicit<UnresolvableMarker>(descriptor, null!);

        act.Should().Throw<CompositionException>()
            .WithMessage("*explicit value*not nullable*");
    }

    // No "ShareExplicit throws for a non-assignable runtime type" test: ShareExplicit<TValue>'s
    // `value` parameter is statically typed as TValue, so the compiler already guarantees it's
    // assignable - that branch of the shared ValidateAuthoritativeValue helper is only reachable from
    // a pipeline-produced object? (a registration/provider result), already covered by
    // ComposerRegistrationTests, not independently reachable through this strongly-typed entry point.

    [Fact]
    public void ShareExplicit_MakesTheValueVisible_ToALaterOrdinaryRequestOfTheSameType()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(CompositionRowTests));
        var sharedDescriptor = TestParameterDescriptor(ordinal: 0, "repository");
        var laterDescriptor = TestParameterDescriptor(ordinal: 1, "another");
        var value = new UnresolvableMarker("explicit");

        row.ShareExplicit(sharedDescriptor, value);
        var later = row.Resolve<UnresolvableMarker>(laterDescriptor);

        later.Should().BeSameAs(value);
    }

    [Fact]
    public void Diagnostic_RootTypeIsTheDeclaringType_WhenARowFails()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(CompositionRowTests));
        var descriptor = TestParameterDescriptor(ordinal: 0, "unresolvable");

        var act = () => row.Resolve<UnresolvableMarker>(descriptor);

        act.Should().Throw<CompositionException>()
            .Where(ex => ex.Diagnostic!.RootType == typeof(CompositionRowTests));
    }

    [Fact]
    public void ShareExplicit_DoesNotLeakAcrossRows_FromTwoIndependentCreateRowCalls()
    {
        var composer = Composer.Create();
        var firstRow = composer.CreateRow(typeof(CompositionRowTests));
        var secondRow = composer.CreateRow(typeof(CompositionRowTests));
        var descriptor = TestParameterDescriptor(ordinal: 0, "repository");

        firstRow.ShareExplicit(descriptor, new UnresolvableMarker("only-in-first-row"));
        var act = () => secondRow.Resolve<UnresolvableMarker>(descriptor);

        act.Should().Throw<CompositionException>();
    }

    [Fact]
    public void CreateRow_UsesTheConfiguredSeed_RoundTrippedExactlyForANegativeValue()
    {
        var composer = Composer.Create(builder => builder.WithSeed(-500));

        var row = composer.CreateRow(typeof(CompositionRowTests));

        row.Seed.Should().Be(-500);
    }

    [Fact]
    public void CreateRow_UsesTheConfiguredSeed_RoundTrippedExactlyForAPositiveValue()
    {
        var composer = Composer.Create(builder => builder.WithSeed(8492173));

        var row = composer.CreateRow(typeof(CompositionRowTests));

        row.Seed.Should().Be(8492173);
    }

    [Fact]
    public void CreateRow_GeneratesANonNegativeSeed_WhenNoneIsConfigured()
    {
        var composer = Composer.Create();

        var row = composer.CreateRow(typeof(CompositionRowTests));

        row.Seed.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Diagnostic_SeedTextMatchesRowSeed_WhenAnUnseededRowFails()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(CompositionRowTests));
        var descriptor = TestParameterDescriptor(ordinal: 0, "unresolvable");

        var act = () => row.Resolve<UnresolvableMarker>(descriptor);

        act.Should().Throw<CompositionException>()
            .Where(ex => ex.Diagnostic!.ToString().Contains($"Seed: {row.Seed}"));
    }

    private static CompositionRequestDescriptor TestParameterDescriptor(int ordinal, string name) =>
        new(CompositionRequestKind.TestParameter, ordinal, name, declaringType: typeof(CompositionRowTests), Nullability.NotNullable);

    // Never given a PlanCache<T> plan, no registration - deliberately uncomposable, so a test can
    // assert "nothing satisfies this except an explicit/shared value" unambiguously.
    private sealed record UnresolvableMarker(string Value);

    // Given a real PlanCache<T> plan only inside the one test that needs a genuinely *composed* shared
    // value - a distinct type from UnresolvableMarker so no other test in this class can observe its
    // PlanCache<T> mutation, even under parallel execution.
    private sealed record ComposedMarker(string Value);

    private sealed class FixedPlan(ComposedMarker value) : ICompositionPlan<ComposedMarker>
    {
        public ComposedMarker Compose(ICompositionContext context) => value;
    }
}
