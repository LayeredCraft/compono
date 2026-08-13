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

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            var placedAt = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

            var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());
            var repository = composer.Create<IRepository>();

            repository.Configure().CountAsync().Returns(Task.FromResult(7));
            repository.Configure().UtcNow().Returns(placedAt);

            var count = await repository.CountAsync();
            var utcNow = repository.UtcNow;

            if (count != 7)
                throw new InvalidOperationException($"Expected CountAsync() to return 7, got {count}.");

            if (utcNow != placedAt)
                throw new InvalidOperationException($"Expected UtcNow to return {placedAt}, got {utcNow}.");

            Console.WriteLine(
                $"PASS: generated double (composer.Create<T>() + UseGeneratedTestDoubles(), full base-" +
                $"interface closure) survived Native AOT - CountAsync()={count}, UtcNow={utcNow}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }
}
