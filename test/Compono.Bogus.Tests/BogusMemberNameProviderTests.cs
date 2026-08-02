namespace Compono.Bogus.Tests;

/// <summary>
/// <see cref="BogusMemberNameProvider.TryProvide"/> unit coverage, exercised through a real
/// <see cref="Composer"/>/<see cref="CompositionRow"/> - <see cref="CompositionProviderResult"/>'s
/// <c>Value</c>/<c>IsHandled</c> are internal to <c>Compono</c>, so a provider's own outcome is only
/// observable from outside through the pipeline it feeds, matching
/// <c>Compono.NSubstitute.Tests.NSubstituteProviderTests</c>'s own precedent - PLAN-0006 Phase 3. See
/// <c>docs/adr/0027-compono-bogus-package-design.md</c>.
/// </summary>
public sealed class BogusMemberNameProviderTests
{
    public static TheoryData<string> AllowlistedNames =>
    [
        "FirstName", "LastName", "FullName", "Email", "PhoneNumber",
        "StreetAddress", "City", "State", "PostalCode", "CompanyName",
    ];

    [Theory]
    [MemberData(nameof(AllowlistedNames))]
    public void AllowlistedName_AgainstString_ProducesADifferentValueThanTheNoProviderFallback(string name)
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, name, declaringType: null, Nullability.NotNullable);

        var withoutProvider = Resolve<string>(descriptor, addProvider: false);
        var withProvider = Resolve<string>(descriptor, addProvider: true);

        // BogusMemberNameProvider never touches randomness before deciding to handle a request (no
        // context.DeriveSeed()/Faker constructed on a declined path), so an unrelated fallback stage's
        // own draw is unperturbed by the provider merely being registered - see the equivalence tests
        // below, which rely on that same property to prove a decline instead.
        withProvider.Should().NotBe(withoutProvider);
    }

    [Theory]
    [MemberData(nameof(AllowlistedNames))]
    public void AllowlistedName_AgainstANonStringType_Declines(string name)
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, name, declaringType: null, Nullability.NotNullable);
        var composer = Composer.Create(builder => builder.WithSeed(4219).AddSemanticProvider(new BogusMemberNameProvider("en")));
        var row = composer.CreateRow(typeof(BogusMemberNameProviderTests));

        // No other stage can satisfy an unregistered interface, so a decline here surfaces as a
        // CompositionException whose trace names BogusMemberNameProvider's own NotHandled attempt -
        // proving the type gate, not just an absence of a thrown-away value.
        var act = () => row.Resolve<ISampleService>(descriptor);

        var diagnostic = act.Should().Throw<CompositionException>().Which.Diagnostic;
        diagnostic!.Trace.Should().Contain(attempt =>
            attempt.Stage == PipelineStage.SemanticProvider
            && attempt.Provider == typeof(BogusMemberNameProvider)
            && attempt.Outcome == CompositionAttemptOutcome.NotHandled);
    }

    [Fact]
    public void Name_Itself_Declines()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Name", declaringType: null, Nullability.NotNullable);

        var withoutProvider = Resolve<string>(descriptor, addProvider: false);
        var withProvider = Resolve<string>(descriptor, addProvider: true);

        withProvider.Should().Be(withoutProvider);
    }

    [Fact]
    public void UnlistedName_Declines()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Sku", declaringType: null, Nullability.NotNullable);

        var withoutProvider = Resolve<string>(descriptor, addProvider: false);
        var withProvider = Resolve<string>(descriptor, addProvider: true);

        withProvider.Should().Be(withoutProvider);
    }

    private static TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor, bool addProvider)
    {
        var composer = Composer.Create(builder =>
        {
            builder.WithSeed(4219);
            if (addProvider)
                builder.AddSemanticProvider(new BogusMemberNameProvider("en"));
        });

        return composer.CreateRow(typeof(BogusMemberNameProviderTests)).Resolve<TValue>(descriptor);
    }

    public interface ISampleService;
}
