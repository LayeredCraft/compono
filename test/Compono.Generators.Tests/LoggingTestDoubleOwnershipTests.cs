using Microsoft.CodeAnalysis;

namespace Compono.Generators.Tests;

/// <summary>
/// ADR-0055 Amendment 4: when Compono.Logging's generation is enabled,
/// <c>ILogger</c>/<c>ILogger&lt;T&gt;</c> are Logging-owned abstractions, excluded from
/// <c>Compono.TestDoubles</c> generation - fixes a real compile-time collision found dogfooding
/// against <c>alexa-vox-craft</c> (both features enabled on the same closed <c>ILogger&lt;T&gt;</c>,
/// TestDoubles' exact-typed <c>Verify(this ILogger&lt;T&gt;)</c> extension silently shadowed
/// Compono.Logging's own <c>Verify(this ILogger)</c> via ordinary C# overload resolution).
/// </summary>
public sealed class LoggingTestDoubleOwnershipTests
{
    private static readonly MetadataReference LoggingAbstractionsReference =
        MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location);

    private static readonly MetadataReference CompanoLoggingReference =
        MetadataReference.CreateFromFile(typeof(Compono.Logging.LoggingFactoryRegistry).Assembly.Location);

    private static readonly IReadOnlyList<MetadataReference> WithCompanoLogging =
        [LoggingAbstractionsReference, CompanoLoggingReference];

    private const string ClosedLoggerSource = """
        namespace TestNamespace;

        public sealed class OrderService
        {
            public OrderService(Microsoft.Extensions.Logging.ILogger<OrderService> logger) { }
        }

        public static class EntryPoint
        {
            public static void Run()
            {
                var composer = Compono.Composer.Create();
                var service = composer.Create<TestNamespace.OrderService>();
            }
        }
        """;

    private const string BareLoggerSource = """
        namespace TestNamespace;

        public sealed class PlainLoggerService
        {
            public PlainLoggerService(Microsoft.Extensions.Logging.ILogger logger) { }
        }

        public static class EntryPoint2
        {
            public static void Run()
            {
                var composer = Compono.Composer.Create();
                var service = composer.Create<TestNamespace.PlainLoggerService>();
            }
        }
        """;

    private static (bool HasLoggingActivation, bool HasTestDoubleForType) Generate(
        string source, bool testDoublesEnabled, bool loggingEnabled)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = source,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string>
                {
                    ["ComponoGeneratedTestDoubles"] = testDoublesEnabled ? "true" : "false",
                    ["ComponoGeneratedLogging"] = loggingEnabled ? "true" : "false",
                },
            },
            TestContext.Current.CancellationToken);

        var allText = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(t => t.GetText().ToString()));

        return (allText.Contains("LoggingFactoryRegistry"), allText.Contains("ILogger_global__"));
    }

    // ---- Closed ILogger<T> - the four-way matrix ----

    [Fact]
    public void ClosedILoggerOfT_BothDisabled_NeitherGeneratorTouchesIt()
    {
        var (hasActivation, hasDouble) = Generate(ClosedLoggerSource, testDoublesEnabled: false, loggingEnabled: false);
        hasActivation.Should().BeFalse();
        hasDouble.Should().BeFalse();
    }

    [Fact]
    public void ClosedILoggerOfT_LoggingOnly_ActivationGenerated()
    {
        var (hasActivation, hasDouble) = Generate(ClosedLoggerSource, testDoublesEnabled: false, loggingEnabled: true);
        hasActivation.Should().BeTrue();
        hasDouble.Should().BeFalse();
    }

    [Fact]
    public void ClosedILoggerOfT_TestDoublesOnly_DoubleGenerated_PreExistingBehaviorPreserved()
    {
        var (hasActivation, hasDouble) = Generate(ClosedLoggerSource, testDoublesEnabled: true, loggingEnabled: false);
        hasActivation.Should().BeFalse();
        hasDouble.Should().BeTrue();
    }

    [Fact]
    public void ClosedILoggerOfT_BothEnabled_LoggingOwnsIt_NoCompetingTestDouble()
    {
        var (hasActivation, hasDouble) = Generate(ClosedLoggerSource, testDoublesEnabled: true, loggingEnabled: true);
        hasActivation.Should().BeTrue();
        hasDouble.Should().BeFalse();
    }

    // ---- Bare ILogger - same matrix; Logging never generates activation for it either way, and
    // (confirmed empirically pre-Amendment-4 too) TestDoubles never generated a double for it
    // either, so this half of the ownership rule is a documented no-op that this suite pins down
    // as a regression guard, not a behavior change. ----

    [Fact]
    public void BareILogger_TestDoublesOnly_NeverGetsGeneratedDouble()
    {
        var (hasActivation, hasDouble) = Generate(BareLoggerSource, testDoublesEnabled: true, loggingEnabled: false);
        hasActivation.Should().BeFalse();
        hasDouble.Should().BeFalse();
    }

    [Fact]
    public void BareILogger_BothEnabled_NeverGetsGeneratedDouble()
    {
        var (hasActivation, hasDouble) = Generate(BareLoggerSource, testDoublesEnabled: true, loggingEnabled: true);
        hasActivation.Should().BeFalse();
        hasDouble.Should().BeFalse();
    }
}
