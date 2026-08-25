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
}
