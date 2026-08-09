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
    public Task AbstractRootType_GeneratesNoPlan_ProviderResolvedAtRuntime() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
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
                        // PLAN-0005 Phase 2 (ADR-0024 root-check loosening): an abstract root used to
                        // be rejected with CMP0003 - reachable only from a derived class's own
                        // constructor, which the generator has no way to invoke. Milestone 5's public
                        // provider extensibility gives it a real off-ramp instead: left as a bare
                        // context.Resolve<Customer>() call for a registered
                        // ICompositionValueProvider (e.g. Compono.NSubstitute's NSubstituteProvider)
                        // to satisfy at runtime, exactly like an abstract *member* already could.
                        var customer = composer.Create<TestNamespace.Customer>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InterfaceRootType_GeneratesNoPlan_ProviderResolvedAtRuntime() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public interface IHandler
                {
                    void Handle(string message);
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // An interface root reports IsAbstract: true in Roslyn, same as an abstract
                        // class - covered by the same root-check loosening as
                        // AbstractRootType_GeneratesNoPlan_ProviderResolvedAtRuntime above, kept as
                        // its own test since an interface (not just an abstract class) is exactly
                        // this plan's own Goal-section shape (UseNSubstitute()'s IOrderRepository).
                        var handler = composer.Create<TestNamespace.IHandler>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DelegateRootType_GeneratesNoPlan_ProviderResolvedAtRuntime() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public delegate void Handler(string message);

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // Not abstract, but still a leaf per LeafTypeClassifier.IsProviderResolved
                        // (TypeKind.Delegate) - previously excluded at root position only
                        // (CMP0003), now left as a bare context.Resolve<Handler>() call for a
                        // registered provider (e.g. NSubstituteProvider) to satisfy, matching a
                        // delegate member's existing treatment.
                        var handler = composer.Create<TestNamespace.Handler>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

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
    public Task MultiDimensionalArrayTypeArgument_ReportsDiagnostic() =>
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
                            // A rank-1 array root (Customer[]) is a supported collection shape (see
                            // ArrayRootType_GeneratesCollectionPlan) - only rank>1 arrays remain
                            // genuinely unsupported (CollectionWellKnownTypes only classifies
                            // IArrayTypeSymbol { Rank: 1 }), and still need diagnosing here instead of
                            // silently generating nothing and failing only at runtime.
                            var customers = composer.Create<Customer[,]>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0006",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task PointerElementArrayTypeArgument_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    public static class EntryPoint
                    {
                        public static unsafe void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // int*[] is legal C# (unlike List<int*>, which the C# compiler itself
                            // rejects as a generic type argument) - CollectionWellKnownTypes must not
                            // classify a pointer/function-pointer element array as a collection shape,
                            // or a generated collection plan would try to emit context.Resolve<int*>(),
                            // a compiler error in generated code rather than a Compono diagnostic.
                            // Regression coverage for the PR #11 review finding.
                            var value = composer.Create<int*[]>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0006",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task RequiredProperty_EmitsObjectInitializer() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(int id) { Id = id; }

                    public int Id { get; }

                    // No [SetsRequiredMembers] on the constructor - Phase 3 emits an
                    // object-initializer assignment for this after the constructor call, rather
                    // than rejecting the type outright (Phase 0's behavior).
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RequiredField_EmitsObjectInitializer() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer() { }

                    // `required` applies to fields too, not just properties - collected and
                    // validated the same way.
                    public required string Name;
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
    public Task SetsRequiredMembersConstructor_NoInitializerEmitted() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                using System.Diagnostics.CodeAnalysis;

                namespace TestNamespace;

                public sealed class Customer
                {
                    // The constructor already satisfies every required member itself - no
                    // object-initializer assignment should be emitted, just the bare constructor
                    // call (Phase 0's shape).
                    [SetsRequiredMembers]
                    public Customer(string name) { Name = name; }

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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RequiredMemberRefLikeType_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    using System;

                    namespace TestNamespace;

                    public sealed class Widget
                    {
                        public Widget() { }

                        // A ref-like member type can't be Resolve<T>()'s generic type argument
                        // (CS0306/CS0611) - same reasoning as a ref-like constructor parameter
                        // (CMP0004), narrowed onto required-member validation instead (CMP0007).
                        public required Span<int> Values { get; init; }
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
            expectedDiagnosticId: "CMP0007",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NullableConstructorParameter_PassesNullableNullabilityArgument() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    // A generic type argument alone can't distinguish string from string? at
                    // runtime (nullable-reference annotations are erased) - the generated
                    // Resolve<T>() call must pass the annotation explicitly instead.
                    public Customer(string name, string? nickname)
                    {
                        Name = name;
                        Nickname = nickname;
                    }

                    public string Name { get; }
                    public string? Nickname { get; }
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
    public Task NullableRequiredMember_PassesNullableNullabilityArgument() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer() { }

                    public required string Name { get; init; }

                    // Nullability applies to required-member Resolve<T>() calls the same way it
                    // does to constructor parameters.
                    public required string? Nickname { get; init; }
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
    public Task RequiredComposableProperty_GeneratesPlansForBothTypes() =>
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
                    public Customer() { }

                    // A required member's type walks the same transitive-closure/leaf-type rules
                    // a constructor parameter's does - Address gets its own generated plan here
                    // even though it's only reachable through a required property.
                    public required Address HomeAddress { get; init; }
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
    public Task DateOnlyAndTimeOnlyParameters_LeftAsResolveCallsNotRecursedInto() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Reservation
                {
                    public Reservation(System.DateOnly checkIn, System.TimeOnly checkInTime)
                    {
                        CheckIn = checkIn;
                        CheckInTime = checkInTime;
                    }

                    public System.DateOnly CheckIn { get; }
                    public System.TimeOnly CheckInTime { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // DateOnly/TimeOnly are recognized BCL value types (LeafTypeClassifier) -
                        // left as bare context.Resolve<T>() calls, never run through constructor
                        // selection (which would otherwise be ambiguous and wrongly fail this
                        // compile - regression coverage for the PR #11 review finding).
                        var reservation = composer.Create<TestNamespace.Reservation>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PrimitiveRootType_GeneratesNoPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // int is provider-resolved - Composer.Create<int>() must generate no plan at
                        // all (stage 7's built-in provider satisfies it directly at runtime), and must
                        // not reach constructor selection - regression coverage for the PR #11 review
                        // finding that the root type skipped this check entirely (either failing to
                        // compile for types like Guid/string with multiple constructors, or silently
                        // generating a dead plan that always produced default(T) for types like int).
                        var value = composer.Create<int>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EnumRootType_GeneratesNoPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var value = composer.Create<System.DayOfWeek>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NullableValueTypeRootType_GeneratesNoPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // Nullable<T> (int?) is provider-resolved - Composer.Create<int?>() must
                        // generate no plan and not reach constructor selection. Regression coverage
                        // for a real bug caught during PR #11's required manual consuming-project
                        // verification (a genuinely separate throwaway console project, not this test
                        // harness): LeafTypeClassifier never had a Nullable<T> case at all, so any
                        // nullable value type (root or member) reached ConstructorSelector, which sees
                        // Nullable<T>'s two accessible constructors (the parameterless one and
                        // Nullable(T value)) and reports CMP0001 ambiguous construction - a real defect
                        // no generator snapshot test had ever exercised.
                        var value = composer.Create<int?>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NullableValueTypeConstructorParameter_LeftAsResolveCallNotRecursedInto() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public enum Priority { Low, Medium, High }

                public sealed class Order
                {
                    public Order(int? quantity, Priority? priority)
                    {
                        Quantity = quantity;
                        Priority = priority;
                    }

                    public int? Quantity { get; }
                    public Priority? Priority { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // Regression coverage for the same PR #11 manual-verification finding as
                        // NullableValueTypeRootType_GeneratesNoPlan, for the member case specifically
                        // (a nullable primitive and a nullable enum, both as constructor parameters).
                        var order = composer.Create<TestNamespace.Order>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NullableCustomStructRootType_ReportsDiagnostic_NotASilentRuntimeFailure() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public struct Money
                    {
                        public Money(decimal amount) { Amount = amount; }
                        public decimal Amount { get; }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // NullableValueProvider only composes a Nullable<T> whose underlying type
                            // is a primitive/enum/recognized BCL value type - a custom struct's
                            // Nullable<T> (Money?) used to be classified as a leaf regardless of the
                            // underlying type, so this compiled with zero diagnostics and always threw
                            // at runtime ("no registration, provider, or generated plan could satisfy
                            // the request"), confirmed directly before fixing. Now it falls through to
                            // ordinary composable-type handling like any other concrete type;
                            // Nullable<T> has two accessible constructors to Roslyn's symbol model (the
                            // implicit parameterless one and `Nullable(T value)`), so this correctly
                            // reports the same CMP0001 ambiguous-construction diagnostic any other
                            // multi-constructor type gets, at compile time, naming the actual call site.
                            var value = composer.Create<Money?>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0001",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NullableCustomStructConstructorParameter_ReportsDiagnostic_NotASilentRuntimeFailure() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public struct Money
                    {
                        public Money(decimal amount) { Amount = amount; }
                        public decimal Amount { get; }
                    }

                    public sealed class Order
                    {
                        public Order(Money? total) { Total = total; }
                        public Money? Total { get; }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Same gap as NullableCustomStructRootType_ReportsDiagnostic, for the
                            // member case: unlike a member of an ordinary provider-resolved type (left
                            // silently as a bare Resolve<T>() call for a possible future
                            // registration/provider to claim), a member of type Money? could never be
                            // satisfied by anything - NullableValueProvider always declines it, and no
                            // generated plan is ever produced for Nullable<Money> unless it's recursed
                            // into like this fix now does.
                            var order = composer.Create<TestNamespace.Order>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0001",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task BclValueTypeRootWithMultipleConstructors_GeneratesNoPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        // Guid has 8 accessible constructors - before the PR #11 fix, this reached
                        // constructor selection and failed to compile with CMP0001 (ambiguous
                        // construction), even though Guid is a recognized BCL value type with a
                        // built-in runtime provider.
                        var value = composer.Create<System.Guid>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ConcreteRootTypeWithNoAccessibleConstructor_StillReportsDiagnostic_AfterRootProviderCheck() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed class Customer
                    {
                        private Customer() { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Regression guard for PLAN-0005 Phase 2's root-check loosening
                            // (LeafTypeClassifier.IsRuntimeProviderResolved now equals
                            // IsProviderResolved): only interface/abstract-class/delegate roots
                            // became provider-resolved leaves - a concrete, non-abstract, non-delegate
                            // type is still routed into constructor selection exactly as before, and a
                            // sealed type with no accessible constructor must still get CMP0002 at
                            // compile time, not silently compile into a call that can only fail at
                            // runtime.
                            var customer = composer.Create<TestNamespace.Customer>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0002",
            TestContext.Current.CancellationToken);

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

    [Fact]
    public Task ComposableAttributeOnType_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                // No local Create<T>() call site anywhere in the compilation - [Composable] is the
                // only reason this type gets a generated plan (docs/adr/0004's escape hatch, Phase 2).
                [Compono.Composable]
                public sealed class Customer
                {
                    public Customer(string firstName)
                    {
                        FirstName = firstName;
                    }

                    public string FirstName { get; }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposableAttributeWithTypeArgumentOnType_RequestsNamedTypeInstead() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                // [Composable(typeof(Other))] on Customer requests a plan for Other, not Customer -
                // same shape as the assembly-level form, just anchored to an existing declaration.
                [Compono.Composable(typeof(Widget))]
                public sealed class Customer
                {
                    public Customer(string firstName)
                    {
                        FirstName = firstName;
                    }

                    public string FirstName { get; }
                }

                public sealed class Widget
                {
                    public Widget() { }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposableAttributeAtAssemblyLevel_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                [assembly: Compono.Composable(typeof(TestNamespace.Customer))]

                namespace TestNamespace;

                // Customer can't be annotated directly here (standing in for a type this
                // compilation doesn't own, e.g. one from a referenced assembly) - the
                // assembly-level form is the only discovery path that reaches it.
                public sealed class Customer
                {
                    public Customer(string firstName)
                    {
                        FirstName = firstName;
                    }

                    public string FirstName { get; }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposableAttributeAtAssemblyLevelMissingTypeArgument_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    [assembly: Compono.Composable]

                    namespace TestNamespace;

                    public sealed class Customer
                    {
                        public Customer(string firstName)
                        {
                            FirstName = firstName;
                        }

                        public string FirstName { get; }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0008",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposableAttributeAndCreateCallSite_DedupeToSinglePlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                // Discovered by both paths at once - call-site discovery and [Composable] must
                // agree on a single DiscoveredTypeInfo, or Roslyn's AddSource throws on the
                // resulting duplicate hint name.
                [Compono.Composable]
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
    public Task ComposableAttributeAtAssemblyLevelViaAlias_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                // IsAssemblyCandidate must not filter by literal syntax name - an aliased reference
                // to Compono.ComposableAttribute has a syntax name of "Marker", not "Composable", so
                // a name-based syntax filter would silently drop this valid usage before the
                // semantic check (which resolves the alias correctly) ever runs.
                using Marker = Compono.ComposableAttribute;

                [assembly: Marker(typeof(TestNamespace.Customer))]

                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(string firstName)
                    {
                        FirstName = firstName;
                    }

                    public string FirstName { get; }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposableAttributeOnInterface_GeneratesNoPlan_ProviderResolvedAtRuntime() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                // Interfaces are a legal [Composable] target (AttributeTargets.Interface) - an
                // interface reports IsAbstract: true in Roslyn, so [Composable]'s eager-warmup walk
                // classifies it the same way an ordinary Composer.Create<IWidget>() root now does
                // (PLAN-0005 Phase 2's root-check loosening): a provider-resolved leaf with nothing to
                // eagerly generate, not a CMP0003 diagnostic.
                [Compono.Composable]
                public interface IWidget
                {
                    string Name { get; }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposableAttributeOnRefStruct_ReportsDiagnostic() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    // A ref-like constructor PARAMETER is already rejected by ConstructorSelector
                    // (CMP0004) - but nothing stopped the requested type ITSELF from being ref-like,
                    // even though ICompositionPlan<T>/PlanCache<T> both declare a bare `T` with no
                    // `allows ref struct` constraint and can't be closed over one.
                    [Compono.Composable]
                    public ref struct RefWidget
                    {
                        public RefWidget() { }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0009",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposableAttributeAtAssemblyLevelWithParenthesizedTypeof_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                // Legal, redundant parens around the typeof(...) argument - the extraction must not
                // require the argument expression to be exactly TypeOfExpressionSyntax, since a
                // ParenthesizedExpressionSyntax wrapping it is just as valid an argument.
                [assembly: Compono.Composable((typeof(TestNamespace.Customer)))]

                namespace TestNamespace;

                public sealed class Customer
                {
                    public Customer(string firstName)
                    {
                        FirstName = firstName;
                    }

                    public string FirstName { get; }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NullableAndNonNullableGenericInstantiation_ReportsConflictDiagnostic() =>
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
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Box<string> and Box<string?> emit to the identical hint name
                            // (FullyQualifiedFormat erases the top-level nullable annotation), but
                            // Roslyn substitutes the constructor parameter's own NullableAnnotation
                            // differently for each - there's no single Nullability value that's
                            // correct for both requests against the one plan Compono generates, so
                            // this must be reported (CMP0010) rather than silently picking one
                            // (which would make the "losing" call site get incorrect metadata,
                            // dependent on arbitrary discovery order).
                            var notNullable = composer.Create<Box<string>>();
                            var nullable = composer.Create<Box<string?>>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0010",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task OverriddenRequiredProperty_EmittedOnlyOnce() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public abstract class Animal
                {
                    public Animal() { }

                    public required virtual string Name { get; init; }
                }

                public sealed class Dog : Animal
                {
                    public Dog() { }

                    // `required` propagates through an override - both Animal.Name and
                    // Dog.Name report IsRequired: true and share the same member name.
                    // RequiredMemberCollector must collect this once, not twice (a duplicate
                    // object-initializer entry is a compile error in the generated file).
                    public required override string Name { get; init; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var dog = composer.Create<TestNamespace.Dog>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RequiredMembersOnBaseAndDerivedType_BaseOrdinalsPrecedeDerivedInDeclarationOrder() =>
        // Proves docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md's
        // amendment 2 canonical ordinal algorithm: base type members are numbered before derived
        // type members (Species=0, LegCount=1, Name=2 - not declaration-file order, not
        // derived-first), and declaration order is preserved within each type (Species before
        // LegCount, matching how they're declared on Animal).
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public abstract class Animal
                {
                    public Animal() { }

                    public required string Species { get; init; }
                    public required int LegCount { get; init; }
                }

                public sealed class Dog : Animal
                {
                    public Dog() { }

                    public required string Name { get; init; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var dog = composer.Create<TestNamespace.Dog>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RequiredMemberNamedWithReservedKeyword_EscapesIdentifier() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Widget
                {
                    public Widget() { }

                    // A required member can legally use an escaped reserved-keyword identifier -
                    // member.Name reports it as the bare keyword text ("class"), which must be
                    // re-escaped with `@` before landing in the generated object initializer, or
                    // the emitted file fails to compile.
                    public required string @class { get; init; }
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
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SameClosureReachesConflictingNullableInstantiations_ReportsConflictDiagnostic() =>
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

                    public sealed class Container
                    {
                        public Container(Box<string> first, Box<string?> second)
                        {
                            First = first;
                            Second = second;
                        }

                        public Box<string> First { get; }
                        public Box<string?> Second { get; }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            var composer = Compono.Composer.Create();
                            // Both Box<string> and Box<string?> are reached within the SAME
                            // transitive closure walk (sibling constructor parameters of
                            // Container), not from two separate Create<T>() call sites -
                            // SymbolEqualityComparer.Default (ignores nullable annotations) would
                            // silently collapse the second one into the visited set before it ever
                            // became its own DiscoveredTypeInfo, hiding the conflict entirely
                            // instead of reporting CMP0010.
                            var container = composer.Create<TestNamespace.Container>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0010",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task AmbiguousTypeReachedFromTwoCallSites_StillReportsAtBothLocations() =>
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
                            // Address is ambiguous (two accessible constructors) and reached from
                            // two different Create<T>() call sites. Each produces its own failure
                            // DiscoveredTypeInfo carrying its own CMP0001 at its own Location -
                            // DiagnosticInfo.Equals includes Location, so these two failures are
                            // never "equal", and conflict-detection must not fold them into a
                            // synthetic, locationless CMP0010 instead of the real CMP0001s.
                            var customer = composer.Create<TestNamespace.Customer>();
                            var order = composer.Create<TestNamespace.Order>();
                        }
                    }
                    """,
            },
            expectedDiagnosticId: "CMP0001",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task CreateManyOnlyInvocation_GeneratesCompositionPlan() =>
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
                        // No Create<Customer>() call site anywhere in this source, and no
                        // [Composable] attribute - CreateMany<T>() has to be its own discovery
                        // trigger (PR #13 review), not merely piggyback on Create<T>() already
                        // having been called for the same type elsewhere.
                        var customers = composer.CreateMany<TestNamespace.Customer>(3);
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RowResolveWithNoDescriptorOnlyInvocation_GeneratesNoPlan() =>
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
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var row = composer.CreateRow(typeof(EntryPoint));
                        // CompositionRow.Resolve<T>() (the descriptor-less overload) must generate no
                        // plan at all, not merely be untested - it forwards to
                        // ICompositionContext.Resolve<TValue>()'s manual-resolve seam, which throws
                        // unless a registration/configuration-rule factory is actively being invoked,
                        // a condition a CompositionRow-holding caller can never satisfy (PR #22
                        // review, second round: discovering this overload advertised a call shape
                        // that always throws at runtime). Regression coverage for excluding it from
                        // CreateInvocationDiscovery - no Create<Customer>()/CreateMany<Customer>()
                        // call site anywhere in this source, and no [Composable] attribute either, so
                        // Customer must end up with no generated plan whatsoever.
                        var customer = row.Resolve<TestNamespace.Customer>();
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RowResolveWithDescriptorOnlyInvocation_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Order
                {
                    public Order(string reference) { Reference = reference; }
                    public string Reference { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var row = composer.CreateRow(typeof(EntryPoint));
                        var descriptor = new Compono.CompositionRequestDescriptor(
                            Compono.CompositionRequestKind.TestParameter,
                            ordinal: 0,
                            name: "order",
                            declaringType: typeof(EntryPoint),
                            Compono.Nullability.NotNullable);
                        // No Create<Order>()/CreateMany<Order>() call site anywhere in this source,
                        // and no [Composable] attribute - CompositionRow.Resolve<T>(descriptor) has
                        // to be its own discovery trigger, distinct from the descriptor-less
                        // overload above and from ResolveShared<T>(descriptor) below (PR #22 review).
                        var order = row.Resolve<TestNamespace.Order>(descriptor);
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RowResolveSharedOnlyInvocation_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace TestNamespace;

                public sealed class Widget
                {
                    public Widget(string name) { Name = name; }
                    public string Name { get; }
                }

                public static class EntryPoint
                {
                    public static void Run()
                    {
                        var composer = Compono.Composer.Create();
                        var row = composer.CreateRow(typeof(EntryPoint));
                        var descriptor = new Compono.CompositionRequestDescriptor(
                            Compono.CompositionRequestKind.TestParameter,
                            ordinal: 0,
                            name: "widget",
                            declaringType: typeof(EntryPoint),
                            Compono.Nullability.NotNullable);
                        // No Create<Widget>()/CreateMany<Widget>() call site anywhere in this
                        // source, and no [Composable] attribute - ResolveShared<T>(descriptor) has
                        // to be its own discovery trigger too, not merely inherited from matching
                        // Resolve<T>(descriptor)'s method name (PR #22 review).
                        var widget = row.ResolveShared<TestNamespace.Widget>(descriptor);
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposeAttributedMethodParameter_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace Compono.XunitV3
                {
                    // Stands in for the real Compono.XunitV3.ComposeAttribute (a separate package/
                    // assembly, not referenced from this generator test project) - ComposeMethodDiscovery
                    // matches on the fully qualified metadata name alone, so a same-named type here
                    // triggers it identically to the real one.
                    public class ComposeAttribute : System.Attribute
                    {
                        public ComposeAttribute(params object?[] inlineValues) { }
                    }
                }

                namespace TestNamespace
                {
                    public sealed class Invoice
                    {
                        public Invoice(string reference) { Reference = reference; }
                        public string Reference { get; }
                    }

                    public static class TestClass
                    {
                        // No Create<Invoice>()/CreateMany<Invoice>() call site, no [Composable]
                        // attribute, and no CompositionRow.Resolve<T>(descriptor) call site anywhere
                        // in this source - Invoice is reachable only as this [Compose]-attributed
                        // method's own parameter, which ComposeMethodDiscovery must still discover
                        // (ADR-0022 Amendment 2026-07-30, fix #2).
                        [Compono.XunitV3.Compose]
                        public static void Creates_invoice(Invoice invoice)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposeGenericAttributedMethodParameter_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace Compono.XunitV3
                {
                    // Stands in for the real Compono.XunitV3.ComposeAttribute/ICompositionProfile (a
                    // separate package/assembly, not referenced from this generator test project) -
                    // ComposeMethodDiscovery matches on the fully qualified metadata name alone, so
                    // same-named types here trigger it identically to the real ones.
                    public interface ICompositionProfile
                    {
                        void Configure(object builder);
                    }

                    public class ComposeAttribute : System.Attribute
                    {
                        public ComposeAttribute(params object?[] inlineValues) { }
                    }

                    // [Compose<TProfile>]'s attribute class metadata name is the arity-suffixed
                    // "Compono.XunitV3.ComposeAttribute`1" - distinct from the non-generic
                    // ComposeAttribute's metadata name above, and reached only via
                    // ComposeMethodDiscovery.GenericAttributeMetadataName's own, separately
                    // registered discovery provider.
                    public sealed class ComposeAttribute<TProfile> : ComposeAttribute
                        where TProfile : ICompositionProfile, new()
                    {
                        public ComposeAttribute(params object?[] inlineValues) : base(inlineValues) { }
                    }
                }

                namespace TestNamespace
                {
                    public sealed class TestProfile : Compono.XunitV3.ICompositionProfile
                    {
                        public void Configure(object builder) { }
                    }

                    public sealed class Statement
                    {
                        public Statement(string reference) { Reference = reference; }
                        public string Reference { get; }
                    }

                    public static class TestClass
                    {
                        // No Create<Statement>()/CreateMany<Statement>() call site, no [Composable]
                        // attribute, and no non-generic [Compose] use anywhere in this source -
                        // Statement is reachable only as this [Compose<TProfile>]-attributed method's
                        // own parameter, proving the generic-metadata-name discovery path on its own
                        // (PR #23 review: ForAttributeWithMetadataName matches an attribute usage's
                        // own attribute class metadata name, not a base type's, so the non-generic
                        // ComposeMethodDiscovery registration alone never sees this form).
                        [Compono.XunitV3.Compose<TestProfile>]
                        public static void Creates_statement(Statement statement)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposeTwoTypeParameterAttributedMethodParameter_GeneratesCompositionPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace Compono.XunitV3
                {
                    // Stands in for the real Compono.XunitV3.ComposeAttribute/ICompositionProfile (a
                    // separate package/assembly, not referenced from this generator test project) -
                    // ComposeMethodDiscovery matches on the fully qualified metadata name alone, so
                    // same-named types here trigger it identically to the real ones.
                    public interface ICompositionProfile
                    {
                        void Configure(object builder);
                    }

                    public class ComposeAttribute : System.Attribute
                    {
                        public ComposeAttribute(params object?[] inlineValues) { }
                    }

                    public sealed class ComposeAttribute<TProfile> : ComposeAttribute
                        where TProfile : ICompositionProfile, new()
                    {
                        public ComposeAttribute(params object?[] inlineValues) : base(inlineValues) { }
                    }

                    // [Compose<TProfile, TConfig>]'s (ADR-0036) attribute class metadata name is the
                    // arity-suffixed "Compono.XunitV3.ComposeAttribute`2" - distinct from both the
                    // non-generic and the one-type-parameter forms above, and reached only via
                    // ComposeMethodDiscovery.TwoTypeParameterAttributeMetadataName's own, separately
                    // registered discovery provider (PR #65 review).
                    public sealed class ComposeAttribute<TProfile, TConfig> : ComposeAttribute
                        where TProfile : ICompositionProfile
                    {
                        public ComposeAttribute(params object?[] configArguments) { }
                    }
                }

                namespace TestNamespace
                {
                    public sealed record InvoiceConfig(string Reference);

                    public sealed class InvoiceProfile : Compono.XunitV3.ICompositionProfile
                    {
                        public InvoiceProfile(InvoiceConfig config) { }
                        public void Configure(object builder) { }
                    }

                    public sealed class CreditNote
                    {
                        public CreditNote(string reference) { Reference = reference; }
                        public string Reference { get; }
                    }

                    public static class TestClass
                    {
                        // No Create<CreditNote>()/CreateMany<CreditNote>() call site, no [Composable]
                        // attribute, and no non-generic/one-type-parameter [Compose] use anywhere in
                        // this source - CreditNote is reachable only as this
                        // [Compose<TProfile, TConfig>]-attributed method's own parameter, proving the
                        // two-type-parameter-metadata-name discovery path on its own (the exact gap
                        // PR #65 review caught: a concrete parameter type reached only through this
                        // attribute form previously had no generated plan at all).
                        [Compono.XunitV3.Compose<InvoiceProfile, InvoiceConfig>("some-reference")]
                        public static void Creates_creditNote(CreditNote creditNote)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ComposeAttributedGenericMethodParameter_GeneratesNoPlan() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = """
                namespace Compono.XunitV3
                {
                    public class ComposeAttribute : System.Attribute
                    {
                        public ComposeAttribute(params object?[] inlineValues) { }
                    }
                }

                namespace TestNamespace
                {
                    public sealed class Receipt
                    {
                        public Receipt(string reference) { Reference = reference; }
                        public string Reference { get; }
                    }

                    public static class TestClass
                    {
                        // A generic test method is excluded from ComposeMethodDiscovery entirely,
                        // mirroring Compono.XunitV3's own binding algorithm rejecting generic test
                        // methods pre-composition - Receipt must end up with no generated plan
                        // whatsoever (no other discovery path reaches it in this source).
                        [Compono.XunitV3.Compose]
                        public static void Creates_receipt<T>(Receipt receipt, T other)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    // No automated regression test for RequiredMemberCollector.IsAssignableFromGeneratedCode: the
    // shape it defends against (a required member with no accessible setter, or a required
    // readonly field) is one the C# compiler itself refuses to let *any* C#-authored type declare
    // (CS9032/CS9033 fire at the library's own declaration, before this generator ever runs) -
    // confirmed by attempting exactly this shape via GeneratorTestHelpers.CompileLibrary. The gap
    // only exists for a non-C#-compiler-produced assembly (hand-authored IL, a different .NET
    // language with laxer rules), which this test harness has no way to produce without adding
    // real IL-emission infrastructure - disproportionate for this one edge case. The fix itself
    // mirrors ConstructorSelector's already-tested compilation.IsSymbolAccessibleWithin pattern.
}
