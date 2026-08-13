namespace Compono.Tests;

/// <summary>
/// Exercises <see cref="ReturnConfigBuilder{T}"/>'s public write surface over a
/// <see cref="ReturnConfig{T}"/> slot, including the last-configuration-wins semantics ADR-0043
/// Amendment 7 decided for repeated <see cref="ReturnConfigBuilder{T}.Returns"/>/
/// <see cref="ReturnConfigBuilder{T}.Throws"/> calls on the same member.
/// </summary>
public sealed class ReturnConfigBuilderTests
{
    [Fact]
    public void Returns_SetsConfiguredValue()
    {
        var slot = new ReturnConfig<string>();
        var builder = new ReturnConfigBuilder<string>(ref slot);

        builder.Returns("hello");

        slot.HasConfiguredValue.Should().BeTrue();
        slot.ConfiguredValue.Should().Be("hello");
        slot.HasConfiguredException.Should().BeFalse();
    }

    [Fact]
    public void Throws_SetsConfiguredException()
    {
        var slot = new ReturnConfig<string>();
        var builder = new ReturnConfigBuilder<string>(ref slot);
        var exception = new InvalidOperationException("boom");

        builder.Throws(exception);

        slot.HasConfiguredException.Should().BeTrue();
        slot.ConfiguredException.Should().BeSameAs(exception);
        slot.HasConfiguredValue.Should().BeFalse();
    }

    [Fact]
    public void Returns_AfterThrows_ClearsConfiguredException()
    {
        var slot = new ReturnConfig<string>();
        var builder = new ReturnConfigBuilder<string>(ref slot);
        builder.Throws(new InvalidOperationException("boom"));

        builder.Returns("hello");

        slot.HasConfiguredValue.Should().BeTrue();
        slot.ConfiguredValue.Should().Be("hello");
        slot.HasConfiguredException.Should().BeFalse();
    }

    [Fact]
    public void Throws_AfterReturns_ClearsConfiguredValue()
    {
        var slot = new ReturnConfig<string>();
        var builder = new ReturnConfigBuilder<string>(ref slot);
        builder.Returns("hello");

        builder.Throws(new InvalidOperationException("boom"));

        slot.HasConfiguredException.Should().BeTrue();
        slot.HasConfiguredValue.Should().BeFalse();
    }
}
