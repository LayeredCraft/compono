namespace Compono.Generators.Tests;

public sealed class CollectionPlanVerifyTests
{
    [Fact]
    public Task DynamicHashSetElement_ReportsDiagnostic_NotACompilerErrorInGeneratedCode() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // dynamic is legal as a HashSet<T> type argument, but a generated collection
                            // plan can't implement ICompositionPlan<HashSet<dynamic>> at all (CS1966,
                            // "cannot implement a dynamic interface") - CollectionWellKnownTypes must
                            // reject dynamic element/key types entirely, not just fix what the plan body
                            // emits, or this breaks the generated file with a compiler error instead of a
                            // Compono diagnostic. Regression coverage for the PR #11 review finding,
                            // confirmed directly before fixing (the pre-fix failure was CS1966/CS1962 in
                            // generated code, not a Compono diagnostic at all).
                            var value = composer.Create<System.Collections.Generic.HashSet<dynamic>>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0001",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task DynamicDictionaryKey_ReportsDiagnostic_NotACompilerErrorInGeneratedCode() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var value = composer.Create<System.Collections.Generic.Dictionary<dynamic, int>>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0001",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task PrivateElementTypeInCollectionRoot_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Container
                    {
                        private enum PrivateEnum { A, B, C }

                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // The call site is inside PrivateEnum's own containing type, so this
                            // compiles fine at the call site itself - but a generated collection plan
                            // is always a top-level type outside any containing type, so it can never
                            // reference a private/protected element type. Before the fix, this failed
                            // to compile with CS0122 inside the generated file instead of a Compono
                            // diagnostic. Regression coverage for the PR #11 review finding, confirmed
                            // directly before fixing.
                            var value = composer.Create<System.Collections.Generic.List<PrivateEnum>>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0012",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task SameInaccessibleCollectionFromTwoCallSites_ReportsCMP0012NotCMP0011() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Container
                    {
                        private enum PrivateEnum { A, B, C }

                        public static void RunOne()
                        {
                            var composer = Compono.Composer.Create();
                            var value = composer.Create<System.Collections.Generic.List<PrivateEnum>>();
                        }

                        public static void RunTwo()
                        {
                            var composer = Compono.Composer.Create();
                            var value = composer.Create<System.Collections.Generic.List<PrivateEnum>>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0012",
            TestContext.Current.CancellationToken);

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
