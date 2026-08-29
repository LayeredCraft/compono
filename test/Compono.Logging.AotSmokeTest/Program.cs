using Compono;
using Compono.Logging;
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

            Console.WriteLine(
                "PASS: UseLogging()-composed ILogger<OrderService> (generator-discovered activation), " +
                "structured-property capture, Verify(), and direct CapturingLogger<T> construction " +
                "all survived Native AOT through the packaged Compono.Logging dependency chain.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }
}
