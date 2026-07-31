using NSubstitute.Core;

namespace Compono.NSubstitute.Tests;

/// <summary>
/// <see cref="CompositionBuilderExtensions"/>'s <c>UseNSubstitute()</c>/<c>UseNSubstitute(Action{NSubstituteOptions})</c>
/// wiring a working <see cref="NSubstituteProvider"/> into a real <see cref="Composer"/> - PLAN-0005
/// Phase 2.
/// </summary>
public sealed class CompositionBuilderExtensionsTests
{
    [Fact]
    public void UseNSubstitute_WithNoConfiguration_ComposesAnInterfaceAsARealSubstitute()
    {
        var composer = Composer.Create(builder => builder.UseNSubstitute());

        var value = composer.Create<ISampleService>();

        value.Should().BeAssignableTo<ICallRouterProvider>();
    }

    [Fact]
    public void UseNSubstitute_WithNoConfiguration_ComposesAnUnsealedAbstractClassAsARealSubstitute()
    {
        // Default NSubstituteOptions.SubstituteAbstractClasses is true.
        var composer = Composer.Create(builder => builder.UseNSubstitute());

        var value = composer.Create<SampleAbstractService>();

        value.Should().BeAssignableTo<ICallRouterProvider>();
    }

    [Fact]
    public void UseNSubstitute_Configured_AppliesTheSuppliedOptions()
    {
        var composer = Composer.Create(builder => builder.UseNSubstitute(options => options.SubstituteAbstractClasses = false));

        var act = () => composer.Create<SampleAbstractService>();

        act.Should().Throw<CompositionException>().WithMessage("*SampleAbstractService*");
    }

    [Fact]
    public void UseNSubstitute_Configure_ThrowsForANullDelegate()
    {
        var act = () => Composer.Create(builder => builder.UseNSubstitute(null!));

        act.Should().Throw<ArgumentNullException>();
    }

    public interface ISampleService;

    public abstract class SampleAbstractService;
}
