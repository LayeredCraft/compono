using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

// B2 shape/contract tests for the ComposeData attribute family. Execution behavior - the actual row
// production, metadata merge rule, and seed policy - is issue #163 phase B3's scope and is not tested
// here. The only GetData assertion is that the not-yet-implemented stub throws, so a released package
// can never ship it. The AOT counterpart lives in
// test/Compono.XunitV3.Aot.Tests/ComposeDataAttributeTests.cs and asserts the same consumer-facing
// shapes, giving the JIT/AOT parity the ADR requires.
public sealed class ComposeDataAttributeTests
{
    [Fact]
    public void NonGeneric_IsUnsealedDataAttribute_WithMethodScopedNoMultipleUsage()
    {
        typeof(ComposeDataAttribute).BaseType!.FullName.Should().Be("Xunit.v3.DataAttribute");
        typeof(ComposeDataAttribute).IsSealed.Should().BeFalse();

        var usage = typeof(ComposeDataAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Method);
        usage.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void Constructor_SetsMemberName()
    {
        var attribute = new ComposeDataAttribute("Cases");

        attribute.MemberName.Should().Be("Cases");
    }

    [Fact]
    public void Constructor_RejectsNullMemberName()
    {
        Action act = () => _ = new ComposeDataAttribute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MemberType_DefaultsToNull_AndIsSettable()
    {
        var attribute = new ComposeDataAttribute("Cases");

        attribute.MemberType.Should().BeNull();

        attribute.MemberType = typeof(ComposeDataAttributeTests);

        attribute.MemberType.Should().Be(typeof(ComposeDataAttributeTests));
    }

    [Fact]
    public void Seed_DefaultsToZero_ButSeedAsNullableIsNull()
    {
        var attribute = new ComposeDataAttribute("Cases");

        attribute.Seed.Should().Be(0);
        attribute.SeedAsNullable.Should().BeNull();
    }

    [Fact]
    public void Seed_ConfiguredToZero_IsDistinguishableFromUnset()
    {
        var attribute = new ComposeDataAttribute("Cases")
        {
            Seed = 0,
        };

        attribute.Seed.Should().Be(0);
        attribute.SeedAsNullable.Should().Be(0);
    }

    [Fact]
    public void Seed_IsSettable()
    {
        var attribute = new ComposeDataAttribute("Cases")
        {
            Seed = 42,
        };

        attribute.Seed.Should().Be(42);
        attribute.SeedAsNullable.Should().Be(42);
    }

    [Fact]
    public void Seed_ReflectedFromAttributeUsage_DistinguishesUnsetFromConfiguredZero()
    {
        var unset = typeof(SeedSampleMethods)
            .GetMethod(nameof(SeedSampleMethods.UnsetSeed), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetCustomAttribute<ComposeDataAttribute>()!;
        var configuredZero = typeof(SeedSampleMethods)
            .GetMethod(nameof(SeedSampleMethods.ConfiguredZeroSeed), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetCustomAttribute<ComposeDataAttribute>()!;

        unset.Seed.Should().Be(0);
        unset.SeedAsNullable.Should().BeNull();

        configuredZero.Seed.Should().Be(0);
        configuredZero.SeedAsNullable.Should().Be(0);
    }

    [Fact]
    public void MemberType_CarriesDynamicallyAccessedMembersHint()
    {
        var hint = typeof(ComposeDataAttribute)
            .GetProperty(nameof(ComposeDataAttribute.MemberType))!
            .GetCustomAttribute<DynamicallyAccessedMembersAttribute>();

        hint.Should().NotBeNull();
        hint!.MemberTypes.Should().Be(
            DynamicallyAccessedMemberTypes.PublicFields
            | DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicMethods);
    }

    [Fact]
    public void AllForms_ExposeMemberNameMemberTypeAndSeed()
    {
        Type[] forms = [typeof(ComposeDataAttribute), typeof(ComposeDataAttribute<>), typeof(ComposeDataAttribute<,>)];

        foreach (var form in forms)
        {
            form.GetProperty(nameof(ComposeDataAttribute.MemberName)).Should().NotBeNull();
            form.GetProperty(nameof(ComposeDataAttribute.MemberType)).Should().NotBeNull();
            form.GetProperty(nameof(ComposeDataAttribute.Seed)).Should().NotBeNull();
        }
    }

    [Fact]
    public void ProfileGenericForm_IsSealed_DerivesFromNonGeneric_AndRequiresNew()
    {
        typeof(ComposeDataAttribute<>).BaseType.Should().Be(typeof(ComposeDataAttribute));
        typeof(ComposeDataAttribute<>).IsSealed.Should().BeTrue();

        var usage = typeof(ComposeDataAttribute<>).GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Method);
        usage.AllowMultiple.Should().BeFalse();

        var profile = typeof(ComposeDataAttribute<>).GetGenericArguments()[0];

        profile.GetGenericParameterConstraints().Should().Contain(typeof(ICompositionProfile));
        (profile.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint)
            .Should().NotBe(GenericParameterAttributes.None);

        var hint = profile.GetCustomAttribute<DynamicallyAccessedMembersAttribute>();

        hint.Should().NotBeNull();
        hint!.MemberTypes.Should().Be(DynamicallyAccessedMemberTypes.PublicConstructors);

        var attribute = new ComposeDataAttribute<EmptyProfile>("Cases");

        attribute.MemberName.Should().Be("Cases");
        attribute.MemberType.Should().BeNull();
        attribute.Seed.Should().Be(0);
    }

    [Fact]
    public void ProfileConfigGenericForm_IsSealed_DerivesFromNonGeneric_AndHasNoNewConstraint()
    {
        typeof(ComposeDataAttribute<,>).BaseType.Should().Be(typeof(ComposeDataAttribute));
        typeof(ComposeDataAttribute<,>).IsSealed.Should().BeTrue();

        var usage = typeof(ComposeDataAttribute<,>).GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Method);
        usage.AllowMultiple.Should().BeFalse();

        var parameters = typeof(ComposeDataAttribute<,>).GetGenericArguments();

        parameters[0].GetGenericParameterConstraints().Should().Contain(typeof(ICompositionProfile));
        (parameters[0].GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint)
            .Should().Be(GenericParameterAttributes.None);

        parameters[1].GetGenericParameterConstraints().Should().BeEmpty();

        var profileHint = parameters[0].GetCustomAttribute<DynamicallyAccessedMembersAttribute>();
        var configHint = parameters[1].GetCustomAttribute<DynamicallyAccessedMembersAttribute>();

        profileHint.Should().NotBeNull();
        profileHint!.MemberTypes.Should().Be(DynamicallyAccessedMemberTypes.PublicConstructors);
        configHint.Should().NotBeNull();
        configHint!.MemberTypes.Should().Be(DynamicallyAccessedMemberTypes.PublicConstructors);

        var attribute = new ComposeDataAttribute<ConfigProfile, SampleConfig>("Cases");

        attribute.MemberName.Should().Be("Cases");
        attribute.MemberType.Should().BeNull();
        attribute.Seed.Should().Be(0);
    }

    [Fact]
    public void SupportsDiscoveryEnumeration_IsFalse()
    {
        var attribute = new ComposeDataAttribute("Cases");

        attribute.SupportsDiscoveryEnumeration().Should().BeFalse();
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

// Reflection-only fixtures, following Fixtures.SampleTestMethods' established pattern: the xUnit1008
// (data attribute without [Theory]) analyzer warning is expected and suppressed here - these methods
// are never run as real theories, only reflected over via typeof(...).GetMethod(...) to prove an
// explicit Seed = 0 is distinguishable from an unset Seed.
internal static class SeedSampleMethods
{
#pragma warning disable xUnit1008
    [ComposeData("Cases")]
    internal static void UnsetSeed()
    {
    }

    [ComposeData("Cases", Seed = 0)]
    internal static void ConfiguredZeroSeed()
    {
    }
#pragma warning restore xUnit1008
}
