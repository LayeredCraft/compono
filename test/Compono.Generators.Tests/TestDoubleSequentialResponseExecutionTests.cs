using System.Reflection;

namespace Compono.Generators.Tests;

/// <summary>
/// Real end-to-end execution of ADR-0054's sequential/call-count-based responses against a real,
/// generator-emitted double - not a hand-written <see cref="ReturnConfig{T}"/>/
/// <see cref="ReturnConfigBuilder{T}"/> test (<c>Compono.Tests.ReturnConfigSequenceTests</c>, a
/// different assembly, not referenceable from a <c>cref</c> here). Proves the actual generated dispatch code reads
/// <see cref="ReturnConfig{T}.HasConfiguredSequence"/>/<see cref="ReturnConfig{T}.NextSequenceOutcome"/>
/// - the runtime type alone compiling and passing its own unit tests does not prove the generated
/// `Configure()`/dispatch bridge actually reaches it. Covers both dispatch shapes ADR-0054's
/// evidenced need touches: a zero-parameter member (the plain single-field dispatch path -
/// <c>Count()</c> below) and a real-parameter, matching-eligible member (the ADR-0050 entries-list
/// dispatch path - <c>Save(string)</c> below), since each has its own separate ternary/if-chain in
/// the generated code and either could have been missed independently.
/// </summary>
public sealed class TestDoubleSequentialResponseExecutionTests
{
    private const string Source = """
        namespace TestNamespace;

        public interface IRepository
        {
            int Count();
            bool Save(string name);
        }

        public sealed class OrderService
        {
            public OrderService(IRepository repository) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<OrderService>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IRepository), out var value);
                return value!;
            }
        }
        """;

    private static object? Run(string body, CancellationToken cancellationToken) =>
        GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = Source.Replace("public static object CreateDouble()", body + "\n\n    public static object CreateDouble()"),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            cancellationToken);

    [Fact]
    public void ZeroParameterMember_ReturnsSequence_ConsumedInOrder()
    {
        // IRepository.Count() has no parameters - not matching-eligible, so this exercises the
        // plain single-ReturnConfig<T>-field dispatch path.
        var result = Run(
            """
            public static object Run()
            {
                var repository = (IRepository)CreateDouble();
                repository.Configure().Count().ReturnsSequence(1, 2, 3);

                return new[] { repository.Count(), repository.Count(), repository.Count(), repository.Count() };
            }
            """,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { 1, 2, 3, 3 }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ZeroParameterMember_ReturnsSequence_MixedExceptionAndValue_MatchesRealRetryShape()
    {
        var act = () => Run(
            """
            public static object Run()
            {
                var repository = (IRepository)CreateDouble();
                repository.Configure().Count().ReturnsSequence(
                    global::Compono.SequenceOutcome.Throw(new global::System.InvalidOperationException("attempt 1")),
                    global::Compono.SequenceOutcome.Throw(new global::System.InvalidOperationException("attempt 2")),
                    3);

                var results = new object[3];
                for (var i = 0; i < 3; i++)
                {
                    try { results[i] = repository.Count(); }
                    catch (global::System.InvalidOperationException ex) { results[i] = ex.Message; }
                }
                return results;
            }
            """,
            TestContext.Current.CancellationToken);

        act.Should().NotThrow()
            .Which.Should().BeEquivalentTo(
                new object[] { "attempt 1", "attempt 2", 3 },
                options => options.WithStrictOrdering());
    }

    [Fact]
    public void ZeroParameterMember_ReturnsSequence_CallsStillCountTowardVerification()
    {
        var act = () => Run(
            """
            public static object Run()
            {
                var repository = (IRepository)CreateDouble();
                repository.Configure().Count().ReturnsSequence(1, 2, 3);

                repository.Count();
                repository.Count();
                repository.Count();

                repository.Verify().Count().Exactly(3);
                return repository;
            }
            """,
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public void MatchingEligibleMember_ReturnsSequence_ConsumedInOrder()
    {
        // Save(string) has a real parameter and is matching-eligible - exercises the ADR-0050
        // entries-list dispatch path, using the zero-argument "compatibility" Configure() overload
        // (always-matching entry) since this test doesn't need argument matching.
        var result = Run(
            """
            public static object Run()
            {
                var repository = (IRepository)CreateDouble();
                repository.Configure().Save().ReturnsSequence(false, false, true);

                return new[] { repository.Save("a"), repository.Save("b"), repository.Save("c"), repository.Save("d") };
            }
            """,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { false, false, true, true }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void MatchingEligibleMember_TwoIndependentEntries_MaintainIndependentSequenceOrdinals()
    {
        // ADR-0054: sequence state belongs to the matched entry, not the member - two
        // argument-distinguished entries on the same member must not share one ordinal.
        var result = Run(
            """
            public static object Run()
            {
                var repository = (IRepository)CreateDouble();
                repository.Configure().Save(global::Compono.Match.Is<string>(x => x == "alice")).ReturnsSequence(false, true);
                repository.Configure().Save(global::Compono.Match.Is<string>(x => x == "bob")).ReturnsSequence(true, false);

                return new[]
                {
                    repository.Save("alice"),
                    repository.Save("bob"),
                    repository.Save("alice"),
                    repository.Save("bob"),
                };
            }
            """,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { false, true, true, false }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ReconfiguringTheSameEntry_ResetsTheOrdinal()
    {
        var result = Run(
            """
            public static object Run()
            {
                var repository = (IRepository)CreateDouble();
                repository.Configure().Count().ReturnsSequence(1, 2, 3);
                repository.Count();
                repository.Count();

                // Reconfiguring the zero-argument entry replaces its sequence and resets the ordinal -
                // the next call should get 100, not continue at the old sequence's third entry.
                repository.Configure().Count().ReturnsSequence(100, 200);

                return new[] { repository.Count(), repository.Count(), repository.Count() };
            }
            """,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { 100, 200, 200 }, options => options.WithStrictOrdering());
    }
}
