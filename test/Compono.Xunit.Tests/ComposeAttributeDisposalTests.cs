using Compono.Xunit.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.Xunit.Tests;

// GetData deliberately never registers a composed value with disposalTracker (PR #24 review) -
// CompositionRow.Resolve/ResolveShared give no visibility into which pipeline stage produced a
// value, so a freshly-constructed (Compono-owned, safe to dispose) value is indistinguishable from
// one returned by a registration or a configured IServiceProvider (externally owned, per
// docs/adr/0019-registrations-and-service-provider-injection.md's ownership contract). These tests
// guard against silently reintroducing automatic disposal tracking without first solving that.
public sealed class ComposeAttributeDisposalTests
{
    [Fact]
    public async Task GetData_NeverRegistersAComposedDisposableValue_WithTheDisposalTracker()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.DisposableProfile>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithDisposableParameter))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var disposable = (DisposableValue)rows.Single().GetData()[0]!;

        tracker.TrackedObjects.Should().NotContain(disposable);

        await tracker.DisposeAsync();

        disposable.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task GetData_NeverRegistersASharedDisposableValue_EvenWhenALaterParameterReusesIt()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.DisposableProfile>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithSharedDisposableFollowedByOrdinaryOfTheSameType))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();
        var shared = (DisposableValue)data[0]!;
        var ordinary = (DisposableValue)data[1]!;

        ordinary.Should().BeSameAs(shared);
        tracker.TrackedObjects.Should().BeEmpty();
    }
}
