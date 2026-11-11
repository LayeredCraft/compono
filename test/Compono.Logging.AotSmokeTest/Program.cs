using Compono;
using Compono.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compono.Logging.AotSmokeTest;

internal sealed class OrderService(ILogger<OrderService> logger)
{
    public ILogger<OrderService> Logger { get; } = logger;

    public void PlaceOrder(int orderId) => Logger.LogWarning("retrying order {OrderId}", orderId);
}

internal static class Program
{
    private static int Main()
    {
        try
        {
            // The generated composition path (ADR-0055 Amendments 1/3), not direct construction
            // alone - OrderService's own ILogger<OrderService> constructor dependency is discovered
            // by the shared Compono.Generators walk and activated via LoggingFactoryRegistry.
            var composer = Composer.Create(builder => builder.UseLogging());
            var service = composer.Create<OrderService>();

            service.PlaceOrder(42);

            var entries = service.Logger.GetCapturedEntries();
            if (entries.Count != 1)
                throw new InvalidOperationException($"Expected exactly 1 captured entry, got {entries.Count}.");

            var entry = entries[0];
            if (entry.LogLevel != LogLevel.Warning)
                throw new InvalidOperationException($"Expected Warning, got {entry.LogLevel}.");

            if (!entry.Message.Contains("42"))
                throw new InvalidOperationException($"Expected the message to contain 42, got '{entry.Message}'.");

            if (entry.Properties is null || entry.Properties[0].Value is not int orderId || orderId != 42)
                throw new InvalidOperationException("Expected a structured OrderId property with value 42.");

            service.Logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retrying").Once();

            // Direct construction, no generated activation bridge needed - T is statically known at
            // the call site.
            var direct = new CapturingLogger<OrderService>();
            direct.LogInformation("direct construction still works");
            if (direct.GetCapturedEntries().Count != 1)
                throw new InvalidOperationException("Expected direct construction to capture independently.");

            VerifyHostWideCapture();
            VerifyCategoryParityUnderAot();

            Console.WriteLine(
                "PASS: UseLogging()-composed ILogger<OrderService> (generator-discovered activation), " +
                "structured-property capture, Verify(), direct CapturingLogger<T> construction, and " +
                "Generic Host AddComponoCapture/LogCollector host-wide capture all survived Native AOT " +
                "through the packaged Compono.Logging dependency chain.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    private static void VerifyHostWideCapture()
    {
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Host.Smoke");

        using (logger.BeginScope("SmokeScope"))
            logger.LogWarning("host-wide {OrderId}", 42);

        if (collector.GetCapturedEntries().Count != 1)
            throw new InvalidOperationException($"Expected exactly 1 host-captured entry, got {collector.GetCapturedEntries().Count}.");

        var entry = collector.GetLastCapturedEntry()!.Value;
        if (entry.Category != "Host.Smoke")
            throw new InvalidOperationException($"Expected Host.Smoke category, got '{entry.Category}'.");

        if (entry.LogLevel != LogLevel.Warning)
            throw new InvalidOperationException($"Expected Warning, got {entry.LogLevel}.");

        if (entry.Properties is null || entry.Properties[0].Value is not int orderId || orderId != 42)
            throw new InvalidOperationException("Expected a structured OrderId property with value 42.");

        if (entry.Scopes.Count != 1 || !Equals(entry.Scopes[0], "SmokeScope"))
            throw new InvalidOperationException("Expected the host-captured entry to carry its scope.");

        collector.Verify().InCategory("Host.Smoke").AtLevelOrAbove(LogLevel.Warning).Once();
        collector.Verify().AtLevelOrAbove(LogLevel.Error).Never();
    }

    private static void VerifyCategoryParityUnderAot()
    {
        // Compare against the real LoggerFactory's own category, never a hard-coded string - this
        // proves LoggerCategoryName's trim-safe naming matches the BCL under Native AOT.
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        using var host = builder.Build();
        var factory = host.Services.GetRequiredService<ILoggerFactory>();

        factory.CreateLogger<OrderService>().LogInformation("factory category");
        var factoryCategory = collector.GetLastCapturedEntry()!.Value.Category;

        var direct = new CapturingLogger<OrderService>();
        direct.LogInformation("direct category");
        var directCategory = direct.GetCapturedEntries()[0].Category;

        if (!string.Equals(factoryCategory, directCategory, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Category parity failed: factory '{factoryCategory}' vs direct '{directCategory}'.");
    }
}
