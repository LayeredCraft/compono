using System.Diagnostics.Metrics;
using System.Globalization;

namespace Compono.Metrics.Tests;

public sealed class CultureInvarianceTests
{
    [Fact]
    public void FailureMessage_FormatsNumericValueAndTagWithInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // de-DE renders a decimal point as a comma; the message must not follow it.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            using var factory = new CapturingMeterFactory();
            var meter = factory.Create(new MeterOptions("Shop"));
            meter.CreateHistogram<double>("duration").Record(
                1.5,
                new KeyValuePair<string, object?>("ratio", 1.5));

            var act = () => factory.Verify<double>("duration").WithTag("ratio", 2.5).WithValue(9.5).Once();

            act.Should().Throw<TestDoubleVerificationException>()
                .WithMessage(
                    "Expected exactly 1 call(s) to instrument \"duration\" (double) "
                    + "with tag ratio = 2.5 (double) and value 9.5, but received 0.");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
