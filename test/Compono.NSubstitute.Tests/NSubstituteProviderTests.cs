using NSubstitute.Core;

namespace Compono.NSubstitute.Tests;

/// <summary>
/// <see cref="NSubstituteProvider.TryProvide"/> unit coverage, exercised through a real
/// <see cref="Composer"/> (<see cref="CompositionProviderResult"/>'s <c>Value</c>/<c>IsHandled</c> are
/// internal to <c>Compono</c>, so a provider's own outcome is only observable from outside through the
/// pipeline it feeds) - PLAN-0005 Phase 2. See
/// <c>docs/adr/0025-compono-nsubstitute-package-design.md</c>.
/// </summary>
public sealed class NSubstituteProviderTests
{
    [Fact]
    public void TryProvide_ProducesARealNSubstituteSubstitute_ForAnInterface()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new NSubstituteProvider(new NSubstituteOptions())));

        var value = composer.Create<ISampleService>();

        AssertIsARealSubstitute(value);
    }

    [Fact]
    public void TryProvide_ProducesARealNSubstituteSubstitute_ForADelegate()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new NSubstituteProvider(new NSubstituteOptions())));

        var value = composer.Create<SampleDelegate>();

        AssertIsARealSubstitute(value);
    }

    [Fact]
    public void TryProvide_ProducesARealNSubstituteSubstitute_ForAnUnsealedAbstractClass_WhenTheOptionAllowsIt()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new NSubstituteProvider(new NSubstituteOptions { SubstituteAbstractClasses = true })));

        var value = composer.Create<SampleAbstractService>();

        AssertIsARealSubstitute(value);
    }

    [Fact]
    public void TryProvide_DoesNotHandle_AnUnsealedAbstractClass_WhenTheOptionDisallowsIt()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new NSubstituteProvider(new NSubstituteOptions { SubstituteAbstractClasses = false })));

        var act = () => composer.Create<SampleAbstractService>();

        act.Should().Throw<CompositionException>().WithMessage("*SampleAbstractService*");
    }

    [Fact]
    public void TryProvide_DoesNotHandle_ASealedClass()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new NSubstituteProvider(new NSubstituteOptions())));

        var act = () => composer.Create<SampleSealedService>();

        act.Should().Throw<CompositionException>();
    }

    // string isn't re-verified here: Compono's built-in PrimitiveValueProvider (stage 7) always
    // satisfies a bare string request regardless of what NSubstituteProvider does with it, so a
    // composer.Create<string>() call can't observe NSubstituteProvider's own NotHandled outcome the
    // way SampleSealedService's above can. IsSubstitutableTests.IsSubstitutable_ReturnsFalse_ForANonSubstitutableShape
    // already covers string directly against the static check this provider's TryProvide delegates to.

    [Fact]
    public void Constructor_SnapshotsOptions_SoMutatingTheOriginalInstanceAfterwardHasNoEffect()
    {
        // ADR-0025 Amendment 1's mutation regression: the provider must snapshot
        // NSubstituteOptions.SubstituteAbstractClasses at construction, not hold the NSubstituteOptions
        // reference itself - proving the snapshot, not just that it compiles.
        var options = new NSubstituteOptions { SubstituteAbstractClasses = true };
        var provider = new NSubstituteProvider(options);
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider));

        options.SubstituteAbstractClasses = false;

        var value = composer.Create<SampleAbstractService>();

        AssertIsARealSubstitute(value);
    }

    // NSubstitute's own "is this a substitute" check, not a hand-rolled heuristic (PLAN-0005 Phase 2's
    // own task wording). `is ICallRouterProvider` only holds for an interface/abstract-class proxy - a
    // delegate substitute is a plain delegate instance whose target isn't the proxy itself, so
    // GetCallRouterFor (the same lookup NSubstitute's own Received()/Returns() extensions use
    // internally) is the one check that works uniformly across every substitutable shape this provider
    // supports.
    private static void AssertIsARealSubstitute(object? value)
    {
        var act = () => SubstitutionContext.Current.GetCallRouterFor(value!);

        act.Should().NotThrow();
    }

    public interface ISampleService;

    public delegate void SampleDelegate();

    public abstract class SampleAbstractService;

    public sealed class SampleSealedService;
}
