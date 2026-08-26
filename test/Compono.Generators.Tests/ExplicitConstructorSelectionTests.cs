namespace Compono.Generators.Tests;

/// <summary>
/// ADR-0002 Amendment 3 / ADR-0052 (Part B): explicit constructor selection via
/// <c>CompositionTypeRuleBuilder{T}.UseConstructor{...}()</c> - real end-to-end proof against the
/// actual incremental-generator pipeline (not an isolated Roslyn query), using
/// <see cref="GeneratorTestHelpers.CompileAndExecute"/> and
/// <see cref="GeneratorTestHelpers.VerifyFailure"/> the same way this project's other real
/// generator tests do.
/// </summary>
public sealed class ExplicitConstructorSelectionTests
{
    private const string Source = """
        namespace Spike;

        public interface IBar { }
        public interface IBaz { }
        public sealed class BarImpl : IBar { }
        public sealed class BazImpl : IBaz { }

        public sealed class Foo
        {
            public Foo() { }
            public Foo(IBar bar, IBaz baz) { Bar = bar; Baz = baz; }

            public IBar? Bar { get; }
            public IBaz? Baz { get; }
        }

        public sealed class Outer
        {
            public Outer(Foo foo) { Foo = foo; }

            public Foo Foo { get; }
        }

        public static class EntryPoint
        {
            public static void Discover() => Compono.Composer.Create().Create<Outer>();
        }
        """;

    [Fact]
    public Task AmbiguousType_NoSelection_ReportsCmp0001Unchanged()
    {
        var options = new CodeGenerationOptions
        {
            SourceCode = Source.Replace(
                "public static void Discover() => Compono.Composer.Create().Create<Outer>();",
                """
                public static void Discover() => Compono.Composer.Create().Create<Outer>();
                public static void Run() { }
                """),
        };

        return GeneratorTestHelpers.VerifyFailure(options, "CMP0001", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void RootComposition_WithSelection_ComposesDependenciesAndCallsSelectedConstructor()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static void Discover() => Compono.Composer.Create().Create<Outer>();",
                    """
                    public static object Run()
                    {
                        var composer = Compono.Composer.Create(builder =>
                        {
                            builder.For<Foo>().UseConstructor<IBar, IBaz>();
                            builder.Register<IBar>(() => new BarImpl());
                            builder.Register<IBaz>(() => new BazImpl());
                        });

                        var foo = composer.Create<Foo>();
                        return foo.Bar is BarImpl && foo.Baz is BazImpl;
                    }
                    """),
            },
            "Spike.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public void NestedComposition_WithSelection_ComposesTransitively()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace(
                    "public static void Discover() => Compono.Composer.Create().Create<Outer>();",
                    """
                    public static object Run()
                    {
                        var composer = Compono.Composer.Create(builder =>
                        {
                            builder.For<Foo>().UseConstructor<IBar, IBaz>();
                            builder.Register<IBar>(() => new BarImpl());
                            builder.Register<IBaz>(() => new BazImpl());
                        });

                        var outer = composer.Create<Outer>();
                        return outer.Foo is not null && outer.Foo.Bar is not null && outer.Foo.Baz is not null;
                    }
                    """),
            },
            "Spike.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public Task ConflictingSelections_ForSameType_ReportsCmp0033()
    {
        const string conflictSource = """
            namespace SpikeConflict;

            public interface IBar { }
            public interface IBaz { }

            public sealed class Foo
            {
                public Foo() { }
                public Foo(IBar bar, IBaz baz) { }
                public Foo(IBar bar) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar, IBaz>());
                }

                public static void RunAgainDifferently()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar>());
                }
            }
            """;

        var options = new CodeGenerationOptions { SourceCode = conflictSource };

        return GeneratorTestHelpers.VerifyFailure(options, "CMP0033", TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task ThreeConflictingSelections_ReportsCanonicallyOrderedPair_NotDeclarationOrder()
    {
        // Three distinct selections for the same type, deliberately declared in an order that
        // does NOT match the alphabetical order of their own ToDisplayString() text - the largest
        // (three-arg) selection is declared FIRST, the smallest (one-arg) selection declared LAST.
        // Reporting "whichever was visited first vs. last" (the pre-fix behavior) would have named
        // the three-arg and two-arg selections here, since Compilation.SyntaxTrees/descendant-node
        // walk order for a single file tracks declaration order; the fix instead always reports the
        // two constructors with the alphabetically smallest ToDisplayString() text, regardless of
        // where in the source (or in which order) each selection was declared (code-review finding).
        const string threeWayConflictSource = """
            namespace SpikeThreeWayConflict;

            public interface IBar { }
            public interface IBaz { }
            public interface IQux { }

            public sealed class Foo
            {
                public Foo() { }
                public Foo(IBar bar) { }
                public Foo(IBar bar, IBaz baz) { }
                public Foo(IBar bar, IBaz baz, IQux qux) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void RunWithLargestSelectionFirst()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar, IBaz, IQux>());
                }

                public static void RunWithMiddleSelection()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar, IBaz>());
                }

                public static void RunWithSmallestSelectionLast()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar>());
                }
            }
            """;

        var options = new CodeGenerationOptions { SourceCode = threeWayConflictSource };

        return GeneratorTestHelpers.VerifyFailure(options, "CMP0033", TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task InvalidSelection_NoMatchingConstructor_ReportsCmp0034()
    {
        var options = new CodeGenerationOptions
        {
            SourceCode = Source.Replace(
                "public static void Discover() => Compono.Composer.Create().Create<Outer>();",
                """
                public static void Discover() => Compono.Composer.Create().Create<Outer>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBaz, IBar>());
                }
                """),
        };

        return GeneratorTestHelpers.VerifyFailure(options, "CMP0034", TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task RepeatedInvalidSelections_ReportsCanonicallyOrderedChoice_NotDeclarationOrder()
    {
        // Two distinct invalid selections for the same type - neither matches any constructor -
        // deliberately declared so the alphabetically LATER requested-type-list text ("IQux") is
        // declared FIRST, and the alphabetically SMALLER text ("IBaz") is declared LAST.
        // Reporting "whichever invalid selection was visited first" (the pre-fix behavior) would
        // have named the IQux attempt here; the fix instead always reports the invalid selection
        // whose requested-type-list text sorts alphabetically smallest, regardless of declaration
        // order (code-review finding, mirrors the conflicting-selection determinism fix above).
        const string repeatedInvalidSource = """
            namespace SpikeRepeatedInvalid;

            public interface IBar { }
            public interface IBaz { }
            public interface IQux { }

            public sealed class Foo
            {
                public Foo() { }
                public Foo(IBar bar) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void RunWithIQuxFirst()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IQux>());
                }

                public static void RunWithIBazLast()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBaz>());
                }
            }
            """;

        var options = new CodeGenerationOptions { SourceCode = repeatedInvalidSource };

        return GeneratorTestHelpers.VerifyFailure(options, "CMP0034", TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task IdenticalRepeatedSelection_IsIdempotent_NotAConflict()
    {
        const string repeatedSource = """
            namespace SpikeRepeated;

            public interface IBar { }
            public interface IBaz { }

            public sealed class Foo
            {
                public Foo() { }
                public Foo(IBar bar, IBaz baz) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar, IBaz>());
                }

                public static void RunAgain()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar, IBaz>());
                }
            }
            """;

        return GeneratorTestHelpers.Verify(new CodeGenerationOptions { SourceCode = repeatedSource }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task InaccessibleMatchingConstructor_TreatedAsNoMatch_ReportsCmp0034()
    {
        const string inaccessibleSource = """
            namespace SpikeInaccessible;

            public interface IBar { }
            public interface IBaz { }

            public sealed class Foo
            {
                public Foo() { }
                private Foo(IBar bar, IBaz baz) { }
                public Foo(IBar bar) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar, IBaz>());
                }
            }
            """;

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions { SourceCode = inaccessibleSource }, "CMP0034", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ConstructedGenericAndNullableAnnotatedParameterTypes_MatchExactly()
    {
        const string genericSource = """
            namespace SpikeGeneric;

            public sealed class Wrapper<T>
            {
                public Wrapper(T value) { Value = value; }
                public T Value { get; }
            }

            public sealed class Foo
            {
                public Foo() { }
                public Foo(Wrapper<string> wrapper, string? note) { Wrapper = wrapper; Note = note; }

                public Wrapper<string>? Wrapper { get; }
                public string? Note { get; }
            }

            public static class EntryPoint
            {
                public static object Run()
                {
                    var composer = Compono.Composer.Create(builder =>
                    {
                        builder.For<Foo>().UseConstructor<Wrapper<string>, string?>();
                        builder.Register<Wrapper<string>>(() => new Wrapper<string>("wrapped"));
                        builder.Register<string?>(() => "a note");
                    });

                    var foo = composer.Create<Foo>();
                    return foo.Wrapper?.Value == "wrapped" && foo.Note == "a note";
                }
            }
            """;

        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions { SourceCode = genericSource },
            "SpikeGeneric.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public void SelectionOnAlreadyUnambiguousType_IsHarmlessNoOp()
    {
        const string unambiguousSource = """
            namespace SpikeUnambiguous;

            public interface IBar { }
            public sealed class BarImpl : IBar { }

            public sealed class Foo
            {
                public Foo(IBar bar) { Bar = bar; }
                public IBar Bar { get; }
            }

            public static class EntryPoint
            {
                public static object Run()
                {
                    var composer = Compono.Composer.Create(builder =>
                    {
                        builder.For<Foo>().UseConstructor<IBar>();
                        builder.Register<IBar>(() => new BarImpl());
                    });

                    return composer.Create<Foo>().Bar is BarImpl;
                }
            }
            """;

        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions { SourceCode = unambiguousSource },
            "SpikeUnambiguous.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public Task ParamsConstructor_SelectionRequestingArrayParameter_ReportsCmp0034()
    {
        // Foo has a `params IBar[]` constructor and a `string` constructor - neither matches the
        // deliberately-mismatched `UseConstructor<int>()` selection below, proving a `params`
        // constructor's presence doesn't confuse symbol matching into a wrong/silent match.
        const string paramsSource = """
            namespace SpikeParams;

            public interface IBar { }

            public sealed class Foo
            {
                public Foo() { }
                public Foo(params IBar[] bars) { }
                public Foo(string label) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<int>());
                }
            }
            """;

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions { SourceCode = paramsSource }, "CMP0034", TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task UnrelatedSameNamedBuilderType_IsNotMistakenForCompono_StillReportsCmp0001()
    {
        // A consumer-defined type that happens to share the name/arity/method-name shape of
        // Compono's own CompositionTypeRuleBuilder<T>.UseConstructor<...>() must never be mistaken
        // for a real selection - the scanner must compare against Compono's actual symbol, not just
        // the containing type's simple name (code-review finding). Foo stays genuinely ambiguous
        // (CMP0001), even though a decoy call with an identical-looking shape exists elsewhere in
        // the same compilation and would, if wrongly matched, either "select" a constructor Foo
        // doesn't have (CMP0034) or accidentally resolve Foo's own ambiguity.
        const string decoySource = """
            namespace SpikeDecoy;

            public interface IBar { }
            public interface IBaz { }

            public sealed class Foo
            {
                public Foo() { }
                public Foo(IBar bar, IBaz baz) { }
            }

            // Same simple name, same arity, same generic method name as Compono's real
            // CompositionTypeRuleBuilder<T> - deliberately NOT Compono's type.
            public sealed class CompositionTypeRuleBuilder<T>
            {
                public void UseConstructor<T1>() { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    new CompositionTypeRuleBuilder<Foo>().UseConstructor<IBar>();
                }
            }
            """;

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions { SourceCode = decoySource }, "CMP0001", TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task StaleSelectionOnUnambiguousType_StillReportsCmp0034()
    {
        // Foo now has exactly one accessible constructor - Compono would auto-select it with no
        // UseConstructor<...>() call at all - but a selection naming a signature that matches
        // nothing must still be diagnosed, not silently ignored just because the type happens to
        // be unambiguous today (code-review finding: the single-constructor fast path previously
        // returned before the scanner was ever consulted, so a stale selection left behind after a
        // constructor overload was removed would go completely unreported).
        const string staleSource = """
            namespace SpikeStale;

            public interface IBar { }
            public interface IBaz { }

            public sealed class Foo
            {
                public Foo(IBar bar) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar, IBaz>());
                }
            }
            """;

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions { SourceCode = staleSource }, "CMP0034", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ByRefOverload_ExcludedFromMatching_UsableOverloadSelectedRegardlessOfDeclarationOrder()
    {
        // Foo(ref int) is declared BEFORE Foo(int) - both satisfy a pure parameter-type comparison
        // for UseConstructor<int>(), so matching by type alone would let source declaration order
        // decide which one FirstOrDefault picks. Excluding ref/out/ref-readonly parameters from
        // matching entirely means the usable by-value overload is selected regardless of which one
        // was declared first (code-review finding).
        const string byRefSource = """
            namespace SpikeByRef;

            public sealed class Foo
            {
                public Foo(ref int value) { }
                public Foo(int value) { Value = value; }

                public int Value { get; }
            }

            public static class EntryPoint
            {
                public static object Run()
                {
                    var composer = Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<int>());

                    return composer.Create<Foo>().Value;
                }
            }
            """;

        // If the ref overload had been matched instead, generation would report CMP0004
        // (unsupported ref parameter kind) and CompileAndExecute would throw well before this
        // assertion - reaching a real int value at all proves the by-value overload was selected.
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions { SourceCode = byRefSource },
            "SpikeByRef.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().BeOfType<int>();
    }

    [Fact]
    public void ParameterlessSelection_SelectsParameterlessConstructor()
    {
        // Foo has both a parameterless constructor and a one-arg constructor - a consumer who
        // wants the PARAMETERLESS one had no way to express that selection before, since C# has no
        // empty generic argument-list syntax and UseConstructor started at arity one (code-review
        // finding). The non-generic UseConstructor() overload closes this gap.
        const string parameterlessSource = """
            namespace SpikeParameterless;

            public interface IBar { }
            public sealed class BarImpl : IBar { }

            public sealed class Foo
            {
                public Foo() { Bar = null; }
                public Foo(IBar bar) { Bar = bar; }

                public IBar? Bar { get; }
            }

            public static class EntryPoint
            {
                public static object? Run()
                {
                    var composer = Compono.Composer.Create(builder =>
                    {
                        builder.For<Foo>().UseConstructor();
                        builder.Register<IBar>(() => new BarImpl());
                    });

                    // If the (IBar) constructor had been selected instead, Bar would be a real
                    // BarImpl, not null - reaching null proves the parameterless constructor ran.
                    return composer.Create<Foo>().Bar;
                }
            }
            """;

        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions { SourceCode = parameterlessSource },
            "SpikeParameterless.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public Task InAndByValueOverloads_SameRequestedTypes_ReportsCmp0034NotAmbiguousPick()
    {
        // Foo(in int) and Foo(int) both satisfy a pure parameter-TYPE match for
        // UseConstructor<int>() - "in" is otherwise deliberately still matchable (a plain by-value
        // argument legally binds to an "in" parameter). But the generated call site never writes
        // "in" (an ordinary expression), so real C# overload resolution there always prefers the
        // by-value constructor over the "in" one, regardless of which one this scanner's matching
        // logic happens to record as "selected" - a genuine risk of the scanner claiming/validating
        // one constructor while a different one actually executes every time (code-review finding).
        // Treated as no unambiguous match at all (CMP0034), not a silent declaration-order pick.
        const string inVsByValueSource = """
            namespace SpikeInVsByValue;

            public sealed class Foo
            {
                public Foo() { }
                public Foo(in int value) { }
                public Foo(int value) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<int>());
                }
            }
            """;

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions { SourceCode = inVsByValueSource }, "CMP0034", TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task SelectedConstructorIsTheOnlyOneAndInaccessible_ReportsCmp0034NotCmp0002()
    {
        // Foo has exactly ONE declared constructor, and it's private - zero ACCESSIBLE constructors
        // exist at all. Round-8 code-review finding: ConstructorSelector previously returned the
        // zero-accessible-constructor CMP0002 diagnostic before ever consulting the selection
        // scanner, masking a real, documented CMP0034 (the stale/inaccessible selection) behind a
        // generic "no accessible constructor" message - and made the outcome depend on whether some
        // OTHER, unrelated accessible constructor happened to exist on the type (this test's own
        // sibling, InaccessibleMatchingConstructor_TreatedAsNoMatch_ReportsCmp0034, already covers
        // that case; this one is the zero-accessible-constructors-at-all case that previously
        // reported the wrong diagnostic).
        const string soleInaccessibleSource = """
            namespace SpikeSoleInaccessible;

            public interface IBar { }

            public sealed class Foo
            {
                private Foo(IBar bar) { }
            }

            public static class EntryPoint
            {
                public static void Discover() => Compono.Composer.Create().Create<Foo>();

                public static void Run()
                {
                    Compono.Composer.Create(builder => builder.For<Foo>().UseConstructor<IBar>());
                }
            }
            """;

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions { SourceCode = soleInaccessibleSource }, "CMP0034", TestContext.Current.CancellationToken);
    }
}
