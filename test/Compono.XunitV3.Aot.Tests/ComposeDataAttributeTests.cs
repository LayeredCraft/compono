using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.Tests;

// AOT counterpart to Compono.XunitV3.Tests.ComposeDataAttributeTests: asserts the same consumer-facing
// shapes the ADR specifies, giving the required JIT/AOT public API parity. Execution behavior
// (generator discovery, emission, runtime row production) is issue #163 phases B4/B5's scope and is not
// tested here. Unlike the JIT package, the AOT markers are compile-time-only and carry no
// DynamicallyAccessedMembers hints, since no runtime reflection reads them.
public sealed class ComposeDataAttributeTests
{
    [Fact]
    public void NonGeneric_IsSealedDataAttribute_WithMethodScopedNoMultipleUsage()
    {
        Assert.Equal("Xunit.v3.DataAttribute", typeof(ComposeDataAttribute).BaseType!.FullName);
        Assert.True(typeof(ComposeDataAttribute).IsSealed);

        var usage = typeof(ComposeDataAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Method, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void Constructor_SetsMemberName()
    {
        var attribute = new ComposeDataAttribute("Cases");

        Assert.Equal("Cases", attribute.MemberName);
    }

    [Fact]
    public void MemberType_DefaultsToNull_AndIsSettable()
    {
        var attribute = new ComposeDataAttribute("Cases");

        Assert.Null(attribute.MemberType);

        attribute.MemberType = typeof(ComposeDataAttributeTests);

        Assert.Equal(typeof(ComposeDataAttributeTests), attribute.MemberType);
    }

    [Fact]
    public void Seed_DefaultsToZero_AndIsSettable()
    {
        var attribute = new ComposeDataAttribute("Cases");

        Assert.Equal(0, attribute.Seed);

        attribute.Seed = 42;

        Assert.Equal(42, attribute.Seed);
    }

    [Fact]
    public void MemberType_CarriesNoDynamicallyAccessedMembersHint()
    {
        var hint = typeof(ComposeDataAttribute)
            .GetProperty(nameof(ComposeDataAttribute.MemberType))!
            .GetCustomAttribute<DynamicallyAccessedMembersAttribute>();

        Assert.Null(hint);
    }

    [Fact]
    public void AllForms_ExposeMemberNameMemberTypeAndSeed()
    {
        AssertExposesMemberNameMemberTypeAndSeed(typeof(ComposeDataAttribute));
        AssertExposesMemberNameMemberTypeAndSeed(typeof(ComposeDataAttribute<>));
        AssertExposesMemberNameMemberTypeAndSeed(typeof(ComposeDataAttribute<,>));
    }

    // Trimming-safe: annotating the parameter keeps the public properties of each attribute form reachable
    // under Native AOT, so this test asserts the same members in JIT and AOT builds without IL warnings.
    private static void AssertExposesMemberNameMemberTypeAndSeed(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type form)
    {
        Assert.NotNull(form.GetProperty(nameof(ComposeDataAttribute.MemberName)));
        Assert.NotNull(form.GetProperty(nameof(ComposeDataAttribute.MemberType)));
        Assert.NotNull(form.GetProperty(nameof(ComposeDataAttribute.Seed)));
    }

    [Fact]
    public void ProfileGenericForm_IsSealedIndependentMarker_WithNewConstraint()
    {
        Assert.Equal("Xunit.v3.DataAttribute", typeof(ComposeDataAttribute<>).BaseType!.FullName);
        Assert.True(typeof(ComposeDataAttribute<>).IsSealed);

        var usage = typeof(ComposeDataAttribute<>).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Method, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);

        var profile = typeof(ComposeDataAttribute<>).GetGenericArguments()[0];

        Assert.Contains(typeof(ICompositionProfile), profile.GetGenericParameterConstraints());
        Assert.True(
            (profile.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint)
            != GenericParameterAttributes.None);

        var attribute = new ComposeDataAttribute<EmptyProfile>("Cases");

        Assert.Equal("Cases", attribute.MemberName);
        Assert.Null(attribute.MemberType);
        Assert.Equal(0, attribute.Seed);
    }

    [Fact]
    public void ProfileConfigGenericForm_IsSealedIndependentMarker_WithNoNewConstraint()
    {
        Assert.Equal("Xunit.v3.DataAttribute", typeof(ComposeDataAttribute<,>).BaseType!.FullName);
        Assert.True(typeof(ComposeDataAttribute<,>).IsSealed);

        var usage = typeof(ComposeDataAttribute<,>).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Method, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);

        var parameters = typeof(ComposeDataAttribute<,>).GetGenericArguments();

        Assert.Contains(typeof(ICompositionProfile), parameters[0].GetGenericParameterConstraints());
        Assert.True(
            (parameters[0].GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint)
            == GenericParameterAttributes.None);
        Assert.Empty(parameters[1].GetGenericParameterConstraints());

        var attribute = new ComposeDataAttribute<ConfigProfile, SampleConfig>("Cases");

        Assert.Equal("Cases", attribute.MemberName);
        Assert.Null(attribute.MemberType);
        Assert.Equal(0, attribute.Seed);
    }

    private sealed class EmptyProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder)
        {
        }
    }

    private sealed record SampleConfig(string Value);

    private sealed class ConfigProfile : ICompositionProfile
    {
        public ConfigProfile(SampleConfig config) => Config = config;

        public SampleConfig Config { get; }

        public void Configure(CompositionBuilder builder) => builder.Register(() => Config.Value);
    }
}
