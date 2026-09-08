using Microsoft.Extensions.Options;

namespace Compono.Options.Tests;

/// <summary>
/// <see cref="TestOptionsSource{T}"/>'s own <see cref="IOptionsMonitor{TOptions}"/> behavior, named
/// options, change notification, disposal, and concurrency - independent of composition wiring
/// (<see cref="CompositionBuilderExtensionsTests"/> covers the graph-level identity/coherence
/// contract). See docs/adr/0061-compono-options-testing-support.md and docs/plans/0064-... Task 5.
/// </summary>
public sealed class TestOptionsSourceTests
{
    private sealed record Settings(string Value);

    // IOptionsMonitor<T> contract

    [Fact]
    public void CurrentValue_ReturnsTheInitialValue()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));

        source.CurrentValue.Should().Be(new Settings("initial"));
    }

    [Fact]
    public void CurrentValue_AndGetDefaultName_Agree()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));

        source.CurrentValue.Should().Be(source.Get(Microsoft.Extensions.Options.Options.DefaultName));
    }

    [Fact]
    public void Get_ForAConfiguredName_ReturnsThatNamesValue()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));
        source.Change("named", new Settings("named-value"));

        source.Get("named").Should().Be(new Settings("named-value"));
    }

    [Fact]
    public void Change_UpdatesCurrentValue()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));

        source.Change(new Settings("updated"));

        source.CurrentValue.Should().Be(new Settings("updated"));
    }

    [Fact]
    public void Change_FiresSubscribers()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));
        Settings? observed = null;
        string? observedName = null;
        source.OnChange((value, name) =>
        {
            observed = value;
            observedName = name;
        });

        source.Change(new Settings("updated"));

        observed.Should().Be(new Settings("updated"));
        observedName.Should().Be(Microsoft.Extensions.Options.Options.DefaultName);
    }

    [Fact]
    public void Change_ValueIsVisibleFromInsideTheChangeCallback()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));
        Settings? observedCurrentValue = null;
        source.OnChange((_, _) => observedCurrentValue = source.CurrentValue);

        source.Change(new Settings("updated"));

        observedCurrentValue.Should().Be(new Settings("updated"));
    }

    [Fact]
    public void Change_CallbacksAreSynchronous()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));
        var invokedOnCallingThread = false;
        var callingThreadId = Environment.CurrentManagedThreadId;
        source.OnChange((_, _) => invokedOnCallingThread = Environment.CurrentManagedThreadId == callingThreadId);

        source.Change(new Settings("updated"));

        invokedOnCallingThread.Should().BeTrue();
    }

    [Fact]
    public void Change_TwoIndependentSubscribers_BothFire()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));
        var firstFired = false;
        var secondFired = false;
        source.OnChange((_, _) => firstFired = true);
        source.OnChange((_, _) => secondFired = true);

        source.Change(new Settings("updated"));

        firstFired.Should().BeTrue();
        secondFired.Should().BeTrue();
    }

    [Fact]
    public void DisposingOneSubscription_StopsOnlyThatListener()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));
        var firstFireCount = 0;
        var secondFireCount = 0;
        var firstSubscription = source.OnChange((_, _) => firstFireCount++);
        source.OnChange((_, _) => secondFireCount++);

        firstSubscription.Dispose();
        source.Change(new Settings("updated"));

        firstFireCount.Should().Be(0);
        secondFireCount.Should().Be(1);
    }

    [Fact]
    public void DisposingASubscriptionTwice_DoesNotThrow_AndDoesNotAffectOtherSubscriptions()
    {
        var source = new TestOptionsSource<Settings>(new Settings("initial"));
        var otherFireCount = 0;
        var subscription = source.OnChange((_, _) => { });
        source.OnChange((_, _) => otherFireCount++);

        subscription.Dispose();
        var act = subscription.Dispose;

        act.Should().NotThrow();
        source.Change(new Settings("updated"));
        otherFireCount.Should().Be(1);
    }

    [Fact]
    public void AThrowingSubscriber_PreventsALaterRegisteredSubscriberInTheSameInvocation_FromFiring()
    {
        // Matches real OptionsMonitor<T> exactly - no per-subscriber exception isolation. Asserted here
        // as intended, documented behavior, not an accidental discovery - docs/adr/0061's
        // "Change-notification robustness" section.
        var source = new TestOptionsSource<Settings>(new Settings("initial"));
        var laterSubscriberFired = false;
        source.OnChange((_, _) => throw new InvalidOperationException("boom"));
        source.OnChange((_, _) => laterSubscriberFired = true);

        var act = () => source.Change(new Settings("updated"));

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        laterSubscriberFired.Should().BeFalse();
    }

    // Named options

    [Fact]
    public void DefaultName_AndAnExplicitName_AreIndependent()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));
        source.Change("named", new Settings("named-value"));

        source.CurrentValue.Should().Be(new Settings("default"));
        source.Get("named").Should().Be(new Settings("named-value"));
    }

    [Fact]
    public void NameComparison_IsCaseSensitive()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));
        source.Change("Foo", new Settings("upper"));

        var act = () => source.Get("foo");

        act.Should().Throw<UnconfiguredNamedOptionException>();
        source.Get("Foo").Should().Be(new Settings("upper"));
    }

    [Fact]
    public void ChangingOneName_DoesNotAffectAnotherName()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));
        source.Change("a", new Settings("a-value"));
        source.Change("b", new Settings("b-value"));

        source.Change("a", new Settings("a-updated"));

        source.Get("a").Should().Be(new Settings("a-updated"));
        source.Get("b").Should().Be(new Settings("b-value"));
    }

    // Diagnostics

    [Fact]
    public void Get_ForAnUnconfiguredName_ThrowsUnconfiguredNamedOptionException()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));

        var act = () => source.Get("missing");

        act.Should().Throw<UnconfiguredNamedOptionException>()
            .WithMessage($"*{nameof(Settings)}*missing*");
    }

    [Fact]
    public void Get_ForTheDefaultName_WhenOnlyANamedValueWasChanged_StillThrows()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));

        var act = () => source.Get("configured-elsewhere-but-not-this-name");

        act.Should().Throw<UnconfiguredNamedOptionException>();
    }

    // Disposal (source itself)

    [Fact]
    public void TestOptionsSource_DoesNotImplementIDisposableOrIAsyncDisposable()
    {
        typeof(TestOptionsSource<Settings>).Should().NotBeAssignableTo<IDisposable>();
        typeof(TestOptionsSource<Settings>).Should().NotBeAssignableTo<IAsyncDisposable>();
    }

    // Concurrency (focused correctness, not stress/perf benchmarking)

    [Fact]
    public async Task ConcurrentReads_WhileAChangeIsInFlight_NeverObserveATornValue()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var knownValues = Enumerable.Range(0, 50).Select(i => new Settings($"v{i}")).ToArray();
        var observedUnknown = false;
        using var barrier = new Barrier(2);
        var cancellationToken = TestContext.Current.CancellationToken;

        var writer = Task.Run(
            () =>
            {
                barrier.SignalAndWait(cancellationToken);
                foreach (var value in knownValues)
                {
                    source.Change(value);
                }
            }, cancellationToken);
        var reader = Task.Run(
            () =>
            {
                barrier.SignalAndWait(cancellationToken);
                for (var i = 0; i < 5000; i++)
                {
                    var observed = source.CurrentValue;
                    if (observed.Value != "v0" && !knownValues.Any(v => v == observed))
                    {
                        observedUnknown = true;
                    }
                }
            }, cancellationToken);

        await Task.WhenAll(writer, reader);

        observedUnknown.Should().BeFalse();
    }

    [Fact]
    public void ConcurrentChanges_ForDifferentNames_DoNotCorruptEachOthersStorage()
    {
        var source = new TestOptionsSource<Settings>(new Settings("default"));
        const int nameCount = 20;
        var names = Enumerable.Range(0, nameCount).Select(i => $"name{i}").ToArray();

        Parallel.ForEach(names, name =>
        {
            for (var i = 0; i < 100; i++)
            {
                source.Change(name, new Settings($"{name}-{i}"));
            }
        });

        foreach (var name in names)
        {
            source.Get(name).Value.Should().StartWith(name);
        }
    }

    [Fact]
    public void ConcurrentSubscribeAndUnsubscribe_IncludingDuringAnInFlightNotification_DoesNotThrowOrCorruptTheSubscriberList()
    {
        var source = new TestOptionsSource<Settings>(new Settings("v0"));
        var iterations = Enumerable.Range(0, 200);

        var act = () => Parallel.ForEach(iterations, _ =>
        {
            var subscription = source.OnChange((_, _) => { });
            source.Change(new Settings(Guid.NewGuid().ToString()));
            subscription.Dispose();
        });

        act.Should().NotThrow();
    }
}
