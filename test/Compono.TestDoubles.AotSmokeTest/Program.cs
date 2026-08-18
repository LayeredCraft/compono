using Compono;

namespace Compono.TestDoubles.AotSmokeTest;

// IRepository extends IClock, not to re-litigate Amendment 11 Finding Z here (already proven by
// Compono.TestDoubles.SampleTests' own Verify()/real-runner coverage) but to exercise the exact same
// generated shape (closure-walked double, a Task<T>-returning member, a property) under Native AOT,
// not just under the ordinary JIT that runs Compono.TestDoubles.SampleTests' own dotnet test.
internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal interface IRepository : IClock
{
    Task<int> CountAsync();
}

// PLAN-0044 Phase 3: an overloaded member, exercising the per-overload Configure()/Verify()
// discriminator mechanism under Native AOT - not proven by IRepository above, which has no overloads.
internal interface IGateway
{
    void Send(string message);
    void Send(int retryCount, string message);
}

// PLAN-0044 Phase 3: a generic method whose return type doesn't depend on its own type parameter
// (the Requirement 2 shape, mirroring ILogger<T>.Log<TState>) - the Configure()/Verify() extension
// stays non-generic while the explicit interface implementation stays generic.
internal interface ILoggerLike
{
    void Log<TState>(int logLevel, TState state);
}

// PLAN-0045 Phase 2: the configuration-required dispatch shape (ADR-0045/PLAN-0045 Phase 0) under
// Native AOT specifically - a synchronous non-nullable-reference-returning method, a same-shaped
// property, and a Task<T>-returning method, each proven both unconfigured (throws
// TestDoubleNotConfiguredException) and configured (Returns(...) dispatches the literal
// generation-time-emitted throw branch correctly under trimming/AOT, not just under the ordinary JIT
// that runs Compono.TestDoubles.SampleTests' own dotnet test).
internal interface IProfileRepository
{
    string GetName();

    string Description { get; }

    Task<string> GetNameAsync();
}

// ADR-0046: a static abstract member declared on a base interface, already resolved by a more-
// derived interface's own concrete implementation (C#'s "most specific implementation" rule,
// IAmazonS3-shaped) - the generated double must be completely unaffected by it under Native AOT
// too, not just under the ordinary JIT that runs Compono.TestDoubles.SampleTests' own dotnet test.
internal interface IProfileFactory
{
    static abstract IProfileFactory CreateDefault();
}

internal interface IProfileRepositoryWithStaticAbstractBase : IProfileFactory
{
    static IProfileFactory IProfileFactory.CreateDefault() =>
        throw new NotSupportedException("real production implementation, never invoked here");

    string GetName();
}

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            var placedAt = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

            var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());
            var repository = composer.Create<IRepository>();
            var gateway = composer.Create<IGateway>();
            var logger = composer.Create<ILoggerLike>();
            var profileRepository = composer.Create<IProfileRepository>();
            var profileRepositoryWithStaticAbstractBase = composer.Create<IProfileRepositoryWithStaticAbstractBase>();

            repository.Configure().CountAsync().Returns(Task.FromResult(7));
            repository.Configure().UtcNow().Returns(placedAt);

            var count = await repository.CountAsync();
            var utcNow = repository.UtcNow;

            if (count != 7)
                throw new InvalidOperationException($"Expected CountAsync() to return 7, got {count}.");

            if (utcNow != placedAt)
                throw new InvalidOperationException($"Expected UtcNow to return {placedAt}, got {utcNow}.");

            // Overloaded member: each overload dispatches through its own generated slot.
            gateway.Send("hello");
            gateway.Send(3, "retrying");

            gateway.Verify().Send("hello").Once();
            gateway.Verify().Send(3, "retrying").Once();

            // Generic method independent of its own type parameter: one non-generic Configure()/
            // Verify() slot covers every closed instantiation a real caller exercises.
            logger.Log(1, "first");
            logger.Log(2, 99);

            logger.Verify().Log().Exactly(2);

            // Call verification against the pre-existing IRepository shape too, proving
            // RecordCall()/CallVerifier survive Native AOT for a Task<T>-returning member and a
            // property, not just void methods.
            repository.Verify().CountAsync().Once();
            repository.Verify().UtcNow().Once();

            // Configuration-required members (ADR-0045/PLAN-0045 Phase 0): unconfigured-throws proven
            // first, then the same members configured and dispatching their real values, all under
            // Native AOT.
            var unconfiguredMethod = () => profileRepository.GetName();
            if (!ThrowsNotConfigured(unconfiguredMethod))
                throw new InvalidOperationException("Expected unconfigured GetName() to throw TestDoubleNotConfiguredException.");

            var unconfiguredProperty = () => profileRepository.Description;
            if (!ThrowsNotConfigured(unconfiguredProperty))
                throw new InvalidOperationException("Expected unconfigured Description to throw TestDoubleNotConfiguredException.");

            var unconfiguredAsyncMethod = async () => await profileRepository.GetNameAsync();
            if (!await ThrowsNotConfiguredAsync(unconfiguredAsyncMethod))
                throw new InvalidOperationException("Expected unconfigured GetNameAsync() to throw TestDoubleNotConfiguredException.");

            profileRepository.Configure().GetName().Returns("Ada");
            profileRepository.Configure().Description().Returns("a test double");
            profileRepository.Configure().GetNameAsync().Returns(Task.FromResult("Ada"));

            var name = profileRepository.GetName();
            var description = profileRepository.Description;
            var asyncName = await profileRepository.GetNameAsync();

            if (name != "Ada")
                throw new InvalidOperationException($"Expected GetName() to return 'Ada', got '{name}'.");

            if (description != "a test double")
                throw new InvalidOperationException($"Expected Description to return 'a test double', got '{description}'.");

            if (asyncName != "Ada")
                throw new InvalidOperationException($"Expected GetNameAsync() to return 'Ada', got '{asyncName}'.");

            // ADR-0046: a static abstract member resolved via a derived interface's own concrete
            // implementation doesn't reject the leaf interface, and every other instance member on
            // it works exactly as normal - proven under Native AOT here.
            profileRepositoryWithStaticAbstractBase.Configure().GetName().Returns("Ada");
            var staticAbstractBaseName = profileRepositoryWithStaticAbstractBase.GetName();

            if (staticAbstractBaseName != "Ada")
                throw new InvalidOperationException($"Expected GetName() to return 'Ada', got '{staticAbstractBaseName}'.");

            Console.WriteLine(
                $"PASS: generated doubles (composer.Create<T>() + UseGeneratedTestDoubles(), full " +
                $"base-interface closure, overloaded member, generic method, call verification, " +
                $"configuration-required sync method/property/Task<T> method both unconfigured-throws " +
                $"and configured, a leaf interface whose base declares a static abstract member " +
                $"already resolved by the leaf itself) survived Native AOT - CountAsync()={count}, " +
                $"UtcNow={utcNow}, GetName()={name}, Description={description}, " +
                $"GetNameAsync()={asyncName}, static-abstract-base GetName()={staticAbstractBaseName}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    // No AwesomeAssertions/xUnit available here (this project deliberately removes those global
    // usings - see the .csproj) - a plain try/catch stands in for
    // act.Should().Throw<TestDoubleNotConfiguredException>().
    private static bool ThrowsNotConfigured<T>(Func<T> act)
    {
        try
        {
            act();
            return false;
        }
        catch (TestDoubleNotConfiguredException)
        {
            return true;
        }
    }

    private static async Task<bool> ThrowsNotConfiguredAsync(Func<Task> act)
    {
        try
        {
            await act();
            return false;
        }
        catch (TestDoubleNotConfiguredException)
        {
            return true;
        }
    }
}
