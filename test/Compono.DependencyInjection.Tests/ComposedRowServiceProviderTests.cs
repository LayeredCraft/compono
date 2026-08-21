namespace Compono.DependencyInjection.Tests;

/// <summary>
/// <c>row.AsServiceProvider()</c> - proves it's a correct <see cref="IServiceProvider"/> per
/// ADR-0047's own contract. Not a proof of any specific consumer's fallback-provider ordering (e.g.
/// bUnit's <c>AddFallbackServiceProvider</c>) - that's owned by that consumer's own test suite, per
/// ADR-0047/PLAN-0047; the two composing correctly end to end is the separately deferred
/// <c>trivia-manager</c> dogfood.
/// </summary>
public sealed class ComposedRowServiceProviderTests
{
    [Fact]
    public void GetService_ResolvesAValueComposedViaComponoTestDoubles()
    {
        var expected = new FakeApiClient();
        GeneratedTestDoubleRegistry.RegisterFactory<IApiClient>(() => expected);
        var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var provider = row.AsServiceProvider();

        var value = provider.GetService(typeof(IApiClient));

        value.Should().BeSameAs(expected);
    }

    [Fact]
    public void GetService_ReturnsNull_ForAnUnsatisfiableType()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var provider = row.AsServiceProvider();

        var value = provider.GetService(typeof(IUnregisteredService));

        value.Should().BeNull();
    }

    [Fact]
    public void GetService_ReturnsTheSameInstance_OnRepeatedCallsForTheSameType()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new FreshInstanceProvider()));
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var provider = row.AsServiceProvider();

        var first = provider.GetService(typeof(FreshMarker));
        var second = provider.GetService(typeof(FreshMarker));

        first.Should().NotBeNull();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetService_ReturnsTheSameInstance_UnderConcurrentFirstCalls()
    {
        // Regression for a Codex PR review finding: two initial GetService calls for the same type,
        // both missing the adapter's cache at the same time, used to be able to race into the same
        // mutable CompositionRow/CompositionContext simultaneously - corrupting its shared path/random/
        // trace bookkeeping and potentially handing same-type callers different instances despite the
        // documented stable-identity guarantee. A short delay inside the provider widens the race
        // window so concurrent callers are actually likely to overlap inside GetService, not just
        // happen to interleave outside its lock.
        var provider2 = new DelayedInstanceProvider();
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider2));
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var provider = row.AsServiceProvider();

        var results = new object?[16];
        Parallel.For(0, results.Length, i => results[i] = provider.GetService(typeof(DelayedMarker)));

        results.Should().OnlyContain(r => r != null);
        results.Should().OnlyContain(r => ReferenceEquals(r, results[0]));
    }

    [Fact]
    public void AsServiceProvider_ReturnsTheSameInstance_ForTheSameRow()
    {
        // Regression for a Codex PR review finding: AsServiceProvider() used to construct a fresh
        // adapter (and fresh lock) on every call - wrapping the same row twice produced two adapters
        // with independent locks, each only serializing its own calls, not against each other.
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));

        var first = row.AsServiceProvider();
        var second = row.AsServiceProvider();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetService_IsSafeAcrossTwoSeparatelyObtainedProviders_ForTheSameRow()
    {
        // Same fix as above, proven under real concurrency: two separately-obtained
        // AsServiceProvider() results for the same row (which are now the identical instance) used
        // concurrently must not corrupt the row's shared CompositionContext or hand out different
        // instances for the same type - exactly the failure mode a per-call adapter/lock allowed.
        var provider2 = new DelayedInstanceProvider();
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider2));
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var a = row.AsServiceProvider();
        var b = row.AsServiceProvider();

        var results = new object?[16];
        Parallel.For(0, results.Length, i => results[i] = (i % 2 == 0 ? a : b).GetService(typeof(DelayedMarker)));

        results.Should().OnlyContain(r => r != null);
        results.Should().OnlyContain(r => ReferenceEquals(r, results[0]));
    }

    [Fact]
    public void GetService_DoesNotCacheAMiss_AndRetriesOnALaterCall()
    {
        var provider2 = new SwitchableProvider();
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider2));
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var provider = row.AsServiceProvider();

        var first = provider.GetService(typeof(SwitchableMarker));
        provider2.StartHandling();
        var second = provider.GetService(typeof(SwitchableMarker));

        first.Should().BeNull();
        second.Should().NotBeNull();
        provider2.CallCount.Should().Be(2);
    }

    [Fact]
    public void GetService_CachesALegitimatelyNullResolution_AndDoesNotReinvokeTheProvider()
    {
        var provider2 = new CountingNullProvider();
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider2));
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var provider = row.AsServiceProvider();

        var first = provider.GetService(typeof(NullableMarker));
        var second = provider.GetService(typeof(NullableMarker));

        first.Should().BeNull();
        second.Should().BeNull();
        provider2.CallCount.Should().Be(1);
    }

    [Fact]
    public void GetService_DoesNotResolveAType_OnlySatisfiableViaTheConfiguredServiceProvider()
    {
        var composer = Composer.Create(builder => builder.UseServiceProvider(new FakeServiceProvider(new ServiceProviderMarker())));
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
        var provider = row.AsServiceProvider();

        var value = provider.GetService(typeof(ServiceProviderMarker));

        value.Should().BeNull();
    }

    [Fact]
    public void GetService_DoesNotResolveAType_OnlySatisfiableViaOrdinaryGeneratedPlanComposition()
    {
        PlanCache<ComposedMarker>.Instance = new FixedPlan(new ComposedMarker());

        try
        {
            var composer = Composer.Create();
            var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));
            var provider = row.AsServiceProvider();

            var value = provider.GetService(typeof(ComposedMarker));

            value.Should().BeNull();
        }
        finally
        {
            PlanCache<ComposedMarker>.Instance = null;
        }
    }

    [Fact]
    public void AsServiceProvider_DoesNotImplementDisposal()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(ComposedRowServiceProviderTests));

        var provider = row.AsServiceProvider();

        provider.Should().NotBeAssignableTo<IDisposable>();
        provider.Should().NotBeAssignableTo<IAsyncDisposable>();
    }

    public interface IApiClient;

    public interface IUnregisteredService;

    private sealed class FakeApiClient : IApiClient;

    private sealed record FreshMarker;

    private sealed class FreshInstanceProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            request.RequestedType == typeof(FreshMarker) ? CompositionProviderResult.Handled(new FreshMarker()) : CompositionProviderResult.NotHandled;
    }

    private sealed record DelayedMarker;

    private sealed class DelayedInstanceProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
        {
            if (request.RequestedType != typeof(DelayedMarker))
                return CompositionProviderResult.NotHandled;

            Thread.Sleep(20);
            return CompositionProviderResult.Handled(new DelayedMarker());
        }
    }

    private sealed record SwitchableMarker;

    private sealed class SwitchableProvider : ICompositionValueProvider
    {
        private bool _handles;

        internal int CallCount { get; private set; }

        internal void StartHandling() => _handles = true;

        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
        {
            if (request.RequestedType != typeof(SwitchableMarker))
                return CompositionProviderResult.NotHandled;

            CallCount++;
            return _handles ? CompositionProviderResult.Handled(new SwitchableMarker()) : CompositionProviderResult.NotHandled;
        }
    }

    private sealed record NullableMarker;

    private sealed class CountingNullProvider : ICompositionValueProvider
    {
        internal int CallCount { get; private set; }

        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
        {
            if (request.RequestedType != typeof(NullableMarker))
                return CompositionProviderResult.NotHandled;

            CallCount++;
            return CompositionProviderResult.Handled(null);
        }
    }

    private sealed record ServiceProviderMarker;

    private sealed class FakeServiceProvider(ServiceProviderMarker value) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ServiceProviderMarker) ? value : null;
    }

    private sealed record ComposedMarker;

    private sealed class FixedPlan(ComposedMarker value) : ICompositionPlan<ComposedMarker>
    {
        public ComposedMarker Compose(ICompositionContext context) => value;
    }
}
