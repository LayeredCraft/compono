using System.Reflection;

namespace Compono.Generators.Tests;

/// <summary>
/// Real end-to-end execution of the generated <c>Verify()</c> bridge against a real, generator-emitted
/// double (ADR-0044 Requirement 3) - not a snapshot/compile check
/// (<see cref="TestDoubleVerifyTests"/>). Uses <see cref="GeneratedTestDoubleRegistry.TryCreate"/>
/// directly, the same registry the generated <c>Configure()</c>/<c>Verify()</c> bridges themselves
/// resolve through, rather than going through <c>Compono.TestDoubles</c>'s own
/// <c>GeneratedTestDoubleProvider</c>/<c>UseGeneratedTestDoubles()</c> - that provider lives in a
/// separate package this test harness (<see cref="GeneratorTestHelpers"/>) doesn't reference, and
/// isn't itself under test here; only the generated double's own dispatch/counting/verification
/// behavior is.
/// </summary>
public sealed class TestDoubleVerificationExecutionTests
{
    private const string Source = """
        namespace TestNamespace;

        public interface IRepository
        {
            int Count();
            void Save(string name);
        }

        public sealed class OrderService
        {
            public OrderService(IRepository repository) { }
        }

        public static class EntryPoint
        {
            // Never actually invoked - exists purely so the generator's discovery walk reaches
            // IRepository as a Composer.Create<T>() dependency (the same idiom every compile-only
            // TestDoubleVerifyTests snapshot test already uses). Run() below never calls Compono
            // composition at all, so it can execute without Compono.TestDoubles's own runtime
            // provider being on the reference list.
            private static void Discover() => Compono.Composer.Create().Create<OrderService>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IRepository), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void Once_AfterExactlyOneCall_DoesNotThrow()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Count();
                        repository.Verify().Count().Once();
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public void Once_AfterZeroCalls_ThrowsWithMessage()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Verify().Count().Once();
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 1 call(s) to global::TestNamespace.IRepository.Count, but received 0.");
    }

    [Fact]
    public void Never_AfterZeroCalls_DoesNotThrow()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Verify().Count().Never();
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public void Never_AfterOneCall_ThrowsWithMessage()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Count();
                        repository.Verify().Count().Never();
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 0 call(s) to global::TestNamespace.IRepository.Count, but received 1.");
    }

    [Fact]
    public void Exactly_MatchesTheRealNumberOfCalls_DoesNotThrow()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Count();
                        repository.Count();
                        repository.Count();
                        repository.Verify().Count().Exactly(3);
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public void Exactly_DiffersFromTheRealNumberOfCalls_ThrowsWithMessage()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Count();
                        repository.Verify().Count().Exactly(3);
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 3 call(s) to global::TestNamespace.IRepository.Count, but received 1.");
    }

    [Fact]
    public void VerifiedCall_WithConfiguredReturnValue_CountsAndDispatchesBothCorrectly()
    {
        // Proves RecordCall() and the configured-return/-exception dispatch don't interfere with
        // each other - a call is counted whether it hits configured, default, or throw behavior.
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Configure().Count().Returns(4);

                        var first = repository.Count();
                        var second = repository.Count();

                        repository.Verify().Count().Exactly(2);
                        return first + second;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(8);
    }

    [Fact]
    public void DistinctMembers_VerifyIndependently()
    {
        // Each member has its own backing field/counter - proven here across two distinct
        // (non-overloaded) members sharing one interface.
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Count();
                        repository.Save("Ada");
                        repository.Save("Grace");

                        repository.Verify().Count().Once();
                        repository.Verify().Save().Exactly(2);
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public void OverloadedMember_VerifiesEachOverloadIndependently()
    {
        // Verification reuses Requirement 1's per-overload discriminator mechanism -
        // Verify().Speak(string.Empty) selects the same overload-specific slot
        // Configure().Speak(string.Empty) would, per ADR-0044 Requirement 3.
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IResponseBuilder
                    {
                        void Speak(string? text);
                        void Speak(int repeatCount);
                    }

                    public sealed class Wrapper
                    {
                        public Wrapper(IResponseBuilder builder) { }
                    }

                    public static class EntryPoint
                    {
                        private static void Discover() => Compono.Composer.Create().Create<Wrapper>();

                        public static object Run()
                        {
                            Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IResponseBuilder), out var value);
                            var builder = (IResponseBuilder)value!;

                            builder.Speak("hello");
                            builder.Speak("hello");
                            builder.Speak(3);

                            builder.Verify().Speak(string.Empty).Exactly(2);
                            builder.Verify().Speak(0).Once();
                            return builder;
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    // ADR-0050 regression: the zero-argument Verify() compatibility overload for a matching-eligible
    // member (Save(string) - real parameter, non-overloaded) must read the shared call-log list's
    // Count, not a removed per-entry field - the exact bug the ADR-0050 spike hit (16/18 execution
    // tests before the fix). Two zero-argument Configure() calls (appending two entries, per the
    // "repeated call wins by recency" compatibility shape) followed by two real calls must still
    // report exactly 2, regardless of how many response entries exist.
    [Fact]
    public void ZeroArgumentVerify_OnMatchingEligibleMember_CountsFromTheSharedCallLog_NotARemovedPerEntryField()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var repository = (IRepository)CreateDouble();
                        repository.Configure().Save().Returns(default(Compono.Unit));
                        repository.Configure().Save().Returns(default(Compono.Unit));

                        repository.Save("first");
                        repository.Save("second");

                        repository.Verify().Save().Exactly(2);
                        return repository;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }
}
