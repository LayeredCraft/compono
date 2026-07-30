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
        var original = new InvalidOperationException("boom");
        var composer = Composer.Create(builder => builder.Register<Failing>(_ =>
        {
            attempts++;
            throw original;
        }));

        // A factory-thrown exception is an authoritative stage-3 Failure per ADR-0010 - it surfaces
        // as a structured CompositionException with the original preserved as InnerException, not
        // raw, per the "throw a structured CompositionException" contract every other authoritative
        // stage in this codebase already follows.
        var first = () => composer.Create<Failing>();
        first.Should().Throw<CompositionException>().Which.InnerException.Should().BeSameAs(original);

        // If the factory-reentrance guard's finally hadn't popped this factory off its stack, this
        // second, independent Create<T>() call would see the factory as "already active" and throw
        // the reentrance CompositionException (naming a recursion cycle) instead of genuinely
        // invoking it again and wrapping the same original exception a second time.
        var second = () => composer.Create<Failing>();
        second.Should().Throw<CompositionException>().Which.InnerException.Should().BeSameAs(original);
        attempts.Should().Be(2);
    }

    [Fact]
    public void InvokeFactory_DoesNotRewrapACompositionExceptionAlreadyThrownByANestedResolveCall()
    {
        // A factory whose nested context.Resolve<T>() call fails already produces a fully-diagnosed
        // CompositionException (its own path/trace/seed) from that nested ResolveCore call - InvokeFactory
        // must let it propagate as-is, not catch and re-wrap it behind a generic "the factory threw"
        // message that would discard the more specific inner diagnostic.
        var composer = Composer.Create(builder => builder.Register<Wrapper>(ctx => new Wrapper(ctx.Resolve<Unresolvable>())));

        var act = () => composer.Create<Wrapper>();

        act.Should().Throw<CompositionException>()
            .WithMessage("*No registration*")
            .Where(e => !e.Message.Contains("threw"));
    }

    [Fact]
    public void InvokeFactory_WrapsACompositionExceptionThrownDirectlyByTheFactoryItself()
    {
        // Only a CompositionException built by BuildException (from a nested Resolve<T>() call) has a
        // non-null Diagnostic. A factory that constructs and throws `new CompositionException("...")`
        // directly - never having called Resolve<T>() at all - produces one with a null Diagnostic, and
        // must be treated the same as any other factory-thrown exception: wrapped into an authoritative
        // stage-3 Failure with this call's own path/seed/trace, and the original preserved as
        // InnerException, not left to escape as-is with none of that context.
        var original = new CompositionException("factory-authored failure, no nested Resolve<T>() involved");
        var composer = Composer.Create(builder => builder.Register<Failing>(_ => throw original));

        var act = () => composer.Create<Failing>();

        var exception = act.Should().Throw<CompositionException>().Which;
        exception.Should().NotBeSameAs(original);
        exception.InnerException.Should().BeSameAs(original);
        exception.Diagnostic.Should().NotBeNull();
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

    private sealed record Wrapper(Unresolvable Value);

    private sealed record Unresolvable;
}
