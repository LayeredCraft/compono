using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class VerificationBuilderTests
{
    [Fact]
    public void WithValue_MatchesExactly()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(3);

        factory.Verify<long>("orders").WithValue(3).Once();
    }

    [Fact]
    public void WithValue_WrongValue_Fails()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(3);

        var act = () => factory.Verify<long>("orders").WithValue(4).Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*with value 4*");
    }

    [Fact]
    public void WithValue_FloatEqualityIsExact()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateHistogram<float>("f").Record(0.1f);

        factory.Verify<float>("f").WithValue(0.1f).Once();

        var act = () => factory.Verify<float>("f").WithValue(0.2f).Once();
        act.Should().Throw<TestDoubleVerificationException>();
    }

    [Fact]
    public void WithTag_IsSubsetMatch_AndIgnoresOtherTags()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1,
            new KeyValuePair<string, object?>("region", "eu"),
            new KeyValuePair<string, object?>("extra", "ignored"));

        factory.Verify<long>("orders").WithTag("region", "eu").Once();
    }

    [Fact]
    public void WithTag_DoesNotCoerceNumericTypes()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1, new KeyValuePair<string, object?>("code", 200));

        factory.Verify<long>("orders").WithTag("code", 200).Once();

        var act = () => factory.Verify<long>("orders").WithTag("code", 200L).Once();
        act.Should().Throw<TestDoubleVerificationException>();
    }

    [Fact]
    public void WithoutTag_MatchesMeasurementsMissingTheKey()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1, new KeyValuePair<string, object?>("region", "eu"));

        factory.Verify<long>("orders").WithoutTag("tenant").Once();

        var act = () => factory.Verify<long>("orders").WithoutTag("region").Once();
        act.Should().Throw<TestDoubleVerificationException>();
    }

    [Fact]
    public void ChainedFilters_AllApply()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");
        counter.Add(1, new KeyValuePair<string, object?>("region", "eu"));
        counter.Add(1, new KeyValuePair<string, object?>("region", "us"));

        factory.Verify<long>("orders").WithTag("region", "eu").WithValue(1).Once();
    }

    [Fact]
    public void Matching_UsesCustomPredicate()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateHistogram<double>("duration").Record(250.0);

        factory.Verify<double>("duration").Matching(m => m.Value > 200).Once();
    }

    [Fact]
    public void Never_PassesWhenNoMeasurementMatches()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1);

        factory.Verify<long>("orders").WithValue(99).Never();
    }

    [Fact]
    public void Exactly_AtLeast_AtMost_BehaveAsCounts()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");
        counter.Add(1);
        counter.Add(1);

        factory.Verify<long>("orders").Exactly(2);
        factory.Verify<long>("orders").AtLeast(1);
        factory.Verify<long>("orders").AtMost(3);

        var act = () => factory.Verify<long>("orders").Exactly(1);
        act.Should().Throw<TestDoubleVerificationException>();
    }

    [Fact]
    public void FailureMessage_DescribesInstrumentTagsAndValue()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1, new KeyValuePair<string, object?>("region", "eu"));

        var act = () => factory.Verify<long>("orders").WithTag("region", "us").WithValue(9).Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage(
                "Expected exactly 1 call(s) to instrument \"orders\" (long) "
                + "with tag region = \"us\" and value 9, but received 0.");
    }

    [Fact]
    public void FailureMessage_AnnotatesExpectedNonStringTagValueWithItsRuntimeType()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));

        // The measurement's tag is a string, but the expected value passed to WithTag is an int: the
        // typed message describes the expectation, not the captured measurement.
        meter.CreateCounter<long>("orders").Add(
            1,
            new KeyValuePair<string, object?>("status", "bad"));

        var act = () => factory.Verify<long>("orders").WithTag("status", 200).Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage(
                "Expected exactly 1 call(s) to instrument \"orders\" (long) "
                + "with tag status = 200 (int), but received 0.");
    }
}
