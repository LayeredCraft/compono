using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

/// <summary>
/// ADR-0055 Amendment 4's compile-level regression. This project references both
/// <c>Compono.TestDoubles</c> (<c>ComponoGeneratedTestDoubles=true</c>) and enables
/// <c>ComponoGeneratedLogging=true</c> - the exact shape that broke in real <c>alexa-vox-craft</c>
/// dogfooding (a composed type with both an interface dependency and an <see cref="ILogger{T}"/>
/// dependency reachable from the same root). Before Amendment 4's fix, this file would fail to
/// compile with CS1061 ("AtLevel not found") because Compono.TestDoubles' generated, exact-typed
/// <c>Verify(this ILogger&lt;OrderProcessor&gt;)</c> extension would win overload resolution over
/// Compono.Logging's own <c>Verify(this ILogger)</c>. The fact that this file compiles at all -
/// and that <see cref="LogVerificationBuilder.AtLevel"/> resolves and behaves correctly - is itself
/// the regression proof, not just the assertions below.
/// </summary>
public sealed class LoggingTestDoubleOwnershipCompileRegressionTests
{
    public interface IOrderRepository
    {
        int GetPendingCount();
    }

    public sealed class OrderProcessor(IOrderRepository repository, ILogger<OrderProcessor> logger)
    {
        public ILogger<OrderProcessor> Logger { get; } = logger;

        public void Process()
        {
            var count = repository.GetPendingCount();
            Logger.LogWarning("processing {Count} pending orders", count);
        }
    }

    [Fact]
    public void BothFeaturesEnabled_LoggerVerifyBindsToComponoLogging_NotTestDoublesVerify()
    {
        var composer = Composer.Create(builder => builder.UseLogging().UseGeneratedTestDoubles());

        var processor = composer.Create<OrderProcessor>();

        processor.Process();

        // Compiles and resolves to Compono.Logging.LogVerificationBuilder.AtLevel - if
        // Compono.TestDoubles' generated Verify() extension were still winning overload
        // resolution, this line wouldn't compile at all.
        processor.Logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("pending orders").Once();
    }
}
