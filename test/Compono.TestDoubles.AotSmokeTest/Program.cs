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

            Console.WriteLine(
                $"PASS: generated doubles (composer.Create<T>() + UseGeneratedTestDoubles(), full " +
                $"base-interface closure, overloaded member, generic method, call verification) " +
                $"survived Native AOT - CountAsync()={count}, UtcNow={utcNow}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }
}
