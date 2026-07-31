using System.Collections.Concurrent;
using Compono.XunitV3.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

public sealed class ComposeAttributeConcurrencyTests
{
    [Fact]
    public async Task GetData_ProducesNoExceptionsOrDataRaces_WhenCalledConcurrently_OnOneSharedAttributeInstance()
    {
        // GetData runs fully synchronously and returns an already-completed ValueTask, so awaiting
        // Task.WhenAll over a lazily-Select-projected sequence of .AsTask() calls does NOT exercise
        // overlapping execution at all - WhenAll enumerates that deferred sequence one element at a
        // time, and since each GetData call has already finished by the time .AsTask() returns, call
        // N+1 is never even created until call N is done (PR #26 review, third round).
        // Parallel.ForEachAsync genuinely dispatches all 200 calls across the thread pool
        // concurrently, so this actually exercises concurrent first-touch access to the shared
        // Lazy<Composer>/binding-plan initialization ADR-0022's Caching section describes.
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var lengths = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (_, _) =>
        {
            var rows = await attribute.GetData(method, new DisposalTracker());
            lengths.Add(rows.Single().GetData().Length);
        });

        lengths.Should().HaveCount(200).And.OnlyContain(length => length == 2);
    }
}
