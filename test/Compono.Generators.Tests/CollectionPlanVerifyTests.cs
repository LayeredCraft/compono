namespace Compono.Generators.Tests;

public sealed class CollectionPlanVerifyTests
{
    [Fact]
    public Task ListRootType_GeneratesOnlyACollectionPlan_NoCompositionPlanForTheElementType() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // A collection root (List<int>) must be recorded as a collection - exactly
                        // like a collection reached as a nested member - not walked as an ordinary
                        // composable type. Regression coverage for the PR #11 review finding: before
                        // the fix, this failed to compile with CMP0001 (List<int> has 3 accessible
                        // constructors).
                        var value = composer.Create<System.Collections.Generic.List<int>>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ArrayRootType_GeneratesCollectionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Address
                {
                    public Address(string street) { Street = street; }
                    public string Street { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // A rank-1 array root must reach collection discovery the same as any other
                        // collection root, not the INamedTypeSymbol check (arrays are never
                        // INamedTypeSymbol). Regression coverage for the PR #11 review finding: before
                        // the fix, this failed to compile with CMP0006 (array roots were rejected
                        // before collection classification ever ran).
                        var addresses = composer.Create<Address[]>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ArrayConstructorParameter_GeneratesCollectionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Address
                {
                    public Address(string street) { Street = street; }
                    public string Street { get; }
                }

                public sealed class Customer
                {
                    public Customer(Address[] addresses) { Addresses = addresses; }
                    public Address[] Addresses { get; }
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
    public Task ListConstructorParameter_GeneratesCollectionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Address
                {
                    public Address(string street) { Street = street; }
                    public string Street { get; }
                }

                public sealed class Customer
                {
                    public Customer(System.Collections.Generic.List<Address> addresses) { Addresses = addresses; }
                    public System.Collections.Generic.List<Address> Addresses { get; }
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
    public Task ReadOnlyListConstructorParameter_GeneratesCollectionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Address
                {
                    public Address(string street) { Street = street; }
                    public string Street { get; }
                }

                public sealed class Customer
                {
                    public Customer(System.Collections.Generic.IReadOnlyList<Address> addresses) { Addresses = addresses; }
                    public System.Collections.Generic.IReadOnlyList<Address> Addresses { get; }
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
    public Task HashSetConstructorParameter_GeneratesCollectionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(System.Collections.Generic.HashSet<int> favoriteNumbers) { FavoriteNumbers = favoriteNumbers; }
                    public System.Collections.Generic.HashSet<int> FavoriteNumbers { get; }
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
    public Task DictionaryConstructorParameter_GeneratesCollectionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(System.Collections.Generic.Dictionary<System.Guid, string> tags) { Tags = tags; }
                    public System.Collections.Generic.Dictionary<System.Guid, string> Tags { get; }
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
    public Task SameClosedListReachedWithDifferentElementNullability_ReportsConflictDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class NotNullableHolder
                    {
                        public NotNullableHolder(System.Collections.Generic.List<string> items) { Items = items; }
                        public System.Collections.Generic.List<string> Items { get; }
                    }

                    public sealed class NullableHolder
                    {
                        public NullableHolder(System.Collections.Generic.List<string?> items) { Items = items; }
                        public System.Collections.Generic.List<string?> Items { get; }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Both List<string> and List<string?> reach the identical closed
                            // collection type (element nullability is erased from the emitted type
                            // name), but disagree on ElementIsNullable - no single collection plan is
                            // correct for both, so this must be reported (CMP0011) rather than
                            // silently picking whichever discovery happened to come first.
                            var notNullable = composer.Create<TestNamespace.NotNullableHolder>();
                            var nullable = composer.Create<TestNamespace.NullableHolder>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0011",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NestedListOfList_GeneratesCollectionPlansForBothShapes() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(System.Collections.Generic.List<System.Collections.Generic.List<int>> groups) { Groups = groups; }
                    public System.Collections.Generic.List<System.Collections.Generic.List<int>> Groups { get; }
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
}
