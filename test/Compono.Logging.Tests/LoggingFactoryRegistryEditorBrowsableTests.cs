namespace Compono.Logging.Tests;

public sealed class LoggingFactoryRegistryEditorBrowsableTests
{
    [Fact]
    public void Register_IsHidden_WhileTryCreateRemainsVisible()
    {
        var register = typeof(LoggingFactoryRegistry).GetMethod(nameof(LoggingFactoryRegistry.Register))!;
        var tryCreate = typeof(LoggingFactoryRegistry).GetMethod(nameof(LoggingFactoryRegistry.TryCreate))!;
        var attributes = register.GetCustomAttributes(
            typeof(System.ComponentModel.EditorBrowsableAttribute),
            inherit: false);

        attributes.Should().ContainSingle();
        ((System.ComponentModel.EditorBrowsableAttribute)attributes[0]).State
            .Should().Be(System.ComponentModel.EditorBrowsableState.Never);
        tryCreate.GetCustomAttributes(typeof(System.ComponentModel.EditorBrowsableAttribute), inherit: false)
            .Should().BeEmpty();
    }
}
