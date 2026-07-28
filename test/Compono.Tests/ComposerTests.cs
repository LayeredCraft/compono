namespace Compono.Tests;

public sealed class ComposerTests
{
    [Fact]
    public void Create_ReturnsComposedInstance_WhenGeneratedPlanIsRegistered()
    {
        PlanCache<ComposerProbe>.Instance = new FixedPlan(new ComposerProbe("from-composer"));

        try
        {
            var composer = Composer.Create();

            var result = composer.Create<ComposerProbe>();

            result.Value.Should().Be("from-composer");
        }
        finally
        {
            PlanCache<ComposerProbe>.Instance = null;
        }
    }

    [Fact]
    public void Create_Throws_WhenNoGeneratedPlanIsRegistered()
    {
        var composer = Composer.Create();

        var act = () => composer.Create<UnresolvableType>();

        act.Should().Throw<CompositionException>()
            .WithMessage("*UnresolvableType*");
    }

    private sealed record ComposerProbe(string Value);

    private sealed class UnresolvableType
    {
    }

    private sealed class FixedPlan(ComposerProbe value) : ICompositionPlan<ComposerProbe>
    {
        public ComposerProbe Compose(ICompositionContext context) => value;
    }
}
