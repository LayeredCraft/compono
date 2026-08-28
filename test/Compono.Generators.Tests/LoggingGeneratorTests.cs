using Microsoft.CodeAnalysis;

namespace Compono.Generators.Tests;

/// <summary>
/// Compono.Logging's own logging-activation-generation feature (ADR-0055 Amendments 1/3) - lives
/// inside this existing generator, not a separate one, so its tests live in this existing project
/// too, exercising <see cref="ComponoIncrementalGenerator"/> end to end exactly like every other
/// discovery path's own tests.
/// </summary>
public sealed class LoggingGeneratorTests
{
    private static readonly MetadataReference LoggingAbstractionsReference =
        MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location);

    private static readonly MetadataReference CompanoLoggingReference =
        MetadataReference.CreateFromFile(typeof(Compono.Logging.LoggingFactoryRegistry).Assembly.Location);

    private static readonly IReadOnlyList<MetadataReference> WithCompanoLogging = [LoggingAbstractionsReference, CompanoLoggingReference];
    private static readonly IReadOnlyList<MetadataReference> WithILoggerOnly = [LoggingAbstractionsReference];

    private const string OrderServiceSource = """
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

    // ---- Property-gating matrix (ADR-0055 Amendment 3) ----

    [Fact]
    public void PropertyAbsent_CompanoLoggingReferenced_NoDiscoveryOrEmissionOrDiagnostic()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions { SourceCode = OrderServiceSource, ExtraReferences = WithCompanoLogging },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().NotContain(t => t.GetText().ToString().Contains("LoggingFactoryRegistry"));
    }

    [Fact]
    public void PropertyTrue_CompanoLoggingPresent_DiscoversAndEmitsActivation()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = OrderServiceSource,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        var generatedText = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        generatedText.Should().Contain("global::Compono.Logging.LoggingFactoryRegistry.Register<global::TestNamespace.OrderService>");
        generatedText.Should().Contain("new global::Compono.Logging.CapturingLogger<global::TestNamespace.OrderService>(options)");
    }

    [Fact]
    public void ExplicitFalse_OverridesEnabled_NoEmission()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = OrderServiceSource,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "false" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().NotContain(t => t.GetText().ToString().Contains("LoggingFactoryRegistry"));
    }

    [Fact]
    public void PropertyTrue_CompanoLoggingMissing_ReportsDiagnostic_NoEmission()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = OrderServiceSource,
                ExtraReferences = WithILoggerOnly, // ILogger<T> resolvable, Compono.Logging itself is not
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().Contain(d => d.Id == "CMP0038");
        result.GeneratedTrees.Should().NotContain(t => t.GetText().ToString().Contains("LoggingFactoryRegistry"));
    }

    [Fact]
    public void ILoggerAvailable_ButLoggingDisabled_NoEmission_RegardlessOfPresence()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions { SourceCode = OrderServiceSource, ExtraReferences = WithCompanoLogging },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        // OrderService itself still gets its own ordinary composition plan regardless of logging -
        // disabling ComponoGeneratedLogging only suppresses the logging-specific registration, never
        // ordinary plan generation.
        result.GeneratedTrees.Should().Contain(t => t.GetText().ToString().Contains("OrderServiceCompositionPlan"));
        result.GeneratedTrees.Should().NotContain(t => t.GetText().ToString().Contains("LoggingFactoryRegistry"));
    }

    [Fact]
    public void NoLoggingAbstractionsReferencedAtAll_ProducesZeroOutputAndZeroErrors()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Customer
                    {
                        public Customer(string name) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var customer = composer.Create<TestNamespace.Customer>();
                        }
                    }
                    """,
                // Deliberately no MSBuildProperties at all - an ordinary consumer who never
                // references Compono.Logging and never sets the property (the plan's own bullet:
                // "references neither Microsoft.Extensions.Logging.Abstractions nor sets the
                // property at all"). This must stay silent and free, not report CMP0038 - that
                // diagnostic exists only for an explicit/enabled-by-package misconfiguration, never
                // for an ordinary Compono consumer who has nothing to do with logging at all.
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().NotContain(t => t.GetText().ToString().Contains("LoggingFactoryRegistry"));
    }

    // ---- Discovery correctness (reuses the existing pipeline; no second walker) ----

    [Fact]
    public void NestedTransitiveDependency_CategoryDiscoveredThroughIntermediateComposableType()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Leaf
                    {
                        public Leaf(Microsoft.Extensions.Logging.ILogger<Leaf> logger) { }
                    }

                    public sealed class Root
                    {
                        public Root(Leaf leaf) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var root = composer.Create<TestNamespace.Root>();
                        }
                    }
                    """,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        var generatedText = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        generatedText.Should().Contain("global::Compono.Logging.LoggingFactoryRegistry.Register<global::TestNamespace.Leaf>");
    }

    [Fact]
    public void SameCategoryDiscoveredViaTwoRoots_EmitsExactlyOneRegistration()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Shared
                    {
                        public Shared(Microsoft.Extensions.Logging.ILogger<Shared> logger) { }
                    }

                    public sealed class RootA
                    {
                        public RootA(Shared shared) { }
                    }

                    public sealed class RootB
                    {
                        public RootB(Shared shared) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var a = composer.Create<TestNamespace.RootA>();
                            var b = composer.Create<TestNamespace.RootB>();
                        }
                    }
                    """,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        var occurrences = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .Count(text => text.Contains("global::Compono.Logging.LoggingFactoryRegistry.Register<global::TestNamespace.Shared>"));

        occurrences.Should().Be(1);
    }

    [Fact]
    public void ILoggerOfTOnTypeUnreachableFromAnyRoot_ProducesNoRegistration()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class NeverComposed
                    {
                        // Never reached by Create<T>()/[Composable]/a [Compose] parameter anywhere -
                        // proves this isn't a compilation-wide scan.
                        public NeverComposed(Microsoft.Extensions.Logging.ILogger<NeverComposed> logger) { }
                    }

                    public sealed class ActualRoot
                    {
                        public ActualRoot(string name) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var root = composer.Create<TestNamespace.ActualRoot>();
                        }
                    }
                    """,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        var generatedText = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        generatedText.Should().NotContain("NeverComposed");
    }

    [Fact]
    public void BareNonGenericILogger_NeedsNoGeneratedRegistration()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class PlainLoggerService
                    {
                        public PlainLoggerService(Microsoft.Extensions.Logging.ILogger logger) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var service = composer.Create<TestNamespace.PlainLoggerService>();
                        }
                    }
                    """,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().NotContain(t => t.GetText().ToString().Contains("LoggingFactoryRegistry"));
    }

    [Fact]
    public void GeneratedActivator_AcceptsLoggingOptionsAsRuntimeParameter_NotCapturedOrZeroArgument()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = OrderServiceSource,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var generatedText = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(t => t.GetText().ToString()));

        generatedText.Should().Contain("static options => new global::Compono.Logging.CapturingLogger<global::TestNamespace.OrderService>(options)");
    }

    [Fact]
    public void InaccessibleCategoryType_ReportsCMP0039_ButStillCompiles()
    {
        // A logging category unreachable from a top-level generated registration - mirrors
        // DiscoveredCollectionInfo's identical CMP0012 accessibility check, confirmed by real
        // baseline spike (PLAN-0055 task 2 follow-up) to be a genuinely new Compono.Logging-only
        // concern, not a narrowing of anything core Compono already supports.
        var (driver, originalCompilation) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Outer
                    {
                        private sealed class PrivateCategory { }

                        public sealed class UsesPrivateCategory
                        {
                            public UsesPrivateCategory(Microsoft.Extensions.Logging.ILogger<PrivateCategory> logger) { }
                        }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var value = composer.Create<TestNamespace.Outer.UsesPrivateCategory>();
                        }
                    }
                    """,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().Contain(d => d.Id == "CMP0039");
        // The ordinary UsesPrivateCategoryCompositionPlan legitimately still names PrivateCategory
        // (an unremarkable context.Resolve<ILogger<PrivateCategory>>() leaf call - ordinary
        // composition never required PrivateCategory to be independently accessible). Only the
        // logging-specific activation must never be emitted for it.
        var generatedText = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        generatedText.Should().NotContain("LoggingFactoryRegistry.Register<global::TestNamespace.Outer.PrivateCategory>");
        generatedText.Should().NotContain("LoggingFactoryRegistry");
    }

    [Fact]
    public void CategoryReachableOnlyThroughHandWrittenRegistrationFactory_IsIntentionallyNotDiscovered()
    {
        // ADR-0052 Finding-B shape: a hand-written Register<T>(...) factory's own internal
        // context.Resolve<ILogger<TSomething>>() call is not visible to this generator's discovery
        // walk (which only sees ordinary constructor parameters/[Compose] parameters) - recorded
        // here as the documented, intentional limitation it is, not a bug this plan fixes.
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class HiddenCategory { }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create(builder =>
                                builder.Register<Microsoft.Extensions.Logging.ILogger<HiddenCategory>>(
                                    ctx => (Microsoft.Extensions.Logging.ILogger<HiddenCategory>)null!));
                        }
                    }
                    """,
                ExtraReferences = WithCompanoLogging,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedLogging"] = "true" },
            },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        var generatedText = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        generatedText.Should().NotContain("HiddenCategory");
    }
}
