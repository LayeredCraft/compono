namespace Compono.Generators.Tests;

/// <summary>
/// Regression coverage for the review feedback on PR #4 - see
/// <c>docs/plans/0001-milestone-1-source-generation-foundation.md</c>'s Notes.
/// </summary>
public sealed class CodexFeedbackRegressionTests
{
    [Fact]
    public Task GlobalNamespaceType_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                public sealed class Customer
                {
                    public Customer(string firstName)
                    {
                        FirstName = firstName;
                    }

                    public string FirstName { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var customer = composer.Create<Customer>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AbstractType_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public abstract class Customer
                    {
                        // Legal on an abstract type - only ever called from a derived class's
                        // constructor - but `new Customer(...)` is never legal regardless.
                        public Customer(string firstName) { }
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
            },
            expectedDiagnosticId: "CMP0003",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task SameSimpleNameInDifferentNamespaces_GeneratesBothPlans() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace Sales
                {
                    public sealed class Customer
                    {
                        public Customer(string name) { Name = name; }
                        public string Name { get; }
                    }
                }

                namespace Support
                {
                    public sealed class Customer
                    {
                        public Customer(string name) { Name = name; }
                        public string Name { get; }
                    }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var salesCustomer = composer.Create<Sales.Customer>();
                        var supportCustomer = composer.Create<Support.Customer>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ConditionalAccessInvocation_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(string firstName) { FirstName = firstName; }
                    public string FirstName { get; }
                }

                public static class EntryPoint
                {
                    public static void Run(Compono.Composer? composer)
                    {
                        var customer = composer?.Create<TestNamespace.Customer>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InternalConstructorInReferencedAssembly_ReportsDiagnostic()
    {
        var libraryReference = GeneratorTestHelpers.CompileLibrary(
            """
            namespace LibraryNamespace;

            public sealed class LibraryType
            {
                // Public type, but the only constructor is internal - a generated plan living in a
                // *different* assembly (the one under test below) can't legally call this without an
                // InternalsVisibleTo grant, which this library deliberately doesn't have.
                internal LibraryType(string value) { }
            }
            """,
            "RegressionTestLibrary");

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var value = composer.Create<LibraryNamespace.LibraryType>();
                        }
                    }
                    """,
                ExtraReferences = [libraryReference],
            },
            expectedDiagnosticId: "CMP0002",
            TestContext.Current.CancellationToken);
    }
}
