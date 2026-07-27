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
    public Task DelegateType_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public delegate void Handler(string message);

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Not abstract, and Roslyn exposes a synthetic (object, IntPtr)
                            // constructor on it - `new Handler(arg1, arg2)` isn't legal delegate
                            // construction syntax, so this must be rejected before it reaches
                            // codegen rather than emitting uncompilable generated code.
                            var handler = composer.Create<TestNamespace.Handler>();
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

    [Fact]
    public Task ConstructedGenericTypes_GenerateDistinctPlans() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Box<T>
                {
                    public Box(T value) { Value = value; }
                    public T Value { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // Both closed forms produce a plan class named BoxCompositionPlan - only
                        // legal because each generated plan class is file-scoped.
                        var intBox = composer.Create<Box<int>>();
                        var stringBox = composer.Create<Box<string>>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NamespaceShadowedByTypeName_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace Acme;

                // Shadows the `Acme` namespace segment inside this scope - an unqualified
                // `Acme.Customer` in generated code would bind through this type and fail;
                // only `global::Acme.Customer` is unambiguous.
                public sealed class Acme
                {
                    public Acme() { }
                }

                public sealed class Customer
                {
                    public Customer(string name) { Name = name; }
                    public string Name { get; }
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
    public Task RefConstructorParameter_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Widget
                    {
                        public Widget(ref int count) { count++; }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var widget = composer.Create<TestNamespace.Widget>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0004",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task RefLikeConstructorParameter_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    using System;

                    namespace TestNamespace;

                    public sealed class Widget
                    {
                        public Widget(Span<int> values) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            var widget = composer.Create<TestNamespace.Widget>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0004",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task OpenGenericTypeArgument_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Box<T>
                    {
                        public Box(T value) { Value = value; }
                        public T Value { get; }
                    }

                    public static class EntryPoint
                    {
                        // `T` here is the method's own type parameter, not a real closed type -
                        // `composer.Create<Box<T>>()` must be rejected rather than emitting a
                        // generated plan that references an out-of-scope `T`.
                        public static void Run<T>()
                        {
                            var composer = Compono.Composer.Create();
                            var box = composer.Create<TestNamespace.Box<T>>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0005",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task DirectMethodTypeParameter_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Widget
                    {
                        public Widget() { }
                    }

                    public static class EntryPoint
                    {
                        // `composer.Create<T>()` where `T` is directly the method's own type
                        // parameter: the type argument is an ITypeParameterSymbol, not even an
                        // INamedTypeSymbol - a narrower symbol shape than Box<T>'s constructed
                        // generic type, and must be rejected the same way.
                        public static void Run<T>()
                        {
                            var composer = Compono.Composer.Create();
                            var value = composer.Create<T>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0005",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NestedTypeInGenericContainer_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Outer<T>
                    {
                        // Not itself generic - its unresolved `T` lives on ContainingType, not on
                        // its own (empty) TypeArguments, a shape the type-parameter walk has to
                        // check separately from Box<T>'s case above.
                        public sealed class Inner
                        {
                            public Inner() { }
                        }
                    }

                    public static class EntryPoint
                    {
                        public static void Run<T>()
                        {
                            var composer = Compono.Composer.Create();
                            var value = composer.Create<TestNamespace.Outer<T>.Inner>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0005",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task ArrayTypeArgument_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Customer
                    {
                        public Customer() { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Array types have no constructors for ConstructorSelector to select -
                            // discovery must diagnose this itself instead of silently generating
                            // nothing and failing only at runtime.
                            var customers = composer.Create<Customer[]>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0006",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task RequiredMemberWithoutSetsRequiredMembers_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Customer
                    {
                        // Phase 0 only ever emits bare `new Customer(...)` - no object initializer -
                        // so a required member with no [SetsRequiredMembers] on the constructor
                        // would make the generated call CS9035. Required-member composition is
                        // deferred to a later milestone.
                        public required string Name { get; init; }
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
            expectedDiagnosticId: "CMP0007",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NestedComposableProperty_GeneratesPlansForBothTypes() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Address
                {
                    public Address(string city) { City = city; }
                    public string City { get; }
                }

                public sealed class Customer
                {
                    public Customer(string name, Address homeAddress)
                    {
                        Name = name;
                        HomeAddress = homeAddress;
                    }

                    public string Name { get; }
                    public Address HomeAddress { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // Address has no local Create<Address>() call site of its own - its plan
                        // only exists because Customer's constructor closure walk (Phase 1) reaches
                        // it through the HomeAddress parameter.
                        var customer = composer.Create<TestNamespace.Customer>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NestedComposablePropertySharedAcrossParents_GeneratesSinglePlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Address
                {
                    public Address(string city) { City = city; }
                    public string City { get; }
                }

                public sealed class Customer
                {
                    public Customer(Address homeAddress) { HomeAddress = homeAddress; }
                    public Address HomeAddress { get; }
                }

                public sealed class Order
                {
                    public Order(Address shipToAddress) { ShipToAddress = shipToAddress; }
                    public Address ShipToAddress { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // Address is reachable from both Customer and Order - it must still only
                        // get exactly one generated plan (AddSource throws on a duplicate hint).
                        var customer = composer.Create<TestNamespace.Customer>();
                        var order = composer.Create<TestNamespace.Order>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LeafParameterType_LeftAsResolveCallNotRecursedInto() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                using System;

                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(string name, DateTime birthDate)
                    {
                        Name = name;
                        BirthDate = birthDate;
                    }

                    public string Name { get; }
                    public DateTime BirthDate { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // DateTime is a recognized BCL value type (LeafTypeClassifier) - left as a
                        // bare context.Resolve<DateTime>() call, never run through constructor
                        // selection (which would otherwise be ambiguous - DateTime has several
                        // constructors - and wrongly fail this compile).
                        var customer = composer.Create<TestNamespace.Customer>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NestedTypeFailsConstructorSelection_ReportsDiagnosticAtOriginalCallSite() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Address
                    {
                        public Address(string city) { }
                        public Address(string city, string state) { }
                    }

                    public sealed class Customer
                    {
                        public Customer(string name, Address homeAddress)
                        {
                            Name = name;
                            HomeAddress = homeAddress;
                        }

                        public string Name { get; }
                        public Address HomeAddress { get; }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Address is eligible for generated composition (a concrete type) but
                            // has two accessible constructors - ambiguous. Must be diagnosed at
                            // this Create<Customer>() call site (naming the HomeAddress path), not
                            // silently left as context.Resolve<Address>(), which would hide an
                            // invalid generated graph behind a runtime failure instead.
                            var customer = composer.Create<TestNamespace.Customer>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0001",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task SanitizationCollidingNames_GenerateDistinctHints() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Foo<T>
                {
                    public Foo(T value) { Value = value; }
                    public T Value { get; }
                }

                // Sanitizes to the same readable hint text as Foo<int> (`Foo_int_`) - only the
                // stable-hash suffix keeps the two AddSource hint names distinct.
                public sealed class Foo_int_
                {
                    public Foo_int_(string name) { Name = name; }
                    public string Name { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var generic = composer.Create<Foo<int>>();
                        var literal = composer.Create<Foo_int_>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);
}
