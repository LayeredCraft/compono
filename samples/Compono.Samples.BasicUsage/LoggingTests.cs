using Compono.Logging;
using Compono.XunitV3;
using Microsoft.Extensions.Logging;

namespace Compono.Samples.BasicUsage;

// Compono.Logging: UseLogging() composes ILogger<T> as a CapturingLogger<T> - no substitute
// needed to assert what NotificationService actually logged. [Shared] reuses the exact same
// composed logger NotificationService's own constructor received (the same reuse mechanic
// BasicUsageTests.SharedRepositoryIsReusedByTheService demonstrates for a plain type).
public sealed class LoggingTests
{
    [Theory]
    [Compose<LoggingSampleProfile>]
    public void Notify_LogsAnInformationEntryContainingTheCustomerName(
        [Shared] ILogger<NotificationService> logger, NotificationService service, CreateOrder order)
    {
        service.Notify(order);

        logger.Verify().AtLevel(LogLevel.Information).WithMessageContaining(order.CustomerName).Once();
    }
}
