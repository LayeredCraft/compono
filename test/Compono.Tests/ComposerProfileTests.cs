namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 3 Phase 2's public profile surface -
/// <see cref="CompositionBuilder.AddProfile{TProfile}()"/>, <see cref="CompositionBuilder.AddProfile(ICompositionProfile)"/>,
/// cycle detection, and source-chain provenance - through the real
/// <see cref="Composer.Create(Action{CompositionBuilder})"/> path. See
/// <c>docs/adr/0018-composition-profiles.md</c>.
/// </summary>
public sealed class ComposerProfileTests
{
    [Fact]
    public void AddProfileOfT_AppliesConfigure_Synchronously()
    {
        var composer = Composer.Create(builder => builder.AddProfile<WidgetProfile>());

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-profile");
    }

    [Fact]
    public void AddProfileInstance_AppliesConfigure_Synchronously()
    {
        var composer = Composer.Create(builder => builder.AddProfile(new WidgetProfile()));

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-profile");
    }

    [Fact]
    public void AddProfile_AppliesEveryProfile_InCallOrder()
    {
        var order = new List<string>();

        Composer.Create(builder => builder
            .AddProfile(new OrderRecordingProfile("first", order))
            .AddProfile(new OrderRecordingProfile("second", order)));

        order.Should().Equal("first", "second");
    }

    [Fact]
    public void AddProfile_ThatAddsANonCyclicProfileFromItsOwnConfigure_AppliesBothProfiles()
    {
        var composer = Composer.Create(builder => builder.AddProfile<BundlingProfile>());

        var widget = composer.Create<Widget>();
        var gadget = composer.Create<Gadget>();

        widget.Value.Should().Be("from-inner-profile");
        gadget.Value.Should().Be("from-bundling-profile");
    }

    [Fact]
    public void Register_DirectAndProfile_SameType_ThrowsNamingBothSources()
    {
        var act = () => Composer.Create(builder => builder
            .Register<Widget>(() => new Widget("direct"))
            .AddProfile<WidgetProfile>());

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateRegistration>().Which;
        error.RegisteredType.Should().Be(typeof(Widget));
        error.Sources.Should().HaveCount(2);
        error.Sources[0].Should().Be(ConfigurationSource.Direct);
        var profileSource = error.Sources[1].Should().BeOfType<ConfigurationSource.ProfileChain>().Which;
        profileSource.Profiles.Should().Equal(typeof(WidgetProfile));
    }

    [Fact]
    public void Register_ProfileAndProfile_SameType_ThrowsNamingBothProfileSources()
    {
        var act = () => Composer.Create(builder => builder
            .AddProfile<WidgetProfile>()
            .AddProfile<OtherWidgetProfile>());

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateRegistration>().Which;
        var firstSource = error.Sources[0].Should().BeOfType<ConfigurationSource.ProfileChain>().Which;
        firstSource.Profiles.Should().Equal(typeof(WidgetProfile));
        var secondSource = error.Sources[1].Should().BeOfType<ConfigurationSource.ProfileChain>().Which;
        secondSource.Profiles.Should().Equal(typeof(OtherWidgetProfile));
    }

    [Fact]
    public void AddProfile_DirectCycle_ThrowsImmediately_BeforeBuildRuns()
    {
        var act = () => Composer.Create(builder => builder.AddProfile<SelfReferencingProfile>());

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.ProfileCycle>().Which;
        error.Chain.Should().Equal(typeof(SelfReferencingProfile), typeof(SelfReferencingProfile));
    }

    [Fact]
    public void AddProfile_TwoLevelCycle_NamesTheFullChain()
    {
        var act = () => Composer.Create(builder => builder.AddProfile<ProfileA>());

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.ProfileCycle>().Which;
        error.Chain.Should().Equal(typeof(ProfileA), typeof(ProfileB), typeof(ProfileA));
    }

    [Fact]
    public void AddProfile_CycleBelowANonCyclicOuterProfile_ChainExcludesTheOuterProfile()
    {
        // A codex-review regression: the chain used to be sliced from the bottom of the whole
        // _applyingProfiles stack, so a non-cyclic outer profile wrapping a real cycle
        // (RootProfile -> ProfileA -> ProfileB -> ProfileA) leaked into Chain even though it isn't
        // part of the cycle - violating ProfileCycle.Chain's own "repeated type at both ends"
        // contract. The chain must start at ProfileA's first occurrence, not at RootProfile.
        var act = () => Composer.Create(builder => builder.AddProfile<RootProfile>());

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.ProfileCycle>().Which;
        error.Chain.Should().Equal(typeof(ProfileA), typeof(ProfileB), typeof(ProfileA));
    }

    [Fact]
    public void AddProfile_ThreeLevelNestedConflict_NamesTheFullChain_NotJustTheInnermost()
    {
        var act = () => Composer.Create(builder => builder
            .Register<Widget>(() => new Widget("direct"))
            .AddProfile<OuterProfile>());

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateRegistration>().Which;
        var profileSource = error.Sources.OfType<ConfigurationSource.ProfileChain>().Should().ContainSingle().Which;
        profileSource.Profiles.Should().Equal(typeof(OuterProfile), typeof(MiddleProfile), typeof(InnerProfile));
    }

    private sealed record Widget(string Value);

    private sealed record Gadget(string Value);

    private sealed class WidgetProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) =>
            builder.Register<Widget>(() => new Widget("from-profile"));
    }

    private sealed class OtherWidgetProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) =>
            builder.Register<Widget>(() => new Widget("from-other-profile"));
    }

    private sealed class OrderRecordingProfile(string name, List<string> order) : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => order.Add(name);
    }

    private sealed class BundlingProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder
            .AddProfile<InnerWidgetProfile>()
            .Register<Gadget>(() => new Gadget("from-bundling-profile"));
    }

    private sealed class InnerWidgetProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) =>
            builder.Register<Widget>(() => new Widget("from-inner-profile"));
    }

    private sealed class SelfReferencingProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.AddProfile<SelfReferencingProfile>();
    }

    private sealed class RootProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.AddProfile<ProfileA>();
    }

    private sealed class ProfileA : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.AddProfile<ProfileB>();
    }

    private sealed class ProfileB : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.AddProfile<ProfileA>();
    }

    private sealed class OuterProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.AddProfile<MiddleProfile>();
    }

    private sealed class MiddleProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.AddProfile<InnerProfile>();
    }

    private sealed class InnerProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) =>
            builder.Register<Widget>(() => new Widget("from-inner-profile"));
    }
}
