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

    // ADR-0044: a real overload (two members sharing a name but not a full signature identity) now
    // gets its own Configure()/Verify() surface per overload instead of rejecting the whole
    // interface - each overload's configuration extension takes real, value-discarded parameters
    // mirroring the real overload, so ordinary C# overload resolution picks the right one.
    [Fact]
    public Task OverloadedMember_GeneratesDoubleWithPerOverloadConfiguration() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
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
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().Get(1);
                        repository.Configure().Get("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

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

    // ADR-0044 Amendment 3, Finding 8: a diamond collision (the same signature independently
    // declared by two different base interfaces) now withholds Configure()/Verify() surface for
    // that one identity only - the double still generates (both explicit implementations get an
    // inline deterministic default), not a whole-interface rejection like v1.
    [Fact]
    public Task DiamondInheritedSameNameProperty_ReportsScopedOverloadedDiagnostic() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        int Value { get; set; }
                    }

                    public interface IBaseB
                    {
                        int Value { get; set; }
                    }

                    public interface IRepository : IBaseA, IBaseB;

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
    public Task StaticAbstractProperty_ReportsUnsupportedMemberKindDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        static abstract int DefaultTimeout { get; }
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
    public Task StaticAbstractOperator_ReportsUnsupportedMemberKindDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        static abstract IRepository operator +(IRepository left, IRepository right);
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
    public Task PropertyNamedToString_ReportsObjectMemberCollisionDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        string ToString { get; }
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
            "CMP0024",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NonNullableValueTaskOfReference_ReportsUnsupportedReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        System.Threading.Tasks.ValueTask<string> GetNameAsync();
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

    [Fact]
    public Task MultidimensionalArrayReturn_ReportsUnsupportedReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        int[,] GetGrid();
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

    [Fact]
    public Task NullableCollectionReturn_GeneratesDoubleWithEmptyDefault() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    System.Collections.Generic.List<int>? GetValues();
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PrivateDefaultInterfaceMethod_GeneratesDoubleIgnoringIt() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    string? GetName();

                    private int Helper() => 1;
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ConfigureMemberWithDifferentArity_GeneratesDoubleWithoutCollision() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void Configure(int mode);

                    string? GetName();
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task HashSetReturn_GeneratesDoubleWithEmptyDefault() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    System.Collections.Generic.HashSet<int> GetIds();
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PrivateDefaultMethodSharesNameWithPublicMember_DoesNotFalselyReportOverload() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void Get();

                    private int Helper() { return 1; }
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
        }, TestContext.Current.CancellationToken);

    // Two call sites reach the same closed generic interface with disagreeing nullability
    // (IProvider<string> vs IProvider<string?>) - TransitiveClosureWalker now walks and analyzes
    // both independently (IncludeNullability, PR #83 review round 3) rather than silently
    // collapsing to whichever is discovered first. Here the two disagree on whether Get() even has
    // a deterministic default at all, so the merge step's own conflict handling (mirroring
    // DiscoveredTypeInfo's CMP0010 pattern) surfaces the real per-location CMP0025 failure
    // deterministically, regardless of discovery order, rather than an order-dependent silent pick.
    [Fact]
    public Task SameInterfaceDiscoveredWithDisagreeingNullability_ReportsRealFailureDeterministically() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IProvider<T>
                    {
                        T Get();
                    }

                    public sealed class A
                    {
                        public A(IProvider<string> provider) { }
                    }

                    public sealed class B
                    {
                        public B(IProvider<string?> provider) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            Compono.Composer.Create().Create<TestNamespace.A>();
                            Compono.Composer.Create().Create<TestNamespace.B>();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0025",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task DictionaryReturn_GeneratesDoubleWithEmptyDefault() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    System.Collections.Generic.Dictionary<string, int> GetCounts();
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ConfigureMemberWithOptionalParameter_ReportsCollisionDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Configure(int mode = 0);
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
    public Task ConfigureMemberWithParamsArray_ReportsCollisionDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Configure(params int[] modes);
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
    public Task NullableReferenceReturnAndParameter_PreservesAnnotationInGeneratedCode() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    System.Threading.Tasks.Task<string?> FindNameAsync(System.Guid id);

                    void Save(string? name);
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task IDictionaryReturn_GeneratesDoubleWithConcreteEmptyDictionary() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    System.Collections.Generic.IDictionary<string, int> GetCounts();

                    System.Collections.Generic.IReadOnlyDictionary<string, int> GetReadOnlyCounts();
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PropertyWithNonPublicDefaultSetter_GeneratesGetOnlyDouble() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    int Value { get => 0; private set { } }
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
        }, TestContext.Current.CancellationToken);

    // The real IResponseBuilder-shaped motivating case from the lightsaber-skill dogfooding finding
    // (docs/roadmap/post-mvp.md): a nullable-string overload alongside a params overload of a
    // different element type, both distinct identities under ADR-0044's discriminator hash.
    [Fact]
    public Task ParamsAndNullableStringOverloads_GeneratesDoubleWithPerOverloadConfiguration() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface ISsml;

                public interface IResponseBuilder
                {
                    void Speak(string? text);

                    void Speak(params ISsml[] parts);
                }

                public sealed class OrderService
                {
                    public OrderService(IResponseBuilder builder) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IResponseBuilder builder)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        builder.Configure().Speak("hello");
                        builder.Configure().Speak();
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // ADR-0044 Amendment 5: an overload whose own shape is unsupported (a ref/out/in parameter)
    // falls back to a deterministic-default body without a Configure() surface, but its
    // sibling overload is unaffected - and reports an informational CMP0030, not a whole-interface
    // rejection (CMP0026).
    [Fact]
    public Task OverloadWithOutParameterHavingDefault_FallsBackWithoutRejectingSiblingOverload() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        bool TryGet(int id, out string? value);

                        bool TryGet(int id);
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IRepository repository)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            repository.Configure().TryGet().Returns(true);
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0030",
            TestContext.Current.CancellationToken);

    // Amendment 8, Finding 20: an out parameter with no deterministic default (a non-nullable
    // reference type) has no constructible fallback body at all - the whole interface still falls
    // back to the ordinary runtime-provider path, unlike the case above.
    [Fact]
    public Task OutParameterWithNoDeterministicDefault_ReportsUnsupportedParameterShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        bool TryGet(int id, out string value);

                        bool TryGet(int id);
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
            "CMP0026",
            TestContext.Current.CancellationToken);

    // Amendment 11: the object-collision check now compares the *generated discriminator
    // extension's* own applicability, not the interface member's raw name - an overloaded
    // ToString(int) sharing a name with another ToString overload keeps its surface, since its own
    // extension carries a real, non-zero parameter list and never collides with the always-
    // zero-argument object.ToString().
    [Fact]
    public Task OverloadedToStringSharingNameWithAnotherOverload_DoesNotCollideWithObjectMember() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    string? ToString(int format);

                    string? ToString(string format);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().ToString(1).Returns("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Amendment 12: a params/all-optional overload is applicable to a zero-argument call, but the
    // corrected check requires a genuinely zero-parameter extension for the ToString/GetHashCode/
    // GetType collision - so this overload keeps its surface, unlike a truly zero-parameter
    // ToString() would. (A genuinely zero-*argument* call site, e.g. `.ToString()`, still always
    // binds to the inherited object.ToString() regardless - ordinary member lookup finds it
    // applicable before extension-method fallback is even considered - so reaching this overload's
    // own extension needs an explicit argument, same as any other non-zero-parameter overload.)
    [Fact]
    public Task OverloadedToStringWithParamsArray_DoesNotCollideWithObjectMember() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    string? ToString(params object[] values);

                    string? ToString(int format);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().ToString(new object[] { "value" }).Returns("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Amendment 14: unlike ToString/GetHashCode/GetType (which need a genuinely zero-parameter
    // extension to collide), object.Equals(object) is one-argument - an overloaded, non-generic
    // Equals(int) whose own extension carries exactly one, non-ref-like-typed parameter collides
    // with it, with no escape hatch (boxing/reference conversion to object always applies).
    [Fact]
    public Task OverloadedEqualsWithConvertibleParameter_ReportsObjectMemberCollisionDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        bool Equals(int format);

                        bool Equals(long format);
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
            "CMP0024",
            TestContext.Current.CancellationToken);

    // Amendment 16: a ref-like-typed (ref struct) parameter has no boxing or reference conversion
    // to object at all, so object.Equals(object) is never actually applicable to it - the surface
    // is kept, unlike the boxable-int case above.
    [Fact]
    public Task OverloadedEqualsWithRefLikeParameter_DoesNotCollideWithObjectMember() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    bool Equals(System.Span<int> value);

                    bool Equals(int a, int b);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().Equals(default(System.Span<int>)).Returns(true);
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // ADR-0044 Amendment 10: nint/nuint canonicalize to System.IntPtr/System.UIntPtr for
    // discriminator-hash purposes - the same type via a different keyword collapses to the same
    // identity, so this is a genuine diamond collision, not two independent overloads.
    [Fact]
    public Task DiamondInheritedNintAndIntPtrOverload_ReportsScopedOverloadedDiagnostic() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        void Seek(nint offset);
                    }

                    public interface IBaseB
                    {
                        void Seek(System.IntPtr offset);
                    }

                    public interface IRepository : IBaseA, IBaseB;

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

    // Codex review, PR #88: an overloaded member's configuration extension carries the real overload's
    // own parameter names alongside the "this" receiver - a real parameter literally named "self"
    // would collide with a receiver also named "self" (CS0100, duplicate parameter), if the receiver
    // weren't renamed to something a real parameter can't produce via EscapeIdentifier's @-escaping.
    [Fact]
    public Task OverloadedMemberWithParameterNamedSelf_GeneratesDoubleWithoutParameterNameCollision() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void Save(int self);

                    void Save(string self);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().Save(1);
                        repository.Configure().Save("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: RefKind.RefReadOnlyParameter (a C# 12 `ref readonly` parameter) is a
    // distinct enum value from RefKind.RefReadOnly (a by-ref-readonly *return*) - the ref-kind-prefix
    // switch must map it to "ref readonly ", or the generated explicit interface implementation's
    // signature silently drops the modifier and no longer matches the interface member (CS0535).
    // ref readonly still routes through the ref/out/in overload-set-internal-unsupported fallback
    // (no Configure()/Verify() surface), same as ref/out/in - only the explicit-impl signature itself
    // is under test here.
    [Fact]
    public Task RefReadOnlyParameter_GeneratesDoubleWithMatchingExplicitImplementationSignature() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Seek(ref readonly int offset);

                        void Seek(int offset);
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IRepository repository)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            repository.Configure().Seek();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0030",
            TestContext.Current.CancellationToken);

    // Codex review, PR #88: "Overload-set-internal partial support" (ADR-0044's own name for this
    // feature) presupposes an overload set - a *solo* ref/out/in member (no same-named sibling of
    // any shape) has no set to preserve, and must keep v1's original whole-interface-rejection
    // disposition, not the per-overload fallback treatment a real overload gets.
    [Fact]
    public Task SoloRefOutInMemberWithNoSibling_ReportsUnsupportedParameterShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Seek(ref int offset);

                        string? GetName();
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
            "CMP0026",
            TestContext.Current.CancellationToken);

    // Codex review, PR #88: a property and a same-named zero-parameter method inherited from two
    // different base interfaces don't share a full signature (so the diamond-collision check doesn't
    // catch them), but both generate the exact same zero-argument extension shape
    // (`Value(this Double)`) - an unresolvable CS0111 collision if both kept their surface.
    [Fact]
    public Task PropertyAndZeroParameterMethodShareName_ReportsZeroArgumentExtensionCollisionDiagnostic() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        int Value { get; }
                    }

                    public interface IBaseB
                    {
                        int Value();
                    }

                    public interface IRepository : IBaseA, IBaseB;

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
            "CMP0029",
            TestContext.Current.CancellationToken);

    // The zero-argument-extension collision is scoped to the colliding identity only - a genuine
    // overload of the same name that keeps a real, non-zero parameter list is unaffected and keeps
    // its own per-overload surface.
    [Fact]
    public Task PropertyCollidesWithZeroParameterOverloadButSiblingOverloadIsUnaffected() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        int Value { get; }
                    }

                    public interface IBaseB
                    {
                        int Value();

                        int Value(int offset);
                    }

                    public interface IRepository : IBaseA, IBaseB;

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IRepository repository)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            repository.Configure().Value(1).Returns(5);
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0029",
            TestContext.Current.CancellationToken);

    // Codex review, PR #88: `Collision.hgQWPcvxVjdw` and `Collision.cTtIHWbVrHlp` genuinely different
    // parameter types both hash to the same 8-hex-character FNV-1a value (60724af7) under this
    // canonicalization scheme - a real, demonstrated 32-bit hash collision, not a hypothetical one.
    // Identity/equality decisions (diamond-collision grouping) must compare the full canonical
    // signature, never the hash, or these two genuinely different overloads would be misclassified as
    // a diamond collision and both would wrongly lose their Configure() surface.
    [Fact]
    public Task OverloadsWithHashCollidingParameterTypes_BothKeepConfigurationSurface() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace.Collision
                {
                    public sealed class hgQWPcvxVjdw;

                    public sealed class cTtIHWbVrHlp;
                }

                namespace TestNamespace
                {
                    public interface IBaseA
                    {
                        void Handle(Collision.hgQWPcvxVjdw value);
                    }

                    public interface IBaseB
                    {
                        void Handle(Collision.cTtIHWbVrHlp value);
                    }

                    public interface IRepository : IBaseA, IBaseB;

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IRepository repository)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            repository.Configure().Handle(new TestNamespace.Collision.hgQWPcvxVjdw());
                            repository.Configure().Handle(new TestNamespace.Collision.cTtIHWbVrHlp());
                        }
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: a real parameter can be named "__self" (EscapeIdentifier only @-escapes
    // reserved keywords, it never renames a leading-underscore identifier) - the extension receiver
    // must keep lengthening past it, not just past "self".
    [Fact]
    public Task OverloadedMemberWithParameterNamedDunderSelf_GeneratesDoubleWithoutParameterNameCollision() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void Save(int __self);

                    void Save(string __self);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().Save(1);
                        repository.Configure().Save("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: `int*[]` (an array of pointers) has TypeKind.Array at the top level, not
    // Pointer - a top-level-only check would accept it as an overload's own parameter type,
    // generating a discriminator extension containing a pointer type with no unsafe context (CS0214
    // in the consumer). This overload's sibling M(int) proves it's the pointer-array parameter
    // specifically that rejects the whole interface, not overloading itself.
    [Fact]
    public Task OverloadWithArrayOfPointersParameter_ReportsUnsupportedParameterShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public unsafe interface IRepository
                    {
                        void M(int value);

                        void M(int*[] values);
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
            "CMP0026",
            TestContext.Current.CancellationToken);

    // Codex review, PR #88: INamedTypeSymbol.TypeArguments only ever holds a nested type's *own*
    // generic parameters, never an outer type's substitution - Outer<int>.Inner and Outer<string>.Inner
    // would otherwise canonicalize identically (both "Outer<T>.Inner", since Inner itself declares no
    // type parameters) and be misdiagnosed as a diamond collision instead of two legal, distinct
    // overloads.
    [Fact]
    public Task OverloadsWithDifferentContainingTypeGenericArguments_BothKeepConfigurationSurface() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Outer<T>
                {
                    public sealed class Inner;
                }

                public interface IRepository
                {
                    void Handle(Outer<int>.Inner value);

                    void Handle(Outer<string>.Inner value);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().Handle(new TestNamespace.Outer<int>.Inner());
                        repository.Configure().Handle(new TestNamespace.Outer<string>.Inner());
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: a differently-named real member can literally be named after another
    // overload's generated hash suffix - M(int)'s own discriminator hashes to "b9dfaa09", so a solo
    // member M_b9dfaa09() would generate the exact same field name ("__M_b9dfaa09") unless suffix
    // uniqueness is checked globally, not just within the "M" name group.
    [Fact]
    public Task OverloadSuffixCollidesWithDifferentlyNamedRealMember_GeneratesDoubleWithDistinctFieldNames() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void M(int value);

                    void M(string value);

                    void M_b9dfaa09();
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().M(1);
                        repository.Configure().M("value");
                        repository.Configure().M_b9dfaa09();
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: "scoped" must be restated on the explicit implementation's ref/out/in
    // parameter to match the interface member's ref-safety contract, or the consumer gets CS8987 -
    // this overload has no Configure() surface either way (ref/out/in fallback), but its dispatch
    // body's own signature still has to compile.
    [Fact]
    public Task OverloadWithScopedRefParameter_GeneratesDoubleWithMatchingRefSafetyContract() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Seek(scoped ref System.Span<int> value);

                        void Seek(int value);
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IRepository repository)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            repository.Configure().Seek();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0030",
            TestContext.Current.CancellationToken);

    // Codex review, PR #88: object.Equals(object) is inapplicable to a zero-argument or two-plus-
    // argument call, so an overloaded Equals(params int[] values) keeps a reachable spelling via
    // Configure().Equals() or Configure().Equals(a, b) even though a literal one-argument call still
    // collides - it's not genuinely a one-required-argument overload, same "params keeps the surface"
    // reasoning Amendment 12 already applies to ToString/GetHashCode/GetType.
    [Fact]
    public Task OverloadedEqualsWithParamsArray_DoesNotCollideWithObjectMember() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    bool Equals(params int[] values);

                    bool Equals(long a, long b, long c);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().Equals().Returns(true);
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: the dynamic-canonicalization branch wrote a bare "object", while a real
    // `object` parameter goes through the named-type path and writes "global::System.Object" - two
    // different strings for what should be the same identity, so IA.M(dynamic)/IB.M(object) were
    // never actually recognized as a diamond collision despite the ADR's explicit claim that they
    // would be.
    [Fact]
    public Task DiamondInheritedDynamicAndObjectOverload_ReportsScopedOverloadedDiagnostic() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        void M(dynamic value);
                    }

                    public interface IBaseB
                    {
                        void M(object value);
                    }

                    public interface IRepository : IBaseA, IBaseB;

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

    // Codex review, PR #88: a by-value ref-like parameter gets no implicit scoping by default -
    // `scoped Span<int> value` must restate "scoped" on the explicit implementation to match the
    // interface's ref-safety contract, verified with a real compile spike
    // (ScopedKind.ScopedValue only when "scoped" is actually written in source). Unlike ref/out/in,
    // this parameter shape is RefKind.None, so the member keeps its own Configure() surface entirely
    // - proving the fix applies to the has-configuration-surface dispatch body too, not just the
    // ref/out/in fallback body.
    [Fact]
    public Task OverloadWithScopedByValueRefLikeParameter_GeneratesDoubleWithMatchingRefSafetyContract() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void Seek(scoped System.Span<int> value);

                    void Seek(int value);
                }

                public sealed class OrderService
                {
                    public OrderService(IRepository repository) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IRepository repository)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        repository.Configure().Seek(1);
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);
}
