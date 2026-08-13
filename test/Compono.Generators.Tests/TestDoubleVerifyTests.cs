namespace Compono.Generators.Tests;

/// <summary>
/// Exercises ADR-0043's generated test doubles end to end through the real generator - the
/// compile-time opt-in, an interface leaf reached from a composed type's constructor, and the
/// "opt-in off means zero output change" regression the plan's Phase 0 explicitly calls for.
/// </summary>
public sealed class TestDoubleVerifyTests
{
    private const string InterfaceAndService = """
        namespace TestNamespace
        {
            public interface IRepository
            {
                System.Threading.Tasks.Task<string?> FindNameAsync(System.Guid id);

                void Save(string name);

                int Count { get; set; }
            }

            public sealed class OrderService
            {
                public OrderService(IRepository repository)
                {
                    Repository = repository;
                }

                public IRepository Repository { get; }
            }

            public static class EntryPoint
            {
                public static void Run()
                {
                    var composer = Compono.Composer.Create();
                    var service = composer.Create<TestNamespace.OrderService>();
                }
            }
        }
        """;

    // A different namespace than IRepository's own (TestNamespace), with no `using` importing
    // anything Configure()-related - proves ADR-0043 Amendment 11's global-namespace placement
    // actually makes Configure() reachable with no import, not just design intent.
    private const string ConfigureFromAnotherNamespace = """


        namespace AnotherNamespace
        {
            public static class ConsumerTest
            {
                public static void ConfigureFromAnotherNamespace(TestNamespace.IRepository repository)
                {
                    repository.Configure().FindNameAsync().Returns(System.Threading.Tasks.Task.FromResult<string?>("value"));
                    repository.Configure().Save().Throws(new System.InvalidOperationException());
                    repository.Configure().Count().Returns(42);
                }
            }
        }
        """;

    [Fact]
    public Task TestDoublesEnabled_InterfaceLeaf_GeneratesDoubleReachableFromAnotherNamespace() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = InterfaceAndService + ConfigureFromAnotherNamespace,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TestDoublesDisabled_InterfaceLeaf_GeneratesNoDouble() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = InterfaceAndService,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Event_ReportsUnsupportedMemberKindDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        event System.Action Changed;
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0021",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task ConfigureNamedMember_ReportsCollisionDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Configure();
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0023",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task SetOnlyProperty_ReportsUnsupportedDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        int Value { set; }
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0027",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task OverloadedMember_ReportsUnsupportedDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Get(int id);
                        void Get(string id);
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0022",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NonNullableReferenceReturn_ReportsUnsupportedReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        string GetName();
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0025",
            TestContext.Current.CancellationToken);
}
