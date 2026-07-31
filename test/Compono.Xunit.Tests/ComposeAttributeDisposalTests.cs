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

    [Fact]
    public async Task GetData_RegistersASharedDisposableValueOnce_EvenWhenALaterParameterReusesIt()
    {
        // [Shared] DisposableValue and the ordinary DisposableValue parameter after it resolve to
        // the exact same instance (CompositionContext.ResolveCore's stage-2 scope read) - both must
        // not independently register it with disposalTracker, or DisposeAsync disposes it twice.
        var attribute = new ComposeAttribute<SampleTestMethods.DisposableProfile>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithSharedDisposableFollowedByOrdinaryOfTheSameType))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();
        var shared = (DisposableValue)data[0]!;
        var ordinary = (DisposableValue)data[1]!;

        ordinary.Should().BeSameAs(shared);
        tracker.TrackedObjects.Should().ContainSingle(tracked => ReferenceEquals(tracked, shared));

        await tracker.DisposeAsync();

        shared.DisposeCount.Should().Be(1);
    }
}
