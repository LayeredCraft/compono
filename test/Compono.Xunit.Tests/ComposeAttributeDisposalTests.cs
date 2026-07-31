using Compono.Xunit.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.Xunit.Tests;

public sealed class ComposeAttributeDisposalTests
{
    [Fact]
    public async Task GetData_RegistersAComposedDisposableValue_WithTheDisposalTracker()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.DisposableProfile>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithDisposableParameter))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var disposable = (DisposableValue)rows.Single().GetData()[0]!;

        tracker.TrackedObjects.Should().Contain(disposable);
    }

    [Fact]
    public async Task DisposingTheTracker_DisposesAComposedDisposableValue()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.DisposableProfile>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithDisposableParameter))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var disposable = (DisposableValue)rows.Single().GetData()[0]!;
        await tracker.DisposeAsync();

        disposable.Disposed.Should().BeTrue();
    }
}
