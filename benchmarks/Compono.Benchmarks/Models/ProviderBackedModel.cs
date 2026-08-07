namespace Compono.Benchmarks.Models;

/// <summary>An interface-typed dependency <c>Compono.NSubstitute</c> can satisfy - see <see cref="ProviderBackedModel"/>'s remarks.</summary>
public interface IClock
{
    /// <summary>The current instant, per this clock.</summary>
    DateTimeOffset Now { get; }
}

/// <summary>A fixed, non-substitute <see cref="IClock"/> - the baseline registration ADR-0034's provider-overhead comparisons measure against, not a real system clock.</summary>
public sealed class FixedClock : IClock
{
    private static readonly DateTimeOffset FixedInstant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public DateTimeOffset Now => FixedInstant;
}

/// <summary>
/// The provider-eligible representative model, per ADR-0034: an interface-typed member
/// (<see cref="IClock"/>) <c>Compono.NSubstitute</c>'s stage-6 provider can satisfy, and a
/// <c>string</c> member named <c>Email</c> - matching <c>Compono.Bogus</c>'s built-in
/// member-name-convention allowlist - its stage-5 provider can satisfy. Reused by
/// ConsumerScenarios' Bogus/NSubstitute-enabled cases and FeatureOverhead's provider-overhead
/// comparisons, so both categories draw on the same model rather than inventing their own.
/// </summary>
public sealed record ProviderBackedModel(IClock Clock, string Email);
