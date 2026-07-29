namespace Compono.Generators.Tests;

public sealed class CollectionPlanVerifyTests
{
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
