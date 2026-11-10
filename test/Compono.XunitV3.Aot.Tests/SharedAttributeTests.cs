using System.Reflection;
using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.Tests;

// Locks the AOT [Shared] marker's public contract (ADR-0070 Amendment 3): a pure, parameterless,
// parameter-scoped marker with no additional public members, matching Compono.XunitV3.SharedAttribute.
// Generator discovery/validation of the marker is issue #163 phase B4's scope; the generated sharing
// behavior is B5's, so neither is exercised here.
public sealed class SharedAttributeTests
{
    [Fact]
    public void IsSealedAttribute_WithParameterScopedNoMultipleUsage()
    {
        Assert.Equal("System.Attribute", typeof(SharedAttribute).BaseType!.FullName);
        Assert.True(typeof(SharedAttribute).IsSealed);

        var usage = typeof(SharedAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Parameter, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void HasParameterlessConstructor_AndNoAdditionalPublicMembers()
    {
        var type = typeof(SharedAttribute);

        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));

        Assert.Empty(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void IsInAotNamespace()
    {
        Assert.Equal("Compono.XunitV3.Aot.SharedAttribute", typeof(SharedAttribute).FullName);
    }
}
