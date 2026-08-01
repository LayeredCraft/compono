namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 6 Phase 0's <see cref="ICompositionContext.DeriveSeed"/> - a path-derived,
/// on-demand deterministic seed a registration/configuration-rule factory or a public
/// <see cref="ICompositionValueProvider"/> can use for its own randomness, without exposing the
/// engine's own internal random source or path representation. See
/// <c>docs/adr/0026-deterministic-seed-derivation-for-providers.md</c>.
/// </summary>
public sealed class DeriveSeedTests
{
    [Fact]
    public void DeriveSeed_SameSeedAndSameRequestPath_ProducesTheSameValue()
    {
        var first = ComposeRoot();
        var second = ComposeRoot();

        first.Should().Be(second);

        static int ComposeRoot() =>
            Composer.Create(builder => builder
                    .WithSeed(4219)
                    .Register<Widget>(ctx => new Widget(ctx.DeriveSeed())))
                .Create<Widget>()
                .Value;
    }

    [Fact]
    public void DeriveSeed_SiblingRequestsInsideOneFactory_DeriveIndependentValues()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<int>(ctx => ctx.DeriveSeed())
            .Register<Pair>(ctx => new Pair(ctx.Resolve<int>(), ctx.Resolve<int>())));

        var result = composer.Create<Pair>();

        result.First.Should().NotBe(result.Second);
    }

    [Fact]
    public void DeriveSeed_CalledTwiceForTheSameActiveRequest_ReturnsTheSameValueBothTimes()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<Pair>(ctx => new Pair(ctx.DeriveSeed(), ctx.DeriveSeed())));

        var result = composer.Create<Pair>();

        result.First.Should().Be(result.Second);
    }

    [Fact]
    public void DeriveSeed_IsUnaffectedByRenamingAConstructorParameter_ButChangedByReorderingIt()
    {
        var original = ComposeViaDescriptor(ordinal: 0, name: "a");
        var renamed = ComposeViaDescriptor(ordinal: 0, name: "b");
        var reordered = ComposeViaDescriptor(ordinal: 1, name: "a");

        renamed.Should().Be(original);
        reordered.Should().NotBe(original);

        static int ComposeViaDescriptor(int ordinal, string name)
        {
            var descriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.ConstructorParameter, ordinal, name, declaringType: null, Nullability.NotNullable);

            var composer = Composer.Create(builder => builder
                .WithSeed(4219)
                .Register<int>(ctx => ctx.DeriveSeed())
                .Register<Outer>(ctx => new Outer(ctx.Resolve<int>(descriptor))));

            return composer.Create<Outer>().Value;
        }
    }

    [Fact]
    public void DeriveSeed_WithNoActiveFactoryOrProviderInvocation_Throws()
    {
        var context = new CompositionContext();

        var act = () => context.DeriveSeed();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeriveSeed_IsCallableFromInsideAPublicProviderTryProvide_AndIsDeterministicForTheSameSeed()
    {
        var first = Composer.Create(builder => builder.WithSeed(4219).AddTestDoubleProvider(new SeedCapturingProvider())).Create<Widget>();
        var second = Composer.Create(builder => builder.WithSeed(4219).AddTestDoubleProvider(new SeedCapturingProvider())).Create<Widget>();

        first.Should().Be(second);
    }

    [Fact]
    public void DeriveSeed_CalledConcurrently_ProducesTheSameCorrectOutputPerCall_WhenSeedIsExplicit()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<Widget>(ctx => new Widget(ctx.DeriveSeed())));
        var expected = composer.Create<Widget>();
        var results = new Widget[50];

        Parallel.For(0, results.Length, i => results[i] = composer.Create<Widget>());

        // Every concurrent Create<Widget>() call against the same shared Composer landing on the
        // exact same, independently-verified-correct value is strong evidence against shared mutable
        // state bleeding between concurrent DeriveSeed() calls, not just an absence of exceptions.
        results.Should().AllSatisfy(result => result.Should().Be(expected));
    }

    private sealed record Widget(int Value);

    private sealed record Pair(int First, int Second);

    private sealed record Outer(int Value);

    private sealed class SeedCapturingProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            CompositionProviderResult.Handled(new Widget(context.DeriveSeed()));
    }
}
