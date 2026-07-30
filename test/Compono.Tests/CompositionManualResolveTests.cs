namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 3 Phase 1's manual-resolve invocation-frame mechanics -
/// <see cref="ICompositionContext.Resolve{TValue}()"/>'s call-sequence-ordinal path identity, and the
/// factory-reentrance guard invoked registration/rule factories share - per
/// <c>docs/adr/0019-registrations-and-service-provider-injection.md</c>.
/// </summary>
public sealed class CompositionManualResolveTests
{
    [Fact]
    public void Resolve_SiblingCallsInsideOneFactory_ForkIndependently()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<Pair>(ctx => new Pair(ctx.Resolve<ulong>(), ctx.Resolve<ulong>())));

        var result = composer.Create<Pair>();

        result.First.Should().NotBe(result.Second);
    }

    [Fact]
    public void Resolve_NestedFactoryInvocation_GetsAnIndependentCounter_FromTheOuterFactory()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<Inner>(ctx => new Inner(ctx.Resolve<ulong>()))
            .Register<Outer>(ctx => new Outer(ctx.Resolve<ulong>(), ctx.Resolve<Inner>(), ctx.Resolve<ulong>())));

        var result = composer.Create<Outer>();

        // Outer's ManualResolve(0) and Inner's own ManualResolve(0) are distinct path nodes (children
        // of Outer vs. children of Inner) - a regression sharing one counter across the nested
        // invocation would make Inner.Value collide with Outer.First instead.
        result.First.Should().NotBe(result.Inner.Value);
        result.First.Should().NotBe(result.Third);
        result.Inner.Value.Should().NotBe(result.Third);
    }

    [Fact]
    public void Resolve_ProducesIdenticalOutput_ForRepeatedCompositions_WithTheSameSeed()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<Pair>(ctx => new Pair(ctx.Resolve<ulong>(), ctx.Resolve<ulong>())));

        var first = composer.Create<Pair>();
        var second = composer.Create<Pair>();

        first.Should().Be(second);
    }

    [Fact]
    public void InvokeFactory_PopsTheFactoryReentranceStack_WhenTheFactoryThrows_SoASubsequentCallInvokesItAgain()
    {
        var attempts = 0;
        var composer = Composer.Create(builder => builder.Register<Failing>(_ =>
        {
            attempts++;
            throw new InvalidOperationException("boom");
        }));

        var first = () => composer.Create<Failing>();
        first.Should().Throw<InvalidOperationException>().WithMessage("boom");

        // If the factory-reentrance guard's finally hadn't popped this factory off its stack, this
        // second, independent Create<T>() call would see the factory as "already active" and throw
        // the reentrance CompositionException instead of genuinely invoking it again.
        var second = () => composer.Create<Failing>();
        second.Should().Throw<InvalidOperationException>().WithMessage("boom");
        attempts.Should().Be(2);
    }

    [Fact]
    public void Resolve_WithNoActiveFactoryInvocation_Throws()
    {
        var context = new CompositionContext();

        var act = () => context.Resolve<int>();

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed record Pair(ulong First, ulong Second);

    private sealed record Inner(ulong Value);

    private sealed record Outer(ulong First, Inner Inner, ulong Third);

    private sealed record Failing;
}
