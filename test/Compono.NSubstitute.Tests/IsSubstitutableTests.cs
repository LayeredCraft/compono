namespace Compono.NSubstitute.Tests;

/// <summary>
/// <see cref="NSubstituteProvider.IsSubstitutable"/> unit coverage - PLAN-0005 Phase 2. See
/// <c>docs/adr/0025-compono-nsubstitute-package-design.md</c> (Amendment 1 for the
/// <see cref="Delegate"/>/<see cref="MulticastDelegate"/> distinction this file's negative cases
/// exist to lock).
/// </summary>
public sealed class IsSubstitutableTests
{
    [Theory]
    [InlineData(typeof(ISampleInterface))]
    [InlineData(typeof(SampleDelegate))]
    [InlineData(typeof(SampleAbstractClass))]
    public void IsSubstitutable_ReturnsTrue_ForASubstitutableShape_WithAbstractClassesAllowed(Type requestedType)
    {
        var result = NSubstituteProvider.IsSubstitutable(requestedType, substituteAbstractClasses: true);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(ISampleInterface))]
    [InlineData(typeof(SampleDelegate))]
    public void IsSubstitutable_ReturnsTrue_ForAnInterfaceOrDelegate_RegardlessOfTheAbstractClassOption(Type requestedType)
    {
        var result = NSubstituteProvider.IsSubstitutable(requestedType, substituteAbstractClasses: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSubstitutable_ReturnsFalse_ForAnUnsealedAbstractClass_WhenTheOptionDisallowsIt()
    {
        var result = NSubstituteProvider.IsSubstitutable(typeof(SampleAbstractClass), substituteAbstractClasses: false);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(SampleSealedClass))]
    [InlineData(typeof(SampleStruct))]
    [InlineData(typeof(string))]
    public void IsSubstitutable_ReturnsFalse_ForANonSubstitutableShape(Type requestedType)
    {
        var resultWithAbstractClassesAllowed = NSubstituteProvider.IsSubstitutable(requestedType, substituteAbstractClasses: true);
        var resultWithAbstractClassesDisallowed = NSubstituteProvider.IsSubstitutable(requestedType, substituteAbstractClasses: false);

        resultWithAbstractClassesAllowed.Should().BeFalse();
        resultWithAbstractClassesDisallowed.Should().BeFalse();
    }

    // ADR-0025 Amendment 1 regression: IsSubclassOf(typeof(MulticastDelegate)) must not treat the
    // framework base types themselves as a substitutable "delegate type" - only a real, concrete
    // delegate type (SampleDelegate above) qualifies.
    [Theory]
    [InlineData(typeof(Delegate))]
    [InlineData(typeof(MulticastDelegate))]
    public void IsSubstitutable_ReturnsFalse_ForTheDelegateFrameworkBaseTypesThemselves(Type requestedType)
    {
        var result = NSubstituteProvider.IsSubstitutable(requestedType, substituteAbstractClasses: true);

        result.Should().BeFalse();
    }

    private interface ISampleInterface;

    private delegate void SampleDelegate();

    private abstract class SampleAbstractClass;

    private sealed class SampleSealedClass;

    private struct SampleStruct;
}
