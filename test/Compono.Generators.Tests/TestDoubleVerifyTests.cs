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

    // ADR-0045: a non-nullable-reference-return member with no deterministic default no longer
    // rejects its whole interface - it generates as configuration-required instead, reported via
    // the interface-scoped, count-only CMP0032 (Amendment 1), not the whole-interface CMP0025.
    [Fact]
    public Task NonNullableReferenceReturn_GeneratesConfigurationRequiredMember() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
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
            "CMP0032",
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

    // ADR-0046: a static abstract member declared on a BASE interface in the closure can already
    // be resolved by a MORE-DERIVED interface in the same closure providing a concrete
    // implementation - C#'s own "most specific implementation" rule for static interface members
    // (verified via Roslyn's ITypeSymbol.FindImplementationForInterfaceMember). This is the actual
    // Gate-B shape: AWSSDK's IAmazonS3 re-implements its base IAmazonService.CreateDefaultClientConfig()
    // concretely, even though IAmazonService itself only declares it abstract - the analyzer's old
    // per-interface closure walk was inspecting IAmazonService's raw declaration in isolation and
    // incorrectly treating an already-resolved member as an unimplemented requirement. This must
    // generate a fully normal, completely unaffected double (no new diagnostic, no stub) - every
    // instance member works exactly as it would if IBase's static abstract member didn't exist.
    [Fact]
    public Task StaticAbstractMethodResolvedByDerivedInterface_GeneratesUnaffectedDouble() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IBase
                {
                    static abstract int CreateDefault();
                }

                public interface IRepository : IBase
                {
                    static int IBase.CreateDefault() => 42;

                    string? Name { get; }
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
                        repository.Configure().Name().Returns("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #99: a resolved static abstract member reached via the closure walk is
    // IsAbstract: true on its raw (unresolved-looking) symbol, so without excluding it from
    // collision preprocessing it would still enter `eligibleCandidates` - and its canonical
    // signature (TestDoubleOverloadIdentity.CanonicalSignatureFor) only encodes arity/parameter
    // types, never return type or static-ness, so a zero-parameter resolved static member sharing
    // a *name* with a zero-parameter *instance* member of the same interface would be misclassified
    // as a diamond-colliding identity - silently withholding the real instance member's
    // Configure()/Verify() surface, and (combined with ADR-0045) rejecting the whole interface
    // outright once that instance member also has no deterministic default, since it would then
    // look like a combined-shape (no surface + no default) rather than a real configuration-
    // required member. This must generate as configuration-required (CMP0032), not CMP0025 -
    // proving the instance member's real, working surface survives the name collision.
    [Fact]
    public Task StaticAbstractMemberResolvedByDerivedInterface_DoesNotCollideWithSameNamedInstanceMember() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBase
                    {
                        static abstract string Name();
                    }

                    public interface IRepository : IBase
                    {
                        static string IBase.Name() => "static-resolved";

                        string Name();
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
                            repository.Configure().Name().Returns("value");
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0032",
            TestContext.Current.CancellationToken);

    // Same rule, a static abstract property - the general Roslyn/interface-inheritance behavior,
    // not a method-specific special case.
    [Fact]
    public Task StaticAbstractPropertyResolvedByDerivedInterface_GeneratesUnaffectedDouble() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IBase
                {
                    static abstract int DefaultTimeout { get; }
                }

                public interface IRepository : IBase
                {
                    static int IBase.DefaultTimeout => 42;

                    string? Name { get; }
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
                        repository.Configure().Name().Returns("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Same rule, a static abstract operator.
    [Fact]
    public Task StaticAbstractOperatorResolvedByDerivedInterface_GeneratesUnaffectedDouble() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IBase
                {
                    static abstract IBase operator +(IBase left, IBase right);
                }

                public interface IRepository : IBase
                {
                    static IBase IBase.operator +(IBase left, IBase right) => left;

                    string? Name { get; }
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
                        repository.Configure().Name().Returns("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // A genuinely unresolved static abstract method (no override anywhere in the closure) stays
    // whole-interface rejected (CMP0021), unchanged from before ADR-0046 - the second finding
    // ADR-0046 records: C# itself forbids using an interface with a genuinely unresolved static
    // abstract member as a generic type argument at all (CS8920, verified with a real compile
    // spike against Compono's own unconstrained ICompositionContext.Resolve<TValue>()), so such an
    // interface was never actually composable through Compono's generic composition path -
    // generating any kind of stub for it would be unreachable, dead machinery.
    [Fact]
    public Task StaticAbstractMethod_ReportsUnsupportedMemberKindDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        static abstract int CreateDefault();
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

    // ADR-0045's "Async returns" section: ReturnConfig<T> is already generic over the member's
    // real declared return type (Task<T>/ValueTask<T> itself), so a Task<T>/ValueTask<T> member
    // with a no-default T needs no separate implementation - same configuration-required
    // treatment as a synchronous member, same CMP0032.
    [Fact]
    public Task NonNullableValueTaskOfReference_GeneratesConfigurationRequiredMember() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
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
            "CMP0032",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task MultidimensionalArrayReturn_GeneratesConfigurationRequiredMember() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
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
            "CMP0032",
            TestContext.Current.CancellationToken);

    // ADR-0045: a property with no deterministic default gets the same configuration-required
    // treatment as a method - IOptions<T>.Value/ILambdaContext.AwsRequestId-shaped.
    [Fact]
    public Task NonNullablePropertyReturn_GeneratesConfigurationRequiredMember() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        string Name { get; }
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
            "CMP0032",
            TestContext.Current.CancellationToken);

    // ADR-0045 Amendment 1: an IAmazonS3-shaped interface with several configuration-required
    // members still emits exactly one CMP0032 with the correct count, not one per member - the
    // snapshotted message text itself is the assertion that the count is right.
    [Fact]
    public Task MultipleConfigurationRequiredMembers_ReportsSingleCmp0032WithCorrectCount() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        string GetName();
                        string Description { get; }
                        int GetCount();
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
            "CMP0032",
            TestContext.Current.CancellationToken);

    // ADR-0045 Amendment 3: a member combining "no deterministic default" with "no configuration
    // surface for an unrelated reason" keeps the unchanged whole-interface CMP0025 rejection - a
    // ref/out/in overload is one of the four combined shapes. An unrelated, genuinely
    // configuration-required member (Description) on the same interface confirms the combined-
    // shape gate doesn't accidentally leak this member into CMP0032's count instead.
    [Fact]
    public Task RefOutInOverloadWithNoDefaultReturn_StillReportsUnsupportedReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        string GetName(out int code);
                        string GetName();
                        string Description { get; }
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

    // ADR-0045 Amendment 3: a diamond-colliding identity with a no-default return type is another
    // of the four combined shapes - it keeps the unchanged whole-interface CMP0025 rejection.
    [Fact]
    public Task DiamondCollisionWithNoDefaultReturn_StillReportsUnsupportedReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBase1
                    {
                        string GetName();
                    }

                    public interface IBase2
                    {
                        string GetName();
                    }

                    public interface IRepository : IBase1, IBase2
                    {
                        string Description { get; }
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

    // ADR-0045 Amendment 3: a same-named zero-argument-extension collision with a no-default
    // return type is the third combined shape - a naive gate checking only diamond/ref-out-in/
    // object-collision would wrongly mark this configuration-required despite its Configure()
    // surface being withheld, generating a member that throws unconditionally forever.
    [Fact]
    public Task ZeroArgumentExtensionCollisionWithNoDefaultReturn_StillReportsUnsupportedReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        string GetName { get; }
                        string GetName();
                        string Description { get; }
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

    // ADR-0045 Amendment 6 (withdrawing Amendment 5's incorrect "falls through to CMP0024, which
    // is fine" reasoning): a method-shaped object-member collision with a no-default return type
    // is the fourth combined shape, method-specific - it must keep reporting CMP0025, not fall
    // through to the unrelated object-collision check and get relabeled CMP0024.
    [Fact]
    public Task ObjectMemberCollisionMethodWithNoDefaultReturn_ReportsUnsupportedReturnShapeNotObjectCollision() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        string ToString();
                        string Description { get; }
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
    // collapsing to whichever is discovered first. Here the two disagree on whether the out
    // parameter of TryGet(out T, ...) even has a deterministic default at all (ADR-0045 doesn't
    // change out-parameter default-lookup behavior - only a member's own return type), so the
    // merge step's own conflict handling (mirroring DiscoveredTypeInfo's CMP0010 pattern)
    // surfaces the real per-location CMP0026 failure deterministically, regardless of discovery
    // order, rather than an order-dependent silent pick. A same-named sibling (Get()) is required
    // so the out-parameter member takes the overload-set-internal fallback path (Amendment 5) that
    // still hard-fails when its own out-parameter type has no deterministic default, rather than
    // the plain return-type check this ADR itself relaxed.
    [Fact]
    public Task SameInterfaceDiscoveredWithDisagreeingNullability_ReportsRealFailureDeterministically() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IProvider<T>
                    {
                        void Get(out T value);
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
            "CMP0026",
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
    public Task VerifyNamedMember_ReportsCollisionDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Verify();
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

    // Codex review, PR #88: IMethodSymbol.Parameters excludes the __arglist sentinel entirely for a
    // C-style variable-argument method, so it would otherwise be silently treated as an ordinary
    // fixed-arity overload and get an explicit implementation with the wrong signature (CS0535 - it
    // doesn't actually implement the vararg interface member). Verified with a real compile spike
    // (IsVararg is true, Parameters.Length doesn't include the sentinel).
    [Fact]
    public Task VarargOverload_ReportsUnsupportedMemberKindDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void M(int x, __arglist);

                        void M(string x);
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

    // Codex review, PR #88: [UnscopedRef] on an out parameter is the *inverse* of out's normal
    // implicit scoping - verified with a real compile spike that it reports ScopedKind.None instead
    // of the usual ScopedRef. A plain generated "out" parameter would still be implicitly scoped,
    // disagreeing with the interface's explicitly unscoped contract (CS8987) - the attribute itself
    // has to be restated on the explicit implementation.
    [Fact]
    public Task OverloadWithUnscopedRefOutParameter_GeneratesDoubleWithMatchingRefSafetyContract() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        void Seek([System.Diagnostics.CodeAnalysis.UnscopedRef] out int value);

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

    // Codex review, PR #88: M(int value = 0) is callable as M() on the real interface, but the
    // generated discriminator extension emitted it as a required parameter, so Configure().M()
    // failed to compile - optionality wasn't preserved, unlike params (Amendment 12/16). The
    // default value is now mirrored onto the extension too.
    [Fact]
    public Task OverloadedMemberWithOptionalParameter_ConfigureIsCallableWithoutTheOptionalArgument() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void M(int value = 0);

                    void M(string value);
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
                        repository.Configure().M();
                        repository.Configure().M(5);
                        repository.Configure().M("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: an enum-typed default is exposed as its boxed *underlying* numeric value
    // (e.g. Mode.Active surfaces as the boxed int 1) - emitting that raw primitive directly
    // (`Mode mode = 1`) fails consumer compilation (CS1750, no standard conversion from int to Mode).
    // Verified with a real compile spike that a cast to the enum type is a legal constant
    // default-parameter-value expression.
    [Fact]
    public Task OverloadedMemberWithNonZeroEnumDefault_GeneratesDoubleWithTypeCompatibleDefault() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public enum Mode
                {
                    None = 0,
                    Active = 1,
                }

                public interface IRepository
                {
                    void M(Mode mode = Mode.Active);

                    void M(string value);
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
                        repository.Configure().M();
                        repository.Configure().M(Mode.None);
                        repository.Configure().M("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: Equals(int value = 0) is not genuinely a one-required-argument overload,
    // same reasoning as the params case - Configure().Equals() reaches the generated extension fine
    // (object.Equals(object) is inapplicable to zero arguments), so this overload keeps a reachable
    // spelling and its surface, unlike a genuinely-required-one-argument Equals(int).
    [Fact]
    public Task OverloadedEqualsWithOptionalParameter_DoesNotCollideWithObjectMember() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    bool Equals(int value = 0);

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

    // Codex review, PR #88: a property named "ToString" and a same-named zero-parameter method
    // "ToString()" inherited from different base interfaces are already correctly withheld by the
    // zero-argument-extension collision check (CMP0029) - the property branch's own separate
    // object-collision check must skip itself in that case too, or it redundantly rejects the whole
    // interface (CMP0024) even though the double would otherwise compile fine.
    [Fact]
    public Task PropertyAndMethodBothNamedToStringCollideOnlyViaZeroArgumentCheck() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        string? ToString { get; }
                    }

                    public interface IBaseB
                    {
                        string? ToString();
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

    // Codex review, PR #88: for a nullable-enum-typed parameter (Mode? mode = Mode.Active),
    // parameter.Type.TypeKind is Struct (Nullable<T> itself is a struct), not Enum - the unguarded
    // enum check missed this shape entirely and still emitted the raw underlying integer
    // (`Mode? mode = 1`, CS1750). Verified with a real compile spike that a cast to the *non-nullable*
    // enum type ((Mode)1) is what's needed - it converts implicitly to Mode? in this context.
    [Fact]
    public Task OverloadedMemberWithNonZeroNullableEnumDefault_GeneratesDoubleWithTypeCompatibleDefault() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public enum Mode
                {
                    None = 0,
                    Active = 1,
                }

                public interface IRepository
                {
                    void M(Mode? mode = Mode.Active);

                    void M(string value);
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
                        repository.Configure().M();
                        repository.Configure().M(Mode.None);
                        repository.Configure().M("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: PLAN-0044 Phase 0's own task text admitted this canonicalization case
    // (nullable-reference annotation excluded from identity, ADR-0044 Amendment 6 Finding 14) had no
    // dedicated diamond test - only the recursive AppendCanonical code path exercised it indirectly.
    // Nullable-reference annotation is compiler-tracked metadata, not part of a method's real
    // signature, so IA.M(string) and IB.M(string?) are the same overload identity - a genuine diamond
    // collision, not two independent overloads.
    [Fact]
    public Task DiamondInheritedNullableAnnotationOverload_ReportsScopedOverloadedDiagnostic() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        void M(string value);
                    }

                    public interface IBaseB
                    {
                        void M(string? value);
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

    // Codex review, PR #88: PLAN-0044 Phase 0's own task text admitted this canonicalization case
    // (named-tuple element names excluded from identity, ADR-0044 Amendment 8 Finding 19) had no
    // dedicated diamond test either. A named tuple's element names aren't part of a method's real
    // signature - only its underlying ValueTuple<...> shape is - so IA.M((int X, int Y)) and
    // IB.M((int A, int B)) are the same overload identity.
    [Fact]
    public Task DiamondInheritedTupleElementNameOverload_ReportsScopedOverloadedDiagnostic() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        void M((int X, int Y) value);
                    }

                    public interface IBaseB
                    {
                        void M((int A, int B) value);
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

    // Codex review, PR #88: a parameter can be optional purely via [Optional] (common on metadata
    // imported from COM-flavored or VB-compiled assemblies) with no explicit default value at all -
    // verified with a real compile spike that IsOptional is true while HasExplicitDefaultValue stays
    // false, and that the real interface still allows the argument to be omitted entirely. The
    // generated discriminator extension needs to stay reachable the same way.
    [Fact]
    public Task OverloadedMemberWithAttributeOnlyOptionalParameter_ConfigureIsCallableWithoutTheOptionalArgument() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    void M([System.Runtime.InteropServices.Optional] int value);

                    void M(string value);
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
                        repository.Configure().M();
                        repository.Configure().M(5);
                        repository.Configure().M("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #88: the Equals-collision check's own "optional single parameter keeps the
    // surface" exception needs the same IsOptional (not HasExplicitDefaultValue) fix.
    [Fact]
    public Task OverloadedEqualsWithAttributeOnlyOptionalParameter_DoesNotCollideWithObjectMember() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IRepository
                {
                    bool Equals([System.Runtime.InteropServices.Optional] int value);

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

    // PLAN-0044 Phase 1: the ADR's own motivating shape - ILogger<T>'s Log<TState> (return
    // independent of TState) and BeginScope<TState> (return independent of TState, constrained
    // `notnull`) - neither returns TState or anything built from it, so both are fully supported.
    // The explicit interface implementation stays generic (type parameters copied, never
    // constraints - CS0460); the configuration extension is ordinary, non-generic, member-level
    // (Requirement 2's own rule: the slot type never depends on the method's own type parameter).
    [Fact]
    public Task GenericMethodsIndependentOfTypeParameter_GeneratesDoubleWithNonGenericConfigurationExtensions() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface ILoggerLike
                {
                    void Log<TState>(int logLevel, TState state, System.Exception? exception);

                    System.IDisposable? BeginScope<TState>(TState state) where TState : notnull;
                }

                public sealed class OrderService
                {
                    public OrderService(ILoggerLike logger) { }
                }

                public static class EntryPoint
                {
                    public static void Run(ILoggerLike logger)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        logger.Configure().Log().Throws(new System.InvalidOperationException());
                        logger.Configure().BeginScope().Returns(null);
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // A multi-type-parameter generic method, one of them constrained - proves constraint
    // propagation isn't hardcoded to a single type parameter, and that a constrained type parameter
    // stays fully supported (Amendment 9 withdrew any special-casing beyond the genuinely-
    // unconstrained-T?-parameter exclusion).
    [Fact]
    public Task MultiTypeParameterGenericMethod_GeneratesDoubleWithConstrainedExplicitImplementation() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IMultiMapper
                {
                    void Map<TKey, TValue>(TKey key, TValue value) where TKey : notnull;
                }

                public sealed class OrderService
                {
                    public OrderService(IMultiMapper mapper) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IMultiMapper mapper)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        mapper.Configure().Map().Throws(new System.InvalidOperationException());
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // ADR-0044 Requirement 2 / Amendment 13, ADR-0049: a generic method whose return type references
    // its own type parameter has no constructible body at any granularity - diagnosed and excluded,
    // whole interface falls back to the runtime-provider path - UNLESS it matches ADR-0049's
    // narrower closed-instantiation-eligible shape (exactly T, or the sole type argument of
    // Task<T>/ValueTask<T>, for a single method-type-parameter - see the
    // ClosedInstantiationEligible* tests below). T nested deeper (here, inside a List<T> inside a
    // Task<T>) is exactly the shape ADR-0049's own "Scope boundary" left out of scope, unevidenced -
    // this fixture was the original PLAN-0044-era `T Create<T>()` case, moved to this deeper-nesting
    // shape once ADR-0049 made the plain `T Create<T>()` case itself succeed (see
    // ClosedInstantiationEligibleSoloMember_GeneratesDoubleWithGenericConfigurationExtension below).
    [Fact]
    public Task GenericMethodWithReturnTypeNestedDeeperThanOwnTypeParameter_ReportsUnsupportedGenericReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactory
                    {
                        System.Threading.Tasks.Task<System.Collections.Generic.List<T>> CreateMany<T>();
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0031",
            TestContext.Current.CancellationToken);

    // ADR-0049's "Scope boundary": a self-referencing return with more than one method-type-parameter
    // stays whole-interface-rejected, unevidenced - IConversationalContextManager's real
    // GetContextDataAsync<T> has exactly one.
    [Fact]
    public Task MultiTypeParameterGenericMethodWithReturnTypeDependentOnOwnTypeParameter_ReportsUnsupportedGenericReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactory
                    {
                        TResult Get<TKey, TResult>(TKey key);
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0031",
            TestContext.Current.CancellationToken);

    // Codex review, PR #107: a type parameter declared `where T : allows ref struct` (C# 13's
    // ref-like-capable anti-constraint) otherwise matches ADR-0049's closed-instantiation-eligible
    // return shape (T itself), but the generated state class's ReturnConfig<T>/ReturnConfigBuilder<T>
    // fields (Compono's existing, unmodified runtime types) declare no `allows ref struct` on their
    // own T - a real caller closing this method's T over an actual ref struct would fail to compile
    // with CS9244 inside generated code instead of getting the clean CMP0031 whole-interface-fallback
    // diagnostic every other unsupported shape gets. Excluded at the source
    // (IsClosedInstantiationEligibleReturnShape), so it falls back to whole-interface rejection
    // exactly like every other no-constructible-body shape.
    [Fact]
    public Task GenericMethodWithRefStructCapableTypeParameterReturningItself_ReportsUnsupportedGenericReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactory
                    {
                        T Create<T>() where T : allows ref struct;
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0031",
            TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 4): the BCL Task<T>/ValueTask<T> shape check only compared
    // ContainingNamespace + simple Name, never ContainingType - a consumer's own nested type also
    // named "Task<T>", declared inside some other type living in the "System.Threading.Tasks"
    // namespace, shares both of those with the real BCL Task<T> (a namespace is the same regardless
    // of nesting depth) and was misclassified as the supported shape. Downstream, TestDoubleDefaults
    // would then emit a real global::System.Threading.Tasks.Task.FromResult<T>(...) default-value
    // expression for a member whose actual declared return type is this unrelated nested type - a
    // real type-mismatch compile error in generated code. Fixed by also requiring ContainingType is
    // null (the real BCL Task<T>/ValueTask<T> are always top-level). VerifyFailure proves the fake
    // nested Task<T> now correctly falls back to whole-interface CMP0031, not broken generated code.
    [Fact]
    public Task GenericMethodReturningNestedTypeNamedLikeBclTask_ReportsUnsupportedGenericReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace System.Threading.Tasks
                    {
                        public sealed class Container
                        {
                            public sealed class Task<T>
                            {
                            }
                        }
                    }

                    namespace TestNamespace
                    {
                        public interface IFactory
                        {
                            System.Threading.Tasks.Container.Task<T?> Get<T>() where T : class;
                        }

                        public sealed class OrderService
                        {
                            public OrderService(IFactory factory) { }
                        }

                        public static class EntryPoint
                        {
                            public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0031",
            TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 5, finding 1, P1): TestDoubleDefaults' ValueTask<T> default-value
    // expression used `new ValueTask<TResult>(inner)`, and ValueTask<TResult> has TWO constructors -
    // (TResult result) and (Task<TResult> task) - both of which the bare `default` literal converts
    // to with no better-conversion tie-breaker, a real CS0121 ambiguous-call compiler error. This was
    // a latent, pre-existing bug in TestDoubleDefaults.cs itself (reachable by ANY defaultable
    // ValueTask<T> member, not just a closed-instantiation-eligible one), never actually exercised by
    // an existing test until ADR-0049 made ValueTask<T>/ValueTask<T?> the return type of a
    // self-referencing generic member for the first time. Fixed by switching to the unambiguous
    // static ValueTask.FromResult<TResult>(TResult) factory (mirrors the Task<T> branch's own,
    // already-unambiguous Task.FromResult<T>(...) shape). Verify proves the real generated code
    // compiles - the actual check that would have caught this.
    [Fact]
    public Task ClosedInstantiationEligibleMemberReturningNullableValueTask_GeneratesDoubleThatCompiles() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IFactory
                {
                    System.Threading.Tasks.ValueTask<T?> Get<T>() where T : class;
                }

                public sealed class OrderService
                {
                    public OrderService(IFactory factory) { }
                }

                public static class EntryPoint
                {
                    public static async System.Threading.Tasks.Task Run(IFactory factory)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        factory.Configure().Get<string>().Returns(new System.Threading.Tasks.ValueTask<string?>("Ada"));

                        var value = await factory.Get<string>();

                        factory.Verify().Get<string>().Once();
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 5, finding 2): round 4's "ContainingType is null" fix only ruled
    // out a NESTED impostor sharing the BCL Task<T>'s namespace/simple-name - it didn't cover a
    // genuinely TOP-LEVEL consumer type reopening the exact same "System.Threading.Tasks" namespace
    // with their own "Task<T>" (legal C# - a source-declared type is even permitted to shadow an
    // imported one of the identical fully-qualified name, CS0436, a warning not an error).
    // ContainingType is null for a top-level type regardless of which assembly/source declares it, so
    // that check alone can't distinguish the two. An interim fix comparing symbol identity via the
    // simpler, singular Compilation.GetTypeByMetadataName looked plausible but was PROVEN WRONG by
    // this very test: GetTypeByMetadataName follows the same "source wins" rule as ordinary C# name
    // resolution (CS0436) rather than returning null for the ambiguity, so it silently returned the
    // consumer's own shadow type - the fix appeared to do nothing, and this test (run as Verify(),
    // not VerifyFailure(), specifically to force a real recompile) caught a genuine CS0029 in the
    // generated code before the real fix was found. TaskWellKnownTypes now resolves the real,
    // externally-referenced BCL type via GetTypesByMetadataName (plural) filtered to exclude any
    // candidate declared in the current compilation's own assembly - the interface's own declared
    // return type still resolves to the shadow (per the same source-wins rule), so the identity
    // comparison now correctly fails, and this member falls back to whole-interface CMP0031.
    [Fact]
    public Task GenericMethodReturningTopLevelTypeShadowingBclTaskNamespace_ReportsUnsupportedGenericReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace System.Threading.Tasks
                    {
                        public sealed class Task<T>
                        {
                        }
                    }

                    namespace TestNamespace
                    {
                        public interface IFactory
                        {
                            System.Threading.Tasks.Task<T?> Get<T>() where T : class;
                        }

                        public sealed class OrderService
                        {
                            public OrderService(IFactory factory) { }
                        }

                        public static class EntryPoint
                        {
                            public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0031",
            TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 6, finding 1): the generated state class's own type parameter can't
    // be renamed away from the real method's type parameter identifier - it's baked verbatim into
    // every pre-rendered type-string this candidate's slot/parameter types already use (Roslyn's own
    // ToDisplayString, not a name this code chooses). So when the consumer's own type parameter is
    // literally named the same as this file's derived state-class name ("__Get_State" for a member
    // named "Get"), the generated `class __Get_State<__Get_State>` is CS0694 ("type parameter has the
    // same name as the type"), a real compiler error. Fixed by reserving the candidate's own type
    // parameter name as an already-taken literal field name before the derived-name collision check
    // runs, routing this through the same collision detection every other derived name in this file
    // already uses - falls back to whole-interface CMP0031, not a new exclusion mechanism.
    [Fact]
    public Task ClosedInstantiationEligibleMemberWithTypeParameterNamedLikeGeneratedStateClass_ReportsUnsupportedGenericReturnShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactory
                    {
                        __Get_State Get<__Get_State>();
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0031",
            TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 7, finding 1): round 6's own fix for the CS0694 self-collision
    // above was itself a real bug - it reserved the candidate's type parameter name into the SHARED,
    // interface-wide usedFieldNames set, so an entirely UNRELATED method whose own type parameter
    // merely happens to share that string (not because it collides with anything, just coincidence)
    // wrongly poisoned the first method too. Here, `Get<T>()` derives the state-class name
    // "__Get_State", and the unrelated `Other<__Get_State>()` has its own type parameter literally
    // named "__Get_State" - the two are never actually in the same declaration and never actually
    // collide (Other's own derived state-class name is "__Other_State", not "__Get_State"). Fixed by
    // making the check strictly self-scoped: compare a candidate's own type parameter name only
    // against its OWN derived state-class name, never reserved into the shared pool. Verify (full
    // recompile) proves both members generate real, independently-working surfaces.
    [Fact]
    public Task ClosedInstantiationEligibleMembersWithUnrelatedTypeParameterNameCollision_GeneratesBothMembersCleanly() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactory
                    {
                        T Get<T>();

                        __Get_State Other<__Get_State>();
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IFactory factory)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            factory.Configure().Get<string>().Returns("Ada");
                            factory.Configure().Other<string>().Returns("Bob");

                            var get = factory.Get<string>();
                            var other = factory.Other<string>();

                            factory.Verify().Get<string>().Once();
                            factory.Verify().Other<string>().Once();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0032",
            TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 7, finding 2): TestDoubleDefaults.TryGetDefaultExpression's own
    // Task/ValueTask identification had the identical namespace/simple-name-only imprecision the
    // eligibility check in TestDoubleAnalyzer already had fixed (rounds 4-5) - and unlike that check,
    // this one is reached by ANY member whose declared return type is Task/Task<T>/ValueTask<T>, not
    // just a closed-instantiation-eligible one, so it was never actually gated behind ADR-0049 at all.
    // A consumer's own top-level ValueTask<T> reopening the BCL namespace, returned from an entirely
    // ordinary (non-generic) member, would be misidentified as the real BCL type, and the generated
    // default-value expression would return the real BCL ValueTask<T> for a member whose actual
    // declared return type is the consumer's shadow type - a genuine CS0029. Fixed by verifying
    // symbol identity here too (TaskWellKnownTypes), mirroring the analyzer's own fix. Verify (full
    // recompile, not VerifyFailure) proves the generated code actually compiles.
    [Fact]
    public Task OrdinaryMemberReturningShadowedValueTaskOfT_GeneratesDoubleThatCompiles() =>
        GeneratorTestHelpers.Verify(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace System.Threading.Tasks
                    {
                        public readonly struct ValueTask<T>
                        {
                            public ValueTask(T result) { }
                        }
                    }

                    namespace TestNamespace
                    {
                        public interface IRepository
                        {
                            System.Threading.Tasks.ValueTask<string?> GetNameAsync();
                        }

                        public sealed class OrderService
                        {
                            public OrderService(IRepository repository) { }
                        }

                        public static class EntryPoint
                        {
                            public static void Run() => Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            TestContext.Current.CancellationToken);

    // ADR-0049: the narrowest evidenced closed-instantiation-eligible shape - a solo (non-overloaded)
    // generic method returning exactly its own unconstrained type parameter T. Unconstrained T has no
    // deterministic default, so this member is also configuration-required (ADR-0045, CMP0032) - both
    // capabilities compose. Proves the generated Configure<T>()/Verify<T>() surface is generic, that
    // two different closed T's are independently configurable/verifiable on the same double instance,
    // and that the member is genuinely callable (not just diagnosed away) once configured.
    [Fact]
    public Task ClosedInstantiationEligibleSoloMember_GeneratesDoubleWithGenericConfigurationExtension() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactory
                    {
                        T Create<T>();
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IFactory factory)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            factory.Configure().Create<string>().Returns("widget");
                            factory.Configure().Create<int>().Returns(42);
                            var widget = factory.Create<string>();
                            var count = factory.Create<int>();
                            factory.Verify().Create<string>().Once();
                            factory.Verify().Create<int>().Once();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0032",
            TestContext.Current.CancellationToken);

    // ADR-0049: the real evidenced shape - a solo generic method returning Task<T?>, with real
    // (non-T) parameters that get ADR-0048's Match<TParam>-wrapped argument-aware Configure<T>()/
    // Verify<T>() surface scoped per closed T, mirroring IConversationalContextManager.GetContextDataAsync<T>
    // exactly. T? is a real nullable reference constraint (where T : class), so this member has a
    // deterministic default (null) and is NOT configuration-required - both ADR-0045 branches are
    // exercised across this test and the solo-unconstrained-T test above.
    [Fact]
    public Task ClosedInstantiationEligibleSoloMemberWithRealParameters_GeneratesDoubleWithArgumentAwareGenericConfiguration() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IContextManager
                {
                    System.Threading.Tasks.Task<T?> GetContextDataAsync<T>(string key) where T : class;
                }

                public sealed class OrderService
                {
                    public OrderService(IContextManager contextManager) { }
                }

                public static class EntryPoint
                {
                    public static async System.Threading.Tasks.Task Run(IContextManager contextManager)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        contextManager.Configure().GetContextDataAsync<string>(Compono.Match.Is<string>(k => k == "user"))
                            .Returns(System.Threading.Tasks.Task.FromResult<string?>("Ada"));

                        var value = await contextManager.GetContextDataAsync<string>("user");
                        var unconfigured = await contextManager.GetContextDataAsync<object>("other");

                        contextManager.Verify().GetContextDataAsync<string>(Compono.Match.Any<string>()).Once();
                        contextManager.Verify().GetContextDataAsync<object>(Compono.Match.Any<string>()).Once();
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // ADR-0049 / PLAN-0049 (revised eligibility): an overloaded closed-instantiation-eligible member
    // is eligible, not excluded - it reuses ADR-0044 Requirement 1's existing overload-discriminator
    // machinery (real, un-wrapped parameter types, per-overload suffix) rather than ADR-0048's
    // Match<TParam> surface, the same disposition every other overloaded member already has. Proves
    // the per-overload bucket-by-closed-T mechanism composes with per-overload discriminators, and
    // that the *same* closed T used on both overloads keeps fully independent state (mirrors the
    // ADR-0049 design spike's own proof).
    [Fact]
    public Task OverloadedClosedInstantiationEligibleMember_GeneratesDoubleWithPerOverloadGenericConfiguration() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IContextManager
                {
                    System.Threading.Tasks.Task<T?> GetDataAsync<T>(string id) where T : class;

                    System.Threading.Tasks.Task<T?> GetDataAsync<T>(string id, int version) where T : class;
                }

                public sealed class OrderService
                {
                    public OrderService(IContextManager contextManager) { }
                }

                public static class EntryPoint
                {
                    public static async System.Threading.Tasks.Task Run(IContextManager contextManager)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        contextManager.Configure().GetDataAsync<string>("user").Returns(System.Threading.Tasks.Task.FromResult<string?>("v1"));
                        contextManager.Configure().GetDataAsync<string>("user", 2).Returns(System.Threading.Tasks.Task.FromResult<string?>("v2"));

                        var first = await contextManager.GetDataAsync<string>("user");
                        var second = await contextManager.GetDataAsync<string>("user", 2);

                        contextManager.Verify().GetDataAsync<string>("user").Once();
                        contextManager.Verify().GetDataAsync<string>("user", 2).Once();
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 2): a closed-instantiation-shaped member can match ADR-0049's
    // return-shape check (so it isn't whole-interface-rejected) yet still end up with NO configuration
    // surface for an unrelated reason - here, ADR-0044 Amendment 5's ref/out/in overload-set-internal
    // fallback (a ref parameter, with a sibling overload to preserve). That member still gets a real
    // explicit interface implementation (a deterministic-default-only fallback body, no Configure()/
    // Verify()) - and since its return type is `Task<T?>` on a `where T : class`-constrained method,
    // that fallback implementation hits the exact same CS9334/CS0453 cascade the round-1 fix
    // (IsClosedInstantiationEligible-only stripping) didn't cover, because it was gated on
    // HasConfigurationSurface too. Fixed by keying the nullable-stripping fix off the new, surface-
    // independent IsClosedInstantiationEligibleShape flag instead. VerifyWithInfoDiagnostic proves both
    // halves: the ref-parameter overload falls back with an informational CMP0030 (not a whole-
    // interface CMP0026/CMP0031), AND the generated code actually compiles - which is exactly the
    // check that would have caught the CS9334 cascade if this fix were missing.
    [Fact]
    public Task ClosedInstantiationShapedRefParameterOverloadFallback_CompilesWithoutConfigurationSurface() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactory
                    {
                        System.Threading.Tasks.Task<T?> Get<T>(ref int x) where T : class;

                        System.Threading.Tasks.Task<T?> Get<T>(string key) where T : class;
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static async System.Threading.Tasks.Task Run(IFactory factory)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            factory.Configure().Get<string>("user").Returns(System.Threading.Tasks.Task.FromResult<string?>("Ada"));

                            var value = await factory.Get<string>("user");

                            factory.Verify().Get<string>("user").Once();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0030",
            TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 3, finding A): the OLD ADR-0048 derived-auxiliary-name pre-pass
    // (`_calls`/`_lock`/`_m_{param}`) didn't exclude closed-instantiation-eligible candidates, so it
    // wrongly reserved `__Get_calls` on behalf of `Get<T>(string key)` even though that member never
    // emits an outer `_calls` field at all (its own Calls list lives inside `__Get_State<T>` instead).
    // An unrelated sibling literally named `Get_calls` then collided with that phantom reservation,
    // stripping `Get<T>`'s configuration surface via `derivedNameCollisionMembers` and rejecting the
    // WHOLE interface (it has no deterministic default). Fixed by excluding closed-instantiation
    // candidates from that old pre-pass (they're reserved through their own, differently-named
    // `_State`/`_buckets`/`_Bucket` pass instead). Plain `Verify()` proves the whole interface
    // generates cleanly and BOTH members are independently configurable - no false collision at all.
    [Fact]
    public Task ClosedInstantiationEligibleMemberWithLiterallyCollidingSiblingName_GeneratesBothMembersCleanly() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IFactory
                {
                    System.Threading.Tasks.Task<T?> Get<T>(string key) where T : class;

                    string? Get_calls { get; }
                }

                public sealed class OrderService
                {
                    public OrderService(IFactory factory) { }
                }

                public static class EntryPoint
                {
                    public static async System.Threading.Tasks.Task Run(IFactory factory)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        factory.Configure().Get<string>(Compono.Match.Any<string>()).Returns(System.Threading.Tasks.Task.FromResult<string?>("Ada"));
                        factory.Configure().Get_calls().Returns("sibling-value");

                        var value = await factory.Get<string>("user");
                        var sibling = factory.Get_calls;

                        factory.Verify().Get<string>(Compono.Match.Any<string>()).Once();
                        factory.Verify().Get_calls().Once();
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #107 (round 3, finding B): the zeroArgExtensionSharers collision-detection
    // pre-pass still assumed the pre-ADR-0049 rule that a NON-overloaded generic method's extension
    // is always non-generic (Requirement 2) - so a solo closed-instantiation-eligible member's real
    // generic arity was computed as 0, the same as an unrelated zero-arg, non-generic sibling (a
    // property of the same name inherited from a different base interface). That false collision
    // stripped the closed-instantiation member's configuration surface, and since it's an
    // unconstrained `T Get<T>()` with no deterministic default, rejected the WHOLE interface. Fixed
    // by mirroring the new rule (a closed-instantiation-eligible candidate's real extension IS
    // generic even when solo) in this pre-pass's own arity/genericity computation. Plain `Verify()`
    // proves both differently-shaped `Get` members (one generic, one not, from separate base
    // interfaces) generate real, independently-working surfaces - the actual emitted
    // `Get<T>(this Double)`/`Get(this Double)` signatures are genuinely distinct, never a real
    // CS0111 collision in the first place.
    [Fact]
    public Task ClosedInstantiationEligibleSoloMemberWithZeroArgSiblingFromDifferentBaseInterface_GeneratesBothMembersCleanly() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IFactoryA
                    {
                        T Get<T>();
                    }

                    public interface IFactoryB
                    {
                        string? Get { get; }
                    }

                    public interface IFactory : IFactoryA, IFactoryB
                    {
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IFactory factory) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run(IFactory factory)
                        {
                            Compono.Composer.Create().Create<TestNamespace.OrderService>();
                            factory.Configure().Get<string>().Returns("Ada");
                            factory.Configure().Get().Returns("prop-value");

                            var generic = factory.Get<string>();
                            var property = ((IFactoryB)factory).Get;

                            factory.Verify().Get<string>().Once();
                            factory.Verify().Get().Once();
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0032",
            TestContext.Current.CancellationToken);

    // Amendment 5 Finding 11, moved here from Phase 0 now that generic methods exist to construct
    // the case with: type-parameter *names* aren't part of a method's identity, only their ordinal
    // position - IA.M<T>(T) and IB.M<U>(U) are the same signature and must still trigger the
    // diamond-collision check even though the discriminator hash canonicalizes each by ordinal
    // rather than declared name.
    [Fact]
    public Task InheritedGenericOverloadsWithDifferentlyNamedTypeParameters_ReportsScopedOverloadedDiagnostic() =>
        GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBaseA
                    {
                        void M<T>(T value);
                    }

                    public interface IBaseB
                    {
                        void M<U>(U value);
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

    // ADR-0044 Amendment 1: the combined overload+generic interaction. Process<T>(T)/
    // Process<T>(IEnumerable<T>) share a name (Requirement 1's per-overload discriminator applies)
    // and are each generic (Requirement 2's constraint-propagation applies) - the configuration
    // extension for each becomes generic itself, reusing the overload's own type parameter purely
    // for compile-time overload selection, while the backing slot stays fixed per Requirement 2.
    [Fact]
    public Task OverloadedGenericMethod_GeneratesDoubleWithPerOverloadGenericConfigurationExtensions() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IWidget
                {
                    void Process<T>(T value);

                    void Process<T>(System.Collections.Generic.IEnumerable<T> values);
                }

                public sealed class OrderService
                {
                    public OrderService(IWidget widget) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IWidget widget)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        widget.Configure().Process(0).Throws(new System.InvalidOperationException());
                        widget.Configure().Process(System.Array.Empty<string>()).Returns(default);
                        widget.Configure().Process<string>(System.Array.Empty<string>()).Returns(default);
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Amendment 14: a solo generic member with zero required value parameters is never an implicit
    // zero-argument candidate (nothing for the compiler to infer its type parameter from) - a bare
    // `Configure()` call is inapplicable to the real, generic `Configure<T>()` interface member, so
    // member lookup falls through to extension search and reaches the bridge normally.
    [Fact]
    public Task SoloGenericConfigureMember_DoesNotCollideWithBridge() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IThing
                {
                    void Configure<T>();
                }

                public sealed class OrderService
                {
                    public OrderService(IThing thing) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IThing thing)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        // Configure<T>() is solo (not overloaded), so its own generated extension
                        // stays non-generic/zero-argument per Requirement 2 - calling it with no
                        // explicit type argument is the point: it proves the real interface member
                        // never shadowed the Configure() bridge it shares a name with.
                        thing.Configure().Configure().Throws(new System.InvalidOperationException());
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Amendment 16: Amendment 14's object-collision escape hatch is gated on the *generated
    // extension's* own genericity, not the real member's - a solo generic ToString<T>() gets a
    // non-generic, zero-argument extension (Requirement 2), which has no escape hatch at all and
    // collides with object.ToString() exactly like a non-generic solo ToString() would. Explicitly
    // called out in Amendment 16 as its own required test.
    [Fact]
    public Task SoloGenericToStringMember_StillCollidesWithObjectMemberDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IThing
                    {
                        string? ToString<T>();
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IThing thing) { }
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

    // Amendment 14's actual finding, corrected by Amendment 16: an *overloaded* generic ToString<T>()
    // gets a generic discriminator extension too (Amendment 1) - an explicit-type-argument call can
    // never match the non-generic object.ToString(), so the real escape hatch exists here and no
    // collision is reported.
    [Fact]
    public Task OverloadedGenericToStringMember_DoesNotCollideWithObjectMember() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IThing
                {
                    string? ToString<T>();

                    string? ToString<T>(T value);
                }

                public sealed class OrderService
                {
                    public OrderService(IThing thing) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IThing thing)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        thing.Configure().ToString<int>().Returns("value");
                        thing.Configure().ToString(0).Returns("value");
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Amendment 6 Finding 15: an unconstrained type parameter used as `T?` in a parameter can
    // require a C# 9+ "default constraint" on the explicit implementation to disambiguate its
    // inherited, oblivious reference-or-value-type meaning - diagnosed and excluded rather than
    // guessed at.
    [Fact]
    public Task GenericMethodWithUnconstrainedNullableTypeParameterParameter_ReportsUnsupportedParameterShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IHandler
                    {
                        void Handle<T>(T? value);
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IHandler handler) { }
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

    // Amendment 9 (withdrawing Amendment 8's narrower constrained-only exception): a *constrained*
    // `T?` (`where T : class`) is excluded exactly like the unconstrained case - two review rounds
    // disagreed about the exact permitted constraint-restatement keyword set even for the
    // constrained case, so this ADR never attempts to guess it. Codex review, PR #89.
    [Fact]
    public Task GenericMethodWithConstrainedNullableTypeParameterParameter_ReportsUnsupportedParameterShapeDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IHandler
                    {
                        void Handle<T>(T? value) where T : class;
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IHandler handler) { }
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

    // Codex review, PR #89: an overloaded generic member constrained with C# 13's `allows ref
    // struct` anti-constraint must carry it onto the generated extension too - omitting it would
    // silently narrow what the real interface member permits (a caller closing T over Span<int>
    // would compile against the real member but fail against the generated extension, CS8377).
    [Fact]
    public Task OverloadedGenericMethodWithAllowsRefStructConstraint_GeneratesDoubleWithAntiConstraintPreserved() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IWidget
                {
                    void Process<T>(T value) where T : allows ref struct;

                    void Process<T>(string label, T value) where T : allows ref struct;
                }

                public sealed class OrderService
                {
                    public OrderService(IWidget widget) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IWidget widget)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        widget.Configure().Process(0).Throws(new System.InvalidOperationException());
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #89: two overloaded, zero-value-parameter generic methods of *different*
    // generic arity (M<T>()/M<T, U>()) emit distinguishable M<T>(this Double)/M<T, U>(this Double)
    // extensions - not a real CS0111 collision - so the zero-argument-extension-collision check
    // (CMP0029) must fold generic arity into its own grouping, not just real value-parameter count.
    [Fact]
    public Task OverloadedGenericMethodsOfDifferentArity_DoNotCollideAsZeroArgumentExtensions() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IThing
                {
                    void M<T>();

                    void M<T, U>();
                }

                public sealed class OrderService
                {
                    public OrderService(IThing thing) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IThing thing)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        thing.Configure().M<int>().Throws(new System.InvalidOperationException("single"));
                        thing.Configure().M<int, string>().Throws(new System.InvalidOperationException("double"));
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);

    // Codex review, PR #89: SafeReceiverName only checked real value-parameter names for a
    // collision, but an overloaded generic member's extension declares its own type parameters in
    // the same identifier space as the receiver parameter - a type parameter literally named
    // "__self" must also be avoided, or the generated extension declares both a type parameter and
    // a receiver parameter named "__self" (CS0412).
    [Fact]
    public Task OverloadedGenericMethodWithTypeParameterNamedDunderSelf_GeneratesDoubleWithDistinctReceiverName() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IWidget
                {
                    void Process<__self>(__self value);

                    void Process<__self>(System.Collections.Generic.IEnumerable<__self> values);
                }

                public sealed class OrderService
                {
                    public OrderService(IWidget widget) { }
                }

                public static class EntryPoint
                {
                    public static void Run(IWidget widget)
                    {
                        Compono.Composer.Create().Create<TestNamespace.OrderService>();
                        widget.Configure().Process(0).Throws(new System.InvalidOperationException());
                    }
                }
                """,
            MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
        }, TestContext.Current.CancellationToken);
}
