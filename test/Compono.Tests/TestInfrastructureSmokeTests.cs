namespace Compono.Tests;

public sealed class TestInfrastructureSmokeTests
{
    [Theory]
    [InlineData("alpha")]
    [InlineData("beta")]
    public void AwesomeAssertions_ShouldAssertOnExplicitValues(string value)
    {
        value.Should().NotBeNullOrEmpty();
    }

    public interface IProbe
    {
        string Name { get; }
    }

    [Fact]
    public void NSubstitute_ShouldAutoMockInterfaces()
    {
        var probe = Substitute.For<IProbe>();
        probe.Name.Returns("configured");

        probe.Name.Should().Be("configured");
    }
}
