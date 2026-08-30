namespace Compono.Generators.Tests;

/// <summary>End-to-end execution coverage for ADR-0053 invocation-aware responses.</summary>
public sealed class TestDoubleCallbackExecutionTests
{
    private static readonly IReadOnlyDictionary<string, string> TestDoubleProperties =
        new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" };

    [Fact]
    public void ReturnsCallback_ComputesResultFromInvocationArguments()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    repository.Configure().Add(Compono.Match.Any<int>(), Compono.Match.Any<int>())
                        .ReturnsCallback((left, right) => left + right);
                    return repository.Add(20, 22);
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(42);
    }

    [Fact]
    public async Task ReturnsCallback_AcceptsDeclaredTaskReturnAndCapturedDelegateArgument()
    {
        var result = (Task<int>)GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    repository.Configure().PipelineAsync(
                            Compono.Match.Any<int>(),
                            Compono.Match.Any<System.Func<System.Threading.Tasks.Task<int>>>())
                        .ReturnsCallback(async (value, next) => value + await next());
                    return repository.PipelineAsync(20, () => System.Threading.Tasks.Task.FromResult(22));
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken)!;

        (await result).Should().Be(42);
    }

    [Fact]
    public async Task ReturnsCallback_AcceptsDeclaredValueTaskReturn()
    {
        var result = (ValueTask<int>)GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    repository.Configure().ValueAsync(Compono.Match.Any<int>())
                        .ReturnsCallback(value => new System.Threading.Tasks.ValueTask<int>(value + 2));
                    return repository.ValueAsync(40);
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken)!;

        (await result).Should().Be(42);
    }

    [Fact]
    public void ReturnsCallback_ComposesWithMultipleMatchedEntriesAndVerification()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    repository.Configure().Add(1, Compono.Match.Any<int>()).ReturnsCallback((left, right) => left + right);
                    repository.Configure().Add(2, Compono.Match.Any<int>()).ReturnsCallback((left, right) => left * right);
                    var sum = repository.Add(1, 4);
                    var product = repository.Add(2, 4);
                    repository.Verify().Add(1, Compono.Match.Any<int>()).Once();
                    repository.Verify().Add(2, Compono.Match.Any<int>()).Once();
                    return sum + product;
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(13);
    }

    [Fact]
    public void ReturnsCallback_IsDistinctFromReturningADelegateValue()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    repository.Configure().Factory(Compono.Match.Any<int>())
                        .ReturnsCallback(offset => value => value + offset);
                    return repository.Factory(2)!(40);
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(42);
    }

    [Fact]
    public void ReturnsCallback_IsLastConfigurationWinsWithOtherResponseKinds()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    var configuration = repository.Configure().Identity(Compono.Match.Any<int>());
                    configuration.ReturnsCallback(value => value + 1);
                    var staticValue = repository.Identity(20);
                    configuration.Returns(22);
                    var callbackWasCleared = repository.Identity(20);
                    configuration.Throws(new System.InvalidOperationException());
                    var exceptionWasConfigured = false;
                    try { repository.Identity(20); }
                    catch (System.InvalidOperationException) { exceptionWasConfigured = true; }
                    configuration.ReturnsSequence(30, 31);
                    var sequence = repository.Identity(20) + repository.Identity(20);
                    configuration.ReturnsCallback(value => value * 2);
                    return staticValue == 21 && callbackWasCleared == 22 && exceptionWasConfigured && sequence == 61 && repository.Identity(21) == 42;
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public void ReturnsCallback_RejectsNullAndRecordsCallsBeforeCallbackExceptions()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    var configuration = repository.Configure().Identity(Compono.Match.Any<int>());
                    var nullWasRejected = false;
                    try { configuration.ReturnsCallback(null!); }
                    catch (System.ArgumentNullException) { nullWasRejected = true; }
                    configuration.ReturnsCallback(value => throw new System.InvalidOperationException());
                    var callbackExceptionPropagated = false;
                    try { repository.Identity(20); }
                    catch (System.InvalidOperationException) { callbackExceptionPropagated = true; }
                    repository.Verify().Identity(Compono.Match.Any<int>()).Once();
                    return nullWasRejected && callbackExceptionPropagated;
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public void ReturnsCallback_RunsUserCodeOutsideTheMatchedEntryLock()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    repository.Configure().Identity(2).Returns(42);
                    repository.Configure().Identity(1).ReturnsCallback(value =>
                    {
                        var reentrantCall = System.Threading.Tasks.Task.Run(() => repository.Identity(2));
                        if (!reentrantCall.Wait(System.TimeSpan.FromSeconds(1)))
                            throw new System.TimeoutException("The callback still holds the matched-entry lock.");
                        return reentrantCall.Result;
                    });
                    return repository.Identity(1);
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(42);
    }

    [Fact]
    public void ReturnsCallback_LeavesPropertyAndVoidMemberSurfacesUnchanged()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            Options("""
                public static object Run()
                {
                    var repository = CreateDouble();
                    repository.Configure().Name().Returns("Ada");
                    repository.Configure().Notify(Compono.Match.Any<string>()).Returns(default);
                    repository.Notify("configured");
                    repository.Verify().Notify(Compono.Match.Any<string>()).Once();
                    return repository.Name;
                }
                """),
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be("Ada");
    }

    private static CodeGenerationOptions Options(string runMethod) => new()
    {
        SourceCode = $$"""
            namespace TestNamespace;

            public interface IRepository
            {
                int Add(int left, int right);
                System.Threading.Tasks.Task<int> PipelineAsync(
                    int value,
                    System.Func<System.Threading.Tasks.Task<int>> next);
                System.Threading.Tasks.ValueTask<int> ValueAsync(int value);
                System.Func<int, int>? Factory(int offset);
                int Identity(int value);
                string? Name { get; }
                void Notify(string message);
            }

            public sealed class Service
            {
                public Service(IRepository repository) { }
            }


            public static class EntryPoint
            {
                private static void Discover() => Compono.Composer.Create().Create<Service>();

                private static IRepository CreateDouble()
                {
                    Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IRepository), out var value);
                    return (IRepository)value!;
                }

                {{runMethod}}
            }
            """,
        MSBuildProperties = TestDoubleProperties,
    };
}
