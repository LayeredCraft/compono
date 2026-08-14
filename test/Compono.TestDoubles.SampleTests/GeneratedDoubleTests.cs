using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// Applies UseGeneratedTestDoubles() to this row's own CompositionBuilder, exactly like an
// application's Program.cs would (coding-standards.md's application-level-wiring rule) - reached
// only through GeneratedDoubleTests' own [Compose<TProfile>] theory parameters. Mirrors
// Compono.XunitV3.SampleTests' own NSubstituteTestProfile.
public sealed class GeneratedTestDoubleProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseGeneratedTestDoubles();
}

// PLAN-0043 Phase 2's own end-to-end proof: a real generated double, reached only through the
// packaged Compono -> Compono.Generators -> Compono.TestDoubles dependency chain (not
// Compono.TestDoubles.Tests' hand-populated registry, and not Compono.Generators.Tests' isolated
// Verify() snapshot), satisfying an interface leaf via composer.Create<T>(), reused into the SUT's
// own constructor via [Shared], and configured from this test file via the generator-emitted
// Configure() bridge - repository.Configure() below resolves with no `using` for the generated
// double's own (global-namespace) type, proving Amendment 11 Finding AA's "no import needed" claim
// for real, against a real non-global consumer namespace (Compono.TestDoubles.SampleTests).
public sealed class GeneratedDoubleTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Repository_double_satisfies_the_service_using_configured_returns(
        [Shared] IRepository repository,
        OrderService service)
    {
        var placedAt = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

        // CountAsync/UtcNow are both configured through the generated Configure() bridge -
        // CountAsync is declared directly on IRepository, UtcNow is inherited from IClock, proving
        // the closure walk produced a real, callable configuration extension for both.
        repository.Configure().CountAsync().Returns(Task.FromResult(5));
        repository.Configure().UtcNow().Returns(placedAt);

        var order = await service.PlaceAsync(3);

        order.Total.Should().Be(8);
        order.PlacedAt.Should().Be(placedAt);
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Repository_double_throws_when_configured_to(
        [Shared] IRepository repository,
        OrderService service)
    {
        repository.Configure().CountAsync().Throws(new InvalidOperationException("boom"));

        var act = async () => await service.PlaceAsync(3);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
