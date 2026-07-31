using Compono.XunitV3.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

public sealed class ComposeAttributeConcurrencyTests
{
    [Fact]
    public async Task GetData_ProducesNoExceptionsOrDataRaces_WhenCalledConcurrently_OnOneSharedAttributeInstance()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var calls = Enumerable.Range(0, 200)
            .Select(_ => attribute.GetData(method, new DisposalTracker()).AsTask());

        var results = await Task.WhenAll(calls);

        results.Should().OnlyContain(rows => rows.Single().GetData().Length == 2);
    }
}
