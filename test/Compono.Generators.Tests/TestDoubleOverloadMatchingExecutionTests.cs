namespace Compono.Generators.Tests;

/// <summary>
/// Real end-to-end execution of ADR-0044 Amendment 21's overload-safe argument matching against a
/// real, generator-emitted double - not a hand-written unit test, and not just a compile-only
/// snapshot (<c>TestDoubleVerifyTests.cs</c>'s own overload-matching fixtures). Proves the corrected
/// architecture: the matching-specific member name only configures/observes; the SUT-visible
/// dispatch always goes through the real overload, and both surfaces share the same
/// entries/call-log/lock state per overload (PLAN-0054 Phase 2's "Architecture (revised)").
///
/// Uses a stand-in shaped after the real dogfood-evidenced <c>IAmazonDynamoDB.DeleteItemAsync</c>
/// overload (a request object argument-matched by a member predicate, plus a
/// <see cref="System.Threading.CancellationToken"/>) without taking a new external package
/// dependency, per PLAN-0054's acceptance criteria. A synchronous <see langword="bool"/> return
/// (rather than <c>Task&lt;DeleteItemResponse&gt;</c>) keeps every member's deterministic default
/// available, so no member here is ADR-0045 configuration-required (CMP0032) - that dimension is
/// already covered elsewhere (<c>TestDoubleConfigurationRequiredExecutionTests.cs</c>-style
/// coverage) and isn't what this file is proving.
/// </summary>
public sealed class TestDoubleOverloadMatchingExecutionTests
{
    private const string Source = """
        namespace TestNamespace;

        public sealed class DeleteItemRequest
        {
            public string TableName { get; init; } = "";
        }

        public interface IAmazonDynamoDB
        {
            bool DeleteItemAsync(DeleteItemRequest request, System.Threading.CancellationToken cancellationToken);

            bool DeleteItemAsync(string tableName, System.Threading.CancellationToken cancellationToken);
        }

        public sealed class OrderService
        {
            public OrderService(IAmazonDynamoDB client) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<OrderService>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IAmazonDynamoDB), out var value);
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
    public void CoexistencePrecedence_MatchingEntryOverridesDiscriminatorFallback_ForMatchingCallsOnly()
    {
        // User-specified example: a broad discriminator-only Configure() registered first, then a
        // narrower .Matching(...) override registered after it. A call matching the predicate gets
        // the special response; a call that doesn't falls through to the discriminator's own
        // always-matching entry.
        var result = Run(
            """
            public static object Run()
            {
                var client = (IAmazonDynamoDB)CreateDouble();

                client.Configure().DeleteItemAsync(new DeleteItemRequest(), global::System.Threading.CancellationToken.None).Returns(false);
                client.Configure().DeleteItemAsyncMatching(
                    global::Compono.Match.Is<DeleteItemRequest>(x => x.TableName == "special"),
                    global::Compono.Match.Any<global::System.Threading.CancellationToken>()).Returns(true);

                var specialResult = client.DeleteItemAsync(new DeleteItemRequest { TableName = "special" }, default);
                var fallbackResult = client.DeleteItemAsync(new DeleteItemRequest { TableName = "ordinary" }, default);

                return new[] { specialResult, fallbackResult };
            }
            """,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { true, false }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void SiblingOverloadIndependence_ConfiguringOneOverloadNeverAffectsTheOther()
    {
        var result = Run(
            """
            public static object Run()
            {
                var client = (IAmazonDynamoDB)CreateDouble();

                client.Configure().DeleteItemAsyncMatching(
                    global::Compono.Match.Any<DeleteItemRequest>(),
                    global::Compono.Match.Any<global::System.Threading.CancellationToken>()).Returns(true);
                client.Configure().DeleteItemAsync("orders", global::System.Threading.CancellationToken.None).Returns(false);

                var requestOverloadResult = client.DeleteItemAsync(new DeleteItemRequest(), default);
                var nameOverloadResult = client.DeleteItemAsync("orders", default);

                client.Verify().DeleteItemAsyncMatching(global::Compono.Match.Any<DeleteItemRequest>(), global::Compono.Match.Any<global::System.Threading.CancellationToken>()).Once();
                client.Verify().DeleteItemAsync("orders", global::System.Threading.CancellationToken.None).Once();

                return new[] { requestOverloadResult, nameOverloadResult };
            }
            """,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { true, false }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void FilteredVerification_CountsOnlyRealCallsMatchingThePredicate()
    {
        var act = () => Run(
            """
            public static object Run()
            {
                var client = (IAmazonDynamoDB)CreateDouble();
                client.Configure().DeleteItemAsync(new DeleteItemRequest(), global::System.Threading.CancellationToken.None).Returns(true);

                client.DeleteItemAsync(new DeleteItemRequest { TableName = "special" }, default);
                client.DeleteItemAsync(new DeleteItemRequest { TableName = "ordinary" }, default);
                client.DeleteItemAsync(new DeleteItemRequest { TableName = "special" }, default);

                client.Verify().DeleteItemAsyncMatching(
                    global::Compono.Match.Is<DeleteItemRequest>(x => x.TableName == "special"),
                    global::Compono.Match.Any<global::System.Threading.CancellationToken>()).Exactly(2);
                client.Verify().DeleteItemAsyncMatching(
                    global::Compono.Match.Is<DeleteItemRequest>(x => x.TableName == "ordinary"),
                    global::Compono.Match.Any<global::System.Threading.CancellationToken>()).Once();

                return client;
            }
            """,
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public void DiscriminatorVerification_StillReportsTotalRealCallCount_BackedByTheCallLogNow()
    {
        var act = () => Run(
            """
            public static object Run()
            {
                var client = (IAmazonDynamoDB)CreateDouble();
                client.Configure().DeleteItemAsync(new DeleteItemRequest(), global::System.Threading.CancellationToken.None).Returns(true);

                client.DeleteItemAsync(new DeleteItemRequest(), default);
                client.DeleteItemAsync(new DeleteItemRequest(), default);
                client.DeleteItemAsync(new DeleteItemRequest(), default);

                client.Verify().DeleteItemAsync(new DeleteItemRequest(), global::System.Threading.CancellationToken.None).Exactly(3);

                return client;
            }
            """,
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public void SequencingOnAMatchingEligibleEntry_EveryRealCallStillRecordedInTheSharedCallLog()
    {
        var result = Run(
            """
            public static object Run()
            {
                var client = (IAmazonDynamoDB)CreateDouble();

                client.Configure().DeleteItemAsyncMatching(
                    global::Compono.Match.Is<DeleteItemRequest>(x => x.TableName == "flaky"),
                    global::Compono.Match.Any<global::System.Threading.CancellationToken>()).ReturnsSequence(
                        global::Compono.SequenceOutcome.Throw(new global::System.InvalidOperationException("attempt 1 fails")),
                        true);

                var results = new object[2];
                try { client.DeleteItemAsync(new DeleteItemRequest { TableName = "flaky" }, default); }
                catch (global::System.InvalidOperationException ex) { results[0] = ex.Message; }
                results[1] = client.DeleteItemAsync(new DeleteItemRequest { TableName = "flaky" }, default);

                client.Verify().DeleteItemAsyncMatching(
                    global::Compono.Match.Is<DeleteItemRequest>(x => x.TableName == "flaky"),
                    global::Compono.Match.Any<global::System.Threading.CancellationToken>()).Exactly(2);
                client.Verify().DeleteItemAsync(new DeleteItemRequest(), global::System.Threading.CancellationToken.None).Exactly(2);

                return results;
            }
            """,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new object[] { "attempt 1 fails", true }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void LiteralShorthandOnTheMatchingNamedSurface_CompilesAndMatchesByEquality()
    {
        // Match<T>'s own implicit conversion from a literal T (Amendment 18) applies uniformly to
        // any Match<T>-typed parameter, including the new Matching-named surface's - there is no
        // separate rule excluding it here. A literal argument becomes an equality matcher, exactly
        // like it already does on the pre-existing non-overloaded matching-eligible surface.
        var result = Run(
            """
            public static object Run()
            {
                var client = (IAmazonDynamoDB)CreateDouble();
                var request = new DeleteItemRequest { TableName = "literal" };

                client.Configure().DeleteItemAsyncMatching(request, global::System.Threading.CancellationToken.None).Returns(true);

                return new[]
                {
                    client.DeleteItemAsync(request, default),
                    client.DeleteItemAsync(new DeleteItemRequest { TableName = "literal" }, default),
                };
            }
            """,
            TestContext.Current.CancellationToken);

        // Reference-equality matching (DeleteItemRequest has no value equality) - the SAME instance
        // matches, a distinct instance with equal property values does not.
        result.Should().BeEquivalentTo(new[] { true, false }, options => options.WithStrictOrdering());
    }

    // PLAN-0054's own numeric-widening evidence for the matching-specific member name (Amendment 18's
    // original CS0121 finding, re-verified on this surface): a bare literal is rejected only when TWO
    // sibling overloads sharing the SAME "<Member>Matching" alias name have Match<T> parameter types
    // the literal implicitly converts to ambiguously (int widens to long) - a different, narrower
    // scenario than the (non-ambiguous, unrelated-types) literal case proven above.
    [Fact]
    public void LiteralShorthandAmbiguousAcrossSiblingOverloads_FailsToCompile()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IRepository
                    {
                        bool Get(int id);

                        bool Get(long id);
                    }

                    public sealed class OrderService
                    {
                        public OrderService(IRepository repository) { }
                    }

                    public static class EntryPoint
                    {
                        private static void Discover() => Compono.Composer.Create().Create<OrderService>();

                        public static object Run()
                        {
                            Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IRepository), out var value);
                            var repository = (IRepository)value!;
                            repository.Configure().GetMatching(5).Returns(true);
                            return repository;
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().Throw<Xunit.Sdk.XunitException>().WithMessage("*CS0121*");
    }
}
