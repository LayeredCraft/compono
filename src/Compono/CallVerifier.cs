namespace Compono;

/// <summary>
/// Asserts how many times a generated test double's member was called, backed by
/// <see cref="ReturnConfig{T}.ConfiguredCallCount"/>. Deliberately minimal - <see cref="Never"/>/
/// <see cref="Once"/>/<see cref="Exactly"/> only, no argument matchers, no call-order verification,
/// per ADR-0044 Requirement 3.
/// </summary>
/// <param name="observedCount">How many times the member's dispatch body actually ran.</param>
/// <param name="memberDescription">
/// The declaring interface's display name plus member name, used to describe a verification failure.
/// </param>
public readonly struct CallVerifier(int observedCount, string memberDescription)
{
    /// <summary>Asserts the member was never called.</summary>
    /// <exception cref="TestDoubleVerificationException">The member was called at least once.</exception>
    public void Never() => Exactly(0);

    /// <summary>Asserts the member was called exactly once.</summary>
    /// <exception cref="TestDoubleVerificationException">The member was not called exactly once.</exception>
    public void Once() => Exactly(1);

    /// <summary>Asserts the member was called exactly <paramref name="times"/> times.</summary>
    /// <exception cref="TestDoubleVerificationException">
    /// The member was not called exactly <paramref name="times"/> times.
    /// </exception>
    public void Exactly(int times)
    {
        if (observedCount != times)
        {
            throw new TestDoubleVerificationException(
                $"Expected exactly {times} call(s) to {memberDescription}, but received {observedCount}.");
        }
    }
}
