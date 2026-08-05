namespace Compono.Samples.AspNetApi.Tests;

// Applies UseNSubstitute()/UseBogus() to this row's own CompositionBuilder, exactly like an
// application's Program.cs would - coding-standards.md's application-level-wiring rule. Composed
// through the packaged Compono.NSubstitute/Compono.Bogus -> Compono dependency chain, same pattern
// test/Compono.XunitV3.SampleTests/BogusTests.cs establishes.
public sealed class ApiTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseNSubstitute().UseBogus();
}

// A Bogus-generated customer - FirstName/LastName/Email come from BogusMemberNameProvider's
// deterministically-seeded semantic member-name matching (docs/packages/compono-bogus.md), not a
// hand-written fixture.
public sealed class Customer
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }
}
