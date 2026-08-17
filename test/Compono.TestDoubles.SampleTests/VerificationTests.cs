using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// PLAN-0044 Phase 2's own end-to-end proof: Verify()/Once()/Never()/Exactly(n) against a real
// generated double, reached only through the packaged Compono -> Compono.Generators ->
// Compono.TestDoubles dependency chain (not Compono.Generators.Tests' isolated Verify() snapshot,
// nor Compono.Tests' CallVerifier unit tests against a hand-constructed slot).
public sealed class VerificationTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Verify_AfterRealCallsThroughTheService_PassesForTheActualCallCount(
        [Shared] IRepository repository,
        OrderService service)
    {
        repository.Configure().CountAsync().Returns(Task.FromResult(5));

        await service.PlaceAsync(3);

        repository.Verify().CountAsync().Once();
        repository.Verify().Save().Once();
        repository.Verify().UtcNow().Once();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Verify_Never_PassesWhenTheMemberWasNeverCalled([Shared] IRepository repository)
    {
        repository.Verify().Save().Never();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Verify_Once_ThrowsWithMessage_WhenCalledMoreThanOnce(
        [Shared] IRepository repository,
        OrderService service)
    {
        repository.Configure().CountAsync().Returns(Task.FromResult(5));

        await service.PlaceAsync(3);
        await service.PlaceAsync(3);

        var act = () => repository.Verify().CountAsync().Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*CountAsync*received 2*");
    }
}
