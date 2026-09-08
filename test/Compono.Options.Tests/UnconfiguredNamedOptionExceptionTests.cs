using Microsoft.Extensions.Options;

namespace Compono.Options.Tests;

/// <summary>
/// <see cref="UnconfiguredNamedOptionException"/>'s message content - asserted per throwing interface
/// surface (<see cref="IOptionsMonitor{TOptions}"/>, <see cref="IOptionsSnapshot{TOptions}"/>) per
/// docs/plans/0064-... Task 5 "Diagnostics". <see cref="IOptions{TOptions}"/> is excluded - see the
/// note in <see cref="CompositionBuilderExtensionsTests"/> for why it can never observably throw this.
/// </summary>
public sealed class UnconfiguredNamedOptionExceptionTests
{
    private sealed record Settings(string Value);

    [Fact]
    public void Monitor_Message_NamesTheSettingsTypeAndTheRequestedName()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        IOptionsMonitor<Settings> monitor = source;

        var act = () => monitor.Get("missing-name");

        act.Should().Throw<UnconfiguredNamedOptionException>()
            .WithMessage("*Settings*missing-name*");
    }

    [Fact]
    public void Snapshot_Message_NamesTheSettingsTypeAndTheRequestedName()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var snapshot = composer.Create<IOptionsSnapshot<Settings>>();

        var act = () => snapshot.Get("missing-name");

        act.Should().Throw<UnconfiguredNamedOptionException>()
            .WithMessage("*Settings*missing-name*");
    }

    [Fact]
    public void ExceptionType_IsADedicatedType_NotAGenericFrameworkException()
    {
        // Matches Compono.Http's UnmatchedHttpRequestException precedent - a dedicated, package-owned
        // exception type, not KeyNotFoundException/InvalidOperationException.
        typeof(UnconfiguredNamedOptionException).Should().BeDerivedFrom<Exception>();
        typeof(UnconfiguredNamedOptionException).Should().NotBe(typeof(KeyNotFoundException));
    }
}
