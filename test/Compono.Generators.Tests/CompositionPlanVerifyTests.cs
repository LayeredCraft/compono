namespace Compono.Generators.Tests;

public sealed class CompositionPlanVerifyTests
{
    [Fact]
    public Task SingleConstructor_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(string firstName, string lastName)
                    {
                        FirstName = firstName;
                        LastName = lastName;
                    }

                    public string FirstName { get; }
                    public string LastName { get; }
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AmbiguousConstructor_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Customer
                    {
                        public Customer(string firstName) { }
                        public Customer(string firstName, string lastName) { }
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
            expectedDiagnosticId: "CMP0001",
            TestContext.Current.CancellationToken);
}
