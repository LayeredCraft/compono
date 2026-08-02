using NSubstitute.Core;

namespace Compono.Bogus.Tests;

/// <summary>
/// <c>UseBogus()</c> and <c>UseNSubstitute()</c> registered on the same real <see cref="Composer"/>,
/// in both call orders - this plan's own Goal scenario, exercised directly against
/// <see cref="Composer"/>/<see cref="CompositionRow"/> rather than through a packaged consumer (that's
/// <c>test/Compono.XunitV3.SampleTests</c>'s own real-runner proof). PLAN-0006 Phase 3.
/// </summary>
public sealed class CoexistenceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BogusThenNSubstitute_OrNSubstituteThenBogus_EachClaimsOnlyItsOwnRequestShape(bool bogusFirst)
    {
        var composer = Composer.Create(builder =>
        {
            builder.WithSeed(4219);
            if (bogusFirst)
                builder.UseBogus().UseNSubstitute();
            else
                builder.UseNSubstitute().UseBogus();
        });
        var row = composer.CreateRow(typeof(CoexistenceTests));

        var email = row.Resolve<string>(new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Email", declaringType: null, Nullability.NotNullable));
        var repository = row.Resolve<IRepository>(new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 1, "repository", declaringType: null, Nullability.NotNullable));

        email.Should().Contain("@");
        SubstitutionContext.Current.GetCallRouterFor(repository).Should().NotBeNull();
    }

    [Fact]
    public void BogusNeverClaims_AnInterfaceRequest_EvenWhenItIsTheOnlyRegisteredSemanticProvider()
    {
        // An interface request reaches BogusMemberNameProvider's stage (it's registered as the only
        // semantic provider here) but is always declined (type-gated to string) - proving the disjoint-
        // shape claim via the diagnostic trace itself, not just the overall failure outcome, per this
        // plan's own Phase 3 task wording. No NSubstitute registered, so the request fails overall,
        // making the trace observable through the thrown CompositionException.
        var composer = Composer.Create(builder => builder.WithSeed(4219).UseBogus());
        var row = composer.CreateRow(typeof(CoexistenceTests));

        var act = () => row.Resolve<IRepository>(new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "repository", declaringType: null, Nullability.NotNullable));

        var diagnostic = act.Should().Throw<CompositionException>().Which.Diagnostic;
        diagnostic!.Trace.Should().Contain(attempt =>
            attempt.Stage == PipelineStage.SemanticProvider
            && attempt.Provider == typeof(BogusMemberNameProvider)
            && attempt.Outcome == CompositionAttemptOutcome.NotHandled);
    }

    [Fact]
    public void ExplicitRegistration_WinsOverBothBogusAndNSubstitute()
    {
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .UseBogus()
            .UseNSubstitute()
            .For<string>().Use("explicit-value"));

        var value = composer.CreateRow(typeof(CoexistenceTests)).Resolve<string>(new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Email", declaringType: null, Nullability.NotNullable));

        value.Should().Be("explicit-value");
    }

    [Fact]
    public void SharedNSubstituteSubstitute_AndBogusSuppliedScalars_CoexistInOneRowsScope()
    {
        var composer = Composer.Create(builder => builder.WithSeed(4219).UseBogus().UseNSubstitute());
        var row = composer.CreateRow(typeof(CoexistenceTests));

        var sharedDescriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, ordinal: 0, "repository", declaringType: typeof(CoexistenceTests), Nullability.NotNullable);
        var repository = row.ResolveShared<IRepository>(sharedDescriptor);
        var email = row.Resolve<string>(new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 1, "Email", declaringType: null, Nullability.NotNullable));

        SubstitutionContext.Current.GetCallRouterFor(repository).Should().NotBeNull();
        email.Should().Contain("@");
    }

    public interface IRepository;
}
