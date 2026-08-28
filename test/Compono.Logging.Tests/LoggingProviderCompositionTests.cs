using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class LoggingProviderCompositionTests
{
    [Fact]
    public void UseLogging_ComposesBareILogger_DirectConstructPath()
    {
        var composer = Composer.Create(builder => builder.UseLogging());

        var service = composer.Create<PlainLoggerService>();
        service.LogSomething();

        service.Logger.GetCapturedEntries().Should().HaveCount(1);
    }

    [Fact]
    public void UseLogging_ComposesGenericILoggerOfT_ViaGeneratorDiscoveredActivation()
    {
        // The primary, realistic end-to-end path: OrderService's own ILogger<OrderService>
        // constructor dependency is discovered by the shared Compono.Generators walk (task 2) and
        // activated via LoggingFactoryRegistry - never a hand-called LoggingProvider.TryProvide.
        var composer = Composer.Create(builder => builder.UseLogging());

        var service = composer.Create<OrderService>();
        service.PlaceOrder(42);

        var entry = service.Logger.GetLastCapturedEntry()!.Value;
        entry.LogLevel.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("42");
    }

    [Fact]
    public void UseLogging_DoesNotBreakComposition_ForTypesWithNoLoggerDependency()
    {
        var composer = Composer.Create(builder => builder.UseLogging());

        var value = composer.Create<PlainRecord>();

        value.Should().NotBeNull();
    }

    [Fact]
    public void EachComposedRequest_GetsItsOwnIndependentLogger_AbsentShared()
    {
        var composer = Composer.Create(builder => builder.UseLogging());

        var first = composer.Create<OrderService>();
        var second = composer.Create<OrderService>();

        first.PlaceOrder(1);

        first.Logger.GetCapturedEntries().Should().HaveCount(1);
        second.Logger.GetCapturedEntries().Should().BeEmpty();
    }

    [Fact]
    public void UseLoggingBeforeUseNSubstitute_ILoggerOfT_ResolvesToCapturingLogger()
    {
        var composer = Composer.Create(builder => builder.UseLogging().UseNSubstitute());

        var service = composer.Create<OrderService>();

        (service.Logger is CapturingLogger<OrderService>).Should().BeTrue();
    }

    [Fact]
    public void UseNSubstituteBeforeUseLogging_ILoggerOfT_ResolvesToSubstitute_NotCapturingLogger()
    {
        // First-registered-wins (ADR-0024/ADR-0043) - reversing registration order is an explicit,
        // documented consequence, not a diagnostic. Confirms Compono.Logging changed nothing about
        // this existing pipeline behavior.
        var composer = Composer.Create(builder => builder.UseNSubstitute().UseLogging());

        var service = composer.Create<OrderService>();

        (service.Logger is CapturingLogger<OrderService>).Should().BeFalse();
    }

    [Fact]
    public void WrongProviderProducedLogger_ThrowsNonCompanoLoggerDiagnostic_NotInvalidCastOrEmpty()
    {
        var composer = Composer.Create(builder => builder.UseNSubstitute().UseLogging());
        var service = composer.Create<OrderService>();

        var act = () => service.Logger.GetCapturedEntries();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a Compono.Logging capturing logger*");
    }

    [Fact]
    public void MissingGeneratedActivation_ThrowsDistinctDiagnostic_NeverFallsThrough()
    {
        // NeverComposedCategory is named here only via typeof() - never as an actual constructor
        // parameter, Composer.Create<T>() root, or [Compose] parameter anywhere in this compilation
        // - so the shared generator (task 2) never discovers/registers it. Calling
        // LoggingProvider.TryProvide directly (InternalsVisibleTo) is the only way to exercise this
        // path deterministically: any real composition root reaching ILogger<NeverComposedCategory>
        // would itself become a discoverable root and defeat the point of this test.
        var provider = new LoggingProvider(new LoggingOptions());
        var request = new CompositionProviderRequest(
            typeof(ILogger<NeverComposedCategory>), declaringType: null, name: null, Nullability.NotNullable);

        var act = () => provider.TryProvide(request, new NeverInvokedCompositionContext());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*recognized*ILogger*no generated activation*");
    }
}

public sealed class NeverComposedCategory;

/// <summary>LoggingProvider.TryProvide never actually calls back into the context - this exists
/// purely to satisfy the interface parameter.</summary>
public sealed class NeverInvokedCompositionContext : ICompositionContext
{
    public TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor) => throw new NotSupportedException();
    public TValue Resolve<TValue>() => throw new NotSupportedException();
    public int DeriveSeed() => throw new NotSupportedException();
    public int ResolveCollectionSize() => throw new NotSupportedException();
}

public sealed class PlainRecord
{
    public int Value { get; set; } = 1;
}

public sealed class PlainLoggerService
{
    public ILogger Logger { get; }

    public PlainLoggerService(ILogger logger) => Logger = logger;

    public void LogSomething() => Logger.LogInformation("plain logger works");
}

public sealed class OrderService
{
    public ILogger<OrderService> Logger { get; }

    public OrderService(ILogger<OrderService> logger) => Logger = logger;

    public void PlaceOrder(int orderId) => Logger.LogWarning("retrying order {OrderId}", orderId);
}
