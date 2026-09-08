using Microsoft.Extensions.Options;

namespace Compono.Options.Tests;

/// <summary>
/// <c>CompositionBuilderExtensions.UseOptions&lt;T&gt;</c>'s graph-level identity/coherence contract -
/// <see cref="IOptions{TOptions}"/>/<see cref="IOptionsSnapshot{TOptions}"/>/<see cref="IOptionsMonitor{TOptions}"/>
/// all backed by one <see cref="TestOptionsSource{T}"/>. See
/// docs/adr/0061-compono-options-testing-support.md's "Decision Outcome" and docs/plans/0064-... Task 5.
/// </summary>
public sealed class CompositionBuilderExtensionsTests
{
    private sealed record Settings(string Value);

    // IOptions<T>

    [Fact]
    public void IOptions_Value_ReflectsTheSourcesValue_AtTheMomentOfFirstResolution()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));

        var options = composer.Create<IOptions<Settings>>();

        options.Value.Should().Be(new Settings("v0"));
    }

    [Fact]
    public void IOptions_SameInstance_OnEveryResolution_WithinOneGraph()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var row = composer.CreateRow(typeof(CompositionBuilderExtensionsTests));

        var first = row.Resolve<IOptions<Settings>>(Descriptor(0));
        var second = row.Resolve<IOptions<Settings>>(Descriptor(1));

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void IOptions_RemainsUnchanged_AfterALaterChangeOnTheSource()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));

        var options = composer.Create<IOptions<Settings>>();
        source.Change(new Settings("v1"));

        options.Value.Should().Be(new Settings("v0"));
    }

    // IOptionsSnapshot<T>

    [Fact]
    public void IOptionsSnapshot_ReflectsTheSourcesCurrentState_AtResolutionTime()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        source.Change(new Settings("v1"));
        var composer = Composer.Create(builder => builder.UseOptions(source));

        var snapshot = composer.Create<IOptionsSnapshot<Settings>>();

        snapshot.Value.Should().Be(new Settings("v1"));
    }

    [Fact]
    public void IOptionsSnapshot_RepeatedReads_OnTheSameResolvedInstance_StayStable_EvenIfTheSourceChangesAfterward()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));

        var snapshot = composer.Create<IOptionsSnapshot<Settings>>();
        source.Change(new Settings("v1"));

        snapshot.Value.Should().Be(new Settings("v0"));
        snapshot.Value.Should().Be(new Settings("v0"));
    }

    [Fact]
    public void IOptionsSnapshot_ASecondLaterResolution_AfterASourceChange_ProducesANewInstance_ReflectingTheNewState()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var row = composer.CreateRow(typeof(CompositionBuilderExtensionsTests));

        var first = row.Resolve<IOptionsSnapshot<Settings>>(Descriptor(0));
        source.Change(new Settings("v1"));
        var second = row.Resolve<IOptionsSnapshot<Settings>>(Descriptor(1));

        ReferenceEquals(first, second).Should().BeFalse();
        first.Value.Should().Be(new Settings("v0"));
        second.Value.Should().Be(new Settings("v1"));
    }

    [Fact]
    public void IOptionsSnapshot_NamedValues_WorkIdenticallyToMonitors()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));
        source.Change("named", new Settings("named-value"));
        var composer = Composer.Create(builder => builder.UseOptions(source));

        var snapshot = composer.Create<IOptionsSnapshot<Settings>>();

        snapshot.Get("named").Should().Be(new Settings("named-value"));
    }

    // IOptionsMonitor<T>

    [Fact]
    public void IOptionsMonitor_IsTheSourceItself()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));

        var monitor = composer.Create<IOptionsMonitor<Settings>>();

        ReferenceEquals(monitor, source).Should().BeTrue();
    }

    [Fact]
    public void IOptionsMonitor_SameInstance_OnEveryResolution_WithinOneGraph()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var row = composer.CreateRow(typeof(CompositionBuilderExtensionsTests));

        var first = row.Resolve<IOptionsMonitor<Settings>>(Descriptor(0));
        var second = row.Resolve<IOptionsMonitor<Settings>>(Descriptor(1));

        ReferenceEquals(first, second).Should().BeTrue();
    }

    // Unconfigured named options - Monitor and Snapshot lookup surfaces only (IOptions<T> has no
    // named-lookup surface at all - see the exception-message test below for its own throw path).

    [Fact]
    public void IOptionsMonitor_Get_ForAnUnconfiguredName_Throws()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var monitor = composer.Create<IOptionsMonitor<Settings>>();

        var act = () => monitor.Get("missing");

        act.Should().Throw<UnconfiguredNamedOptionException>();
    }

    [Fact]
    public void IOptionsSnapshot_Get_ForAnUnconfiguredName_Throws()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var snapshot = composer.Create<IOptionsSnapshot<Settings>>();

        var act = () => snapshot.Get("missing");

        act.Should().Throw<UnconfiguredNamedOptionException>();
    }

    // Note: IOptions<T> exposes no named-lookup surface at all (only Value), and its one lookup path -
    // the default name - is always established by TestOptionsSource<T>'s constructor, so
    // UnconfiguredNamedOptionException is never observably reachable through IOptions<T> in practice.
    // Diagnostic message coverage is therefore Monitor + Snapshot only (below), not all three
    // interfaces - see docs/plans/0064-... Notes.

    // Coherence - the central cross-interface claim

    [Fact]
    public void Coherence_OneSourceOneWiringCall_KeepsAllThreeInterfacesCorrectlyRelated_AcrossAChange()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var row = composer.CreateRow(typeof(CompositionBuilderExtensionsTests));

        var options = row.Resolve<IOptions<Settings>>(Descriptor(0));
        var monitor = row.Resolve<IOptionsMonitor<Settings>>(Descriptor(1));
        var firstSnapshot = row.Resolve<IOptionsSnapshot<Settings>>(Descriptor(2));

        source.Change(new Settings("v1"));

        var secondSnapshot = row.Resolve<IOptionsSnapshot<Settings>>(Descriptor(3));

        monitor.CurrentValue.Should().Be(new Settings("v1"), "Monitor sees the new value");
        options.Value.Should().Be(new Settings("v0"), "the already-resolved IOptions<T> stays frozen");
        firstSnapshot.Value.Should().Be(new Settings("v0"), "the already-resolved Snapshot stays frozen");
        secondSnapshot.Value.Should().Be(new Settings("v1"), "a newly-resolved Snapshot after the change sees the new value");
    }

    // Registration/composition

    [Fact]
    public void OrdinaryFirstRegistrationWinsPrecedence_HoldsUnchanged_ForAnExplicitConsumerOverride()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var explicitInstance = Microsoft.Extensions.Options.Options.Create(new Settings("explicit"));

        var act = () => Composer.Create(builder => builder
            .Register<IOptions<Settings>>(() => explicitInstance)
            .UseOptions(source));

        // UseOptions<T> also calls Register<IOptions<T>>(...) internally - an explicit consumer
        // registration before it collides under Compono's ordinary, unchanged strict
        // duplicate-registration rule. No special-cased precedence is introduced by this package.
        act.Should().Throw<CompositionConfigurationException>();
    }

    [Fact]
    public void ShareIsUsedCorrectly_ForIOptionsAndIOptionsMonitor_AndCorrectlyNotUsedForIOptionsSnapshot()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var composer = Composer.Create(builder => builder.UseOptions(source));
        var row = composer.CreateRow(typeof(CompositionBuilderExtensionsTests));

        var optionsA = row.Resolve<IOptions<Settings>>(Descriptor(0));
        var optionsB = row.Resolve<IOptions<Settings>>(Descriptor(1));
        var monitorA = row.Resolve<IOptionsMonitor<Settings>>(Descriptor(2));
        var monitorB = row.Resolve<IOptionsMonitor<Settings>>(Descriptor(3));
        var snapshotA = row.Resolve<IOptionsSnapshot<Settings>>(Descriptor(4));
        var snapshotB = row.Resolve<IOptionsSnapshot<Settings>>(Descriptor(5));

        ReferenceEquals(optionsA, optionsB).Should().BeTrue("IOptions<T> is shared");
        ReferenceEquals(monitorA, monitorB).Should().BeTrue("IOptionsMonitor<T> is shared");
        ReferenceEquals(snapshotA, snapshotB).Should().BeFalse("IOptionsSnapshot<T> is deliberately not shared");
    }

    [Fact]
    public void UseOptions_ReadsIdentically_InlineAndInsideAProfile()
    {
        var inlineSource = new TestOptionsSource<Settings>(new Settings("v0"));
        var inlineComposer = Composer.Create(builder => builder.UseOptions(inlineSource));

        var profileSource = new TestOptionsSource<Settings>(new Settings("v0"));
        var profileComposer = Composer.Create(builder => builder.AddProfile(new SettingsProfile(profileSource)));

        inlineComposer.Create<IOptions<Settings>>().Value.Should().Be(profileComposer.Create<IOptions<Settings>>().Value);
    }

    // Argument validation

    [Fact]
    public void UseOptions_NullBuilder_ThrowsArgumentNullException()
    {
        CompositionBuilder builder = null!;
        var source = new TestOptionsSource<Settings>(new Settings("v0"));

        var act = () => builder.UseOptions(source);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseOptions_NullSource_ThrowsArgumentNullException()
    {
        var act = () => Composer.Create(builder => builder.UseOptions<Settings>(null!));

        act.Should().Throw<ArgumentNullException>();
    }

    private static CompositionRequestDescriptor Descriptor(int ordinal) =>
        new(CompositionRequestKind.TestParameter, ordinal, $"p{ordinal}", declaringType: typeof(CompositionBuilderExtensionsTests), Nullability.NotNullable);

    private sealed class SettingsProfile(TestOptionsSource<Settings> source) : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.UseOptions(source);
    }
}
